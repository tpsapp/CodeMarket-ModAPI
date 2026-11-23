using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeMarket.ModAPI.Core.Contracts;
using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.DataPacks;

public sealed class ScriptRegistry : IScriptRegistry
{
    private static readonly Regex IdentifierPattern = new Regex(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<ScriptMethodDescriptor> _methods = new List<ScriptMethodDescriptor>();

    public void RegisterMethod(ScriptMethodDescriptor descriptor)
    {
        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
        if (!ModIdValidator.IsValid(descriptor.ModId))
        {
            throw new ArgumentException($"Invalid modId '{descriptor.ModId}'.", nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Name) || !IdentifierPattern.IsMatch(descriptor.Name))
        {
            throw new ArgumentException($"Invalid script method identifier '{descriptor.Name}'.", nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(descriptor.ReturnType))
        {
            throw new ArgumentException("ReturnType is required.", nameof(descriptor));
        }

        if (_methods.Any(m => m.Target == descriptor.Target &&
                              string.Equals(m.Name, descriptor.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Script method '{descriptor.Target}.{descriptor.Name}' is already registered.");
        }

        _methods.Add(descriptor);
    }

    public IReadOnlyList<ScriptMethodDescriptor> GetMethods(ScriptTarget target)
    {
        return _methods.Where(m => m.Target == target).ToList();
    }

    public void RemoveByMod(string modId)
    {
        _methods.RemoveAll(m => m.ModId == modId);
    }
}
