using System.Collections.ObjectModel;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

public sealed class DecorativePieceManager
{
    private readonly List<DecorativePieceDefinition> _registeredDefinitions = new();
    private readonly Dictionary<BuildTool, DecorativePieceTable> _pieceTables = new();

    private readonly List<DecorativePieceDefinition> _customDefinitions;

    public DecorativePieceManager(string customEntries)
    {
        _customDefinitions = ParseCustomEntries(customEntries);
        foreach (var tool in Enum.GetValues(typeof(BuildTool)).Cast<BuildTool>())
        {
            _pieceTables[tool] = new DecorativePieceTable { Name = tool.ToString() };
        }
    }

    public IReadOnlyDictionary<BuildTool, DecorativePieceTable> PieceTables => new ReadOnlyDictionary<BuildTool, DecorativePieceTable>(_pieceTables);
    public int RegisteredCount => _registeredDefinitions.Count;

    public void RefreshKnownPieces()
    {
        ValheimRuntimeBridge.RefreshKnownPieces();
    }

    public int TryRegisterBuiltInCatalog()
    {
        var newlyRegistered = 0;
        foreach (var definition in GetDefaultCatalog().Concat(OtherCatalog.Entries()).Concat(_customDefinitions))
        {
            if (_registeredDefinitions.Any(d => d.PrefabName == definition.PrefabName && d.Tool == definition.Tool))
            {
                continue;
            }

            if (TryRegisterDecorativePiece(definition))
            {
                newlyRegistered++;
            }
        }

        return newlyRegistered;
    }

    private static List<DecorativePieceDefinition> ParseCustomEntries(string customEntries)
    {
        var definitions = new List<DecorativePieceDefinition>();
        foreach (var line in customEntries.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('|');
            if (fields.Length < 4 || !Enum.TryParse(fields[2].Trim(), true, out BuildTool tool))
            {
                System.Console.WriteLine($"[Landscaper] Ignoring invalid custom entry: {line}");
                continue;
            }

            var rotation = Vector3.zero;
            if (fields.Length >= 5)
            {
                var values = fields[4].Split(',');
                if (values.Length == 3 &&
                    float.TryParse(values[0].Trim(), out var x) &&
                    float.TryParse(values[1].Trim(), out var y) &&
                    float.TryParse(values[2].Trim(), out var z))
                {
                    rotation = new Vector3(x, y, z);
                }
            }

            definitions.Add(new DecorativePieceDefinition
            {
                DisplayName = fields[0].Trim(),
                PrefabName = fields[1].Trim(),
                Tool = tool,
                Category = fields[3].Trim(),
                PlacementRotation = rotation
            });
        }

        return definitions;
    }

    public bool TryRegisterDecorativePiece(DecorativePieceDefinition definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (!definition.Enabled)
        {
            System.Console.WriteLine($"[Landscaper] Disabled: {definition.PrefabName}");
            return false;
        }

        if (!PrefabUtilities.TryResolvePrefab(definition.PrefabName, out var prefab))
        {
            System.Console.WriteLine($"[Landscaper] Valheim prefab '{definition.PrefabName}' was not found for {definition.DisplayName}.");
            return false;
        }

        var table = _pieceTables[definition.Tool];
        var piece = new DecorativeBuildPiece
        {
            Name = definition.DisplayName,
            Identifier = definition.PrefabName,
            Tool = definition.Tool,
            Category = definition.Category,
            Buildable = true,
            AllowGround = definition.AllowGround,
            AllowWater = definition.AllowWater,
            AllowInterior = definition.AllowInterior,
            RequireWorkbench = definition.RequireWorkbench,
            PlacementScale = definition.PlacementScale,
            Prefab = prefab
        };

        UnityEngine.GameObject? runtimePrefab = null;
        if (!string.IsNullOrWhiteSpace(prefab.PrefabName))
        {
            ValheimRuntimeBridge.TryResolvePrefabName(prefab.PrefabName, out runtimePrefab);
        }

        if (runtimePrefab is null)
        {
            System.Console.WriteLine($"[Landscaper] Runtime prefab '{prefab.PrefabName}' was not found.");
        }

        var runtimeRegistered = runtimePrefab is not null && ValheimRuntimeBridge.TryRegisterWithJotunn(runtimePrefab, definition.DisplayName, definition.Category, definition.Tool, definition.PlacementRotation);

        if (runtimeRegistered)
        {
            table.AddPiece(piece);
            prefab.EnsureBuildComponents();
            prefab.Piece ??= piece;
            _registeredDefinitions.Add(definition);
            System.Console.WriteLine($"[Landscaper] Queued '{definition.DisplayName}' through Jotunn for {definition.Tool} / {definition.Category}.");
            return true;
        }

        System.Console.WriteLine($"[Landscaper] Could not register '{definition.DisplayName}' in {definition.Tool} / {definition.Category}.");
        return false;
    }

