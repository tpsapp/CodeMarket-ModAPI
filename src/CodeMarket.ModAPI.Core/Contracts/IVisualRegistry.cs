namespace CodeMarket.ModAPI.Core.Contracts;

public sealed class MaterialColorOverride
{
    public string ModId { get; }
    public string TargetHex { get; }
    public string ReplacementHex { get; }
    public float? Smoothness { get; }

    public MaterialColorOverride(string modId, string targetHex, string replacementHex, float? smoothness = null)
    {
        ModId = modId;
        TargetHex = targetHex;
        ReplacementHex = replacementHex;
        Smoothness = smoothness;
    }
}

public interface IVisualRegistry
{
    void RegisterMaterialColorOverride(MaterialColorOverride colorOverride);
    bool TryGetReplacementHex(string targetHex, out MaterialColorOverride? colorOverride);
    void RemoveByMod(string modId);
}
