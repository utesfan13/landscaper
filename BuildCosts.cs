using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>What a piece's automatic cost is based on, measured from its prefab when registered.</summary>
internal sealed class PieceTraits
{
    public PieceTraits(float size, ItemDrop? pickableItem, bool hasLight, Piece.Requirement[]? vanillaResources, CraftingStation? vanillaStation,
        bool isLiquid)
    {
        IsLiquid = isLiquid;
        Size = size;
        PickableItem = pickableItem;
        HasLight = hasLight;
        VanillaResources = vanillaResources;
        VanillaStation = vanillaStation;
    }

    /// <summary>Whether it is a simulated liquid, such as pond water, which is free to place.</summary>
    public bool IsLiquid { get; }

    /// <summary>The cost of the vanilla build piece this piece is cloned from, if it is one.</summary>
    public Piece.Requirement[]? VanillaResources { get; }

    /// <summary>The crafting station the vanilla build piece needs nearby, if any.</summary>
    public CraftingStation? VanillaStation { get; }

    /// <summary>The largest dimension of the model in meters.</summary>
    public float Size { get; }

    /// <summary>The item picking it gives, for bushes, mushrooms and other pickables.</summary>
    public ItemDrop? PickableItem { get; }

    /// <summary>Whether it gives off light, like torches, lanterns and braziers.</summary>
    public bool HasLight { get; }
}

