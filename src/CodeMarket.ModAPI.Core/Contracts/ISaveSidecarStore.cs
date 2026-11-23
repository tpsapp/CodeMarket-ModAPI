using CodeMarket.ModAPI.Core.Json;

namespace CodeMarket.ModAPI.Core.Contracts;

public sealed class SidecarLoadResult
{
    public bool Success { get; }
    public bool RecoveredFromCorruption { get; }
    public string? WarningMessage { get; }

    public SidecarLoadResult(bool success, bool recoveredFromCorruption, string? warningMessage)
    {
        Success = success;
        RecoveredFromCorruption = recoveredFromCorruption;
        WarningMessage = warningMessage;
    }
}

public sealed class SidecarSaveResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public SidecarSaveResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }
}

public interface ISaveSidecarStore
{
    void SetModData(string modId, string modVersion, JsonValue data);
    bool TryGetModData(string modId, out JsonValue? data);
    void ClearInMemory();
    SidecarLoadResult LoadForSave(string saveFilePath);
    SidecarSaveResult SaveForSave(string saveFilePath);
    void DeleteForSave(string saveFilePath);
}
