using System;
using System.Collections.Generic;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.DataPacks;
using CodeMarket.ModAPI.Core.Lifecycle;

namespace CodeMarket.ModAPI.Core.Tests;

internal static class DataPackTests
{
    public static void Register()
    {
        TestHarness.Register("datapacks", "DP-01_HappyPathOverrides", () =>
        {
            const string packJson = """
            {
              "id": "sappsworld.balance-pack",
              "name": "Balance Pack",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "type": "datapack",
              "overrides": {
                "products": {
                  "Tomato": { "sellPrice": 15 }
                },
                "upgrades": {
                  "racks.tomato": { "costMultiplier": 0.5 }
                },
                "storeRates": {
                  "scriptCharLimit": 32768,
                  "priceMultiplier": 1.2
                },
                "materials": {
                  "#FF0000": "#00FFAA"
                }
              }
            }
            """;

            var log = new MemoryLogger();
            var life = new ModLifecycleManager(log);
            var bal = new BalanceRegistry(log);
            var vis = new VisualRegistry();
            var loader = new DataPackLoader(life, bal, vis, log);

            loader.LoadPacks(new[] { ("pack1", packJson) });

            TestHarness.CheckEq(0, life.QuarantinedMods.Count, "No quarantined packs");
            TestHarness.CheckTrue(bal.TryGetProductPrice("Tomato", out int price), "Tomato price overridden");
            TestHarness.CheckEq(15, price, "Tomato sellPrice");
            TestHarness.CheckTrue(bal.TryGetUpgradeOverride("racks.tomato", out UpgradeOverride? up), "Upgrade overridden");
            TestHarness.CheckEq(0.5, up!.CostMultiplier!.Value, "Upgrade multiplier");
            TestHarness.CheckEq(32768, bal.EffectiveScriptCharLimit!.Value, "Script char limit");
            TestHarness.CheckTrue(vis.TryGetReplacementHex("ff0000", out MaterialColorOverride? mat), "Material override");
            TestHarness.CheckEq("#00FFAA", mat!.ReplacementHex, "Material replacement hex");
        });

        TestHarness.Register("datapacks", "DP-02_InvalidNegativeAndZeroBoundsQuarantined", () =>
        {
            const string badPrice = """
            {
              "id": "bad.price",
              "name": "Bad Price",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "type": "datapack",
              "overrides": {
                "products": { "Tomato": { "sellPrice": -5 } }
              }
            }
            """;

            const string badMult = """
            {
              "id": "bad.mult",
              "name": "Bad Mult",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "type": "datapack",
              "overrides": {
                "upgrades": { "racks.tomato": { "costMultiplier": 0 } }
              }
            }
            """;

            var log = new MemoryLogger();
            var life = new ModLifecycleManager(log);
            var bal = new BalanceRegistry(log);
            var vis = new VisualRegistry();
            var loader = new DataPackLoader(life, bal, vis, log);

            loader.LoadPacks(new[] { ("p1", badPrice), ("p2", badMult) });

            TestHarness.CheckEq(2, life.QuarantinedMods.Count, "Both invalid packs quarantined");
            TestHarness.CheckTrue(!bal.TryGetProductPrice("Tomato", out _), "Quarantined product rolled back");
            TestHarness.CheckTrue(!bal.TryGetUpgradeOverride("racks.tomato", out _), "Quarantined upgrade rolled back");
        });

        TestHarness.Register("datapacks", "DP-03_UnknownSectionWarnsNonFatally", () =>
        {
            const string packJson = """
            {
              "id": "future.pack",
              "name": "Future Pack",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "type": "datapack",
              "overrides": {
                "futureFeature": { "foo": 1 },
                "products": { "Tomato": { "sellPrice": 20 } }
              }
            }
            """;

            var log = new MemoryLogger();
            var life = new ModLifecycleManager(log);
            var bal = new BalanceRegistry(log);
            var vis = new VisualRegistry();
            var loader = new DataPackLoader(life, bal, vis, log);

            loader.LoadPacks(new[] { ("future", packJson) });

            TestHarness.CheckEq(0, life.QuarantinedMods.Count, "Unknown section must not quarantine");
            TestHarness.CheckTrue(log.Warnings.Count >= 1, "Unknown section emits warning");
            TestHarness.CheckTrue(bal.TryGetProductPrice("Tomato", out int price) && price == 20, "Valid entries still apply");
        });

        TestHarness.Register("datapacks", "DP-04_DeterministicPriorityOrder", () =>
        {
            const string packB = "{\"id\":\"b.pack\",\"name\":\"B\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\",\"type\":\"datapack\",\"overrides\":{\"products\":{\"Tomato\":{\"sellPrice\":99}}}}";
            const string packA = "{\"id\":\"a.pack\",\"name\":\"A\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\",\"type\":\"datapack\",\"overrides\":{\"products\":{\"Tomato\":{\"sellPrice\":11}}}}";

            var log = new MemoryLogger();
            var life = new ModLifecycleManager(log);
            var bal = new BalanceRegistry(log);
            var vis = new VisualRegistry();
            var loader = new DataPackLoader(life, bal, vis, log);

            // Supply in reverse order; b.pack must still apply after a.pack deterministically.
            loader.LoadPacks(new[] { ("b", packB), ("a", packA) });

            TestHarness.CheckTrue(bal.TryGetProductPrice("Tomato", out int price), "Price present");
            TestHarness.CheckEq(99, price, "Lexicographically later mod ID wins deterministically");
        });

        TestHarness.Register("datapacks", "DP-05_ScriptRegistryContract", () =>
        {
            var reg = new ScriptRegistry();
            var desc = new ScriptMethodDescriptor(
                "sappsworld.qol",
                ScriptTarget.Store,
                "getModVersion",
                "String",
                "Returns active ModAPI version.",
                Array.Empty<(string, string)>(),
                (_, _) => ModApiInfo.PluginVersion);

            reg.RegisterMethod(desc);
            TestHarness.CheckEq(1, reg.GetMethods(ScriptTarget.Store).Count, "Store method registered");

            TestHarness.CheckThrows<InvalidOperationException>(
                () => reg.RegisterMethod(desc),
                "Duplicate method name must throw");

            TestHarness.CheckThrows<ArgumentException>(
                () => reg.RegisterMethod(new ScriptMethodDescriptor(
                    "sappsworld.qol", ScriptTarget.Robot, "123bad", "void", "", Array.Empty<(string, string)>(), (_, _) => null)),
                "Invalid identifier must throw");

            reg.RemoveByMod("sappsworld.qol");
            TestHarness.CheckEq(0, reg.GetMethods(ScriptTarget.Store).Count, "RemoveByMod purges script methods");
        });
    }
}
