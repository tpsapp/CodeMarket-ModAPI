using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Json;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.Sidecars;

public sealed class SidecarStore : ISaveSidecarStore
{
    public const int CurrentSchemaVersion = 1;
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly IModLogger _logger;
    private readonly Dictionary<string, (string Version, JsonValue Data)> _entries =
        new Dictionary<string, (string Version, JsonValue Data)>(StringComparer.Ordinal);

    public SidecarStore(IModLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static string GetSidecarPath(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
        {
            throw new ArgumentException("Save file path is required.", nameof(saveFilePath));
        }

        string trimmed = saveFilePath.Trim();
        int lastSep = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        int lastDot = trimmed.LastIndexOf('.');

        // Replace extension only if the dot belongs to the filename, not a parent directory.
        string stem = lastDot > lastSep ? trimmed.Substring(0, lastDot) : trimmed;
        return stem + ".moddata.json";
    }

    public void SetModData(string modId, string modVersion, JsonValue data)
    {
        if (!ModIdValidator.IsValid(modId))
        {
            throw new ArgumentException($"Invalid modId '{modId}' for sidecar storage.", nameof(modId));
        }

        if (string.IsNullOrWhiteSpace(modVersion))
        {
            throw new ArgumentException("Mod version is required.", nameof(modVersion));
        }

        if (data is null) throw new ArgumentNullException(nameof(data));

        _entries[modId] = (modVersion.Trim(), data);
    }

    public bool TryGetModData(string modId, out JsonValue? data)
    {
        data = null;
        if (!ModIdValidator.IsValid(modId)) return false;

        if (_entries.TryGetValue(modId, out (string Version, JsonValue Data) entry))
        {
            data = entry.Data;
            return true;
        }

        return false;
    }

    public void ClearInMemory()
    {
        _entries.Clear();
    }

    public SidecarLoadResult LoadForSave(string saveFilePath)
    {
        ClearInMemory();

        string sidecarPath;
        try
        {
            sidecarPath = GetSidecarPath(saveFilePath);
        }
        catch (ArgumentException ex)
        {
            return new SidecarLoadResult(false, false, ex.Message);
        }

        if (!File.Exists(sidecarPath))
        {
            return new SidecarLoadResult(true, false, null);
        }

        try
        {
            string raw = File.ReadAllText(sidecarPath, Utf8NoBom);
            JsonValue root = JsonParser.Parse(raw);
            if (root.Kind != JsonKind.Object)
            {
                throw new FormatException("Sidecar root must be a JSON object.");
            }

            if (!root.TryGetProperty("schemaVersion", out JsonValue? sv) || sv is null || sv.Kind != JsonKind.Number)
            {
                throw new FormatException("Sidecar missing numeric 'schemaVersion'.");
            }

            int schemaVersion = sv.AsInt32();
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new FormatException($"Unsupported sidecar schemaVersion {schemaVersion}.");
            }

            if (!root.TryGetProperty("mods", out JsonValue? modsVal) || modsVal is null || modsVal.Kind != JsonKind.Object)
            {
                throw new FormatException("Sidecar missing object 'mods'.");
            }

            foreach (KeyValuePair<string, JsonValue> kv in modsVal.AsObject())
            {
                if (!ModIdValidator.IsValid(kv.Key))
                {
                    _logger.Warning($"[ModAPI] Skipping sidecar entry with invalid modId '{kv.Key}'.");
                    continue;
                }

                if (kv.Value.Kind != JsonKind.Object ||
                    !kv.Value.TryGetProperty("data", out JsonValue? dataVal) || dataVal is null)
                {
                    _logger.Warning($"[ModAPI] Skipping malformed sidecar entry for mod '{kv.Key}'.");
                    continue;
                }

                string version = kv.Value.TryGetProperty("version", out JsonValue? verVal) &&
                                 verVal is not null && verVal.Kind == JsonKind.String
                    ? verVal.AsString()
                    : "0.0.0";

                _entries[kv.Key] = (version, dataVal);
            }

            return new SidecarLoadResult(true, false, null);
        }
        catch (Exception ex) when (ex is JsonParseException || ex is FormatException || ex is InvalidOperationException)
        {
            ClearInMemory();
            string backupPath = PreserveCorruptSidecar(sidecarPath);
            string warning = $"Corrupt mod sidecar recovered (backed up to '{backupPath}'): {ex.Message}";
            _logger.Error($"[ModAPI] {warning}");
            return new SidecarLoadResult(true, true, warning);
        }
        catch (IOException ex)
        {
            string msg = $"Failed to read mod sidecar '{sidecarPath}': {ex.Message}";
            _logger.Error($"[ModAPI] {msg}");
            return new SidecarLoadResult(false, false, msg);
        }
    }

    public SidecarSaveResult SaveForSave(string saveFilePath)
    {
        string sidecarPath;
        try
        {
            sidecarPath = GetSidecarPath(saveFilePath);
        }
        catch (ArgumentException ex)
        {
            return new SidecarSaveResult(false, ex.Message);
        }

        string tmpPath = sidecarPath + ".tmp";
        try
        {
            string? dir = Path.GetDirectoryName(sidecarPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var modsProps = new List<KeyValuePair<string, JsonValue>>();
            foreach (KeyValuePair<string, (string Version, JsonValue Data)> kv in _entries)
            {
                var entryObj = JsonValue.FromObject(new[]
                {
                    new KeyValuePair<string, JsonValue>("version", JsonValue.FromString(kv.Value.Version)),
                    new KeyValuePair<string, JsonValue>("data", kv.Value.Data)
                });
                modsProps.Add(new KeyValuePair<string, JsonValue>(kv.Key, entryObj));
            }

            var root = JsonValue.FromObject(new[]
            {
                new KeyValuePair<string, JsonValue>("schemaVersion", JsonValue.FromNumber(CurrentSchemaVersion)),
                new KeyValuePair<string, JsonValue>("mods", JsonValue.FromObject(modsProps))
            });

            string serialized = JsonWriter.Serialize(root, indented: true);
            byte[] bytes = Utf8NoBom.GetBytes(serialized);

            // Flush to disk before atomic replace so crashes mid-save cannot leave partial JSON.
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }

            if (File.Exists(sidecarPath))
            {
                File.Replace(tmpPath, sidecarPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmpPath, sidecarPath);
            }

            return new SidecarSaveResult(true, null);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            TryDeleteFile(tmpPath);
            string msg = $"Failed to save mod sidecar '{sidecarPath}': {ex.Message}";
            _logger.Error($"[ModAPI] {msg}");
            return new SidecarSaveResult(false, msg);
        }
    }

    public void DeleteForSave(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath)) return;

        string sidecarPath = GetSidecarPath(saveFilePath);
        TryDeleteFile(sidecarPath);
        TryDeleteFile(sidecarPath + ".tmp");
    }

    private string PreserveCorruptSidecar(string sidecarPath)
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        string backupPath = $"{sidecarPath}.corrupt.{stamp}.bak";
        try
        {
            if (File.Exists(sidecarPath))
            {
                File.Move(sidecarPath, backupPath);
            }
        }
        catch (IOException ex)
        {
            _logger.Warning($"[ModAPI] Could not move corrupt sidecar to backup '{backupPath}': {ex.Message}");
        }

        return backupPath;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup for temporary/deleted sidecars.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup when directory permissions restrict deletion.
        }
    }
}
