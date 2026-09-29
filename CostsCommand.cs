using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;

namespace Landscaper;

/// <summary>
/// Console command that turns build costs off or on for everything: Landscaper's own costs and
/// crafting stations (the Costs.Enabled setting), and the world's "no build cost" option, which makes
/// vanilla build pieces free too. Only the host or an admin can use it, since it changes the world
/// for everyone. While costs are off, removing a piece gives nothing back, as in vanilla.
/// </summary>
public sealed class CostsCommand : ConsoleCommand
{
    private readonly ConfigEntry<bool> _costsEnabled;

    internal CostsCommand(ConfigEntry<bool> costsEnabled) => _costsEnabled = costsEnabled;

    public override string Name => "landscaper_costs";

    public override string Help => "[on|off] - Turn build costs on or off for every piece, vanilla ones included (host or admin)";

    public override List<string> CommandOptionList() => new() { "on", "off" };

    public override void Run(string[] args, Terminal context)
    {
        var zoneSystem = ZoneSystem.instance;
        if (zoneSystem is null || ZNet.instance is null)
        {
            context.AddString("Load into a world first.");
            return;
        }

        if (args.Length == 0)
        {
            context.AddString($"Build costs are {(zoneSystem.GetGlobalKey(GlobalKeys.NoBuildCost) || !_costsEnabled.Value ? "off" : "on")}. " +
                              "Use landscaper_costs on or landscaper_costs off.");
            return;
        }

        var on = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
        if (!on && !args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            context.AddString("Usage: landscaper_costs [on|off]");
            return;
        }

        if (!ZNet.instance.LocalPlayerIsAdminOrHost())
        {
            context.AddString("Only the host or an admin can change build costs.");
            return;
        }

        _costsEnabled.Value = on;
        if (on)
        {
            zoneSystem.RemoveGlobalKey(GlobalKeys.NoBuildCost);
        }
        else
        {
            zoneSystem.SetGlobalKey(GlobalKeys.NoBuildCost);
        }

        // Costs.Enabled is synced from the server; push the change to everyone now.
        try
        {
            AccessTools.Method(typeof(SynchronizationManager), "SynchronizeChangedConfig")?.Invoke(SynchronizationManager.Instance, null);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning($"Could not sync the cost setting to other players: {exception.Message}");
        }

        context.AddString(on
            ? "Build costs are on again, for Landscaper and vanilla pieces."
            : "Build costs are off: every piece is free and Landscaper pieces need no crafting station. Removing pieces gives nothing back while costs are off.");
    }
}
