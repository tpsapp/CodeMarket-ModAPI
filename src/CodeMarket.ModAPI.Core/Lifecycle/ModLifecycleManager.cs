using System;
using System.Collections.Generic;
using System.Linq;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.Lifecycle;

public sealed class ModLifecycleManager
{
    private readonly IModLogger _logger;
    private readonly string _hostApiVersion;
    private readonly List<ModRecord> _mods = new List<ModRecord>();
    private readonly Dictionary<string, ModRecord> _byId = new Dictionary<string, ModRecord>(StringComparer.Ordinal);
    private readonly HashSet<string> _disabledIds = new HashSet<string>(StringComparer.Ordinal);

    public ModLifecycleManager(IModLogger logger, string hostApiVersion = ModApiInfo.ApiVersion)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(hostApiVersion))
        {
            throw new ArgumentException("Host API version is required.", nameof(hostApiVersion));
        }

        _hostApiVersion = hostApiVersion;
    }

    public IReadOnlyList<ModRecord> Mods => _mods;

    public IReadOnlyList<ModRecord> QuarantinedMods =>
        _mods.Where(m => m.State == ModState.Quarantined).ToList();

    public bool HasActiveMods => _mods.Any(m => m.State == ModState.Active);

    public IReadOnlyCollection<string> DisabledModIds => _disabledIds;

    public void SetInitiallyDisabled(IEnumerable<string> modIds)
    {
        if (modIds is null) return;
        foreach (string raw in modIds)
        {
            string trimmed = (raw ?? string.Empty).Trim();
            if (ModIdValidator.IsValid(trimmed))
            {
                _disabledIds.Add(trimmed);
            }
        }
    }

    public bool IsDisabled(string modId) => !string.IsNullOrEmpty(modId) && _disabledIds.Contains(modId);

    public bool SetDisabled(string modId, bool disabled, Action? onDisable = null, Action? onEnable = null, Action? rollbackOnEnableError = null)
    {
        if (!_byId.TryGetValue(modId, out ModRecord? record))
        {
            _logger.Warning($"[ModAPI] Toggle requested for unknown mod '{modId}'.");
            return false;
        }

        if (record.State == ModState.Quarantined)
        {
            _logger.Warning($"[ModAPI] Cannot toggle quarantined mod '{modId}'.");
            return false;
        }

        if (disabled)
        {
            _disabledIds.Add(modId);
            record.State = ModState.Disabled;
            try
            {
                onDisable?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error($"[ModAPI] Error while disabling mod '{modId}': {ex.Message}");
            }

            _logger.Info($"[ModAPI] Disabled mod '{modId}'.");
            return true;
        }

        _disabledIds.Remove(modId);
        try
        {
            onEnable?.Invoke();
            record.State = ModState.Active;
            _logger.Info($"[ModAPI] Enabled mod '{modId}'.");
            return true;
        }
        catch (Exception ex)
        {
            QuarantineRecord(record, "Enable", $"{ex.GetType().Name}: {ex.Message}", rollbackOnEnableError);
            return false;
        }
    }

    public ModRecord DiscoverAndValidate(string fallbackId, string manifestJson)
    {
        string safeFallback = string.IsNullOrWhiteSpace(fallbackId) ? "unknown-mod" : fallbackId.Trim();

        if (!ModManifest.TryParse(manifestJson, _hostApiVersion, out ModManifest? manifest, out ManifestError err, out string errMsg) ||
            manifest is null)
        {
            var failed = new ModRecord(safeFallback, ModState.Discovered);
            _mods.Add(failed);
            QuarantineRecord(failed, "Validate", $"{err}: {errMsg}", rollback: null);
            return failed;
        }

        if (_byId.ContainsKey(manifest.Id))
        {
            var dup = new ModRecord(manifest.Id, ModState.Discovered) { Manifest = manifest };
            _mods.Add(dup);
            QuarantineRecord(dup, "Validate", $"DuplicateModId: '{manifest.Id}' is already loaded.", rollback: null);
            return dup;
        }

        var record = new ModRecord(manifest.Id, _disabledIds.Contains(manifest.Id) ? ModState.Disabled : ModState.Validated)
        {
            Manifest = manifest
        };
        _mods.Add(record);
        _byId[manifest.Id] = record;
        _logger.Info(record.State == ModState.Disabled
            ? $"[ModAPI] Validated mod '{manifest.Id}' v{manifest.Version} ({manifest.Type}) [Disabled by config]."
            : $"[ModAPI] Validated mod '{manifest.Id}' v{manifest.Version} ({manifest.Type}).");
        return record;
    }

    public bool ExecuteModPhase(string modId, ModState targetState, Action action, Action? rollback = null)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (!_byId.TryGetValue(modId, out ModRecord? record))
        {
            _logger.Warning($"[ModAPI] ExecuteModPhase ignored for unknown mod '{modId}'.");
            return false;
        }

        if (record.State == ModState.Quarantined || record.State == ModState.Disabled)
        {
            return false;
        }

        try
        {
            action();
            record.State = targetState;
            return true;
        }
        catch (Exception ex)
        {
            QuarantineRecord(record, targetState.ToString(), $"{ex.GetType().Name}: {ex.Message}", rollback);
            return false;
        }
    }

    public void Quarantine(string modId, string phase, string reason, Action? rollback = null)
    {
        if (!_byId.TryGetValue(modId, out ModRecord? record))
        {
            _logger.Warning($"[ModAPI] Quarantine requested for unknown mod '{modId}': {reason}");
            return;
        }

        QuarantineRecord(record, phase, reason, rollback);
    }

    private void QuarantineRecord(ModRecord record, string phase, string reason, Action? rollback)
    {
        record.State = ModState.Quarantined;
        record.QuarantinePhase = phase;
        record.QuarantineReason = reason;
        _logger.Error($"[ModAPI] Quarantined mod '{record.Id}' during {phase}: {reason}");

        if (rollback is not null)
        {
            try
            {
                rollback();
            }
            catch (Exception rbEx)
            {
                _logger.Error($"[ModAPI] Rollback failed for quarantined mod '{record.Id}': {rbEx.Message}");
            }
        }
    }
}
