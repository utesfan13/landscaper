using UnityEngine;

namespace Landscaper;

public static class OtherCatalog
{
    public static IEnumerable<DecorativePieceDefinition> Entries()
    {
        foreach (var entry in ResourceNodes)
        {
            var tool = entry.Prefab.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.IndexOf("Beech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.IndexOf("Fir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Prefab.StartsWith("Pickable_", StringComparison.OrdinalIgnoreCase)
                ? BuildTool.Cultivator
                : BuildTool.Hoe;
            yield return Create(entry, tool, "Resource Nodes");
        }

        foreach (var entry in Furniture)
        {
            yield return Create(entry, BuildTool.Hammer, "Furniture");
        }

        foreach (var entry in BuildingStructures)
        {
            yield return Create(entry, BuildTool.Hammer, "Building Structures");
        }
    }

    private static DecorativePieceDefinition Create((string Name, string Prefab) entry, BuildTool tool, string category)
    {
        return new DecorativePieceDefinition
        {
            DisplayName = entry.Name,
            PrefabName = entry.Prefab,
            Tool = tool,
            Category = category,
            PlacementRotation = Vector3.zero
        };
    }

    private static readonly (string Name, string Prefab)[] ResourceNodes =
    {
        ("Beech Tree", "Beech1"), ("Beech Stump", "Beech_Stub"),
        ("Small Beech Tree 1", "Beech_small1"), ("Small Beech Tree 2", "Beech_small2"),
        ("Birch Tree 1", "Birch1_aut"), ("Birch Tree 2", "Birch2_aut"), ("Birch Stump", "BirchStub"),
        ("Downed Birch", "Birch_log"), ("Downed Birch Half", "Birch_log_half"),
        ("Fir Tree", "FirTree"), ("Fir Stump", "FirTree_Stub"), ("Downed Fir", "FirTree_log"),
        ("Downed Fir Half", "FirTree_log_half"), ("Old Fir Log", "FirTree_oldLog"),
        ("Small Fir", "FirTree_small"), ("Dead Small Fir", "FirTree_small_dead"),
        ("Oak Tree", "Oak1"), ("Oak Stump", "OakStub"), ("Downed Oak", "Oak_log"),
        ("Downed Oak Half", "Oak_log_half"), ("Pine Tree", "Pinetree_01"),
        ("Pine Stump", "Pinetree_01_Stub"), ("Downed Pine", "PineTree_log"),
        ("Downed Pine Half", "PineTree_log_half"), ("Old Pine Log", "PineTree_logOLD"),
        ("Rock 3", "Rock_3"), ("Rock 4", "Rock_4"), ("Rock 4 Plains", "Rock_4_plains"),
        ("Rock 7", "Rock_7"), ("Mountain Boulder", "rock1_mountain"),
        ("Plain Boulder", "rock2_heath"), ("Mountain Boulder 2", "rock2_mountain"),
        ("Mountain Boulder 3", "rock3_mountain"), ("Silver Boulder", "rock3_silver"),
        ("Coastal Boulder", "rock4_coast"), ("Copper Boulder", "rock4_copper"),
        ("Forest Boulder", "rock4_forest"), ("Plain Boulder 2", "rock4_heath"),
        ("Silver Vein", "silvervein"), ("Tall Rock", "highstone"), ("Wide Stone", "widestone"),
        ("Stone Mine Rock", "MineRock_Stone"), ("Copper Mine Rock", "MineRock_Copper"),
        ("Iron Mine Rock", "MineRock_Iron"), ("Tin Mine Rock", "MineRock_Tin"),
        ("Obsidian Mine Rock", "MineRock_Obsidian"), ("Meteorite", "MineRock_Meteorite"),
        ("Muddy Scrap Pile", "mudpile"), ("Large Muddy Scrap Pile", "mudpile2"),
        ("Guck Sack", "GuckSack"), ("Small Guck Sack", "GuckSack_small"),
        ("Pickable Stone", "Pickable_Stone"), ("Pickable Rock", "Pickable_StoneRock"),
        ("Pickable Sulfur Rock", "Pickable_SulfurRock"), ("Pickable Thistle", "Pickable_Thistle")
    };

    private static readonly (string Name, string Prefab)[] Furniture =
    {
        ("Armor Stand", "ArmorStand"), ("Chest", "Chest"), ("Bed", "bed"),
        ("Iron Gate", "iron_gate"), ("Item Stand", "itemstand"), ("Horizontal Item Stand", "itemstandh"),
        ("Black Banner", "piece_banner01"), ("Blue Banner", "piece_banner02"),
        ("White and Red Banner", "piece_banner03"), ("Red Banner", "piece_banner04"),
        ("Green Banner", "piece_banner05"), ("Yellow Banner", "piece_banner08"),
        ("Purple Banner", "piece_banner09"), ("Orange Banner", "piece_banner10"),
        ("White Banner", "piece_banner11"), ("Hot Tub", "piece_bathtub"),
        ("Dragon Bed", "piece_bed02"), ("Bench", "piece_bench01"),
        ("Stool", "piece_chair"), ("Chair", "piece_chair02"), ("Darkwood Chair", "piece_chair03"),
        ("Reinforced Chest", "piece_chest"), ("Personal Chest", "piece_chest_private"),
        ("Wood Chest", "piece_chest_wood"), ("Standing Torch", "piece_groundtorch"),
        ("Sitting Log", "piece_logbench01"), ("Table", "piece_table"),
        ("Oak Table", "piece_table_oak"), ("Raven Throne", "piece_throne01"),
        ("Stone Throne", "piece_throne02"), ("Sconce", "piece_walltorch"),
        ("Deer Rug", "rug_deer"), ("Lox Rug", "rug_fur"), ("Wolf Rug", "rug_wolf"),
        ("Sign", "sign"), ("Coin Pile", "treasure_pile"), ("Coin Stack", "treasure_stack")
    };

    private static readonly (string Name, string Prefab)[] BuildingStructures =
    {
        ("Crystal Wall", "crystal_wall_1x1"), ("Darkwood Arch", "darkwood_arch"),
        ("Darkwood Beam", "darkwood_beam"), ("Darkwood Gate", "darkwood_gate"),
        ("Darkwood Pole", "darkwood_pole"), ("Darkwood Roof", "darkwood_roof"),
        ("Iron Floor", "iron_floor_1x1"), ("Iron Floor 2x2", "iron_floor_2x2"),
        ("Iron Wall", "iron_wall_1x1"), ("Iron Wall 2x2", "iron_wall_2x2"),
        ("Stake Wall", "stake_wall"), ("Stone Arch", "stone_arch"),
        ("Stone Floor 2x2", "stone_floor_2x2"), ("Stone Pillar", "stone_pillar"),
        ("Stone Stair", "stone_stair"), ("Stone Wall 1x1", "stone_wall_1x1"),
        ("Stone Wall 2x1", "stone_wall_2x1"), ("Stone Wall 4x2", "stone_wall_4x2"),
        ("Wood Beam", "wood_beam"), ("Wood Beam 1m", "wood_beam_1"),
        ("Wood Beam 26", "wood_beam_26"), ("Wood Beam 45", "wood_beam_45"),
        ("Wood Door", "wood_door"), ("Wood Fence", "wood_fence"),
        ("Wood Floor", "wood_floor"), ("Wood Floor 1x1", "wood_floor_1x1"),
        ("Wood Gate", "wood_gate"), ("Wood Pole", "wood_pole"),
        ("Wood Pole 2m", "wood_pole2"), ("Wood Stair", "wood_stair"),
        ("Wood Ladder", "wood_stepladder"), ("Wood Wall Half", "wood_wall_half"),
        ("Wood Wall", "woodwall"), ("Wood Roof", "wood_roof"),
        ("Wood Roof 45", "wood_roof_45"), ("Wood Window", "wood_window"),
        ("Wood Iron Beam", "woodiron_beam"), ("Wood Iron Pole", "woodiron_pole")
    };
}
