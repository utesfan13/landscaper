using BepInEx;
using Jotunn.Entities;

namespace Landscaper;

/// <summary>
/// Console command that writes every Landscaper piece's cost to a file, for reviewing costs: tab,
/// name, prefab, cost, crafting station and what the cost is based on, tab-separated.
/// </summary>
public sealed class CostListCommand : ConsoleCommand
{
    public override string Name => "landscaper_costlist";

    public override string Help => "Write every Landscaper piece's cost to BepInEx/landscaper_costs.tsv";

    public override void Run(string[] args, Terminal context)
    {
        if (Plugin.Pieces is not { } pieces || ZNetScene.instance is null)
        {
            context.AddString("Load into a world first.");
            return;
        }

        var path = Path.Combine(Paths.BepInExRootPath, "landscaper_costs.tsv");
        File.WriteAllLines(path, pieces.CostReport());
        context.AddString($"Wrote the costs to {path}.");
    }
}
