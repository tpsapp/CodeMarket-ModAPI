using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.DataPacks;

public sealed class VisualRegistry : IVisualRegistry
{
    private static readonly Regex HexPattern = new Regex(
        @"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<MaterialColorOverride> _overrides = new List<MaterialColorOverride>();

    public void RegisterMaterialColorOverride(MaterialColorOverride colorOverride)
    {
        if (colorOverride is null) throw new ArgumentNullException(nameof(colorOverride));
        if (!ModIdValidator.IsValid(colorOverride.ModId))
        {
            throw new ArgumentException($"Invalid modId '{colorOverride.ModId}'.", nameof(colorOverride));
        }

        string target = NormalizeHex(colorOverride.TargetHex, nameof(colorOverride.TargetHex));
        string replacement = NormalizeHex(colorOverride.ReplacementHex, nameof(colorOverride.ReplacementHex));

        if (colorOverride.Smoothness.HasValue &&
            (float.IsNaN(colorOverride.Smoothness.Value) || colorOverride.Smoothness.Value < 0f || colorOverride.Smoothness.Value > 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(colorOverride), "Smoothness must be in [0.0, 1.0].");
        }

        _overrides.RemoveAll(o => o.ModId == colorOverride.ModId &&
                                  string.Equals(o.TargetHex, target, StringComparison.OrdinalIgnoreCase));
        _overrides.Add(new MaterialColorOverride(colorOverride.ModId, target, replacement, colorOverride.Smoothness));
    }

    public bool TryGetReplacementHex(string targetHex, out MaterialColorOverride? colorOverride)
    {
        colorOverride = null;
        if (string.IsNullOrWhiteSpace(targetHex) || !HexPattern.IsMatch(targetHex.Trim()))
        {
            return false;
        }

        string normalized = NormalizeHex(targetHex, nameof(targetHex));
        for (int i = _overrides.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_overrides[i].TargetHex, normalized, StringComparison.OrdinalIgnoreCase))
            {
                colorOverride = _overrides[i];
                return true;
            }
        }

        return false;
    }

    public void RemoveByMod(string modId)
    {
        _overrides.RemoveAll(o => o.ModId == modId);
    }

    private static string NormalizeHex(string hex, string paramName)
    {
        if (string.IsNullOrWhiteSpace(hex)) throw new ArgumentException("Hex color is required.", paramName);
        string trimmed = hex.Trim();
        if (!HexPattern.IsMatch(trimmed))
        {
            throw new ArgumentException($"Invalid hex color '{hex}'. Expected #RRGGBB or #RRGGBBAA.", paramName);
        }

        return trimmed.StartsWith("#", StringComparison.Ordinal)
            ? trimmed.ToUpperInvariant()
            : "#" + trimmed.ToUpperInvariant();
    }
}
