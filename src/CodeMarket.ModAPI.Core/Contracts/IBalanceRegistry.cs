namespace CodeMarket.ModAPI.Core.Contracts;

public sealed class ProductOverride
{
    public string ModId { get; }
    public string ProductName { get; }
    public int? SellPrice { get; }

    public ProductOverride(string modId, string productName, int? sellPrice)
    {
        ModId = modId;
        ProductName = productName;
        SellPrice = sellPrice;
    }
}

public sealed class UpgradeOverride
{
    public string ModId { get; }
    public string UpgradeId { get; }
    public int? FlatCost { get; }
    public double? CostMultiplier { get; }

    public UpgradeOverride(string modId, string upgradeId, int? flatCost, double? costMultiplier)
    {
        ModId = modId;
        UpgradeId = upgradeId;
        FlatCost = flatCost;
        CostMultiplier = costMultiplier;
    }
}

public sealed class StoreRatesOverride
{
    public string ModId { get; }
    public int? ScriptCharLimit { get; }
    public double? PriceMultiplier { get; }
    public double? PatienceMultiplier { get; }

    public StoreRatesOverride(string modId, int? scriptCharLimit, double? priceMultiplier, double? patienceMultiplier)
    {
        ModId = modId;
        ScriptCharLimit = scriptCharLimit;
        PriceMultiplier = priceMultiplier;
        PatienceMultiplier = patienceMultiplier;
    }
}

public interface IBalanceRegistry
{
    void SetProductPrice(string modId, string productName, int sellPrice);
    void SetUpgradeOverride(string modId, string upgradeId, int? flatCost, double? costMultiplier);
    void SetStoreRates(string modId, StoreRatesOverride rates);
    bool TryGetProductPrice(string productName, out int sellPrice);
    bool TryGetUpgradeOverride(string upgradeId, out UpgradeOverride? ov);
    int? EffectiveScriptCharLimit { get; }
    double EffectivePriceMultiplier { get; }
    double EffectivePatienceMultiplier { get; }
    void RemoveByMod(string modId);
}
