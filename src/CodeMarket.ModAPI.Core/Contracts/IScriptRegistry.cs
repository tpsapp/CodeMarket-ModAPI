using System;
using System.Collections.Generic;

namespace CodeMarket.ModAPI.Core.Contracts;

public enum ScriptTarget
{
    Robot,
    Store
}

public sealed class ScriptMethodDescriptor
{
    public string ModId { get; }
    public ScriptTarget Target { get; }
    public string Name { get; }
    public string ReturnType { get; }
    public string Description { get; }
    public IReadOnlyList<(string Type, string Name)> Parameters { get; }
    public Func<object, object?[], object?> Handler { get; }

    public ScriptMethodDescriptor(
        string modId,
        ScriptTarget target,
        string name,
        string returnType,
        string description,
        IReadOnlyList<(string Type, string Name)> parameters,
        Func<object, object?[], object?> handler)
    {
        ModId = modId;
        Target = target;
        Name = name;
        ReturnType = returnType;
        Description = description;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}

public interface IScriptRegistry
{
    void RegisterMethod(ScriptMethodDescriptor descriptor);
    IReadOnlyList<ScriptMethodDescriptor> GetMethods(ScriptTarget target);
    void RemoveByMod(string modId);
}
