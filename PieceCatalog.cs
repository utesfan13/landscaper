using UnityEngine;

namespace Landscaper;

/// <summary>
/// Built-in pieces. Each entry is cloned as "Landscaper_{prefab}_{tool}" (scaled entries add their
/// display name), and pieces placed in a world are saved under that name, so moving an existing
/// prefab to a different tool makes previously placed copies of it disappear.
/// </summary>
public static class PieceCatalog
{
    public static IEnumerable<DecorativePieceDefinition> Entries()
    {
        foreach (var group in Groups)
        {
            foreach (var entry in group.Entries)
            {
                yield return new DecorativePieceDefinition
                {
                    DisplayName = entry.Name,
                    PrefabName = entry.Prefab,
                    Description = entry.Description,
                    Tool = group.Tool,
                    Category = group.Category,
                    Scale = entry.Scale,
                    OnWater = entry.OnWater
                };
            }
        }
    }

    private sealed class Entry
    {
        public Entry(string name, string prefab, string description, Vector3 scale, bool onWater)
        {
            OnWater = onWater;
            Name = name;
            Prefab = prefab;
            Description = description;
            Scale = scale;
        }

        public string Name { get; }
        public string Prefab { get; }
        public string Description { get; }
        public Vector3 Scale { get; }
        public bool OnWater { get; }
    }

    private sealed class Group
    {
        public Group(BuildTool tool, string category, params Entry[] entries)
        {
            Tool = tool;
            Category = category;
            Entries = entries;
        }

        public BuildTool Tool { get; }
        public string Category { get; }
        public Entry[] Entries { get; }
    }

    private static Entry E(string name, string prefab, string description = "", Vector3? scale = null, bool onWater = false) =>
        new(name, prefab, description, scale ?? Vector3.one, onWater);

