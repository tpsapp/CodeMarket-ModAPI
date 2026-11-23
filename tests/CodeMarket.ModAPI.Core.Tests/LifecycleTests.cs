using System;
using System.Collections.Generic;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Lifecycle;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.Tests;

internal sealed class MemoryLogger : IModLogger
{
    public List<string> Infos { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();

    public void Info(string message) => Infos.Add(message);
    public void Warning(string message) => Warnings.Add(message);
    public void Error(string message) => Errors.Add(message);
}

internal static class LifecycleTests
{
    public static void Register()
    {
        TestHarness.Register("lifecycle", "MAN-01_ValidManifest", () =>
        {
            const string json = """
            {
              "id": "sappsworld.sample-pack",
              "name": "Sample Pack",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "author": "Sappsworld",
              "description": "Test pack",
              "type": "datapack"
            }
            """;

            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");
            ModRecord rec = mgr.DiscoverAndValidate("fallback", json);

            TestHarness.CheckEq(ModState.Validated, rec.State, "Valid manifest must reach Validated");
            TestHarness.CheckEq("sappsworld.sample-pack", rec.Id, "Manifest ID");
            TestHarness.CheckEq(ModType.DataPack, rec.Manifest!.Type, "Manifest type");
            TestHarness.CheckEq(0, mgr.QuarantinedMods.Count, "Zero quarantined mods");
        });

        TestHarness.Register("lifecycle", "MAN-02_InvalidModIds", () =>
        {
            string[] badIds = { "", "a", "../evil", "UPPER.case", "a/b", "a..b", new string('x', 65) };
            foreach (string bad in badIds)
            {
                TestHarness.CheckTrue(!ModIdValidator.IsValid(bad), $"ModId '{bad}' must be rejected");
            }

            TestHarness.CheckTrue(ModIdValidator.IsValid("ab"), "2-char lowercase ID valid");
            TestHarness.CheckTrue(ModIdValidator.IsValid("sappsworld.qol-1_0"), "Standard ID valid");
        });

        TestHarness.Register("lifecycle", "MAN-03_SemVerValidation", () =>
        {
            bool okBad = ModManifest.TryParse(
                "{\"id\":\"a.b\",\"name\":\"N\",\"version\":\"1.0\",\"apiVersion\":\"1.0\"}",
                "1.0", out _, out ManifestError err, out _);
            TestHarness.CheckTrue(!okBad, "1.0 must fail SemVer");
            TestHarness.CheckEq(ManifestError.InvalidVersion, err, "Expected InvalidVersion");

            bool okPre = ModManifest.TryParse(
                "{\"id\":\"a.b\",\"name\":\"N\",\"version\":\"1.0.0-beta.1\",\"apiVersion\":\"1.0\"}",
                "1.0", out ModManifest? m, out _, out _);
            TestHarness.CheckTrue(okPre && m is not null, "1.0.0-beta.1 must pass SemVer");
        });

        TestHarness.Register("lifecycle", "MAN-04_ApiVersionMismatch", () =>
        {
            bool ok = ModManifest.TryParse(
                "{\"id\":\"a.b\",\"name\":\"N\",\"version\":\"1.0.0\",\"apiVersion\":\"2.0\"}",
                "1.0", out _, out ManifestError err, out _);
            TestHarness.CheckTrue(!ok, "Incompatible API version must fail");
            TestHarness.CheckEq(ManifestError.IncompatibleApiVersion, err, "Expected IncompatibleApiVersion");
        });

        TestHarness.Register("lifecycle", "MAN-05_DuplicateModIdQuarantined", () =>
        {
            const string json = "{\"id\":\"sappsworld.qol\",\"name\":\"QoL\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}";
            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");

            ModRecord first = mgr.DiscoverAndValidate("f1", json);
            ModRecord second = mgr.DiscoverAndValidate("f2", json);

            TestHarness.CheckEq(ModState.Validated, first.State, "First mod remains Validated");
            TestHarness.CheckEq(ModState.Quarantined, second.State, "Duplicate mod must be Quarantined");
            TestHarness.CheckTrue(second.QuarantineReason!.Contains("DuplicateModId"), "Duplicate reason logged");
        });

        TestHarness.Register("lifecycle", "LIFE-01_FaultIsolation", () =>
        {
            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");
            mgr.DiscoverAndValidate("a", "{\"id\":\"mod.a\",\"name\":\"A\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}");
            mgr.DiscoverAndValidate("b", "{\"id\":\"mod.b\",\"name\":\"B\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}");

            bool aOk = mgr.ExecuteModPhase("mod.a", ModState.Registered, () => throw new InvalidOperationException("bad hook"));
            bool bReg = mgr.ExecuteModPhase("mod.b", ModState.Registered, () => { });
            bool bAct = mgr.ExecuteModPhase("mod.b", ModState.Active, () => { });

            TestHarness.CheckTrue(!aOk, "Mod A phase must fail");
            TestHarness.CheckTrue(bReg && bAct, "Mod B must succeed unaffected");
            TestHarness.CheckEq(1, mgr.QuarantinedMods.Count, "Only Mod A quarantined");
            TestHarness.CheckTrue(mgr.HasActiveMods, "Mod B is Active");
            TestHarness.CheckTrue(log.Errors.Count >= 1, "Quarantine error logged");
        });

        TestHarness.Register("lifecycle", "LIFE-02_RollbackOnQuarantine", () =>
        {
            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");
            mgr.DiscoverAndValidate("a", "{\"id\":\"mod.a\",\"name\":\"A\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}");

            var simulatedState = new Dictionary<string, int>();
            bool ok = mgr.ExecuteModPhase(
                "mod.a",
                ModState.Registered,
                () =>
                {
                    simulatedState["Tomato"] = 999;
                    throw new InvalidOperationException("crash mid-registration");
                },
                rollback: () => simulatedState.Clear());

            TestHarness.CheckTrue(!ok, "Failed phase returns false");
            TestHarness.CheckEq(0, simulatedState.Count, "Rollback must purge partial state");
        });

        TestHarness.Register("lifecycle", "LIFE-03_DisableAndReenableMod", () =>
        {
            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");
            mgr.DiscoverAndValidate("a", "{\"id\":\"mod.a\",\"name\":\"A\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}");
            mgr.ExecuteModPhase("mod.a", ModState.Active, () => { });

            bool disabledCalled = false;
            bool enabledCalled = false;

            TestHarness.CheckTrue(mgr.SetDisabled("mod.a", true, onDisable: () => disabledCalled = true), "Disable succeeds");
            TestHarness.CheckTrue(disabledCalled, "onDisable callback executed");
            TestHarness.CheckTrue(mgr.IsDisabled("mod.a"), "Mod marked disabled");
            TestHarness.CheckTrue(!mgr.HasActiveMods, "No active mods when disabled");

            TestHarness.CheckTrue(mgr.SetDisabled("mod.a", false, onEnable: () => enabledCalled = true), "Re-enable succeeds");
            TestHarness.CheckTrue(enabledCalled, "onEnable callback executed");
            TestHarness.CheckTrue(!mgr.IsDisabled("mod.a"), "Mod no longer disabled");
            TestHarness.CheckTrue(mgr.HasActiveMods, "Mod returned to Active");
        });

        TestHarness.Register("lifecycle", "LIFE-04_InitiallyDisabledModSkipsRegistration", () =>
        {
            var log = new MemoryLogger();
            var mgr = new ModLifecycleManager(log, "1.0");
            mgr.SetInitiallyDisabled(new[] { "mod.a" });

            ModRecord rec = mgr.DiscoverAndValidate("a", "{\"id\":\"mod.a\",\"name\":\"A\",\"version\":\"1.0.0\",\"apiVersion\":\"1.0\"}");
            TestHarness.CheckEq(ModState.Disabled, rec.State, "Initially disabled mod starts in Disabled state");

            bool ran = false;
            bool phaseOk = mgr.ExecuteModPhase("mod.a", ModState.Registered, () => ran = true);
            TestHarness.CheckTrue(!phaseOk && !ran, "ExecuteModPhase must skip Disabled mod");
        });
    }
}
