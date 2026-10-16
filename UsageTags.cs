using UnityEngine;

namespace Landscaper;

/// <summary>
/// The build menu's usage tags. The game lists pieces "by usage", under tags such as Storage, Doors
/// and Decor (a piece can have several), and Jotunn adds each custom tab there as one more tag. A
/// Landscaper tab that's the same as a vanilla tag isn't made at all: its pieces get that tag
/// instead, so there's one Decor or Furniture list, not two. Every piece also gets the tags for what
/// it is (a chest is storage, a gate a door, a brazier lighting), so a themed piece is listed under
/// its theme and with the others of its kind.
/// </summary>
internal static class UsageTags
{
    /// <summary>Landscaper tabs that are listed as a vanilla tag instead.</summary>
    private static readonly Dictionary<string, Piece.UsageTagFlags> VanillaTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Decor"] = Piece.UsageTagFlags.Decor,
        ["Furniture"] = Piece.UsageTagFlags.Furniture,
        ["Roofs"] = Piece.UsageTagFlags.Roof,
        ["Chests"] = Piece.UsageTagFlags.Storage,
        ["Gates & Doors"] = Piece.UsageTagFlags.Doors,
        ["Stacks & Piles"] = Piece.UsageTagFlags.Stacks,
        ["Building Structures"] = Piece.UsageTagFlags.Building,
        ["Festive"] = Piece.UsageTagFlags.Seasonal,
        ["Misc"] = Piece.UsageTagFlags.Misc
    };

    /// <summary>The custom tab a piece is listed under, or null if its tab is a vanilla tag.</summary>
    public static string? TabFor(string category) => VanillaTabs.ContainsKey(category) ? null : category;

    /// <summary>Sets a piece's usage tags, once it has all its parts and functions.</summary>
    public static void Apply(GameObject clone, DecorativePieceDefinition definition)
    {
        if (!clone.TryGetComponent<Piece>(out var piece))
        {
            return;
        }

        // Listed by its tags alone, not under a tab another mod may have given the object it's made from.
        if (TabFor(definition.Category) is null)
        {
            piece.m_category = Piece.PieceCategory.Misc;
        }

        // A display is only decoration, though the item stand it's made from is storage.
        if (definition.DisplayItem is not null)
        {
            piece.m_usage = Piece.UsageTagFlags.Decor;
            return;
        }

        // A vanilla build piece's own tags are kept; objects from the world have none.
        var usage = piece.m_usage;
        if (VanillaTabs.TryGetValue(definition.Category, out var tab))
        {
            usage |= tab;
        }

        if (clone.GetComponentInChildren<Container>(includeInactive: true) != null)
        {
            usage |= Piece.UsageTagFlags.Storage;
        }

        if (clone.GetComponentInChildren<Door>(includeInactive: true) != null || clone.GetComponentInChildren<LiftGates.Gate>(includeInactive: true) != null)
        {
            usage |= Piece.UsageTagFlags.Doors;
        }

        var name = definition.PrefabName.ToLowerInvariant() + " " + definition.DisplayName.ToLowerInvariant();
        if (clone.GetComponentInChildren<Fireplace>(includeInactive: true) != null ||
            new[] { "torch", "lantern", "brazier", "candle", "fire pit", "firepit", "bonfire", "hearth", "sconce" }.Any(name.Contains))
        {
            usage |= Piece.UsageTagFlags.Lighting;
        }

        if (clone.GetComponentInChildren<Bed>(includeInactive: true) != null || clone.GetComponentInChildren<Chair>(includeInactive: true) != null ||
            piece.m_comfortGroup is Piece.ComfortGroup.Bed or Piece.ComfortGroup.Chair or Piece.ComfortGroup.Table)
        {
            usage |= Piece.UsageTagFlags.Furniture;
        }

        if (clone.GetComponentInChildren<CraftingStation>(includeInactive: true) != null ||
            clone.GetComponentInChildren<StationExtension>(includeInactive: true) != null)
        {
            usage |= Piece.UsageTagFlags.Crafting;
        }

        if (clone.GetComponentInChildren<Sign>(includeInactive: true) != null ||
            new[] { "banner", "rug", "curtain", "carpet" }.Any(name.Contains))
        {
            usage |= Piece.UsageTagFlags.Decor;
        }

        piece.m_usage = usage;
    }
}