    public IEnumerable<string> DumpPrefabs(string keyword = "")
    {
        var results = PrefabUtilities.GetCandidatePrefabs(keyword).ToList();

        foreach (var result in results)
        {
            System.Console.WriteLine($"[Landscaper] Prefab: {result}");
        }

        return results;
    }

    public static List<DecorativePieceDefinition> GetDefaultCatalog()
    {
        return new List<DecorativePieceDefinition>
        {
            new()
            {
                PrefabName = "RaspberryBush",
                DisplayName = "Raspberry Bush",
                Description = "A berry-bearing shrub that fits a cultivated garden.",
                Tool = BuildTool.Cultivator,
                Category = "Plants",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "BlueberryBush",
                DisplayName = "Blueberry Bush",
                Description = "A bush that suits hedges and berry patches.",
                Tool = BuildTool.Cultivator,
                Category = "Plants",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Pickable_Dandelion",
                DisplayName = "Dandelion",
                Description = "A small floral accent for gardens and paths.",
                Tool = BuildTool.Cultivator,
                Category = "Plants",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementRotation = Vector3.zero,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_1",
                DisplayName = "Small Rock",
                Description = "A small natural rock used for landscaping accents.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_2",
                DisplayName = "Rock 2",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_3",
                DisplayName = "Rock 3",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_4",
                DisplayName = "Rock 4",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_5",
                DisplayName = "Rock 5",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_6",
                DisplayName = "Rock 6",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_7",
                DisplayName = "Rock 7",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Rock_8",
                DisplayName = "Rock 8",
                Description = "A natural rock for landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Beech1",
                DisplayName = "Beech Tree",
                Description = "A beech tree for woodland landscaping.",
                Tool = BuildTool.Cultivator,
                Category = "Trees",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Birch1_aut",
                DisplayName = "Birch Tree",
                Description = "A birch tree for woodland landscaping.",
                Tool = BuildTool.Cultivator,
                Category = "Trees",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Oak1",
                DisplayName = "Oak Tree",
                Description = "An oak tree for woodland landscaping.",
                Tool = BuildTool.Cultivator,
                Category = "Trees",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "PineTree",
                DisplayName = "Pine Tree",
                Description = "A pine tree for woodland landscaping.",
                Tool = BuildTool.Cultivator,
                Category = "Trees",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "FirTree",
                DisplayName = "Fir Tree",
                Description = "A fir tree for woodland landscaping.",
                Tool = BuildTool.Cultivator,
                Category = "Trees",
                Enabled = true,
                AllowGround = true,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Boulder",
                DisplayName = "Boulder",
                Description = "A large stone centerpiece for rocky landscaping.",
                Tool = BuildTool.Hoe,
                Category = "Rocks",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "TreeStump",
                DisplayName = "Tree Stump",
                Description = "A stump that feels right in a woodland or garden border.",
                Tool = BuildTool.Hoe,
                Category = "Natural Props",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "Statue01",
                DisplayName = "Stone Statue",
                Description = "A decorative statue that fits shrines and courtyards.",
                Tool = BuildTool.Hammer,
                Category = "Statues",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            },
            new()
            {
                PrefabName = "StoneArch",
                DisplayName = "Stone Arch",
                Description = "A decorative arch piece for entrances and ornamental gardens.",
                Tool = BuildTool.Hammer,
                Category = "Garden Decor",
                Enabled = true,
                AllowGround = true,
                AllowWater = false,
                AllowInterior = true,
                PlacementScale = 1f
            }
        };
    }
}
