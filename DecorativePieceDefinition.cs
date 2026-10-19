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

    /// <summary>Placed on the water surface, like docks, rather than on the ground.</summary>
    public bool OnWater { get; set; }

    /// <summary>Prefab name of a piece in the same menu to list this one right after, if any.</summary>
    public string? ListAfter { get; set; }

    /// <summary>Prefab name of a vanilla piece whose chest, chair or bed function this piece copies.</summary>
    public string? FunctionFrom { get; set; }

    /// <summary>For a display piece (see ItemDisplays): the item it shows on a hidden item stand.</summary>
    public string? DisplayItem { get; set; }

    /// <summary>
    /// Other prefabs whose models are added to this one, at the same spot: a whole Jotun statue is its
    /// parts, which the game models in one shared space. Saved as the main prefab.
    /// </summary>
    public IReadOnlyList<string> ExtraParts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A second piece from the same prefab on the same tool, kept apart by this in its saved name, such
    /// as the openable copy of a decorative gate.
    /// </summary>
    public string? Variant { get; set; }

    /// <summary>The "White" variant: its red fabric made white, so a tint can make it any colour (see FabricWhitening).</summary>
    public bool WhiteFabric => Variant == PieceCatalog.WhiteVariant;

    /// <summary>Lifts like a portcullis when used (see LiftGates), whatever it's made from.</summary>
    public bool LiftGate { get; set; }

    /// <summary>
    /// Names this piece was saved under before (full "Landscaper_..." names), so pieces placed under an
    /// old name keep loading after a rename. Other tools' names are covered automatically.
    /// </summary>
    public IReadOnlyList<string> FormerNames { get; set; } = Array.Empty<string>();

    /// <summary>Items consumed when placing. Empty means the automatic cost (see BuildCosts).</summary>
    public List<PieceRequirement> Requirements { get; } = new();
}
