using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity.Safety;

/// <summary>
/// Runtime enforcement of the Code Market creators' restriction against adding
/// new in-game coding languages or patching language/DLC internals.
/// </summary>
internal static class LanguageRestrictionGuard
{
    public static int BlockedPatchCount { get; private set; }

    public static void AuditAndEnforce(IModLogger logger)
    {
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        var offendingOwners = new HashSet<string>(StringComparer.Ordinal);
        var methodsToStrip = new List<(MethodBase Method, string Owner, string TypeName)>();

        foreach (MethodBase method in Harmony.GetAllPatchedMethods())
        {
            string? typeName = method.DeclaringType?.FullName;
            if (!LanguagePolicy.IsForbiddenPatchTarget(typeName))
            {
                continue;
            }

            HarmonyLib.Patches? patchInfo = Harmony.GetPatchInfo(method);
            if (patchInfo is null) continue;

            foreach (string owner in patchInfo.Owners)
            {
                if (string.Equals(owner, ModApiInfo.PluginGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                offendingOwners.Add(owner);
                methodsToStrip.Add((method, owner, typeName!));
            }
        }

        if (methodsToStrip.Count == 0)
        {
            return;
        }

        foreach ((MethodBase method, string owner, string typeName) in methodsToStrip)
        {
            try
            {
                var unpatcher = new Harmony(owner);
                unpatcher.Unpatch(method, HarmonyPatchType.All, owner);
                BlockedPatchCount++;
                logger.Error($"[ModAPI] Stripped forbidden patch by '{owner}' on '{typeName}.{method.Name}'. {LanguagePolicy.ViolationMessage}");
            }
            catch (Exception ex)
            {
                logger.Error($"[ModAPI] Failed to strip forbidden patch by '{owner}' on '{typeName}.{method.Name}': {ex.Message}");
            }
        }

        foreach (string owner in offendingOwners)
        {
            ModApiRuntime.Lifecycle.Quarantine(
                owner,
                "LanguagePolicyGuard",
                LanguagePolicy.ViolationMessage,
                rollback: () => ModApiRuntime.RollbackMod(owner));
        }
    }
}