/// <summary>
/// Automatic build costs for pieces that don't set their own. Removing a piece refunds its cost.
/// Pieces cloned from vanilla build pieces use the vanilla cost and crafting station instead (see
/// DecorativePieceManager.CostFor); for the rest:
/// <list type="bullet">
/// <item>A few pieces have a set cost, such as the guck sacks (1 or 2 guck).</item>
/// <item>Pickables (bushes, mushrooms, thistle, ...) cost 5 of the item they give; loose stones
/// and fallen branches, which give plain stone or wood, cost 1.</item>
/// <item>Metal pieces (lanterns, braziers, iron torches, chains, ...) cost 1 iron.</item>
/// <item>Everything else costs 2 to 8 depending on the size of its model, of wood or stone depending
/// on what it is, ice for ice and snow, bone fragments for bones, or wood and ice split evenly for
/// the stump shelters and frozen ships.</item>
/// <item>Crafted light sources (torches, lanterns, braziers) cost 1 resin on top; pickables and other
/// natural pieces never do, even if they glow.</item>
/// </list>
/// The multiplier applies to all of them, with at least 1 of anything.
/// </summary>
internal static class BuildCosts
{
    private const string Wood = "Wood";
    private const string Stone = "Stone";
    private const string Iron = "Iron";
    private const string Resin = "Resin";
    private const string Ice = "Ice";
    private const string Bone = "BoneFragments";
    private const int PickableAmount = 5;

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
        "stone", "rock", "crystal", "marble", "cliff", "ice", "bone", "skull", "statue", "boulder"
    };

    /// <summary>Prefab name parts of metal pieces. Checked before the categories.</summary>
    private static readonly string[] MetalNameHints =
    {
        "iron", "metal", "lantern", "brazier", "chain", "groundtorch", "walltorch", "sconce", "grate", "pickaxe",
        "giant_sword", "giant_helmet"
    };

    /// <summary>Reads whether automatic costs are on.</summary>
    public static Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Reads the multiplier applied to automatic costs.</summary>
    public static Func<float> Multiplier { get; set; } = () => 1f;

    /// <summary>The items and amounts a piece costs. An item is null if it can't be found.</summary>
    public static IEnumerable<(ItemDrop? Item, int Amount)> CostFor(DecorativePieceDefinition definition, PieceTraits traits)
    {
        // Pickables cost only 5 of what they give, even the ones that glow. Loose stones and fallen
        // branches give plain stone or wood, so they cost just 1, the same as picking them gives.
        if (traits.IsLiquid)
        {
            yield break;
        }

        if (FixedCosts.TryGetValue(definition.PrefabName, out var fixedCost))
        {
            yield return (ItemNamed(fixedCost.Item), Scaled(fixedCost.Amount));
            yield break;
        }

        if (traits.PickableItem is { } picked)
        {
            var givesBasicMaterial = picked.name is Wood or Stone;
            yield return (picked, Scaled(givesBasicMaterial ? 1 : PickableAmount));
            yield break;
        }

        if (IsMetal(definition))
        {
            yield return (ItemNamed(Iron), Scaled(1));
        }
        else
        {
            // Pieces made of two materials split the amount, so they cost the same in total.
            var materials = MaterialsFor(definition);
            var amount = AmountFor(traits.Size);
            foreach (var material in materials)
            {
                yield return (ItemNamed(material), Mathf.Max(1, Mathf.CeilToInt(amount / (float)materials.Length)));
            }
        }

        if (traits.HasLight && !NaturalCategories.Contains(definition.Category))
        {
            yield return (ItemNamed(Resin), Scaled(1));
        }
    }

    /// <summary>Pieces with a set cost of their own, instead of one worked out from their size.</summary>
    private static readonly Dictionary<string, (string Item, int Amount)> FixedCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GuckSack_small"] = ("Guck", 1),
        ["GuckSack"] = ("Guck", 2)
    };

    /// <summary>
    /// Pieces made of something other than what their category is mostly made of: wooden and cloth
    /// pieces among the stone ruins and dungeon decor, and stone pots among the wooden props.
    /// </summary>
    private static readonly Dictionary<string, string> MaterialOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Natural Props
        ["stubbe"] = Wood, ["stubbe_deepnorth"] = Wood, ["volture_strawpile"] = Wood, ["elaking_trashpile"] = Wood,
        // Jotun Halls
        ["Morkhalla_Banner1"] = Wood, ["Morkhalla_Banner2"] = Wood, ["Morkhalla_Bench"] = Wood, ["Morkhalla_Stool"] = Wood,
        ["Morkhalla_Table"] = Wood, ["Morkhalla_Bedroll1"] = Wood, ["Morkhalla_Bedroll2"] = Wood,
        ["Morkhalla_WeaponStand"] = Wood, ["Morkhalla_Trainingdummy1"] = Wood, ["Morkhalla_Trainingdummy2"] = Wood,
        ["Morkhalla_WoodBoards"] = Wood, ["Morkhalla_Rug_middle"] = Wood, ["Morkhalla_Rug_corner"] = Wood,
        ["Morkhalla_Rug_end1"] = Wood, ["Morkhalla_Rug_end2"] = Wood, ["Morkhalla_Rug_stair"] = Wood,
        // Dungeon Decor
        ["dvergrtown_wood_beam"] = Wood, ["dvergrtown_wood_pole"] = Wood, ["dvergrtown_wood_stake"] = Wood,
        ["dvergrtown_wood_stakewall"] = Wood, ["dvergrtown_stair_corner_wood_left"] = Wood,
        ["dvergrprops_crate_ashlands"] = Wood, ["cloth_hanging_door"] = Wood, ["cloth_hanging_door_double"] = Wood,
        ["fenrirhide_hanging_door"] = Wood, ["wooden_path"] = Wood, ["trader_wagon_destructable"] = Wood,
        ["prop_itemstand"] = Wood, ["prop_piece_chair03"] = Wood,
        // Props and Workshop Props
        ["CastleKit_pot03"] = Stone, ["ashland_pot1_green"] = Stone, ["ashland_pot1_red"] = Stone,
        ["ashland_pot2_green"] = Stone, ["ashland_pot2_red"] = Stone, ["ashland_pot3_green"] = Stone,
        ["ashland_pot3_red"] = Stone, ["prop_cauldron_ext5_mortarandpestle"] = Stone
    };

    /// <summary>Metal pieces whose names don't give it away.</summary>
    private static readonly HashSet<string> MetalPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "prop_piece_cauldron", "prop_piece_MeadCauldron"
    };

    /// <summary>
    /// Plants, trees, rocks and the like. Some of them glow (glowing mushrooms), but only crafted
    /// light sources such as torches and lanterns cost resin.
    /// </summary>
    private static readonly HashSet<string> NaturalCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trees", "Stumps & Logs", "Plants", "Roots & Branches", "Rocks", "Cliffs", "Ice", "Ore & Mining",
        "Natural Props", "Bones & Remains"
    };

    /// <summary>Categories whose pieces are rock even when named after a metal, e.g. the iron mine rock.</summary>
    private static readonly HashSet<string> NeverMetalCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rocks", "Cliffs", "Ore & Mining"
    };

    private static bool IsMetal(DecorativePieceDefinition definition)
    {
        if (MetalPrefabs.Contains(definition.PrefabName))
        {
            return true;
        }

        if (NeverMetalCategories.Contains(definition.Category))
        {
            return false;
        }

        var name = definition.PrefabName.ToLowerInvariant();
        return MetalNameHints.Any(name.Contains);
    }

    /// <summary>Wood and ice: stump shelters and ships frozen in ice.</summary>
    private static readonly HashSet<string> WoodAndIcePrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "StumpHut", "StumpHole", "frozenship", "frozenship02", "frozenship03",
        "Ice_ship_1", "Ice_ship_2", "Ice_ship_3", "Ice_ship_4", "Ice_ship_5", "Ice_ship_6", "Ice_ship_7"
    };

    /// <summary>Ice, besides everything in the Ice category.</summary>
    private static readonly HashSet<string> IcePrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "rock3_ice", "ice_rock1", "IcePond_rock", "FrozenGD"
    };

    /// <summary>Stone, although in the bones category.</summary>
    private static readonly HashSet<string> NotBonePrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "giant_brain"
    };

    /// <summary>What a piece that isn't a pickable or metal is made of: one material, or two for
    /// pieces like the stump hut (wood and ice).</summary>
    private static string[] MaterialsFor(DecorativePieceDefinition definition)
    {
        if (WoodAndIcePrefabs.Contains(definition.PrefabName))
        {
            return new[] { Wood, Ice };
        }

        if (IcePrefabs.Contains(definition.PrefabName) || definition.Category.Equals("Ice", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { Ice };
        }

        if (definition.Category.Equals("Bones & Remains", StringComparison.OrdinalIgnoreCase) && !NotBonePrefabs.Contains(definition.PrefabName))
        {
            return new[] { Bone };
        }

        return new[] { MaterialFor(definition) };
    }

    /// <summary>Wood or stone: by override, by category, or for other categories (such as Building
    /// Structures, Copied and custom ones) by prefab name, falling back to the tool.</summary>
    private static string MaterialFor(DecorativePieceDefinition definition)
    {
        if (MaterialOverrides.TryGetValue(definition.PrefabName, out var material))
        {
            return material;
        }

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

    /// <summary>How much wood or stone, from the largest dimension of the model in meters.</summary>
    private static int AmountFor(float size) => Scaled(size < 2f ? 2 : size < 6f ? 4 : size < 15f ? 6 : 8);

    private static int Scaled(int amount) => Mathf.Max(1, Mathf.RoundToInt(amount * Multiplier()));

    private static ItemDrop? ItemNamed(string name) => PrefabManager.Instance.GetPrefab(name)?.GetComponent<ItemDrop>();
}
