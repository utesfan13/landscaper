using UnityEngine;

namespace Landscaper;

/// <summary>
/// Automatic build costs for pieces that don't set their own: wood or stone depending on what the
/// piece is, and 2 to 8 of it depending on how big its model is. Removing a piece refunds its cost.
/// </summary>
internal static class BuildCosts
{
    private const string Wood = "Wood";
    private const string Stone = "Stone";

    private static readonly HashSet<string> StoneCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rocks", "Cliffs", "Ice", "Ore & Mining", "Natural Props", "Bones & Remains", "Statues", "Ruins",
        "Ashlands Ruins", "Jotun Halls", "Dungeon Decor"
    };

    private static readonly HashSet<string> WoodCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trees", "Stumps & Logs", "Plants", "Roots & Branches", "Furniture", "Props", "Workshop Props",
        "Dvergr", "Fuling Village", "Shipwrecks"
    };

    private static readonly string[] StoneNameHints =
    {
        "stone", "rock", "iron", "crystal", "marble", "cliff", "ice", "bone", "skull", "statue", "boulder"
    };

    /// <summary>Reads whether automatic costs are on.</summary>
    public static Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Reads the multiplier applied to automatic costs.</summary>
    public static Func<float> Multiplier { get; set; } = () => 1f;

    /// <summary>The item a piece costs: by its category, or for other categories (such as Building
    /// Structures, Copied and custom ones) by its prefab name, falling back to the tool.</summary>
    public static string MaterialFor(DecorativePieceDefinition definition)
    {
        if (StoneCategories.Contains(definition.Category))
        {
            return Stone;
        }

        if (WoodCategories.Contains(definition.Category))
        {
            return Wood;
        }

        var name = definition.PrefabName.ToLowerInvariant();
        if (StoneNameHints.Any(name.Contains))
        {
            return Stone;
        }

        return definition.Tool == BuildTool.Hoe ? Stone : Wood;
    }

    /// <summary>How many of that item, from the largest dimension of the model in meters.</summary>
    public static int AmountFor(float size)
    {
        var amount = size < 2f ? 2 : size < 6f ? 4 : size < 15f ? 6 : 8;
        return Mathf.Max(1, Mathf.RoundToInt(amount * Multiplier()));
    }
}
