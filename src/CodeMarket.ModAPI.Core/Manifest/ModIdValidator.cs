using System;
using System.Text.RegularExpressions;

namespace CodeMarket.ModAPI.Core.Manifest;

public static class ModIdValidator
{
    private static readonly Regex IdPattern = new Regex(
        @"^[a-z0-9][a-z0-9._-]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        // Guard against path traversal segments even though slashes are excluded by regex.
        if (id.Contains("..", StringComparison.Ordinal)) return false;
        return IdPattern.IsMatch(id);
    }
}
