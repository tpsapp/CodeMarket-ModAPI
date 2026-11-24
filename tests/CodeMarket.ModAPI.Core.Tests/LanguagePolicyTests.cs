using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.DataPacks;
using CodeMarket.ModAPI.Core.Lifecycle;

namespace CodeMarket.ModAPI.Core.Tests;

internal static class LanguagePolicyTests
{
    public static void Register()
    {
        TestHarness.Register("policy", "LANG-01_BaseLanguagesAllowed", () =>
        {
            TestHarness.CheckTrue(LanguagePolicy.IsBaseGameLanguage(0), "Java (0) is a base-game language");
            TestHarness.CheckTrue(LanguagePolicy.IsBaseGameLanguage(1), "CSharp (1) is a base-game language");
            TestHarness.CheckTrue(!LanguagePolicy.IsBaseGameLanguage(-1), "Negative language ID rejected");
            TestHarness.CheckTrue(!LanguagePolicy.IsBaseGameLanguage(2), "DLC/non-base language ID 2 rejected as base");
            TestHarness.CheckTrue(!LanguagePolicy.IsBaseGameLanguage(99), "Custom language ID 99 rejected");
        });

        TestHarness.Register("policy", "LANG-02_ForbiddenPatchTargetsBlocked", () =>
        {
            string[] forbidden =
            {
                "JavaMarket.App.SteamDlc",
                "JavaMarket.App.PythonCards",
                "JavaMarket.Scripting.CodeLanguage",
                "JavaMarket.Scripting.PythonLanguagePages",
                "JavaMarket.Scripting.CppLanguagePage",
                "JavaMarket.Python.PyVm",
                "JavaMarket.JavaScript.JsVm",
                "JavaMarket.TypeScript.TsVm",
                "JavaMarket.Cpp.CppVm",
                "JavaMarket.Php.PhpVm",
                "JavaMarket.Rust.RustVm",
                "JavaMarket.Go.GoVm",
                "JavaMarket.Java.VirtualMachine",
                "JavaMarket.CSharp.CsVm"
            };

            foreach (string target in forbidden)
            {
                TestHarness.CheckTrue(
                    LanguagePolicy.IsForbiddenPatchTarget(target),
                    $"Expected '{target}' to be blocked by LanguagePolicy");
            }

            string[] allowed =
            {
                "JavaMarket.World.ProductCatalog",
                "JavaMarket.App.Upgrade",
                "JavaMarket.Scripting.RobotCommands",
                "JavaMarket.Scripting.StoreCommands",
                "JavaMarket.World.Rendering.ModelMaterials"
            };

            foreach (string target in allowed)
            {
                TestHarness.CheckTrue(
                    !LanguagePolicy.IsForbiddenPatchTarget(target),
                    $"Expected '{target}' to remain allowed");
            }
        });

        TestHarness.Register("policy", "LANG-03_DataPackWithLanguagesSectionQuarantined", () =>
        {
            const string offendingPack = """
            {
              "id": "bad.newlang",
              "name": "Custom Language Pack",
              "version": "1.0.0",
              "apiVersion": "1.0",
              "type": "datapack",
              "overrides": {
                "products": { "Tomato": { "sellPrice": 99 } },
                "languages": { "Rust": { "enabled": true } }
              }
            }
            """;

            var log = new MemoryLogger();
            var life = new ModLifecycleManager(log);
            var bal = new BalanceRegistry(log);
            var vis = new VisualRegistry();
            var loader = new DataPackLoader(life, bal, vis, log);

            loader.LoadPacks(new[] { ("badlang", offendingPack) });

            TestHarness.CheckEq(1, life.QuarantinedMods.Count, "Data pack with 'languages' section must be quarantined");
            TestHarness.CheckTrue(
                life.QuarantinedMods[0].QuarantineReason!.Contains("PolicyViolation"),
                "Quarantine reason must cite PolicyViolation");
            TestHarness.CheckTrue(
                !bal.TryGetProductPrice("Tomato", out _),
                "Partial overrides from offending data pack must be rolled back");
        });
    }
}
