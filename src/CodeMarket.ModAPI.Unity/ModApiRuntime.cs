using System;
using System.Collections.Generic;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.DataPacks;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Core.Lifecycle;
using CodeMarket.ModAPI.Core.Sidecars;
using UnityEngine;

namespace CodeMarket.ModAPI.Unity;

public static class ModApiRuntime
{
    private static bool _initialized;
    private static ModLifecycleManager? _lifecycle;
    private static BalanceRegistry? _balance;
    private static ScriptRegistry? _scripting;
    private static VisualRegistry? _visuals;
    private static SidecarStore? _sidecars;
    private static IModLogger? _logger;
    private static Action? _onScriptRegistryChanged;
    private static DataPackLoader? _dataPackLoader;
    private static Action<IReadOnlyCollection<string>>? _persistDisabledMods;
    private static readonly Dictionary<string, ICodeMarketMod> PluginMods =
        new Dictionary<string, ICodeMarketMod>(StringComparer.Ordinal);

    public static ModLifecycleManager Lifecycle => Require(_lifecycle);
    public static BalanceRegistry Balance => Require(_balance);
    public static ScriptRegistry Scripting => Require(_scripting);
    public static VisualRegistry Visuals => Require(_visuals);
    public static SidecarStore Sidecars => Require(_sidecars);
    public static IModLogger Logger => Require(_logger);

    public static bool DisableAchievementsWhenModded { get; internal set; }
    public static KeyCode ModMenuToggleKey { get; internal set; } = KeyCode.F8;
    public static bool VerboseHookLogging { get; internal set; }
    public static bool LeaderboardGuardActive { get; internal set; }

    internal static void Initialize(
        IModLogger logger,
        bool disableAchievementsWhenModded,
        KeyCode modMenuToggleKey,
        bool verboseHookLogging,
        Action? onScriptRegistryChanged = null)
    {
        if (_initialized) return;

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycle = new ModLifecycleManager(logger, ModApiInfo.ApiVersion);
        _balance = new BalanceRegistry(logger);
        _scripting = new ScriptRegistry();
        _visuals = new VisualRegistry();
        _sidecars = new SidecarStore(logger);
        DisableAchievementsWhenModded = disableAchievementsWhenModded;
        ModMenuToggleKey = modMenuToggleKey;
        VerboseHookLogging = verboseHookLogging;
        _onScriptRegistryChanged = onScriptRegistryChanged;
        _initialized = true;
    }

    internal static void SetScriptSyncCallback(Action callback)
    {
        _onScriptRegistryChanged = callback;
    }

    internal static void SetDataPackLoader(DataPackLoader loader)
    {
        _dataPackLoader = loader;
    }

    internal static void SetDisabledPersistenceCallback(Action<IReadOnlyCollection<string>> callback)
    {
        _persistDisabledMods = callback;
    }

    public static bool SetModEnabled(string modId, bool enabled)
    {
        if (!_initialized) return false;

        bool changed = Lifecycle.SetDisabled(
            modId,
            disabled: !enabled,
            onDisable: () => RollbackMod(modId),
            onEnable: () =>
            {
                if (PluginMods.TryGetValue(modId, out ICodeMarketMod? plugin))
                {
                    var ctx = new ModRegistrationContext(Balance, Scripting, Visuals, Logger);
                    plugin.Register(ctx);
                    _onScriptRegistryChanged?.Invoke();
                }
                else
                {
                    _dataPackLoader?.TryReapplyPack(modId);
                }
            },
            rollbackOnEnableError: () => RollbackMod(modId));

        if (changed)
        {
            _persistDisabledMods?.Invoke(Lifecycle.DisabledModIds);
        }

        return changed;
    }

    public static bool RegisterPluginMod(ICodeMarketMod mod)
    {
        if (mod is null) throw new ArgumentNullException(nameof(mod));
        if (!_initialized)
        {
            throw new InvalidOperationException("CodeMarket.ModAPI has not initialized yet. Declare a [BepInDependency] on CodeMarket.ModAPI.");
        }

        string manifestJson = JsonWriter.Serialize(JsonValue.FromObject(new[]
        {
            new KeyValuePair<string, JsonValue>("id", JsonValue.FromString(mod.ModId ?? string.Empty)),
            new KeyValuePair<string, JsonValue>("name", JsonValue.FromString(mod.Name ?? string.Empty)),
            new KeyValuePair<string, JsonValue>("version", JsonValue.FromString(mod.Version ?? string.Empty)),
            new KeyValuePair<string, JsonValue>("description", JsonValue.FromString(mod.Description ?? string.Empty)),
            new KeyValuePair<string, JsonValue>("apiVersion", JsonValue.FromString(ModApiInfo.ApiVersion)),
            new KeyValuePair<string, JsonValue>("type", JsonValue.FromString("plugin"))
        }), indented: false);

        ModRecord record = Lifecycle.DiscoverAndValidate(mod.ModId ?? "unknown-plugin", manifestJson);
        if (record.State == ModState.Disabled)
        {
            // Keep reference so the user can re-enable this plugin live from the F8 Mod Menu.
            PluginMods[record.Id] = mod;
            return false;
        }

        if (record.State != ModState.Validated)
        {
            return false;
        }

        PluginMods[record.Id] = mod;

        var context = new ModRegistrationContext(Balance, Scripting, Visuals, Logger);
        bool registered = Lifecycle.ExecuteModPhase(
            record.Id,
            ModState.Registered,
            () => mod.Register(context),
            rollback: () => RollbackMod(record.Id));

        if (!registered) return false;

        bool activated = Lifecycle.ExecuteModPhase(
            record.Id,
            ModState.Active,
            () => _onScriptRegistryChanged?.Invoke(),
            rollback: () => RollbackMod(record.Id));

        if (activated)
        {
            Logger.Info($"[ModAPI] Plugin mod '{record.Id}' v{mod.Version} is Active.");
        }

        return activated;
    }

    internal static void RollbackMod(string modId)
    {
        Balance.RemoveByMod(modId);
        Scripting.RemoveByMod(modId);
        Visuals.RemoveByMod(modId);
    }

    private static T Require<T>(T? value) where T : class
    {
        if (value is null)
        {
            throw new InvalidOperationException("CodeMarket.ModAPI runtime is not initialized.");
        }

        return value;
    }
}
