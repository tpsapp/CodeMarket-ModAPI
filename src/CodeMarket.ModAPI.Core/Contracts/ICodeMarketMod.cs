namespace CodeMarket.ModAPI.Core.Contracts;

public sealed class ModRegistrationContext
{
    public IBalanceRegistry Balance { get; }
    public IScriptRegistry Scripting { get; }
    public IVisualRegistry Visuals { get; }
    public IModLogger Logger { get; }

    public ModRegistrationContext(
        IBalanceRegistry balance,
        IScriptRegistry scripting,
        IVisualRegistry visuals,
        IModLogger logger)
    {
        Balance = balance;
        Scripting = scripting;
        Visuals = visuals;
        Logger = logger;
    }
}

public interface ICodeMarketMod
{
    string ModId { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    void Register(ModRegistrationContext context);
}
