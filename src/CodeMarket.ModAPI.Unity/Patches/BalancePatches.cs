using System;
using HarmonyLib;
using JavaMarket.App;
using JavaMarket.World;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity.Patches;

internal static class BalancePatches
{
    public static void Apply(Harmony harmony, IModLogger logger)
    {
        if (harmony is null) throw new ArgumentNullException(nameof(harmony));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        PatchPostfix(harmony, logger, typeof(ProductCatalog), nameof(ProductCatalog.Price), new[] { typeof(Product) }, nameof(ProductPricePostfix));
        PatchPostfix(harmony, logger, typeof(Upgrade), nameof(Upgrade.CostOfNext), new[] { typeof(int) }, nameof(UpgradeCostOfNextPostfix));
        PatchPostfix(harmony, logger, typeof(StoreRates), "get_Price", Type.EmptyTypes, nameof(StoreRatesPricePostfix));
    }

    private static void PatchPostfix(Harmony harmony, IModLogger logger, Type targetType, string methodName, Type[] paramTypes, string postfixName)
    {
        var target = AccessTools.Method(targetType, methodName, paramTypes);
        var postfix = AccessTools.Method(typeof(BalancePatches), postfixName);
        if (target is null || postfix is null)
        {
            logger.Warning($"[ModAPI] Balance hook target missing: {targetType.FullName}.{methodName}");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(postfix));
    }

    private static void ProductPricePostfix(Product product, ref int __result)
    {
        if (ModApiRuntime.Balance.TryGetProductPrice(product.ToString(), out int overridden))
        {
            __result = overridden;
        }
    }

    private static void UpgradeCostOfNextPostfix(Upgrade __instance, ref int __result)
    {
        if (__instance is null || string.IsNullOrEmpty(__instance.Id)) return;

        if (!ModApiRuntime.Balance.TryGetUpgradeOverride(__instance.Id, out UpgradeOverride? ov) || ov is null)
        {
            return;
        }

        if (ov.FlatCost.HasValue)
        {
            __result = ov.FlatCost.Value;
        }
        else if (ov.CostMultiplier.HasValue)
        {
            __result = Math.Max(0, (int)Math.Round(__result * ov.CostMultiplier.Value));
        }
    }

    private static void StoreRatesPricePostfix(ref float __result)
    {
        double mult = ModApiRuntime.Balance.EffectivePriceMultiplier;
        if (Math.Abs(mult - 1.0) > 1e-6)
        {
            __result = (float)(__result * mult);
        }
    }
}
