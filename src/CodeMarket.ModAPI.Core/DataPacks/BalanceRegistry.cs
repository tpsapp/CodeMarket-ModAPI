using System;
using System.Collections.Generic;
using System.Linq;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.DataPacks;

public sealed class BalanceRegistry : IBalanceRegistry
{
    private const int MinScriptCharLimit = 1024;

    private readonly IModLogger _logger;
    private readonly List<ProductOverride> _productOverrides = new List<ProductOverride>();
    private readonly List<UpgradeOverride> _upgradeOverrides = new List<UpgradeOverride>();
    private readonly List<StoreRatesOverride> _storeRateOverrides = new List<StoreRatesOverride>();

    public BalanceRegistry(IModLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetProductPrice(string modId, string productName, int sellPrice)
    {
        if (!ModIdValidator.IsValid(modId)) throw new ArgumentException($"Invalid modId '{modId}'.", nameof(modId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (sellPrice < 0) throw new ArgumentOutOfRangeException(nameof(sellPrice), "Product sellPrice cannot be negative.");

        string key = productName.Trim();
        if (_productOverrides.Any(p => string.Equals(p.ProductName, key, StringComparison.OrdinalIgnoreCase) && p.ModId != modId))
        {
            _logger.Info($"[ModAPI] Product '{key}' price override replaced by mod '{modId}' (sellPrice={sellPrice}).");
        }

        _productOverrides.RemoveAll(p => p.ModId == modId && string.Equals(p.ProductName, key, StringComparison.OrdinalIgnoreCase));
        _productOverrides.Add(new ProductOverride(modId, key, sellPrice));
    }

    public void SetUpgradeOverride(string modId, string upgradeId, int? flatCost, double? costMultiplier)
    {
        if (!ModIdValidator.IsValid(modId)) throw new ArgumentException($"Invalid modId '{modId}'.", nameof(modId));
        if (string.IsNullOrWhiteSpace(upgradeId)) throw new ArgumentException("Upgrade ID is required.", nameof(upgradeId));
        if (flatCost.HasValue && flatCost.Value < 0) throw new ArgumentOutOfRangeException(nameof(flatCost), "Upgrade flatCost cannot be negative.");
        if (costMultiplier.HasValue && (double.IsNaN(costMultiplier.Value) || double.IsInfinity(costMultiplier.Value) || costMultiplier.Value <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(costMultiplier), "Upgrade costMultiplier must be a finite number > 0.");
        }

        string key = upgradeId.Trim();
        if (_upgradeOverrides.Any(u => string.Equals(u.UpgradeId, key, StringComparison.OrdinalIgnoreCase) && u.ModId != modId))
        {
            _logger.Info($"[ModAPI] Upgrade '{key}' override replaced by mod '{modId}'.");
        }

        _upgradeOverrides.RemoveAll(u => u.ModId == modId && string.Equals(u.UpgradeId, key, StringComparison.OrdinalIgnoreCase));
        _upgradeOverrides.Add(new UpgradeOverride(modId, key, flatCost, costMultiplier));
    }

    public void SetStoreRates(string modId, StoreRatesOverride rates)
    {
        if (!ModIdValidator.IsValid(modId)) throw new ArgumentException($"Invalid modId '{modId}'.", nameof(modId));
        if (rates is null) throw new ArgumentNullException(nameof(rates));

        if (rates.ScriptCharLimit.HasValue && rates.ScriptCharLimit.Value < MinScriptCharLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(rates), $"scriptCharLimit must be >= {MinScriptCharLimit}.");
        }

        ValidatePositiveMultiplier(rates.PriceMultiplier, "priceMultiplier");
        ValidatePositiveMultiplier(rates.PatienceMultiplier, "patienceMultiplier");

        _storeRateOverrides.RemoveAll(r => r.ModId == modId);
        _storeRateOverrides.Add(new StoreRatesOverride(modId, rates.ScriptCharLimit, rates.PriceMultiplier, rates.PatienceMultiplier));
    }

    public bool TryGetProductPrice(string productName, out int sellPrice)
    {
        sellPrice = 0;
        if (string.IsNullOrWhiteSpace(productName)) return false;

        for (int i = _productOverrides.Count - 1; i >= 0; i--)
        {
            ProductOverride ov = _productOverrides[i];
            if (string.Equals(ov.ProductName, productName, StringComparison.OrdinalIgnoreCase) && ov.SellPrice.HasValue)
            {
                sellPrice = ov.SellPrice.Value;
                return true;
            }
        }

        return false;
    }

    public bool TryGetUpgradeOverride(string upgradeId, out UpgradeOverride? ov)
    {
        ov = null;
        if (string.IsNullOrWhiteSpace(upgradeId)) return false;

        for (int i = _upgradeOverrides.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_upgradeOverrides[i].UpgradeId, upgradeId, StringComparison.OrdinalIgnoreCase))
            {
                ov = _upgradeOverrides[i];
                return true;
            }
        }

        return false;
    }

    public int? EffectiveScriptCharLimit
    {
        get
        {
            int? maxLimit = null;
            foreach (StoreRatesOverride r in _storeRateOverrides)
            {
                if (r.ScriptCharLimit.HasValue && (!maxLimit.HasValue || r.ScriptCharLimit.Value > maxLimit.Value))
                {
                    maxLimit = r.ScriptCharLimit.Value;
                }
            }

            return maxLimit;
        }
    }

    public double EffectivePriceMultiplier
    {
        get
        {
            double mult = 1.0;
            foreach (StoreRatesOverride r in _storeRateOverrides)
            {
                if (r.PriceMultiplier.HasValue) mult *= r.PriceMultiplier.Value;
            }

            return mult;
        }
    }

    public double EffectivePatienceMultiplier
    {
        get
        {
            double mult = 1.0;
            foreach (StoreRatesOverride r in _storeRateOverrides)
            {
                if (r.PatienceMultiplier.HasValue) mult *= r.PatienceMultiplier.Value;
            }

            return mult;
        }
    }

    public void RemoveByMod(string modId)
    {
        _productOverrides.RemoveAll(p => p.ModId == modId);
        _upgradeOverrides.RemoveAll(u => u.ModId == modId);
        _storeRateOverrides.RemoveAll(r => r.ModId == modId);
    }

    private static void ValidatePositiveMultiplier(double? value, string fieldName)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(fieldName, $"{fieldName} must be a finite number > 0.");
        }
    }
}
