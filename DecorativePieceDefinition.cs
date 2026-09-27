using System.Collections.ObjectModel;
using UnityEngine;

namespace Landscaper;

public enum BuildTool
{
    Cultivator,
    Hoe,
    Hammer
}

public sealed class PieceRequirement
{
    public string MaterialName { get; set; } = string.Empty;
    public int Amount { get; set; } = 1;
}

public sealed class DecorativePieceDefinition
{
    public string PrefabName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BuildTool Tool { get; set; }
    public string Category { get; set; } = "Miscellaneous";
    public bool Enabled { get; set; } = true;
    public bool AllowGround { get; set; } = true;
    public bool AllowWater { get; set; } = false;
    public bool AllowInterior { get; set; } = true;
    public bool RequireWorkbench { get; set; } = false;
    public float PlacementScale { get; set; } = 1f;
    public Vector3 PlacementRotation { get; set; } = Vector3.zero;
    public bool FreePlacement { get; set; } = true;
    public IList<PieceRequirement> Requirements { get; } = new List<PieceRequirement>();
}

public sealed class DecorativeBuildPiece
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public BuildTool Tool { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool Buildable { get; set; } = true;
    public bool AllowGround { get; set; } = true;
    public bool AllowWater { get; set; } = false;
    public bool AllowInterior { get; set; } = true;
    public bool RequireWorkbench { get; set; } = false;
    public float PlacementScale { get; set; } = 1f;
    public PrefabRecord Prefab { get; set; } = null!;
}

public sealed class DecorativePieceTable
{
    public string Name { get; set; } = string.Empty;
    public List<DecorativeBuildPiece> Pieces { get; } = new();

    public void AddPiece(DecorativeBuildPiece piece)
    {
        if (piece is null)
        {
            throw new ArgumentNullException(nameof(piece));
        }

        Pieces.Add(piece);
    }
}

public sealed class NetworkViewInfo
{
    public string? ViewId { get; set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(ViewId);
}

public sealed class PrefabRecord
{
    public string Name { get; set; } = string.Empty;
    public string PrefabName { get; set; } = string.Empty;
    public bool ActiveSelf { get; set; } = true;
    public DecorativeBuildPiece? Piece { get; set; }
    public NetworkViewInfo? ZNetView { get; set; }

    public void EnsureBuildComponents()
    {
        Piece ??= new DecorativeBuildPiece
        {
            Name = Name,
            Identifier = PrefabName,
            Buildable = true,
            Category = "Decorative"
        };

        ZNetView ??= new NetworkViewInfo { ViewId = Guid.NewGuid().ToString("N") };
    }
}
