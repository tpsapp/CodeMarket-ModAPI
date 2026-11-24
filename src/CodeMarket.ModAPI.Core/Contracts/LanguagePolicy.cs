using System;
using System.Collections.Generic;

namespace CodeMarket.ModAPI.Core.Contracts;

/// <summary>
/// Enforces the Code Market creators' policy prohibiting mods from adding new
/// in-game programming languages or bypassing official Steam language DLC gates
/// (base game: Java &amp; C#; official DLCs: Python, JavaScript/TypeScript, C++, PHP, Rust, Go).
/// </summary>
public static class LanguagePolicy
{
    public const int MinOfficialLanguageId = 0; // Java (base)
    public const int MaxBaseLanguageId = 1;     // CSharp (base)

    public const string ViolationMessage =
        "PolicyViolation: Adding new coding languages or modifying language/DLC systems is not permitted.";

    private static readonly HashSet<string> ForbiddenDataPackSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "language",
        "languages",
        "codingLanguages",
        "codeLanguage",
        "compiler",
        "compilers",
        "runtime",
        "runtimes",
        "dlc",
        "steamDlc"
    };

    private static readonly string[] ForbiddenTypePrefixes =
    {
        "JavaMarket.App.SteamDlc",
        "JavaMarket.App.PythonCards",
        "JavaMarket.App.CppCards",
        "JavaMarket.App.JavaScriptCards",
        "JavaMarket.App.PhpCards",
        "JavaMarket.App.RustCards",
        "JavaMarket.App.GoCards",
        "JavaMarket.Scripting.CodeLanguage",
        "JavaMarket.Scripting.CppLanguagePage",
        "JavaMarket.Scripting.CSharpLanguagePage",
        "JavaMarket.Scripting.JsLanguagePage",
        "JavaMarket.Scripting.PythonLanguagePage",
        "JavaMarket.Scripting.PhpLanguagePage",
        "JavaMarket.Scripting.RustLanguagePage",
        "JavaMarket.Scripting.GoLanguagePage",
        "JavaMarket.Java.",
        "JavaMarket.CSharp.",
        "JavaMarket.Cpp.",
        "JavaMarket.Python.",
        "JavaMarket.JavaScript.",
        "JavaMarket.TypeScript.",
        "JavaMarket.Php.",
        "JavaMarket.Rust.",
        "JavaMarket.Go."
    };

    public static bool IsBaseGameLanguage(int languageEnumValue)
    {
        return languageEnumValue >= MinOfficialLanguageId && languageEnumValue <= MaxBaseLanguageId;
    }

    public static bool IsForbiddenDataPackSection(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName)) return false;
        return ForbiddenDataPackSections.Contains(sectionName!.Trim());
    }

    public static bool IsForbiddenPatchTarget(string? typeFullName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName)) return false;

        string name = typeFullName!.Trim();
        foreach (string prefix in ForbiddenTypePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
