using UnityEngine;

namespace Landscaper;

/// <summary>
/// Marks Landscaper's own pieces (the trees, rocks, props and other clones it registers), as opposed
/// to vanilla build pieces, which Landscaper also makes adjustable (see LandscaperTint). Rules meant
/// for decorations, such as removal with the Cultivator and Hoe, landscaper_remove, break refunds
/// and DecorativeOnly, check for this.
/// </summary>
public sealed class LandscaperPiece : MonoBehaviour
{
}
