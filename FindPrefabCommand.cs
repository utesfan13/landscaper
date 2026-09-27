using Jotunn.Entities;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Console command that lists spawnable prefab names, for use in CustomEntries or OtherCatalog.
/// Only prefabs registered with ZNetScene are listed: those have a ZNetView, so pieces built from
/// them are saved with the world. Dungeon and location sub-parts without one are left out.
/// </summary>
public sealed class FindPrefabCommand : ConsoleCommand
{
    private const int MaxResults = 100;

    public override string Name => "landscaper_find";

    public override string Help => "<keyword> - List spawnable prefab names containing the keyword, e.g. landscaper_find crypt";

    public override void Run(string[] args, Terminal context)
    {
        if (ZNetScene.instance is null)
        {
            context.AddString("Load into a world first; prefabs are only available in game.");
            return;
        }

        var keyword = string.Join(" ", args).Trim();
        if (keyword.Length == 0)
        {
            context.AddString("Usage: landscaper_find <keyword>");
            return;
        }

        var matches = ZNetScene.instance.m_prefabs
            .Where(prefab => prefab is not null &&
                prefab.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !prefab.name.StartsWith("Landscaper_", StringComparison.Ordinal))
            .GroupBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 0)
        {
            context.AddString($"No spawnable prefabs contain '{keyword}'.");
            return;
        }

        context.AddString($"{matches.Count} spawnable prefab(s) contain '{keyword}':");
        foreach (var prefab in matches.Take(MaxResults))
        {
            context.AddString($"  {prefab.name}{DescribeKind(prefab)}");
        }

        if (matches.Count > MaxResults)
        {
            context.AddString($"  ...and {matches.Count - MaxResults} more. Use a longer keyword to narrow the list.");
        }
    }

    private static string DescribeKind(GameObject prefab)
    {
        if (prefab.GetComponent<Piece>() is not null)
        {
            return "  (build piece)";
        }

        if (prefab.GetComponent<ItemDrop>() is not null)
        {
            return "  (item - drops on the ground, not a good piece)";
        }

        if (prefab.GetComponent<Character>() is not null)
        {
            return "  (creature - not a good piece)";
        }

        return string.Empty;
    }
}
