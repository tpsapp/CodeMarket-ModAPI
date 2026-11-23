using System;
using HarmonyLib;
using JavaMarket.App.Persistence;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity.Patches;

public static class SaveSidecarPatches
{
    public static event Action<ISaveSidecarStore>? BeforeSidecarSave;
    public static event Action<ISaveSidecarStore>? AfterSidecarLoad;
    public static event Action<string>? SidecarWarningRaised;

    internal static void Apply(Harmony harmony, IModLogger logger)
    {
        if (harmony is null) throw new ArgumentNullException(nameof(harmony));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        PatchPostfix(harmony, logger, typeof(SaveStorage), nameof(SaveStorage.Write), new[] { typeof(SaveData) }, nameof(WritePostfix));
        PatchPostfix(harmony, logger, typeof(SaveStorage), nameof(SaveStorage.Read), new[] { typeof(string).MakeByRefType() }, nameof(ReadPostfix));
        PatchPostfix(harmony, logger, typeof(SaveStorage), nameof(SaveStorage.Delete), Type.EmptyTypes, nameof(DeletePostfix));
    }

    private static void PatchPostfix(Harmony harmony, IModLogger logger, Type targetType, string methodName, Type[] paramTypes, string postfixName)
    {
        var target = AccessTools.Method(targetType, methodName, paramTypes);
        var postfix = AccessTools.Method(typeof(SaveSidecarPatches), postfixName);
        if (target is null || postfix is null)
        {
            logger.Warning($"[ModAPI] Save sidecar hook target missing: {targetType.FullName}.{methodName}");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(postfix));
    }

    private static void WritePostfix(SaveStorage __instance)
    {
        if (__instance is null || string.IsNullOrWhiteSpace(__instance.FilePath)) return;

        try
        {
            BeforeSidecarSave?.Invoke(ModApiRuntime.Sidecars);
            SidecarSaveResult res = ModApiRuntime.Sidecars.SaveForSave(__instance.FilePath);
            if (!res.Success && !string.IsNullOrWhiteSpace(res.ErrorMessage))
            {
                SidecarWarningRaised?.Invoke(res.ErrorMessage!);
            }
        }
        catch (Exception ex)
        {
            ModApiRuntime.Logger.Error($"[ModAPI] Sidecar save callback failed: {ex.Message}");
        }
    }

    private static void ReadPostfix(SaveStorage __instance)
    {
        if (__instance is null || string.IsNullOrWhiteSpace(__instance.FilePath)) return;

        try
        {
            SidecarLoadResult res = ModApiRuntime.Sidecars.LoadForSave(__instance.FilePath);
            if (res.RecoveredFromCorruption && !string.IsNullOrWhiteSpace(res.WarningMessage))
            {
                SidecarWarningRaised?.Invoke(res.WarningMessage!);
            }

            AfterSidecarLoad?.Invoke(ModApiRuntime.Sidecars);
        }
        catch (Exception ex)
        {
            ModApiRuntime.Logger.Error($"[ModAPI] Sidecar load callback failed: {ex.Message}");
        }
    }

    private static void DeletePostfix(SaveStorage __instance)
    {
        if (__instance is null || string.IsNullOrWhiteSpace(__instance.FilePath)) return;
        ModApiRuntime.Sidecars.DeleteForSave(__instance.FilePath);
    }
}
