using System;
using BepInEx.Logging;
using CodeMarket.ModAPI.Core.Contracts;

namespace CodeMarket.ModAPI.Unity;

internal sealed class BepInExModLogger : IModLogger
{
    private readonly ManualLogSource _source;

    public BepInExModLogger(ManualLogSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public void Info(string message) => _source.LogInfo(message);
    public void Warning(string message) => _source.LogWarning(message);
    public void Error(string message) => _source.LogError(message);
}
