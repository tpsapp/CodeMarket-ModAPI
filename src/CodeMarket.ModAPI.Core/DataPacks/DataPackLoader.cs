using System;
using System.Collections.Generic;
using System.Linq;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Core.Lifecycle;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.DataPacks;

public sealed class DataPackLoader
{
    private readonly ModLifecycleManager _lifecycle;
    private readonly IBalanceRegistry _balance;
    private readonly IVisualRegistry _visuals;
    private readonly IModLogger _logger;

    public DataPackLoader(
        ModLifecycleManager lifecycle,
        IBalanceRegistry balance,
        IVisualRegistry visuals,
        IModLogger logger)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _visuals = visuals ?? throw new ArgumentNullException(nameof(visuals));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private readonly Dictionary<string, string> _packJsonById = new Dictionary<string, string>(StringComparer.Ordinal);

    public void LoadPacks(IEnumerable<(string FallbackId, string ManifestJson)> packs)
    {
        if (packs is null) throw new ArgumentNullException(nameof(packs));

        var validated = new List<(ModRecord Record, string Json)>();
        foreach ((string fallbackId, string json) in packs)
        {
            ModRecord record = _lifecycle.DiscoverAndValidate(fallbackId, json);
            if (record.Manifest is not null && record.Manifest.Type == ModType.DataPack &&
                (record.State == ModState.Validated || record.State == ModState.Disabled))
            {
                _packJsonById[record.Id] = json;
                if (record.State == ModState.Validated)
                {
                    validated.Add((record, json));
                }
            }
        }

        // Deterministic lexicographic load order ensures reproducible conflict resolution across OS filesystems.
        foreach ((ModRecord record, string json) in validated.OrderBy(p => p.Record.Id, StringComparer.Ordinal))
        {
            string modId = record.Id;
            _lifecycle.ExecuteModPhase(
                modId,
                ModState.Registered,
                () => ApplyOverrides(modId, json),
                rollback: () =>
                {
                    _balance.RemoveByMod(modId);
                    _visuals.RemoveByMod(modId);
                });
        }
    }

    public bool TryReapplyPack(string modId)
    {
        if (!_packJsonById.TryGetValue(modId, out string? json))
        {
            return false;
        }

        ApplyOverrides(modId, json);
        return true;
    }

    private void ApplyOverrides(string modId, string json)
    {
        JsonValue root = JsonParser.Parse(json);
        if (!root.TryGetProperty("overrides", out JsonValue? overrides) || overrides is null)
        {
            return;
        }

        if (overrides.Kind != JsonKind.Object)
        {
            throw new FormatException("'overrides' must be a JSON object.");
        }

        foreach (KeyValuePair<string, JsonValue> section in overrides.AsObject())
        {
            if (LanguagePolicy.IsForbiddenDataPackSection(section.Key))
            {
                throw new InvalidOperationException(
                    $"{LanguagePolicy.ViolationMessage} (forbidden section '{section.Key}').");
            }

            switch (section.Key)
            {
                case "products":
                    ApplyProductOverrides(modId, section.Value);
                    break;
                case "upgrades":
                    ApplyUpgradeOverrides(modId, section.Value);
                    break;
                case "storeRates":
                    ApplyStoreRatesOverrides(modId, section.Value);
                    break;
                case "materials":
                    ApplyMaterialOverrides(modId, section.Value);
                    break;
                default:
                    _logger.Warning($"[ModAPI] Data pack '{modId}' contains unknown override section '{section.Key}' (ignored).");
                    break;
            }
        }
    }

    private void ApplyProductOverrides(string modId, JsonValue section)
    {
        if (section.Kind != JsonKind.Object) throw new FormatException("'overrides.products' must be a JSON object.");

        foreach (KeyValuePair<string, JsonValue> kv in section.AsObject())
        {
            if (kv.Value.Kind != JsonKind.Object)
            {
                throw new FormatException($"Product override '{kv.Key}' must be a JSON object.");
            }

            if (kv.Value.TryGetProperty("sellPrice", out JsonValue? priceVal) && priceVal is not null)
            {
                if (priceVal.Kind != JsonKind.Number)
                {
                    throw new FormatException($"Product '{kv.Key}.sellPrice' must be a number.");
                }

                _balance.SetProductPrice(modId, kv.Key, priceVal.AsInt32());
            }
        }
    }

