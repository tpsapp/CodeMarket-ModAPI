using System;
using HarmonyLib;
using JavaMarket.World.Rendering;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity.Patches;

internal static class VisualPatches
{
    public static void Apply(Harmony harmony, IModLogger logger)
    {
        if (harmony is null) throw new ArgumentNullException(nameof(harmony));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        var target = AccessTools.Method(typeof(ModelMaterials), nameof(ModelMaterials.Solid), new[] { typeof(string), typeof(float) });
        var prefix = AccessTools.Method(typeof(VisualPatches), nameof(SolidMaterialPrefix));
        if (target is null || prefix is null)
        {
            logger.Warning("[ModAPI] Could not locate ModelMaterials.Solid(string, float) for visual reskin hook.");
            return;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
    }

    private static void SolidMaterialPrefix(ref string hex, ref float smoothness)
    {
        if (string.IsNullOrEmpty(hex)) return;

        if (ModApiRuntime.Visuals.TryGetReplacementHex(hex, out MaterialColorOverride? ov) && ov is not null)
        {
            hex = ov.ReplacementHex;
            if (ov.Smoothness.HasValue)
            {
                smoothness = ov.Smoothness.Value;
            }
        }
    }
}
