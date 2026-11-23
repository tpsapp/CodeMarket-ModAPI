using CodeMarket.ModAPI.Core.Manifest;

namespace CodeMarket.ModAPI.Core.Lifecycle;

public sealed class ModRecord
{
    public string Id { get; }
    public ModManifest? Manifest { get; internal set; }
    public ModState State { get; internal set; }
    public string? QuarantineReason { get; internal set; }
    public string? QuarantinePhase { get; internal set; }

    public ModRecord(string id, ModState initialState)
    {
        Id = id;
        State = initialState;
    }
}