    private void ApplyUpgradeOverrides(string modId, JsonValue section)
    {
        if (section.Kind != JsonKind.Object) throw new FormatException("'overrides.upgrades' must be a JSON object.");

        foreach (KeyValuePair<string, JsonValue> kv in section.AsObject())
        {
            if (kv.Value.Kind != JsonKind.Object)
            {
                throw new FormatException($"Upgrade override '{kv.Key}' must be a JSON object.");
            }

            int? flatCost = null;
            double? mult = null;

            if (kv.Value.TryGetProperty("flatCost", out JsonValue? flatVal) && flatVal is not null)
            {
                if (flatVal.Kind != JsonKind.Number) throw new FormatException($"Upgrade '{kv.Key}.flatCost' must be a number.");
                flatCost = flatVal.AsInt32();
            }

            if (kv.Value.TryGetProperty("costMultiplier", out JsonValue? multVal) && multVal is not null)
            {
                if (multVal.Kind != JsonKind.Number) throw new FormatException($"Upgrade '{kv.Key}.costMultiplier' must be a number.");
                mult = multVal.AsNumber();
            }

            if (flatCost.HasValue || mult.HasValue)
            {
                _balance.SetUpgradeOverride(modId, kv.Key, flatCost, mult);
            }
        }
    }

    private void ApplyStoreRatesOverrides(string modId, JsonValue section)
    {
        if (section.Kind != JsonKind.Object) throw new FormatException("'overrides.storeRates' must be a JSON object.");

        int? scriptLimit = null;
        double? priceMult = null;
        double? patienceMult = null;

        if (section.TryGetProperty("scriptCharLimit", out JsonValue? limitVal) && limitVal is not null)
        {
            if (limitVal.Kind != JsonKind.Number) throw new FormatException("'storeRates.scriptCharLimit' must be a number.");
            scriptLimit = limitVal.AsInt32();
        }

        if (section.TryGetProperty("priceMultiplier", out JsonValue? pmVal) && pmVal is not null)
        {
            if (pmVal.Kind != JsonKind.Number) throw new FormatException("'storeRates.priceMultiplier' must be a number.");
            priceMult = pmVal.AsNumber();
        }

        if (section.TryGetProperty("patienceMultiplier", out JsonValue? patVal) && patVal is not null)
        {
            if (patVal.Kind != JsonKind.Number) throw new FormatException("'storeRates.patienceMultiplier' must be a number.");
            patienceMult = patVal.AsNumber();
        }

        if (scriptLimit.HasValue || priceMult.HasValue || patienceMult.HasValue)
        {
            _balance.SetStoreRates(modId, new StoreRatesOverride(modId, scriptLimit, priceMult, patienceMult));
        }
    }

    private void ApplyMaterialOverrides(string modId, JsonValue section)
    {
        if (section.Kind != JsonKind.Object) throw new FormatException("'overrides.materials' must be a JSON object.");

        foreach (KeyValuePair<string, JsonValue> kv in section.AsObject())
        {
            if (kv.Value.Kind == JsonKind.String)
            {
                _visuals.RegisterMaterialColorOverride(new MaterialColorOverride(modId, kv.Key, kv.Value.AsString()));
            }
            else if (kv.Value.Kind == JsonKind.Object)
            {
                if (!kv.Value.TryGetProperty("color", out JsonValue? colVal) || colVal is null || colVal.Kind != JsonKind.String)
                {
                    throw new FormatException($"Material override '{kv.Key}' requires a string 'color'.");
                }

                float? smoothness = null;
                if (kv.Value.TryGetProperty("smoothness", out JsonValue? smVal) && smVal is not null)
                {
                    if (smVal.Kind != JsonKind.Number) throw new FormatException($"Material '{kv.Key}.smoothness' must be a number.");
                    smoothness = (float)smVal.AsNumber();
                }

                _visuals.RegisterMaterialColorOverride(new MaterialColorOverride(modId, kv.Key, colVal.AsString(), smoothness));
            }
            else
            {
                throw new FormatException($"Material override '{kv.Key}' must be a hex string or object.");
            }
        }
    }
}
