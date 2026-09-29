using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>What a piece's automatic cost is based on, measured from its prefab when registered.</summary>
internal sealed class PieceTraits
{
    public PieceTraits(float size, ItemDrop? pickableItem, bool hasLight, Piece.Requirement[]? vanillaResources, CraftingStation? vanillaStation,
        bool isLiquid, int pickableAmount = 1, bool pickableRegrows = true)
    {
        PickableAmount = pickableAmount;
        PickableRegrows = pickableRegrows;
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

    /// <summary>How many of <see cref="PickableItem"/> picking it gives.</summary>
    public int PickableAmount { get; }

    /// <summary>Whether it grows back after picking (bushes, mushrooms) rather than being used up
    /// (crops, loose stones).</summary>
    public bool PickableRegrows { get; }

    /// <summary>Whether it gives off light, like torches, lanterns and braziers.</summary>
    public bool HasLight { get; }
}

/// <summary>
/// Automatic build costs for pieces that don't set their own. Removing a piece refunds its cost.
/// Pieces cloned from vanilla build pieces use the vanilla cost and crafting station instead (see
/// DecorativePieceManager.CostFor); for the rest:
/// <list type="bullet">
/// <item>A few pieces have a set cost, such as the guck sacks (1 or 2 guck).</item>
/// <item>Pickables that grow back (bushes, mushrooms, thistle, ...) cost 5 of the item they give.
/// Ones that are used up when picked (crops, loose stones, fallen branches) cost exactly what picking
/// them gives, so placing and picking one breaks even.</item>
/// <item>Metal pieces (lanterns, braziers, iron torches, chains, ...) cost 1 iron.</item>
/// <item>Everything else costs 2 to 8 depending on the size of its model, of wood or stone depending
/// on what it is, ice for ice and snow, bone fragments for bones, or wood and ice split evenly for
/// the stump shelters and frozen ships. Wood, stone and bone are the ones from the piece's biome, such
/// as Yggdrasil wood and black marble for the Mistlands (see BiomeMaterials).</item>
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
    private const string RedJute = "JuteRed";
    private const string Linen = "LinenThread";
    private const string Grausten = "Grausten";
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

    /// <summary>Prefab names the cost rules single out, which should all be in the catalog.</summary>
    public static IEnumerable<string> ReferencedPrefabs() =>
        FixedCosts.Keys.Concat(MaterialOverrides.Keys).Concat(ExactMaterials.Keys).Concat(ExtraCosts.Keys).Concat(SizedExtraCosts.Keys).Concat(NoResinPrefabs).Concat(MetalPrefabs).Concat(WoodAndIcePrefabs)
            .Concat(IcePrefabs).Concat(NotBonePrefabs);

    /// <summary>Item names the cost rules charge, which should all exist in the game.</summary>
    public static IEnumerable<string> ReferencedItems() =>
        new[] { Wood, Stone, Iron, Resin, Ice, Bone }.Concat(FixedCosts.Values.SelectMany(costs => costs.Select(cost => cost.Item)))
            .Concat(ExtraCosts.Values.Select(cost => cost.Item)).Concat(SizedExtraCosts.Values)
            .Concat(MaterialOverrides.Values).Concat(ExactMaterials.Values).Concat(ForgeMaterials).Concat(StonecutterMaterials)
            .Concat(BiomeMaterials.Values.SelectMany(materials => new[] { materials.Wood, materials.Stone, materials.Bone }));

    /// <summary>Where a piece comes from, which decides whose wood, stone and bone it costs.</summary>
    private enum Biome
    {
        Meadows,
        BlackForest,
        Swamp,
        Mountain,
        Plains,
        Mistlands,
        Ashlands,
        DeepNorth
    }

