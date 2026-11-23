using System;
using HarmonyLib;
using JavaMarket.App;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity.Safety;

internal static class SteamSafetyPatches
{
    private const string ModdedBoardProblem = "Leaderboards disabled while CodeMarket.ModAPI is active.";

    public static void Apply(Harmony harmony, IModLogger logger)
    {
        if (harmony is null) throw new ArgumentNullException(nameof(harmony));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        PatchMethod(harmony, logger, typeof(SteamDayBoard), nameof(SteamDayBoard.Open), nameof(OpenDayBoardPrefix));
        PatchMethod(harmony, logger, typeof(SteamDayBoard), nameof(SteamDayBoard.Submit), nameof(SubmitDayBoardPrefix));
        PatchMethod(harmony, logger, typeof(SteamDayBoard), "RaiseBestDayStat", nameof(RaiseBestDayStatPrefix));
        PatchMethod(harmony, logger, typeof(SteamAchievementSink), nameof(SteamAchievementSink.Unlock), nameof(UnlockAchievementPrefix));

        ModApiRuntime.LeaderboardGuardActive = true;
        logger.Info("[ModAPI] Steam safety guards active: SteamDayBoard -> NoDayBoard (unconditional); " +
                    $"Achievements disabled when modded = {ModApiRuntime.DisableAchievementsWhenModded}.");
    }

    private static void PatchMethod(Harmony harmony, IModLogger logger, Type targetType, string methodName, string prefixName)
    {
        var target = AccessTools.Method(targetType, methodName);
        var prefix = AccessTools.Method(typeof(SteamSafetyPatches), prefixName);
        if (target is null || prefix is null)
        {
            logger.Warning($"[ModAPI] Could not locate safety hook target {targetType.FullName}.{methodName}.");
            return;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
    }

    private static bool OpenDayBoardPrefix(ref IDayBoard __result)
    {
        __result = new NoDayBoard(ModdedBoardProblem);
        return false;
    }

    private static bool SubmitDayBoardPrefix(Action<bool>? done)
    {
        done?.Invoke(false);
        return false;
    }

    private static bool RaiseBestDayStatPrefix()
    {
        return false;
    }

    private static bool UnlockAchievementPrefix(string id)
    {
        if (!ModApiRuntime.DisableAchievementsWhenModded)
        {
            return true;
        }

        if (ModApiRuntime.VerboseHookLogging)
        {
            ModApiRuntime.Logger.Info($"[ModAPI] Suppressed Steam achievement '{id}' (DisableAchievementsWhenModded=true).");
        }

        return false;
    }
}
