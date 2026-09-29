using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Console command that checks the catalog against the game, to run after a Valheim update. Pieces
/// whose prefab is gone can't be placed, and their placed copies only survive as invisible stand-ins,
/// so these are worth fixing (usually by updating the prefab name in PieceCatalog.cs, with the old
/// saved name in formerNames). It also checks the names the furniture functions and cost rules use.
/// </summary>
public sealed class CheckCommand : ConsoleCommand
{
    public override string Name => "landscaper_check";

    public override string Help => "Check the Landscaper catalog against this version of Valheim";

    public override void Run(string[] args, Terminal context)
    {
        var pieces = Plugin.Pieces;
        if (pieces is null || ZNetScene.instance is null || ObjectDB.instance is null)
        {
            context.AddString("Load into a world first.");
            return;
        }

        var problems = new List<string>();
        var entries = PieceCatalog.Entries().ToList();

        foreach (var entry in entries.Where(entry => !DecorativePieceManager.PrefabExists(entry.PrefabName)))
        {
            problems.Add($"Missing prefab: {entry.PrefabName} ({entry.DisplayName}, {entry.Tool} {entry.Category})");
        }

        foreach (var entry in entries.Where(entry => entry.FunctionFrom is { } template && ZNetScene.instance.GetPrefab(template) == null))
        {
            problems.Add($"Missing furniture template: {entry.FunctionFrom} (for {entry.DisplayName})");
        }

        var catalogPrefabs = new HashSet<string>(entries.Select(entry => entry.PrefabName), StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in BuildCosts.ReferencedPrefabs().Where(prefab => !catalogPrefabs.Contains(prefab)))
        {
            problems.Add($"Cost rule for a prefab that isn't in the catalog: {prefab}");
        }

        foreach (var item in BuildCosts.ReferencedItems().Concat(BreakRefund.ReferencedItems()).Distinct()
                     .Where(item => PrefabManager.Instance.GetPrefab(item)?.GetComponent<ItemDrop>() == null))
        {
            problems.Add($"Missing item used by the cost rules: {item}");
        }

        foreach (var station in BuildCosts.ReferencedStations()
                     .Where(station => PrefabManager.Instance.GetPrefab(station)?.GetComponent<CraftingStation>() == null))
        {
            problems.Add($"Missing crafting station used by the cost rules: {station}");
        }

        foreach (var name in pieces.StandIns)
        {
            problems.Add($"Kept as an invisible stand-in: {name}");
        }

        foreach (var line in problems)
        {
            context.AddString(line);
            Plugin.Log.LogWarning($"landscaper_check: {line}");
        }

        context.AddString(problems.Count == 0
            ? $"All {entries.Count} catalog pieces and cost rules check out. {pieces.AliasCount} other saved names are mapped to pieces."
            : $"{problems.Count} problem(s) found; also written to the BepInEx log.");
    }
}