    /// <summary>The wood, stone and bone of each biome, so pieces are built with what their biome yields.</summary>
    private static readonly Dictionary<Biome, (string Wood, string Stone, string Bone)> BiomeMaterials = new()
    {
        [Biome.Meadows] = (Wood, Stone, Bone),
        [Biome.BlackForest] = ("RoundLog", Stone, Bone),
        [Biome.Swamp] = ("ElderBark", Stone, Bone),
        [Biome.Mountain] = (Wood, "Obsidian", Bone),
        [Biome.Plains] = ("FineWood", Stone, Bone),
        [Biome.Mistlands] = ("YggdrasilWood", "BlackMarble", Bone),
        [Biome.Ashlands] = ("Blackwood", "Grausten", "CharredBone"),
        [Biome.DeepNorth] = ("Frostwood", Stone, Bone)
    };

    /// <summary>
    /// Prefab name parts that give a piece's biome, checked in order, so that for example an
    /// Ashlands rock counts as Ashlands before "rock" could suggest anything else. The later biomes
    /// come first because their names are the most specific. A hint starting with ^ only matches the
    /// start of the name ("ice" alone would match "spice").
    /// </summary>
    private static readonly (Biome Biome, string[] Hints)[] BiomeNameHints =
    {
        (Biome.Ashlands, new[] { "ashland", "ashwood", "blackwood", "charred", "lava", "asksvin", "morgen", "volture", "fader", "grausten", "flametal", "meteorite", "vineash" }),
        (Biome.DeepNorth, new[] { "deepnorth", "morkhalla", "morkborg", "frozen", "frost", "jotun", "^ice", "_ice", "blackice" }),
        (Biome.Mistlands, new[] { "mistland", "dvergr", "dverger", "yggdrasil", "ygga", "giant_", "creep", "seeker", "blackmarble", "hexagonal" }),
        (Biome.Plains, new[] { "goblin", "fuling", "plains", "heath", "tarlump", "tarpit", "lox" }),
        (Biome.Swamp, new[] { "swamp", "sunken", "guck", "bonemass", "draugr", "leech", "mudpile", "bogwitch", "ancient" }),
        (Biome.Mountain, new[] { "mountain", "snow", "obsidian", "silver", "crystal", "drake", "wolf", "fenrir" }),
        (Biome.BlackForest, new[] { "pine", "firtree", "forest", "troll", "greydwarf", "copper", "_tin" })
    };

