using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.DataPacks;
using CodeMarket.ModAPI.Core.Lifecycle;
using CodeMarket.ModAPI.Unity.Patches;
using CodeMarket.ModAPI.Unity.Safety;
using CodeMarket.ModAPI.Unity.UI;
using UnityEngine;

namespace CodeMarket.ModAPI.Unity;

[BepInPlugin(ModApiInfo.PluginGuid, ModApiInfo.PluginName, ModApiInfo.PluginVersion)]
public sealed class ModApiPlugin : BaseUnityPlugin
{
    private Harmony? _harmony;
    private ConfigEntry<bool>? _disableAchievements;
    private ConfigEntry<KeyCode>? _modMenuToggleKey;
    private ConfigEntry<bool>? _verboseHookLogging;
    private ConfigEntry<string>? _disabledModIds;

    private void Awake()
    {
        _disableAchievements = Config.Bind(
            "Safety",
            "DisableAchievementsWhenModded",
            false,
            "When true, suppresses Steam achievement unlocks while CodeMarket.ModAPI is loaded. Leaderboards are always disabled.");

        _modMenuToggleKey = Config.Bind(
            "UI",
            "ModMenuToggleKey",
            KeyCode.F8,
            "Keyboard shortcut to toggle the in-game CodeMarket.ModAPI Mod Menu overlay.");

        _verboseHookLogging = Config.Bind(
            "Diagnostics",
            "VerboseHookLogging",
            false,
            "Emit verbose diagnostic logs for HarmonyX hooks.");

        _disabledModIds = Config.Bind(
            "Mods",
            "DisabledModIds",
            string.Empty,
            "Comma-separated list of mod IDs disabled via the in-game F8 Mod Menu.");

        var logger = new BepInExModLogger(Logger);
        ModApiRuntime.Initialize(
            logger,
            _disableAchievements.Value,
            _modMenuToggleKey.Value,
            _verboseHookLogging.Value);

        if (!string.IsNullOrWhiteSpace(_disabledModIds.Value))
        {
            ModApiRuntime.Lifecycle.SetInitiallyDisabled(
                _disabledModIds.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        ModApiRuntime.SetDisabledPersistenceCallback(ids =>
        {
            if (_disabledModIds is null) return;
            _disabledModIds.Value = string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));
            Config.Save();
        });

        logger.Info($"[ModAPI] Initializing {ModApiInfo.PluginName} v{ModApiInfo.PluginVersion} " +
                    $"(API {ModApiInfo.ApiVersion}) on Unity {Application.unityVersion}.");

        _harmony = new Harmony(ModApiInfo.PluginGuid);

        // Always install Steam leaderboard protection first so leaderboards stay clean even if a later mod fails.
        SteamSafetyPatches.Apply(_harmony, logger);

        DiscoverAndLoadDataPacks(logger);

        BalancePatches.Apply(_harmony, logger);
        ScriptingPatches.Apply(_harmony, logger);
        VisualPatches.Apply(_harmony, logger);
        SaveSidecarPatches.Apply(_harmony, logger);

        ModMenuOverlay overlay = gameObject.AddComponent<ModMenuOverlay>();
        SaveSidecarPatches.SidecarWarningRaised += overlay.NotifySidecarWarning;

        logger.Info("[ModAPI] Language restriction guard active: built-in languages only.");
    }

    private void Start()
    {
        // Audit Harmony patches once all BepInEx plugins have completed Awake().
        LanguageRestrictionGuard.AuditAndEnforce(ModApiRuntime.Logger);
    }

    private static void DiscoverAndLoadDataPacks(IModLogger logger)
    {
        string pluginsRoot = Paths.PluginPath;
        if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
        {
            logger.Warning($"[ModAPI] BepInEx plugins directory not found: '{pluginsRoot}'.");
            return;
        }

        string[] manifestFiles;
        try
        {
            manifestFiles = Directory.GetFiles(pluginsRoot, "mod.json", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            logger.Warning($"[ModAPI] Failed to scan '{pluginsRoot}' for mod.json files: {ex.Message}");
            return;
        }

        var rawPacks = new List<(string FallbackId, string ManifestJson)>();
        foreach (string path in manifestFiles)
        {
            string folderName = Path.GetFileName(Path.GetDirectoryName(path) ?? "unknown-pack");
            try
            {
                string json = File.ReadAllText(path);
                rawPacks.Add((folderName, json));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ModApiRuntime.Lifecycle.DiscoverAndValidate(folderName, "{");
                logger.Error($"[ModAPI] Could not read '{path}': {ex.Message}");
            }
        }

        var loader = new DataPackLoader(ModApiRuntime.Lifecycle, ModApiRuntime.Balance, ModApiRuntime.Visuals, logger);
        ModApiRuntime.SetDataPackLoader(loader);
        loader.LoadPacks(rawPacks);

        foreach (ModRecord record in ModApiRuntime.Lifecycle.Mods)
        {
            if (record.State == ModState.Registered)
            {
                ModApiRuntime.Lifecycle.ExecuteModPhase(record.Id, ModState.Active, () => { });
            }
        }
    }
}
