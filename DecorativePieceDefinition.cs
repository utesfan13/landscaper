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
    public string ItemName { get; set; } = string.Empty;
    public int Amount { get; set; } = 1;
}

public sealed class DecorativePieceDefinition
{
    public string PrefabName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BuildTool Tool { get; set; }
    public string Category { get; set; } = "Miscellaneous";
    public Vector3 PlacementRotation { get; set; } = Vector3.zero;

    /// <summary>Multiplies the prefab's own scale on each axis.</summary>
    public Vector3 Scale { get; set; } = Vector3.one;

    /// <summary>Items consumed when placing. Empty means the piece is free.</summary>
    public List<PieceRequirement> Requirements { get; } = new();
}
