using System;

namespace CodeMarket.ModAPI.Core.Tests;

internal static class Program
{
    public static int Main(string[] args)
    {
        string? suiteFilter = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--suite" && i + 1 < args.Length)
            {
                suiteFilter = args[++i];
            }
            else if (args[i].StartsWith("--suite=", StringComparison.Ordinal))
            {
                suiteFilter = args[i].Substring("--suite=".Length);
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: '{args[i]}'");
                return 1;
            }
        }

        RegisterSelfTests();
        JsonTests.Register();
        LifecycleTests.Register();
        DataPackTests.Register();
        SidecarTests.Register();

        return TestHarness.Run(suiteFilter);
    }

    private static void RegisterSelfTests()
    {
        TestHarness.Register("self", "ModApiInfoConstants", () =>
        {
            TestHarness.CheckEq("1.0", ModApiInfo.ApiVersion, "ApiVersion mismatch");
            TestHarness.CheckTrue(!string.IsNullOrWhiteSpace(ModApiInfo.PluginGuid), "PluginGuid must be non-empty");
            TestHarness.CheckThrows<InvalidOperationException>(
                () => throw new InvalidOperationException("boom"),
                "CheckThrows should catch expected exception");
        });
    }
}
