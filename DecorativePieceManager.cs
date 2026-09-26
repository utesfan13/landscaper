using System.Collections.ObjectModel;
using UnityEngine;

namespace Landscaper;

public sealed class DecorativePieceManager
{
    private readonly List<DecorativePieceDefinition> _registeredDefinitions = new();
    private readonly Dictionary<BuildTool, PieceTable> _pieceTables = new();

    public DecorativePieceManager()
    {
        foreach (var tool in Enum.GetValues(typeof(BuildTool)).Cast<BuildTool>())
        {
            _pieceTables[tool] = new PieceTable { Name = tool.ToString() };
        }
    }

    public IReadOnlyDictionary<BuildTool, PieceTable> PieceTables => new ReadOnlyDictionary<BuildTool, PieceTable>(_pieceTables);
    public int RegisteredCount => _registeredDefinitions.Count;

    public void RegisterBuiltInCatalog()
    {
        foreach (var definition in GetDefaultCatalog())
        {
            RegisterDecorativePiece(definition);
        }
    }

    public void RegisterDecorativePiece(DecorativePieceDefinition definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (!definition.Enabled)
        {
            System.Console.WriteLine($"[Landscaper] Disabled: {definition.PrefabName}");
            return;
        }

        if (!PrefabUtilities.TryResolvePrefab(definition.PrefabName, out var prefab))
        {
            System.Console.WriteLine($"[Landscaper] Missing prefab '{definition.PrefabName}' for {definition.DisplayName}.");
            return;
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

        table.AddPiece(piece);
        prefab.EnsureBuildComponents();
        prefab.Piece ??= piece;

        UnityEngine.GameObject? runtimePrefab = null;
        if (!string.IsNullOrWhiteSpace(prefab.PrefabName))
        {
            ValheimRuntimeBridge.TryResolvePrefabName(prefab.PrefabName, out runtimePrefab);
        }

        var runtimeRegistered = runtimePrefab is not null && ValheimRuntimeBridge.TryRegisterBuildPiece(runtimePrefab, definition.DisplayName, definition.Category, definition.Tool);

        if (runtimeRegistered)
        {
            System.Console.WriteLine($"[Landscaper] Runtime-registered '{definition.DisplayName}' into the live build collection.");
        }

        _registeredDefinitions.Add(definition);

        System.Console.WriteLine($"[Landscaper] Registered '{definition.DisplayName}' in {definition.Tool} / {definition.Category}");
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
                PrefabName = "Dandelion",
                DisplayName = "Dandelion",
                Description = "A small floral accent for gardens and paths.",
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