    private static readonly Group[] Groups =
    {
        new(BuildTool.Cultivator, "Trees",
            E("Beech Tree", "Beech1", "A beech tree for woodland landscaping."),
            E("Small Beech Tree 1", "Beech_small1"), E("Small Beech Tree 2", "Beech_small2"),
            E("Birch Tree 1", "Birch1_aut", "A birch tree for woodland landscaping."), E("Birch Tree 2", "Birch2_aut"),
            E("Meadows Birch 1", "Birch1"), E("Meadows Birch 2", "Birch2"),
            E("Fir Tree", "FirTree", "A fir tree for woodland landscaping."),
            E("Small Fir", "FirTree_small"), E("Dead Small Fir", "FirTree_small_dead"), E("Big Fir", "FirTree_big"),
            E("Oak Tree", "Oak1", "An oak tree for woodland landscaping."),
            E("Pine Tree 1", "Pinetree_01", "A pine tree for woodland landscaping."), E("Pine Tree 2", "PineTree"),
            E("Swamp Tree", "SwampTree1"), E("Swamp Tree 2", "SwampTree2"), E("Dark Swamp Tree", "SwampTree2_darkland"),
            E("Snowy Pine", "Pinetree_Snow"), E("Dead Snowy Pine", "Pinetree_Snow_dead"),
            E("Snowy Fir", "SnowFirTree"), E("Snowy Fir 2", "SnowFirTree 2"), E("Small Snowy Fir", "SnowFirTree_small"),
            E("Yggdrasil Shoot 1", "YggaShoot1"), E("Yggdrasil Shoot 2", "YggaShoot2"), E("Yggdrasil Shoot 3", "YggaShoot3"),
            E("Small Yggdrasil Shoot", "YggaShoot_small1"),
            E("Ashlands Tree 1", "AshlandsTree1"), E("Ashlands Tree 2", "AshlandsTree3"), E("Ashlands Tree 3", "AshlandsTree4"),
            E("Ashlands Tree 4", "AshlandsTree5"), E("Ashlands Tree 5", "AshlandsTree6"), E("Large Ashlands Tree", "AshlandsTree6_big")),

        new(BuildTool.Cultivator, "Stumps & Logs",
            E("Beech Stump", "Beech_Stub"), E("Birch Stump", "BirchStub"), E("Fir Stump", "FirTree_Stub"),
            E("Oak Stump", "OakStub"), E("Pine Stump", "Pinetree_01_Stub"),
            E("Swamp Tree Stump", "SwampTree1_Stub"), E("Snowy Fir Stump", "FirTree_Snow_Stub"),
            E("Snowy Pine Stump", "Pinetree_Snow_Stub"), E("Yggdrasil Stump", "ShootStump"),
            E("Ashlands Stump 1", "AshlandsTreeStump1"), E("Ashlands Stump 2", "AshlandsTreeStump2"),
            E("Ashlands Stump 3", "AshlandsTreeStump3"),
            E("Old Fir Log", "FirTree_oldLog"), E("Old Pine Log", "PineTree_logOLD"),
            E("Old Snowy Fir Log", "FirTree_oldLog_deepnorth"), E("Hollow Log", "StumpLog")),

        new(BuildTool.Cultivator, "Plants",
            E("Raspberry Bush", "RaspberryBush", "A berry-bearing shrub that fits a cultivated garden."),
            E("Blueberry Bush", "BlueberryBush", "A bush that suits hedges and berry patches."),
            E("Cloudberry Bush", "CloudberryBush"), E("Lingonberry Bush", "LingonberryBush"),
            E("Bush", "Bush01"), E("Bush 2", "Bush02_en"), E("Heath Bush", "Bush01_heath"), E("Snowy Bush", "Bush01_deepnorth"),
            E("Shrub", "shrub_2"), E("Heath Shrub", "shrub_2_heath"),
            E("Dandelion", "Pickable_Dandelion", "A small floral accent for gardens and paths."),
            E("Thistle", "Pickable_Thistle"), E("Fiddlehead", "Pickable_Fiddlehead"),
            E("Wild Flax", "Pickable_Flax_Wild"), E("Wild Barley", "Pickable_Barley_Wild"),
            E("Mushroom", "Pickable_Mushroom"), E("Yellow Mushroom", "Pickable_Mushroom_yellow"),
            E("Blue Mushroom", "Pickable_Mushroom_blue"), E("Magecap", "Pickable_Mushroom_Magecap"),
            E("Jotun Puffs", "Pickable_Mushroom_JotunPuffs"), E("Glowing Mushroom", "GlowingMushroom"),
            E("Ashlands Fern", "FernAshlands"), E("Ashlands Fiddlehead Fern", "FernFiddleHeadAshlands"),
            E("Ashlands Bush 1", "AshlandsBush1"), E("Ashlands Bush 2", "AshlandsBush2"),
            E("Green Vines", "VineGreen"), E("Ash Vines", "VineAsh"), E("Vines", "vines"),
            // Built from Valheim's clutter by ClutterPrefabs.
            E("Cattails", ClutterPrefabs.Prefix + "vass", "Reeds for pond and swamp edges."),
            E("Lily Pads", ClutterPrefabs.Prefix + "waterlilies", "Floats on the water surface.", onWater: true),
            E("Loose Stone", "Pickable_Stone"), E("Loose Rock", "Pickable_StoneRock"),
            E("Sulfur Rock", "Pickable_SulfurRock"), E("Flint", "Pickable_Flint"), E("Fallen Branch", "Pickable_Branch")),

        new(BuildTool.Cultivator, "Roots & Branches",
            E("Root 1", "root07"), E("Root 2", "root08"), E("Root 3", "root11"), E("Root 4", "root12"),
            E("Huge Root", "HugeRoot1"), E("Big Branch", "BigBranch"),
            E("Ashlands Branch 1", "AshlandsBranch1"), E("Ashlands Branch 2", "AshlandsBranch2"),
            E("Ashlands Branch 3", "AshlandsBranch3"),
            E("Root Wall", "HoleRock_rootWall1"), E("Root Floor", "HoleRock_rootFloor1"), E("Root Bush", "HoleRock_root1"),
            E("Root Bush 2", "HoleRock_rootBush1"), E("Yggdrasil Root", "YggdrasilRoot")),

        new(BuildTool.Hoe, "Rocks",
            E("Meadows Rock", "Rock_7_meadows", "A natural rock used for landscaping accents."),
            E("Boulder", "Rock_destructible", "A large stone centerpiece for rocky landscaping."),
            E("Rock 3", "Rock_3"), E("Rock 4", "Rock_4"), E("Rock 4 Plains", "Rock_4_plains"), E("Rock 7", "Rock_7"),
            E("Mountain Boulder", "rock1_mountain"), E("Mountain Boulder 2", "rock2_mountain"),
            E("Mountain Boulder 3", "rock3_mountain"), E("Mountain Boulder 4", "rock3_mountain_1"),
            E("Heath Boulder", "rock2_heath"), E("Heath Boulder 2", "rock4_heath"), E("Coastal Boulder", "rock4_coast"),
            E("Forest Boulder", "rock4_forest"), E("Big Rock", "BigRock"), E("Rock Formation", "rockformation1"),
            E("Tall Rock", "highstone"), E("Tall Rock 2", "highstone_2"),
            E("Wide Stone", "widestone"), E("Wide Stone 2", "widestone_2"),
            E("Snowy Rock 3", "Rock_3_deepnorth"), E("Snowy Rock 4", "Rock_4_deepnorth"), E("Snowy Rock 7", "Rock_7_deepnorth"),
            E("Dolmen 1", "RockDolmen_1"), E("Dolmen 2", "RockDolmen_2"), E("Dolmen 3", "RockDolmen_3"),
            E("Rock Finger", "RockFinger"), E("Broken Rock Finger", "RockFingerBroken"), E("Rock Thumb", "RockThumb"),
            E("Mistlands Rock 1", "rock1_mistlands"), E("Mistlands Rock 2", "rock_mistlands1"),
            E("Mistlands Rock 3", "rock_mistlands2"),
            E("Ashlands Rock 1", "Ashlands_rock1"), E("Ashlands Rock 2", "Ashlands_rock2"), E("Lava Rock", "lavarock_ashlands1"),
            E("Shimmering Sand Rock", "ShimmeringSand_rock"),
            E("Icy Rock", "rock3_ice"), E("Ice Rock", "ice_rock1"), E("Ice Pond Rock", "IcePond_rock")),

        new(BuildTool.Hoe, "Cliffs",
            E("Mistlands Cliff 1", "cliff_mistlands1"), E("Mistlands Cliff 2", "cliff_mistlands2"),
            E("Creeping Mistlands Cliff", "cliff_mistlands1_creep"),
            E("Ashlands Cliff 1", "cliff_ashlands1"), E("Ashlands Cliff 2", "cliff_ashlands2"),
            E("Ashlands Cliff 3", "cliff_ashlands4"), E("Ashlands Cliff 4", "cliff_ashlands5"),
            E("Ashlands Cliff 5", "cliff_ashlands6"), E("Ashlands Cliff 6", "cliff_ashlands8"),
            E("Ashlands Cliff Arch", "cliff_ashlands3_Arch_1"), E("Ashlands Cliff Half Arch", "cliff_ashlands7_HalfArch")),

        new(BuildTool.Hoe, "Ice",
            E("Ice Shard 1", "IceShard_01"), E("Ice Shard 2", "IceShard_02"), E("Ice Shard 3", "IceShard_03"),
            E("Ice Shard 4", "IceShard_04"), E("Ice Shard 5", "IceShard_05"), E("Ice Shard 6", "IceShard_06"),
            E("Ice Stalagmite", "caverock_ice_stalagmite"), E("Ice Pillar Wall", "caverock_ice_pillar_wall"),
            E("Ice Wall", "IceWall"), E("Ice Floor", "Ice_floor"), E("Snow Wall 1", "WallSnow1"),
            E("Snow Wall 2", "WallSnow2"), E("Snow Wall 3", "WallSnow3"), E("Snow Wall 4", "WallSnow4"),
            E("Ice Block", "ice1"), E("Ice Shore", "IceShore"), E("Ice Shore 2", "IceShore_1"),
            E("Ice Shore Shard", "IceShoreShard"), E("Icicle", "caverock_ice_stalagtite"), E("Broken Ice Stalagmite", "caverock_ice_stalagmite_broken"),
            E("Black Ice Shard 1", "BlackIceShard_01"), E("Black Ice Shard 2", "BlackIceShard_02"), E("Ice Shelf 1", "IceShelf_01"),
            E("Ice Shelf 2", "IceShelf_02"), E("Ice Shelf 3", "IceShelf_03"), E("Ice Shelf 4", "IceShelf_04"),
            E("Ice Shelf 5", "IceShelf_05"), E("Ice Shelf 6", "IceShelf_06"), E("Ice Shelf 7", "IceShelf_07"),
            E("Ice Shelf 8", "IceShelf_08"), E("Ice Shelf 9", "IceShelf_09"), E("Ice Shelf 10", "IceShelf_10")),

        new(BuildTool.Hoe, "Water",
            // Valheim's simulated liquid, set to water: it pours in and settles into the lowest ground
            // around where it's placed, so dig a hollow first.
            E("Pond Water", "WaterLiquid", "Water that flows into the hollow you place it in. Dig the hollow first.")),

        new(BuildTool.Hoe, "Ore & Mining",
            E("Copper Boulder", "rock4_copper"), E("Silver Boulder", "rock3_silver"), E("Silver Vein", "silvervein"),
            E("Gold Vein", "goldvein"),
            E("Stone Mine Rock", "MineRock_Stone"), E("Copper Mine Rock", "MineRock_Copper"),
            E("Iron Mine Rock", "MineRock_Iron"), E("Tin Mine Rock", "MineRock_Tin"),
            E("Obsidian Mine Rock", "MineRock_Obsidian"), E("Meteorite", "MineRock_Meteorite")),

        new(BuildTool.Hoe, "Natural Props",
            E("Tree Stump", "stubbe", "A stump that feels right in a woodland or garden border."),
            E("Snowy Tree Stump", "stubbe_deepnorth"),
            E("Muddy Scrap Pile", "mudpile"), E("Large Muddy Scrap Pile", "mudpile2"), E("Old Muddy Scrap Pile", "mudpile_old"),
            E("Guck Sack", "GuckSack"), E("Small Guck Sack", "GuckSack_small"), E("Tar Lump", "tarlump1"),
            E("Volture Nest", "volture_strawpile"), E("Trash Pile", "elaking_trashpile"), E("Rubble Pile", "morgenhole_pile"),
            E("Stump Hole", "StumpHole"), E("Stump Hut", "StumpHut")),

        new(BuildTool.Hoe, "Bones & Remains",
            E("Large Bone", "LargeBone"), E("Large Bone Half 1", "LargeBone_half01"), E("Large Bone Half 2", "LargeBone_half02"),
            E("Lox Ribs", "lox_ribs"), E("Skull 1", "Skull1"), E("Skull 2", "Skull2"), E("Ashlands Skull", "veg_skull_Ashlands"),
            E("Giant Skull", "giant_skull"), E("Giant Ribs", "giant_ribs"), E("Giant Arm", "giant_arm"),
            E("Giant Brain", "giant_brain"), E("Giant Sword 1", "giant_sword1"), E("Giant Sword 2", "giant_sword2"),
            E("Giant Helmet 1", "giant_helmet1"), E("Giant Helmet 2", "giant_helmet2"),
            E("Ancient Skull", "ancient_skull"), E("Asksvin Carcass", "asksvin_carrion"), E("Asksvin Carcass 2", "asksvin_carrion2"),
            E("Frozen Skeleton 1", "FrozenSkeleton_Pose1"), E("Frozen Skeleton 2", "FrozenSkeleton_Pose2"), E("Frost Troll Corpse", "TrollFrost_Dead"),
            E("Frozen Greydwarf", "FrozenGD")),

        new(BuildTool.Hammer, "Statues",
            E("Deer Statue", "StatueDeer", "A decorative statue that fits shrines and courtyards."),
            E("Corgi Statue", "StatueCorgi"), E("Evil Statue", "StatueEvil"), E("Hare Statue", "StatueHare"),
            E("Seed Statue", "StatueSeed"), E("Freya Statue", "StatueFreya"), E("Thor Statue", "StatueThor"),
            E("Broken Freya Statue (Left)", "StatueFreya_broken_left"), E("Broken Freya Statue (Right)", "StatueFreya_broken_right"),
            E("Broken Thor Statue (Bottom)", "StatueThor_broken_bottom"), E("Broken Thor Statue (Top)", "StatueThor_broken_top"),
            E("Memorial Stone", "MemorialStone_Large"), E("Memorial Stone (Medium)", "MemorialStone_Medium"),
            E("Memorial Stone (Small)", "MemorialStone_Small"), E("Mountain Gravestone", "MountainGraveStone01"),
            E("Black Marble Post", "blackmarble_post01")),

        new(BuildTool.Hammer, "Furniture",
            E("Armor Stand", "ArmorStand"), E("Chest", "Chest"), E("Bed", "bed"),
            E("Iron Gate", "iron_grate"), E("Item Stand", "itemstand"), E("Horizontal Item Stand", "itemstandh"),
            E("Black Banner", "piece_banner01"), E("Blue Banner", "piece_banner02"),
            E("White and Red Banner", "piece_banner03"), E("Red Banner", "piece_banner04"),
            E("Green Banner", "piece_banner05"), E("Yellow Banner", "piece_banner08"),
            E("Purple Banner", "piece_banner09"), E("Orange Banner", "piece_banner10"),
            E("White Banner", "piece_banner11"), E("Hot Tub", "piece_bathtub"),
            E("Dragon Bed", "piece_bed02"), E("Bench", "piece_bench01"),
            E("Stool", "piece_chair"), E("Chair", "piece_chair02"), E("Darkwood Chair", "piece_chair03"),
            E("Reinforced Chest", "piece_chest"), E("Personal Chest", "piece_chest_private"),
            E("Wood Chest", "piece_chest_wood"), E("Standing Torch", "piece_groundtorch"),
            E("Sitting Log", "piece_logbench01"), E("Table", "piece_table"),
            E("Oak Table", "piece_table_oak"), E("Raven Throne", "piece_throne01"),
            E("Stone Throne", "piece_throne02"), E("Sconce", "piece_walltorch"),
            E("Deer Rug", "rug_deer"), E("Lox Rug", "rug_fur"), E("Wolf Rug", "rug_wolf"),
            E("Sign", "sign"), E("Coin Pile", "treasure_pile"), E("Coin Stack", "treasure_stack")),

        new(BuildTool.Hammer, "Building Structures",
            E("Crystal Wall", "crystal_wall_1x1"), E("Darkwood Arch", "darkwood_arch"),
            E("Darkwood Beam", "darkwood_beam"), E("Darkwood Gate", "darkwood_gate"),
            E("Darkwood Pole", "darkwood_pole"), E("Darkwood Roof", "darkwood_roof"),
            E("Iron Floor", "iron_floor_1x1"), E("Iron Floor 2x2", "iron_floor_2x2"),
            E("Iron Wall", "iron_wall_1x1"), E("Iron Wall 2x2", "iron_wall_2x2"),
            E("Stake Wall", "stake_wall"), E("Stone Arch", "stone_arch"),
            E("Stone Floor 2x2", "stone_floor_2x2"), E("Stone Pillar", "stone_pillar"),
            E("Stone Stair", "stone_stair"), E("Stone Wall 1x1", "stone_wall_1x1"),
            E("Stone Wall 2x1", "stone_wall_2x1"), E("Stone Wall 4x2", "stone_wall_4x2"),
            E("Wood Beam", "wood_beam"), E("Wood Beam 1m", "wood_beam_1"),
            E("Wood Beam 26", "wood_beam_26"), E("Wood Beam 45", "wood_beam_45"),
            E("Wood Door", "wood_door"), E("Wood Fence", "wood_fence"),
            E("Wood Floor", "wood_floor"), E("Wood Floor 1x1", "wood_floor_1x1"),
            E("Wood Gate", "wood_gate"), E("Wood Pole", "wood_pole"),
            E("Wood Pole 2m", "wood_pole2"), E("Wood Stair", "wood_stair"),
            E("Wood Ladder", "wood_stepladder"), E("Wood Wall Half", "wood_wall_half"),
            E("Wood Wall", "woodwall"), E("Wood Roof", "wood_roof"),
            E("Wood Roof 45", "wood_roof_45"), E("Wood Window", "wood_window"),
            E("Wood Iron Beam", "woodiron_beam"), E("Wood Iron Pole", "woodiron_pole")),

        new(BuildTool.Hammer, "Ruins",
            E("Ruined Stone Wall", "stone_wall_ruin"), E("Ruined Stone Wall 2", "stone_wall_ruin_2"),
            E("Ruined Stone Wall 1x1", "stone_wall_1x1_ruin"), E("Ruined Stone Wall 2x1", "stone_wall_2x1_ruin"),
            E("Old Stone Wall", "stonewall_2"), E("Old Stone Wall 2", "stonewall_3"),
            E("Dvergr Arch", "dvergrtown_arch")),

        new(BuildTool.Hammer, "Ashlands Ruins",
            E("Ashlands Ruin Wall", "Ashlands_Ruins_Wall_4x6"), E("Ashlands Ruin Wall (Windows)", "Ashlands_Ruins_Wall_Windows_Broken_4x6"), E("Ashlands Wall Block", "Ashlands_WallBlock"),
            E("Ashlands Large Floor", "Ashlands_floor_large"), E("Ashlands Fortress Pillar", "Ashlands_Fortress_Wall_Pillar"), E("Ashlands Arch Roof", "Ashlands_ArchRoof"),
            E("Ashlands Twisted Arch", "Ashlands_Ruins_twist_ArchBig"), E("Ashlands Pillar", "Ashlands_Pillar4"), E("Ashlands Broad Stairs", "Ashlands_StairsBroad"),
            E("Ashlands Wall 2x2", "Ashlands_Wall_2x2"), E("Ashlands Wall Top", "Ashlands_Wall_2x2_top"), E("Ashlands Wall Corner (Left)", "Ashlands_Wall_2x2_cornerL"),
            E("Ashlands Wall Corner Top (Left)", "Ashlands_Wall_2x2_cornerL_top"), E("Ashlands Wall Corner (Right)", "Ashlands_Wall_2x2_cornerR"), E("Ashlands Wall Corner Top (Right)", "Ashlands_Wall_2x2_cornerR_top"),
            E("Ashlands Wall Edge", "Ashlands_Wall_2x2_edge"), E("Ashlands Wall Edge Top", "Ashlands_Wall_2x2_edge_top"), E("Ashlands Wall Edge 2", "Ashlands_Wall_2x2_edge2"),
            E("Ashlands Wall Edge 2 Top", "Ashlands_Wall_2x2_edge2_top"), E("Ashlands Floor", "Ashlands_Floor"), E("Ashlands Fortress Floor", "Ashlands_Fortress_Floor"),
            E("Ashlands Ramp", "Ashlands_Ramp"), E("Ashlands Stair", "Ashland_Stair"), E("Ashlands Steep Stair", "Ashland_Steepstair"),
            E("Ashlands Ruin Floor 1.5x1.5", "Ashlands_Ruins_Floor_1point5x1point5"), E("Ashlands Broken Floor 1.5x1.5", "Ashlands_Ruins_Floor_1point5x1point5_broken"), E("Ashlands Ruin Floor 3x3", "Ashlands_Ruins_Floor_3x3"),
            E("Ashlands Broken Floor 3x3 1", "Ashlands_Ruins_Floor_3x3_broken1"), E("Ashlands Broken Floor 3x3 2", "Ashlands_Ruins_Floor_3x3_broken2"), E("Ashlands Broken Floor 3x3 3", "Ashlands_Ruins_Floor_3x3_broken3"),
            E("Ashlands Ruin Floor 6x6", "Ashlands_Ruins_Floor_6x6"), E("Ashlands Broken Floor 6x6 1", "Ashlands_Ruins_Floor_6x6_broken1"), E("Ashlands Broken Floor 6x6 2", "Ashlands_Ruins_Floor_6x6_broken2"),
            E("Ashlands Ruin Ramp", "Ashlands_Ruins_Ramp"), E("Ashlands Ruin Top Stone", "Ashlands_Ruins_TopStone"), E("Ashlands Twisted Pillar Base", "Ashlands_Ruins_twist_PillarBase"),
            E("Ashlands Small Twisted Pillar Base", "Ashlands_Ruins_twist_PillarBaseSmall"), E("Ashlands Broken Ruin Wall 1", "Ashlands_Ruins_Wall_Broken3_4x6"), E("Ashlands Broken Ruin Wall 2", "Ashlands_Ruins_Wall_Broken4_4x6"),
            E("Ashlands Broken Ruin Wall 3", "Ashlands_Ruins_Wall_Broken5_4x6"), E("Ashlands Ruin Wall Top", "Ashlands_Ruins_Wall_Top_wHole"), E("Ashlands Window Wall 1", "Ashlands_Ruins_Wall_Window_4x6_broken2"),
            E("Ashlands Window Wall 2", "Ashlands_Ruins_Wall_Window_4x6_broken3"), E("Ashlands Window Wall 3", "Ashlands_Ruins_Wall_Window_4x6_broken4"), E("Ashlands Window Wall 4", "Ashlands_Ruins_Wall_Window_4x6_broken5"),
            E("Ashlands Window Wall 5", "Ashlands_Ruins_Wall_Window_4x6_broken6"), E("Ashlands Broken Arch 1", "Ashlands_Arch2_Broken1"), E("Ashlands Broken Arch 2", "Ashlands_Arch2_Broken2"),
            E("Ashlands Damaged Arch Roof", "Ashlands_ArchRoofDamaged"), E("Ashlands Damaged Arch Roof Half 1", "Ashlands_ArchRoofDamaged_half1"), E("Ashlands Damaged Arch Roof Half 2", "Ashlands_ArchRoofDamaged_half2"),
            E("Ashlands Long Damaged Arch Roof", "Ashlands_ArchRoofLong_Damaged"), E("Ashlands Pillar Tip", "Ashlands_Pillar4_tip"), E("Ashlands Pillar Tip 2", "Ashlands_Pillar4_tip2"),
            E("Ashlands Pillar Tip 3", "Ashlands_Pillar4_tip3"), E("Ashlands Broken Pillar Tip 1", "Ashlands_Pillar4_tip_broken1"), E("Ashlands Broken Pillar Tip 2", "Ashlands_Pillar4_tip_broken2"),
            E("Ashlands Broken Pillar Tip 3", "Ashlands_Pillar4_tip2_broken1"), E("Ashlands Broken Pillar Tip 4", "Ashlands_Pillar4_tip2_broken2"), E("Ashlands Broken Pillar Tip 5", "Ashlands_Pillar4_tip3_broken1"),
            E("Ashlands Broken Pillar Tip 6", "Ashlands_Pillar4_tip3_broken2"), E("Ashlands Broken Pillar Tip 7", "Ashlands_Pillar4_tip3_broken3"), E("Ashlands Double Pillar Base", "Ashlands_PillarBase3_double"),
            E("Ashlands Great Pillar", "Ashlands_Boss_Pillar"), E("Ashlands Broken Great Pillar 1", "Ashlands_Boss_Pillar_Twist_broken1"), E("Ashlands Broken Great Pillar 2", "Ashlands_Boss_Pillar_Twist_broken2"),
            E("Ashlands Broken Great Pillar 3", "Ashlands_Boss_Pillar_Twist_broken3"), E("Ashlands Fortress Pillar Base", "Ashlands_Fortress_Wall_Pillar_base"), E("Ashlands Fortress Pillar Top", "Ashlands_Fortress_Wall_PillarTop"),
            E("Ashlands Fortress Pillar Top Stone", "Ashlands_Fortress_Wall_PillarTopStone"), E("Ashlands Fortress Spikes", "Ashlands_Fortress_Wall_Spikes")),

        new(BuildTool.Hammer, "Jotun Halls",
            E("Jotun Floor 2x2", "Morkhalla_Floor_2x2"), E("Rubble 1", "Morkhalla_Rubble1"), E("Rubble 2", "Morkhalla_Rubble2"),
            E("Rubble 3", "Morkhalla_Rubble3"), E("Rubble 4", "Morkhalla_Rubble4"), E("Jotun Banner 1", "Morkhalla_Banner1"),
            E("Jotun Banner 2", "Morkhalla_Banner2"), E("Jotun Bench", "Morkhalla_Bench"), E("Jotun Stool", "Morkhalla_Stool"),
            E("Jotun Table", "Morkhalla_Table"), E("Jotun Bedroll 1", "Morkhalla_Bedroll1"), E("Jotun Bedroll 2", "Morkhalla_Bedroll2"),
            E("Jotun Weapon Stand", "Morkhalla_WeaponStand"), E("Jotun Rug", "Morkhalla_Rug_middle"), E("Training Dummy 1", "Morkhalla_Trainingdummy1"),
            E("Training Dummy 2", "Morkhalla_Trainingdummy2"), E("Jotun Statue Arm (Left)", "Morkhalla_StatuePieceArmL"), E("Jotun Statue Arm (Right)", "Morkhalla_StatuePieceArmR"),
            E("Jotun Statue Face", "Morkhalla_StatuePieceFace"), E("Jotun Statue Feet", "Morkhalla_StatuePieceFeet"), E("Jotun Statue Horn", "Morkhalla_StatuePieceHorn"),
            E("Jotun Statue Horn 2", "Morkhalla_StatuePieceHorn2"), E("Jotun Statue Legs", "Morkhalla_StatuePieceLegs"), E("Jotun Statue Sword", "Morkhalla_StatuePieceSword"),
            E("Jotun Statue Torso", "Morkhalla_StatuePieceTorso"), E("Jotun Block", "Morkhalla_Block"), E("Jotun Floor 4x4", "Morkhalla_Floor_4x4"),
            E("Jotun Broken Floor 1", "Morkhalla_Floor_4x4_broken01"), E("Jotun Broken Floor 2", "Morkhalla_Floor_4x4_broken02"), E("Jotun Giant Stairs", "Morkhalla_Stairs_giant_short"),
            E("Jotun Broken Stairs 1", "Morkhalla_Stairs_giant_short_broken1"), E("Jotun Broken Stairs 2", "Morkhalla_Stairs_giant_short_broken2"), E("Jotun Stair Railing", "Morkhalla_Stairs_giant_railing"),
            E("Jotun Railing", "Morkhalla_giant_railing"), E("Jotun Railing Corner", "Morkhalla_giant_railing_corner"), E("Jotun Railing Half", "Morkhalla_giant_railing_half"),
            E("Jotun Railing Post", "Morkhalla_giant_railing_single"), E("Jotun Decorated Railing", "Morkhalla_giant_railing_deco"), E("Jotun Railing Torch", "Morkhalla_giant_railing_torch"),
            E("Jotun Gate", "Morkhalla_jotun_gate"), E("Morkborg Gate", "Morkborg_gate"), E("Jotun Bridge", "Morkhalla_bridge_davinci"),
            E("Jotun Metal Bar", "Morkhalla_MetalBar"), E("Jotun Wood Boards", "Morkhalla_WoodBoards"), E("Jotun Rug Corner", "Morkhalla_Rug_corner"),
            E("Jotun Rug End 1", "Morkhalla_Rug_end1"), E("Jotun Rug End 2", "Morkhalla_Rug_end2"), E("Jotun Stair Rug", "Morkhalla_Rug_stair"),
            E("Jotun Sword Statue", "Morkhalla_StatueSword"), E("Jotun Hanging Sword", "Morkhalla_StatueSword_hanging"), E("Jotun Rubble Pile", "Morkhalla_rubble_trashpile"),
            E("Big Jotun Statue Arm (Left)", "Morkhalla_StatuePieceArmL_big"), E("Big Jotun Statue Arm (Right)", "Morkhalla_StatuePieceArmR_big"), E("Big Jotun Statue Face", "Morkhalla_StatuePieceFace_big"),
            E("Big Jotun Statue Feet", "Morkhalla_StatuePieceFeet_big"), E("Big Jotun Statue Horn", "Morkhalla_StatuePieceHorn_big"), E("Big Jotun Statue Horn 2", "Morkhalla_StatuePieceHorn2_big"),
            E("Big Jotun Statue Legs", "Morkhalla_StatuePieceLegs_big"), E("Big Jotun Statue Sword", "Morkhalla_StatuePieceSword_big"), E("Big Jotun Statue Torso", "Morkhalla_StatuePieceTorso_big")),

        new(BuildTool.Hammer, "Dungeon Decor",
            E("Sunken Crypt Tower Wall", "SunkenKit_int_towerwall_LOD"), E("Stone Chest", "stonechest"), E("Horizontal Web", "horizontal_web"),
            E("Vertical Web", "vertical_web"), E("Web Tunnel", "tunnel_web"), E("Jotun Web Corner", "morkhalla_web_corner"),
            E("Jotun Web", "morkhalla_web_horisontal"), E("Jotun Web Tunnel", "morkhalla_web_tunnel"), E("Infested Hanging Egg 1", "CreepProp_egg_hanging01"),
            E("Infested Hanging Egg 2", "CreepProp_egg_hanging02"), E("Infested Hanging Growth", "CreepProp_hanging01"), E("Infested Wall Growth", "CreepProp_wall01"),
            E("Infested Entrance 1", "CreepProp_entrance1"), E("Infested Entrance 2", "CreepProp_entrance2"), E("Dvergr Town Beam", "dvergrtown_wood_beam"),
            E("Dvergr Town Pole", "dvergrtown_wood_pole"), E("Dvergr Town Stake", "dvergrtown_wood_stake"), E("Dvergr Town Stake Wall", "dvergrtown_wood_stakewall"),
            E("Dvergr Corner Stair", "dvergrtown_stair_corner_wood_left"), E("Dvergr Ashlands Crate", "dvergrprops_crate_ashlands"), E("Broken Dvergr Demister", "dverger_demister_broken"),
            E("Ruined Dvergr Demister", "dverger_demister_ruins"), E("Hanging Cloth Door", "cloth_hanging_door"), E("Hanging Cloth Double Door", "cloth_hanging_door_double"),
            E("Hanging Fenris Hide Door", "fenrirhide_hanging_door"), E("Wooden Path", "wooden_path"), E("Dragon Egg Cup", "dragoneggcup"),
            E("Trader Wagon", "trader_wagon_destructable"), E("Item Stand (Prop)", "prop_itemstand"), E("Darkwood Chair (Prop)", "prop_piece_chair03")),

        new(BuildTool.Hammer, "Dvergr",
            E("Dvergr Barrel", "dvergrprops_barrel"), E("Dvergr Bed", "dvergrprops_bed"), E("Dvergr Chair", "dvergrprops_chair"),
            E("Dvergr Stool", "dvergrprops_stool"), E("Dvergr Table", "dvergrprops_table"), E("Dvergr Shelf", "dvergrprops_shelf"),
            E("Dvergr Crate", "dvergrprops_crate"), E("Dvergr Long Crate", "dvergrprops_crate_long"),
            E("Dvergr Lantern", "dvergrprops_lantern"), E("Dvergr Standing Lantern", "dvergrprops_lantern_standing"),
            E("Dvergr Banner", "dvergrprops_banner"), E("Dvergr Curtain", "dvergrprops_curtain"),
            E("Dvergr Hook and Chain", "dvergrprops_hooknchain"), E("Dvergr Pickaxe", "dvergrprops_pickaxe"),
            E("Dvergr Beam", "dvergrprops_wood_beam"), E("Dvergr Pole", "dvergrprops_wood_pole"),
            E("Dvergr Stake", "dvergrprops_wood_stake"), E("Dvergr Stake Wall", "dvergrprops_wood_stakewall"),
            E("Dvergr Wall", "dvergrprops_wood_wall"), E("Dvergr Support", "dvergrtown_wood_support"),
            E("Dvergr Town Wall 1", "dvergrtown_wood_wall01"), E("Dvergr Town Wall 2", "dvergrtown_wood_wall02"),
            E("Dvergr Town Wall 3", "dvergrtown_wood_wall03"), E("Dvergr Crane", "dvergrtown_wood_crane")),

        new(BuildTool.Hammer, "Fuling Village",
            E("Fuling Banner", "goblin_banner"), E("Fuling Fence", "goblin_fence"), E("Fuling Pole", "goblin_pole"),
            E("Fuling Small Pole", "goblin_pole_small"), E("Fuling Roof", "goblin_roof_45d"),
            E("Fuling Roof Corner", "goblin_roof_45d_corner"), E("Fuling Roof Cap", "goblin_roof_cap"),
            E("Fuling Stairs", "goblin_stairs"), E("Fuling Ladder", "goblin_stepladder"),
            E("Fuling Wall 1m", "goblin_woodwall_1m"), E("Fuling Wall 2m", "goblin_woodwall_2m"),
            E("Fuling Rib Wall", "goblin_woodwall_2m_ribs"), E("Fuling Straw Pile", "goblin_strawpile"),
            E("Fuling Trash Pile", "goblin_trashpile")),

        new(BuildTool.Hammer, "Props",
            E("Barrel", "barrell"), E("Bucket", "bucket"), E("Cargo Crate", "CargoCrate"), E("Braided Box", "CastleKit_braided_box01"),
            E("Clay Pot", "CastleKit_pot03"), E("Wood Stack", "prop_wood_stack"), E("Tankard", "prop_Tankard"),
            E("Castle Brazier", "CastleKit_brazier"), E("Castle Torch", "CastleKit_groundtorch"),
            E("Castle Torch (Blue)", "CastleKit_groundtorch_blue"), E("Castle Torch (Green)", "CastleKit_groundtorch_green"),
            E("Mountain Brazier", "MountainKit_brazier"), E("Mountain Brazier (Blue)", "MountainKit_brazier_blue"),
            E("Mountain Brazier (Purple)", "MountainKit_brazier_purple"), E("Standing Lantern", "deepnorth_lantern_standing"),
            E("Mountain Chair", "mountainkit_chair"), E("Mountain Table", "mountainkit_table"),
            E("Hanging Cloth", "cloth_hanging_long"), E("Hanging Fenris Hide", "fenrirhide_hanging"),
            E("Bog Witch Deer Rug", "rug_bogwitch_deer"), E("Bog Witch Fur Rug", "rug_bogwitch_fur"),
            E("Bog Witch Wolf Rug", "rug_bogwitch_wolf"),
            E("Charred Banner 1", "CharredBanner1"), E("Charred Banner 2", "CharredBanner2"), E("Charred Banner 3", "CharredBanner3"),
            E("Ashlands Pot (Green)", "ashland_pot1_green"), E("Ashlands Pot (Red)", "ashland_pot1_red"),
            E("Ashlands Pot 2 (Green)", "ashland_pot2_green"), E("Ashlands Pot 2 (Red)", "ashland_pot2_red"),
            E("Ashlands Pot 3 (Green)", "ashland_pot3_green"), E("Ashlands Pot 3 (Red)", "ashland_pot3_red")),

        new(BuildTool.Hammer, "Workshop Props",
            E("Hearth (Prop)", "prop_hearth"), E("Cauldron (Prop)", "prop_piece_cauldron"),
            E("Mead Cauldron (Prop)", "prop_piece_MeadCauldron"), E("Spice Rack (Prop)", "prop_cauldron_ext1_spice"),
            E("Butcher Table (Prop)", "prop_cauldron_ext3_butchertable"),
            E("Mortar and Pestle (Prop)", "prop_cauldron_ext5_mortarandpestle"),
            E("Rolling Pins (Prop)", "prop_cauldron_ext6_rollingpins"), E("Prep Table (Prop)", "prop_preptable"),
            E("Forge Cooler (Prop)", "prop_forge_ext2"), E("Forge Toolrack (Prop)", "prop_forge_ext5"),
            E("Chopping Block (Prop)", "prop_piece_workbench_ext1"), E("Tanning Rack (Prop)", "prop_piece_workbench_ext2"),
            E("Adze (Prop)", "prop_piece_workbench_ext3"), E("Tool Shelf (Prop)", "prop_piece_workbench_ext4"),
            E("Runed Bench (Prop)", "prop_piece_bench_runed"), E("Floor Brazier (Prop)", "prop_piece_brazierfloor01"),
            E("Wardrobe (Prop)", "prop_chest_warderobe"), E("Bed (Prop)", "prop_bed02"), E("Ashwood Bed (Prop)", "prop_ashwood_bed"),
            E("Meadows Feast (Prop)", "prop_FeastMeadows"), E("Ashlands Feast (Prop)", "prop_FeastAshlands"),
            E("Deep North Feast (Prop)", "PropFeastDeepNorth"),
            E("Draugr Elite Trophy Stand", "prop_itemstand_TrophyDraugrElite"),
            E("Fuling Berserker Trophy Stand", "prop_itemstand_TrophyGoblinBrute"),
            E("Fuling Shaman Trophy Stand", "prop_itemstand_TrophyGoblinShaman"),
            E("Greydwarf Trophy Stand", "prop_itemstand_TrophyGreydwarf"),
            E("Greydwarf Brute Trophy Stand", "prop_itemstand_TrophyGreydwarfBrute"),
            E("Seeker Soldier Trophy Stand", "prop_itemstand_TrophySeekerBrute")),

        new(BuildTool.Hammer, "Shipwrecks",
            E("Karve Wreck Boards", "shipwreck_karve_bottomboards"), E("Karve Wreck Bow", "shipwreck_karve_bow"),
            E("Karve Wreck Dragonhead", "shipwreck_karve_dragonhead"), E("Karve Wreck Stern", "shipwreck_karve_stern"),
            E("Karve Wreck Sternpost", "shipwreck_karve_sternpost"), E("Longship Wreck Front", "shipwreck_vikingship_front"),
            E("Longship Wreck Frontpiece", "shipwreck_vikingship_frontpiece"), E("Longship Wreck Mast", "shipwreck_vikingship_mast1"),
            E("Longship Wreck Rear", "shipwreck_vikingship_rear"),
            E("Frozen Longship 1", "frozenship"), E("Frozen Longship 2", "frozenship02"), E("Frozen Longship 3", "frozenship03"),
            E("Ice-Bound Ship Part 1", "Ice_ship_1"), E("Ice-Bound Ship Part 2", "Ice_ship_2"), E("Ice-Bound Ship Part 3", "Ice_ship_3"),
            E("Ice-Bound Ship Part 4", "Ice_ship_4"), E("Ice-Bound Ship Part 5", "Ice_ship_5"), E("Ice-Bound Ship Part 6", "Ice_ship_6"),
            E("Ice-Bound Ship Part 7", "Ice_ship_7"))
    };
}
