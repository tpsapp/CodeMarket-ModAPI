using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Unity;
using CodeMarket.ModAPI.Unity.Patches;

namespace CodeMarket.Mods.QoL;

[BepInPlugin("sappsworld.codemarket.qol", "CodeMarket Reference QoL", "1.0.0")]
[BepInDependency(ModApiInfo.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class QoLModPlugin : BaseUnityPlugin, ICodeMarketMod
{
    private const int MinCharLimit = 1024;
    private const int MaxCharLimit = 262144;

    private ConfigEntry<int>? _scriptCharLimit;
    private int _effectiveCharLimit = 65536;

    public string ModId => "sappsworld.qol";
    public string Name => "CodeMarket Reference QoL";
    public string Version => "1.0.0";
    public string Description => "Configurable script character limit, custom Java/C# Robot & Store API methods, and per-save sidecar example.";

    private void Awake()
    {
        _scriptCharLimit = Config.Bind(
            "Editor",
            "ScriptCharacterLimit",
            65536,
            $"Maximum character limit applied to CodeInputField ({MinCharLimit}..{MaxCharLimit}).");

        _effectiveCharLimit = Math.Max(MinCharLimit, Math.Min(MaxCharLimit, _scriptCharLimit.Value));

        ModApiRuntime.RegisterPluginMod(this);

        SaveSidecarPatches.BeforeSidecarSave += OnBeforeSidecarSave;
        SaveSidecarPatches.AfterSidecarLoad += OnAfterSidecarLoad;
    }

    public void Register(ModRegistrationContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Balance.SetStoreRates(ModId, new StoreRatesOverride(
            ModId,
            scriptCharLimit: _effectiveCharLimit,
            priceMultiplier: null,
            patienceMultiplier: null));

        context.Scripting.RegisterMethod(new ScriptMethodDescriptor(
            ModId,
            ScriptTarget.Store,
            "getModApiVersion",
            "String",
            "Returns the active CodeMarket.ModAPI version string.",
            Array.Empty<(string Type, string Name)>(),
            (_, _) => ModApiInfo.PluginVersion));

        context.Scripting.RegisterMethod(new ScriptMethodDescriptor(
            ModId,
            ScriptTarget.Robot,
            "getModInfo",
            "String",
            "Returns active ModAPI and QoL mod summary.",
            Array.Empty<(string Type, string Name)>(),
            (_, _) => $"CodeMarket.ModAPI v{ModApiInfo.PluginVersion} | QoL v{Version} | ActiveMods={ModApiRuntime.Lifecycle.Mods.Count}"));

        context.Logger.Info($"[QoL] Registered ScriptCharacterLimit={_effectiveCharLimit} and custom Java/C# methods (store.getModApiVersion, robot.getModInfo).");
    }

    private void OnBeforeSidecarSave(ISaveSidecarStore store)
    {
        var payload = JsonValue.FromObject(new[]
        {
            new KeyValuePair<string, JsonValue>("scriptCharLimit", JsonValue.FromNumber(_effectiveCharLimit)),
            new KeyValuePair<string, JsonValue>("savedUtc", JsonValue.FromString(DateTime.UtcNow.ToString("O")))
        });
        store.SetModData(ModId, Version, payload);
    }

    private void OnAfterSidecarLoad(ISaveSidecarStore store)
    {
        if (store.TryGetModData(ModId, out JsonValue? data) && data is not null &&
            data.TryGetProperty("savedUtc", out JsonValue? ts) && ts is not null && ts.Kind == JsonKind.String)
        {
            ModApiRuntime.Logger.Info($"[QoL] Restored sidecar state (last saved {ts.AsString()}).");
        }
    }
}
