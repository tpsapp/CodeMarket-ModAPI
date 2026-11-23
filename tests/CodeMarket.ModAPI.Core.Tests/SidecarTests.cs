using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Core.Sidecars;

namespace CodeMarket.ModAPI.Core.Tests;

internal static class SidecarTests
{
    public static void Register()
    {
        TestHarness.Register("sidecars", "SIDE-01_HappyPathRoundTrip", () =>
        {
            string dir = CreateTempDir();
            try
            {
                string savePath = Path.Combine(dir, "save.json");
                File.WriteAllText(savePath, "{\"vanilla\":true}");

                var log = new MemoryLogger();
                var store = new SidecarStore(log);
                store.SetModData("sappsworld.qol", "1.0.0", JsonValue.FromObject(new[]
                {
                    new KeyValuePair<string, JsonValue>("charLimit", JsonValue.FromNumber(32768))
                }));
                store.SetModData("other.mod", "2.1.0", JsonValue.FromString("hello"));

                SidecarSaveResult saveRes = store.SaveForSave(savePath);
                TestHarness.CheckTrue(saveRes.Success, "Save should succeed");
                TestHarness.CheckEq("{\"vanilla\":true}", File.ReadAllText(savePath), "Vanilla save.json must stay untouched");
                TestHarness.CheckTrue(File.Exists(SidecarStore.GetSidecarPath(savePath)), "Sidecar file created");

                var reloaded = new SidecarStore(log);
                SidecarLoadResult loadRes = reloaded.LoadForSave(savePath);
                TestHarness.CheckTrue(loadRes.Success && !loadRes.RecoveredFromCorruption, "Clean reload");
                TestHarness.CheckTrue(reloaded.TryGetModData("sappsworld.qol", out JsonValue? qolData), "QoL data restored");
                TestHarness.CheckTrue(qolData!.TryGetProperty("charLimit", out JsonValue? lim) && lim!.AsInt32() == 32768, "QoL value");
                TestHarness.CheckTrue(reloaded.TryGetModData("other.mod", out JsonValue? otherData) && otherData!.AsString() == "hello", "Other mod value");
            }
            finally
            {
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-02_FreshVanillaSave", () =>
        {
            string dir = CreateTempDir();
            try
            {
                string savePath = Path.Combine(dir, "save.json");
                File.WriteAllText(savePath, "{}");

                var log = new MemoryLogger();
                var store = new SidecarStore(log);
                SidecarLoadResult res = store.LoadForSave(savePath);

                TestHarness.CheckTrue(res.Success && !res.RecoveredFromCorruption, "Missing sidecar loads cleanly");
                TestHarness.CheckEq(0, log.Errors.Count, "No errors logged");
                TestHarness.CheckEq(0, log.Warnings.Count, "No warnings logged");
            }
            finally
            {
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-03_CorruptSidecarRecovery", () =>
        {
            string dir = CreateTempDir();
            try
            {
                string savePath = Path.Combine(dir, "save.json");
                string sidecarPath = SidecarStore.GetSidecarPath(savePath);
                File.WriteAllText(sidecarPath, "{broken json");

                var log = new MemoryLogger();
                var store = new SidecarStore(log);
                SidecarLoadResult res = store.LoadForSave(savePath);

                TestHarness.CheckTrue(res.Success && res.RecoveredFromCorruption, "Corrupt sidecar recovered");
                TestHarness.CheckTrue(!File.Exists(sidecarPath), "Corrupt file moved away");
                TestHarness.CheckEq(1, Directory.GetFiles(dir, "*.corrupt.*.bak").Length, "Backup file created");
                TestHarness.CheckTrue(log.Errors.Count >= 1, "Corruption logged as error");
            }
            finally
            {
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-04_AtomicWriteNoLeftoverTmp", () =>
        {
            string dir = CreateTempDir();
            try
            {
                string savePath = Path.Combine(dir, "save.json");
                var store = new SidecarStore(new MemoryLogger());
                store.SetModData("sappsworld.qol", "1.0.0", JsonValue.FromNumber(1));
                TestHarness.CheckTrue(store.SaveForSave(savePath).Success, "First save ok");
                store.SetModData("sappsworld.qol", "1.0.0", JsonValue.FromNumber(2));
                TestHarness.CheckTrue(store.SaveForSave(savePath).Success, "Overwrite save ok");

                TestHarness.CheckEq(0, Directory.GetFiles(dir, "*.tmp").Length, "No leftover .tmp files");
            }
            finally
            {
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-05_PathConfinementRejectsTraversal", () =>
        {
            var store = new SidecarStore(new MemoryLogger());
            TestHarness.CheckThrows<ArgumentException>(
                () => store.SetModData("../../escape", "1.0.0", JsonValue.Null),
                "Traversal mod ID must be rejected");
        });

        TestHarness.Register("sidecars", "SIDE-06_DeleteForSaveSync", () =>
        {
            string dir = CreateTempDir();
            try
            {
                string savePath = Path.Combine(dir, "save.json");
                var store = new SidecarStore(new MemoryLogger());
                store.SetModData("sappsworld.qol", "1.0.0", JsonValue.FromBool(true));
                store.SaveForSave(savePath);

                store.DeleteForSave(savePath);
                TestHarness.CheckTrue(!File.Exists(SidecarStore.GetSidecarPath(savePath)), "Sidecar deleted");
                store.DeleteForSave(savePath); // idempotent no-op
            }
            finally
            {
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-A_ReadOnlyDirGracefulFailure", () =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            string dir = CreateTempDir();
            try
            {
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                string savePath = Path.Combine(dir, "save.json");
                var log = new MemoryLogger();
                var store = new SidecarStore(log);
                store.SetModData("sappsworld.qol", "1.0.0", JsonValue.FromBool(true));

                SidecarSaveResult res = store.SaveForSave(savePath);
                TestHarness.CheckTrue(!res.Success, "Read-only directory save must fail gracefully");
                TestHarness.CheckTrue(!string.IsNullOrWhiteSpace(res.ErrorMessage), "Structured error message returned");
                TestHarness.CheckTrue(log.Errors.Count >= 1, "I/O failure logged");
            }
            finally
            {
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                CleanupDir(dir);
            }
        });

        TestHarness.Register("sidecars", "SIDE-B_CrossPlatformPathDerivation", () =>
        {
            TestHarness.CheckEq(
                "/a/b/save.moddata.json",
                SidecarStore.GetSidecarPath("/a/b/save.json"),
                "POSIX save path");
            TestHarness.CheckEq(
                @"C:\Users\steamuser\AppData\LocalLow\JavaMarket\save.moddata.json",
                SidecarStore.GetSidecarPath(@"C:\Users\steamuser\AppData\LocalLow\JavaMarket\save.json"),
                "Win32 save path");
            TestHarness.CheckEq(
                "/a/dir.with.dots/slot1.moddata.json",
                SidecarStore.GetSidecarPath("/a/dir.with.dots/slot1"),
                "Extensionless save file in dotted directory");
        });
    }

    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "codemarket_sidecar_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupDir(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
