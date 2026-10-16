using UnityEngine;

namespace Landscaper;

/// <summary>
/// Gives Landscaper's Hammer pieces snap points, so they snap to each other and to vanilla building
/// pieces the way vanilla pieces do: crates stack and line up, shelves meet edge to edge. The game
/// snaps by markers on a piece (direct children tagged "snappoint"); these go on the corners of the
/// box around the model, and the middle of its top and bottom. Natural objects (the Cultivator's and
/// Hoe's pieces) and statues don't get them, and pieces that already have snap points keep their own.
/// Holding the game's alternate place key (Shift by default) places without snapping, as in vanilla.
/// </summary>
internal static class SnapPoints
{
    private const string Tag = "snappoint";

    /// <summary>Hammer tabs whose pieces don't snap: shapes snapping wouldn't suit, or pieces placed on things.</summary>
    private static readonly HashSet<string> NoSnapCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Statues", "Copied"
    };

    /// <summary>Reads the Building.SnapPoints setting; applies when the pieces are registered (at startup).</summary>
    public static Func<bool> Enabled { get; set; } = () => true;

    public static bool Wanted(DecorativePieceDefinition definition) =>
        Enabled() && definition.Tool == BuildTool.Hammer && definition.DisplayItem is null && !NoSnapCategories.Contains(definition.Category);

    /// <summary>Adds the snap points, unless the piece has its own. Bounds are in the piece's own space.</summary>
    public static void Add(GameObject clone, Bounds bounds, DecorativePieceDefinition definition)
    {
        var root = clone.transform;
        for (var i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).CompareTag(Tag))
            {
                return;
            }
        }

        // Jotun statue parts fit together their own way (see StatueSnapping); snap points would compete.
        if (definition.PrefabName.StartsWith("Morkhalla_StatuePiece", StringComparison.OrdinalIgnoreCase) && definition.ExtraParts.Count == 0)
        {
            return;
        }

        var min = bounds.min;
        var max = bounds.max;
        foreach (var (y, height) in new[] { (min.y, "Bottom"), (max.y, "Top") })
        {
            Point($"{height} middle", new Vector3(bounds.center.x, y, bounds.center.z));
            foreach (var (z, depth) in new[] { (min.z, "back"), (max.z, "front") })
            {
                foreach (var (x, side) in new[] { (min.x, "left"), (max.x, "right") })
                {
                    Point($"{height} {depth} {side} corner", new Vector3(x, y, z));
                }
            }
        }

        // The name is what the game shows when choosing a snap point by hand.
        void Point(string name, Vector3 position)
        {
            var point = new GameObject(name) { tag = Tag };
            point.transform.SetParent(root, worldPositionStays: false);
            point.transform.localPosition = position;
        }
    }
}
