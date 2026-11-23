using System;
using System.Text.RegularExpressions;
using CodeMarket.ModAPI.Core.Json;

namespace CodeMarket.ModAPI.Core.Manifest;

public enum ModType
{
    Plugin,
    DataPack
}

public enum ManifestError
{
    None,
    InvalidJson,
    MissingField,
    InvalidId,
    InvalidVersion,
    IncompatibleApiVersion,
    InvalidType
}

public sealed class ModManifest
{
    private static readonly Regex SemVerPattern = new Regex(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string ApiVersion { get; }
    public string Author { get; }
    public string Description { get; }
    public ModType Type { get; }

    public ModManifest(
        string id,
        string name,
        string version,
        string apiVersion,
        string author,
        string description,
        ModType type)
    {
        Id = id;
        Name = name;
        Version = version;
        ApiVersion = apiVersion;
        Author = author;
        Description = description;
        Type = type;
    }

    public static bool TryParse(
        string json,
        string hostApiVersion,
        out ModManifest? manifest,
        out ManifestError error,
        out string errorMessage)
    {
        manifest = null;
        error = ManifestError.None;
        errorMessage = string.Empty;

        JsonValue root;
        try
        {
            root = JsonParser.Parse(json);
        }
        catch (JsonParseException ex)
        {
            error = ManifestError.InvalidJson;
            errorMessage = $"Malformed manifest JSON: {ex.Message}";
            return false;
        }

        if (root.Kind != JsonKind.Object)
        {
            error = ManifestError.InvalidJson;
            errorMessage = "Manifest root must be a JSON object.";
            return false;
        }

        if (!TryReadRequiredString(root, "id", out string id, out errorMessage))
        {
            error = ManifestError.MissingField;
            return false;
        }

        if (!ModIdValidator.IsValid(id))
        {
            error = ManifestError.InvalidId;
            errorMessage = $"Invalid mod id '{id}'. Must match ^[a-z0-9][a-z0-9._-]{{1,63}}$ without '..'.";
            return false;
        }

        if (!TryReadRequiredString(root, "name", out string name, out errorMessage))
        {
            error = ManifestError.MissingField;
            return false;
        }

        if (!TryReadRequiredString(root, "version", out string version, out errorMessage))
        {
            error = ManifestError.MissingField;
            return false;
        }

        if (!SemVerPattern.IsMatch(version))
        {
            error = ManifestError.InvalidVersion;
            errorMessage = $"Invalid SemVer version '{version}'. Expected MAJOR.MINOR.PATCH.";
            return false;
        }

        if (!TryReadRequiredString(root, "apiVersion", out string apiVersion, out errorMessage))
        {
            error = ManifestError.MissingField;
            return false;
        }

        if (!IsApiCompatible(apiVersion, hostApiVersion))
        {
            error = ManifestError.IncompatibleApiVersion;
            errorMessage = $"Mod requires apiVersion '{apiVersion}', host supports '{hostApiVersion}'.";
            return false;
        }

        string author = TryReadOptionalString(root, "author") ?? "Unknown";
        string description = TryReadOptionalString(root, "description") ?? string.Empty;

        string typeRaw = TryReadOptionalString(root, "type") ?? "plugin";
        ModType modType;
        if (string.Equals(typeRaw, "plugin", StringComparison.OrdinalIgnoreCase))
        {
            modType = ModType.Plugin;
        }
        else if (string.Equals(typeRaw, "datapack", StringComparison.OrdinalIgnoreCase))
        {
            modType = ModType.DataPack;
        }
        else
        {
            error = ManifestError.InvalidType;
            errorMessage = $"Unknown mod type '{typeRaw}'. Expected 'plugin' or 'datapack'.";
            return false;
        }

        manifest = new ModManifest(id, name, version, apiVersion, author, description, modType);
        return true;
    }

    private static bool TryReadRequiredString(JsonValue obj, string key, out string value, out string errorMessage)
    {
        value = string.Empty;
        errorMessage = string.Empty;

        if (!obj.TryGetProperty(key, out JsonValue? prop) || prop is null || prop.Kind != JsonKind.String)
        {
            errorMessage = $"Missing or non-string required manifest field '{key}'.";
            return false;
        }

        string s = prop.AsString().Trim();
        if (s.Length == 0)
        {
            errorMessage = $"Required manifest field '{key}' cannot be empty.";
            return false;
        }

        value = s;
        return true;
    }

    private static string? TryReadOptionalString(JsonValue obj, string key)
    {
        if (!obj.TryGetProperty(key, out JsonValue? prop) || prop is null || prop.Kind != JsonKind.String)
        {
            return null;
        }

        return prop.AsString().Trim();
    }

    private static bool IsApiCompatible(string requested, string host)
    {
        if (!TryParseMajorMinor(requested, out int reqMaj, out int reqMin)) return false;
        if (!TryParseMajorMinor(host, out int hostMaj, out int hostMin)) return false;

        return reqMaj == hostMaj && reqMin <= hostMin;
    }

    private static bool TryParseMajorMinor(string version, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        string[] parts = version.Split('.');
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && major >= 0 && minor >= 0;
    }
}