    /// <summary>Tabs whose pieces all come from one biome, for names that don't say.</summary>
    private static readonly Dictionary<string, Biome> BiomeCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ashlands Ruins"] = Biome.Ashlands,
        ["Jotun Halls"] = Biome.DeepNorth,
        ["Dvergr"] = Biome.Mistlands,
        ["Fuling Village"] = Biome.Plains
    };

    private static Biome BiomeOf(DecorativePieceDefinition definition)
    {
        var name = definition.PrefabName.ToLowerInvariant();
        foreach (var (biome, hints) in BiomeNameHints)
        {
            if (hints.Any(hint => hint.StartsWith("^", StringComparison.Ordinal) ? name.StartsWith(hint.Substring(1), StringComparison.Ordinal) : name.Contains(hint)))
            {
                return biome;
            }
        }

        return BiomeCategories.TryGetValue(definition.Category, out var categoryBiome) ? categoryBiome : Biome.Meadows;
    }

    /// <summary>The biome's own wood, stone or bone in place of the plain one; other materials as they are.</summary>
    private static string ForBiome(string material, Biome biome)
    {
        var materials = BiomeMaterials[biome];
        return material switch
        {
            Wood => materials.Wood,
            Stone => materials.Stone,
            Bone => materials.Bone,
            _ => material
        };
    }

    /// <summary>Reads whether automatic costs are on.</summary>
    public static Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Reads the multiplier applied to automatic costs.</summary>
    public static Func<float> Multiplier { get; set; } = () => 1f;

    /// <summary>The items and amounts a piece costs. An item is null if it can't be found.</summary>
    public static IEnumerable<(ItemDrop? Item, int Amount)> CostFor(DecorativePieceDefinition definition, PieceTraits traits)
    {
        // Pickables cost only what they give, even the ones that glow: 5 for ones that grow back, and
        // what one pick gives for ones that are used up, such as crops. Plain stone or wood always
        // costs just 1.
        if (traits.IsLiquid)
        {
            yield break;
        }

        if (FixedCosts.TryGetValue(definition.PrefabName, out var fixedCosts))
        {
            foreach (var (item, amount) in fixedCosts)
            {
                yield return (ItemNamed(item), Scaled(amount));
            }

            yield break;
        }

        if (traits.PickableItem is { } picked)
        {
            var givesBasicMaterial = picked.name is Wood or Stone;
            yield return (picked, Scaled(givesBasicMaterial ? 1 : traits.PickableRegrows ? PickableAmount : Mathf.Max(1, traits.PickableAmount)));
            yield break;
        }

        if (IsMetal(definition))
        {
            yield return (ItemNamed(Iron), Scaled(1));
        }
        else
        {
            // Pieces made of two materials split the amount, so they cost the same in total. Wood,
            // stone and bone are the ones from the piece's biome.
            var biome = BiomeOf(definition);
            var materials = ExactMaterials.TryGetValue(definition.PrefabName, out var exact)
                ? new[] { exact }
                : MaterialsFor(definition).Select(material => ForBiome(material, biome)).ToArray();
            var amount = AmountFor(traits.Size);
            foreach (var material in materials)
            {
                yield return (ItemNamed(material), Mathf.Max(1, Mathf.CeilToInt(amount / (float)materials.Length)));
            }
        }

        if (ExtraCosts.TryGetValue(definition.PrefabName, out var extra))
        {
            yield return (ItemNamed(extra.Item), extra.Amount);
        }

        if (SizedExtraCosts.TryGetValue(definition.PrefabName, out var sizedExtra))
        {
            yield return (ItemNamed(sizedExtra), AmountFor(traits.Size));
        }

        if (traits.HasLight && !NaturalCategories.Contains(definition.Category) && !NoResinPrefabs.Contains(definition.PrefabName))
        {
            yield return (ItemNamed(Resin), Scaled(1));
        }
    }

    /// <summary>Items some pieces cost on top of their material: red jute for the Jotun and charred banners, linen
    /// for the Fuling walls, stairs and ladder, black marble for the infested growths, and the trophy
    /// each trophy stand shows.</summary>
    private static readonly Dictionary<string, (string Item, int Amount)> ExtraCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Morkhalla_Banner1"] = (RedJute, 2), ["Morkhalla_Banner2"] = (RedJute, 2),
        ["CharredBanner1"] = (RedJute, 2), ["CharredBanner2"] = (RedJute, 2), ["CharredBanner3"] = (RedJute, 2),
        ["goblin_woodwall_1m"] = (Linen, 2), ["goblin_woodwall_2m"] = (Linen, 2), ["goblin_woodwall_2m_ribs"] = (Linen, 2),
        ["goblin_stairs"] = (Linen, 2), ["goblin_stepladder"] = (Linen, 2),
        ["CreepProp_egg_hanging01"] = ("BlackMarble", 1), ["CreepProp_egg_hanging02"] = ("BlackMarble", 1), ["CreepProp_hanging01"] = ("BlackMarble", 1),
        ["CreepProp_wall01"] = ("BlackMarble", 1), ["CreepProp_entrance1"] = ("BlackMarble", 1), ["CreepProp_entrance2"] = ("BlackMarble", 1),
        ["prop_itemstand_TrophyDraugrElite"] = ("TrophyDraugrElite", 1),
        ["prop_itemstand_TrophyGoblinBrute"] = ("TrophyGoblinBrute", 1),
        ["prop_itemstand_TrophyGoblinShaman"] = ("TrophyGoblinShaman", 1),
        ["prop_itemstand_TrophyGreydwarf"] = ("TrophyGreydwarf", 1),
        ["prop_itemstand_TrophyGreydwarfBrute"] = ("TrophyGreydwarfBrute", 1),
        ["prop_itemstand_TrophySeekerBrute"] = ("TrophySeekerBrute", 1)
    };

    /// <summary>Items some pieces cost on top of their material, 2 to 8 by size like the material
    /// itself: lox pelt for the Fuling roofs.</summary>
    private static readonly Dictionary<string, string> SizedExtraCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["goblin_roof_45d"] = "LoxPelt", ["goblin_roof_45d_corner"] = "LoxPelt", ["goblin_roof_cap"] = "LoxPelt"
    };

    private const string Workbench = "piece_workbench";
    private const string Forge = "forge";
    private const string Stonecutter = "piece_stonecutter";
    private const string BlackForge = "blackforge";

    /// <summary>The largest dimension, in meters, from which stone pieces need the stonecutter.</summary>
    private const float StonecutterSize = 2f;

    /// <summary>Materials that make a piece forge work: bronze and iron.</summary>
    private static readonly HashSet<string> ForgeMaterials = new(StringComparer.OrdinalIgnoreCase)
    {
        Iron, "Bronze", "IronScrap"
    };

    /// <summary>Materials that make a piece stonecutter work: stone of every biome, and the ore rocks.</summary>
    private static readonly HashSet<string> StonecutterMaterials = new(StringComparer.OrdinalIgnoreCase)
    {
        Stone, "Obsidian", "BlackMarble", Grausten, "CopperOre", "TinOre", "SilverOre", "GoldOre", "FlametalOreNew"
    };

    /// <summary>
    /// The crafting station a piece with an automatic cost needs nearby, like vanilla pieces, by
    /// prefab name: the one that fits what it's made of. Bronze and iron need the forge (the black
    /// forge for Mistlands and later pieces); stone, obsidian, black marble, grausten and ore need the
    /// stonecutter, but only for pieces at least StonecutterSize big, since a small stone thing such as
    /// a clay pot needs no stonecutting; everything else (any wood, hides, jute, bone, ice, ...) needs
    /// the workbench. Cultivator pieces (trees, plants, crops) need none, like planting in vanilla, and
    /// nor does pond water, which is free.
    /// </summary>
    public static string? StationFor(DecorativePieceDefinition definition, PieceTraits traits, IEnumerable<string> costItems)
    {
        if (traits.IsLiquid || definition.Tool == BuildTool.Cultivator)
        {
            return null;
        }

        var items = new HashSet<string>(costItems, StringComparer.OrdinalIgnoreCase);
        if (items.Any(ForgeMaterials.Contains))
        {
            return BiomeOf(definition) >= Biome.Mistlands ? BlackForge : Forge;
        }

        return traits.Size >= StonecutterSize && items.Any(StonecutterMaterials.Contains) ? Stonecutter : Workbench;
    }

    /// <summary>The crafting stations the rules use, which should all exist in the game.</summary>
    public static IEnumerable<string> ReferencedStations() =>
        new[] { Workbench, Forge, Stonecutter, BlackForge };

    /// <summary>Pieces with a set cost of their own, instead of one worked out from their size.</summary>
    private static readonly Dictionary<string, (string Item, int Amount)[]> FixedCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GuckSack_small"] = new[] { ("Guck", 1) },
        ["GuckSack"] = new[] { ("Guck", 2) },
        ["goblin_strawpile"] = new[] { ("Flax", 2), ("Barley", 2) },
        // Webs: sticky and stringy. A flat cost, since the models are 20 to 60 m and would all cost the most by size.
        ["horizontal_web"] = new[] { (Resin, 2), (Linen, 2) },
        ["vertical_web"] = new[] { (Resin, 2), (Linen, 2) },
        ["tunnel_web"] = new[] { (Resin, 2), (Linen, 2) },
        ["morkhalla_web_corner"] = new[] { (Resin, 2), (Linen, 2) },
        ["morkhalla_web_horisontal"] = new[] { (Resin, 2), (Linen, 2) },
        ["morkhalla_web_tunnel"] = new[] { (Resin, 2), (Linen, 2) }
    };

    /// <summary>
    /// Pieces made of something other than what their category is mostly made of: wooden and cloth
    /// pieces among the stone ruins and dungeon decor, and stone pots among the wooden props.
    /// </summary>
    private static readonly Dictionary<string, string> MaterialOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Natural Props
        ["stubbe"] = Wood, ["stubbe_deepnorth"] = Wood, ["volture_strawpile"] = Wood, ["elaking_trashpile"] = Wood,
        // Jotun Halls: wooden furniture, grausten stonework and statues, red jute rugs, a fine wood bridge
        ["Morkhalla_Banner1"] = Wood, ["Morkhalla_Banner2"] = Wood, ["Morkhalla_Bench"] = Wood, ["Morkhalla_Stool"] = Wood,
        ["Morkhalla_Table"] = Wood, ["Morkhalla_Bedroll1"] = Wood, ["Morkhalla_Bedroll2"] = Wood,
        ["Morkhalla_WeaponStand"] = Wood,
        ["Morkhalla_WoodBoards"] = Wood, ["Morkhalla_rubble_trashpile"] = Wood,
        ["Morkhalla_Rug_middle"] = RedJute, ["Morkhalla_Rug_corner"] = RedJute, ["Morkhalla_Rug_end1"] = RedJute,
        ["Morkhalla_Rug_end2"] = RedJute, ["Morkhalla_Rug_stair"] = RedJute,
        ["Morkhalla_bridge_davinci"] = "FineWood",
        ["Morkhalla_giant_railing"] = Grausten, ["Morkhalla_giant_railing_corner"] = Grausten,
        ["Morkhalla_giant_railing_half"] = Grausten, ["Morkhalla_giant_railing_single"] = Grausten,
        ["Morkhalla_giant_railing_deco"] = Grausten, ["Morkhalla_giant_railing_torch"] = Grausten,
        ["Morkhalla_Stairs_giant_railing"] = Grausten,
        ["Morkhalla_Floor_2x2"] = Grausten, ["Morkhalla_Floor_4x4"] = Grausten,
        ["Morkhalla_Floor_4x4_broken01"] = Grausten, ["Morkhalla_Floor_4x4_broken02"] = Grausten,
        ["Morkhalla_StatuePieceArmL"] = Grausten, ["Morkhalla_StatuePieceArmR"] = Grausten,
        ["Morkhalla_StatuePieceFace"] = Grausten, ["Morkhalla_StatuePieceFeet"] = Grausten,
        ["Morkhalla_StatuePieceHorn"] = Grausten, ["Morkhalla_StatuePieceHorn2"] = Grausten,
        ["Morkhalla_StatuePieceLegs"] = Grausten, ["Morkhalla_StatuePieceSword"] = Grausten,
        ["Morkhalla_StatuePieceTorso"] = Grausten,
        ["Morkhalla_StatuePieceArmL_big"] = Grausten, ["Morkhalla_StatuePieceArmR_big"] = Grausten,
        ["Morkhalla_StatuePieceFace_big"] = Grausten, ["Morkhalla_StatuePieceFeet_big"] = Grausten,
        ["Morkhalla_StatuePieceHorn_big"] = Grausten, ["Morkhalla_StatuePieceHorn2_big"] = Grausten,
        ["Morkhalla_StatuePieceLegs_big"] = Grausten, ["Morkhalla_StatuePieceSword_big"] = Grausten,
        ["Morkhalla_StatuePieceTorso_big"] = Grausten,
        ["Morkhalla_StatueSword"] = Grausten, ["Morkhalla_StatueSword_hanging"] = Grausten, ["Morkborg_gate"] = Grausten,
        ["Morkhalla_jotun_gate"] = Grausten, ["Morkhalla_Block"] = Grausten, ["Morkhalla_Stonepile"] = Grausten,
        ["Morkhalla_Stairs_giant_short"] = Grausten, ["Morkhalla_Stairs_giant_short_broken1"] = Grausten,
        ["Morkhalla_Stairs_giant_short_broken2"] = Grausten,
        ["Morkhalla_Rubble1"] = Grausten, ["Morkhalla_Rubble2"] = Grausten, ["Morkhalla_Rubble3"] = Grausten,
        ["Morkhalla_Rubble4"] = Grausten,
        // Ore rocks cost their ore; muddy scrap piles, scrap iron
        ["rock4_copper"] = "CopperOre", ["MineRock_Copper"] = "CopperOre", ["MineRock_Tin"] = "TinOre",
        ["MineRock_Iron"] = "IronScrap", ["rock3_silver"] = "SilverOre", ["silvervein"] = "SilverOre",
        ["goldvein"] = "GoldOre", ["MineRock_Obsidian"] = "Obsidian", ["MineRock_Meteorite"] = "FlametalOreNew",
        ["mudpile"] = "IronScrap", ["mudpile2"] = "IronScrap", ["mudpile_old"] = "IronScrap",
        // Infested mine growths: resin by size, plus 1 black marble (ExtraCosts)
        ["CreepProp_egg_hanging01"] = Resin, ["CreepProp_egg_hanging02"] = Resin, ["CreepProp_hanging01"] = Resin,
        ["CreepProp_wall01"] = Resin, ["CreepProp_entrance1"] = Resin, ["CreepProp_entrance2"] = Resin,
        // Cloth and rugs: red jute, and the Bog Witch rugs the fur they're made of
        ["cloth_hanging_long"] = RedJute, ["goblin_banner"] = RedJute,
        // Fuling roofs: lox pelt
        ["goblin_roof_45d"] = "LoxPelt", ["goblin_roof_45d_corner"] = "LoxPelt", ["goblin_roof_cap"] = "LoxPelt",
        ["rug_bogwitch_deer"] = "DeerHide", ["rug_bogwitch_fur"] = "LoxPelt", ["rug_bogwitch_wolf"] = "WolfPelt",
        // Dungeon Decor
        ["dvergrtown_wood_beam"] = Wood, ["dvergrtown_wood_pole"] = Wood, ["dvergrtown_wood_stake"] = Wood,
        ["dvergrtown_wood_stakewall"] = Wood, ["dvergrtown_stair_corner_wood_left"] = Wood,
        ["dvergrprops_crate_ashlands"] = Wood, ["cloth_hanging_door"] = RedJute, ["cloth_hanging_door_double"] = RedJute,
        ["fenrirhide_hanging_door"] = Wood, ["wooden_path"] = Wood, ["trader_wagon_destructable"] = Wood,
        ["prop_itemstand"] = Wood, ["prop_piece_chair03"] = Wood,
        // Props and Workshop Props
        ["CastleKit_pot03"] = Stone, ["ashland_pot1_green"] = Stone, ["ashland_pot1_red"] = Stone,
        ["ashland_pot2_green"] = Stone, ["ashland_pot2_red"] = Stone, ["ashland_pot3_green"] = Stone,
        ["ashland_pot3_red"] = Stone, ["prop_cauldron_ext5_mortarandpestle"] = Stone
    };

    /// <summary>
    /// Pieces that cost exactly this item, not their biome's version of it: the mountain gravestone is
    /// plain stone, although Mountain stone is otherwise obsidian, and the Jotun training dummies
    /// plain wood rather than frostwood.
    /// </summary>
    private static readonly Dictionary<string, string> ExactMaterials = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MountainGraveStone01"] = Stone,
        ["Morkhalla_Trainingdummy1"] = Wood, ["Morkhalla_Trainingdummy2"] = Wood
    };

    /// <summary>Pieces that give off light but aren't light sources, so they cost no resin.</summary>
    private static readonly HashSet<string> NoResinPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "prop_piece_MeadCauldron"
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
