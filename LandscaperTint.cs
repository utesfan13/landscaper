using UnityEngine;

namespace Landscaper;

/// <summary>
/// Added to every Landscaper clone. A placed piece's tint is saved in its ZDO, so it is kept with
/// the world and shared with other players; this reapplies it whenever the piece is loaded.
/// </summary>
internal sealed class LandscaperTint : MonoBehaviour
{
    private const string ZdoKey = "LandscaperTint";
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Saves a tint on a just-placed piece and applies it.</summary>
    public static void Save(GameObject piece, Color tint)
    {
        var view = piece.GetComponent<ZNetView>();
        if (view == null || !view.IsValid())
        {
            return;
        }

        view.GetZDO().Set(ZdoKey, new Vector3(tint.r, tint.g, tint.b));
        piece.GetComponent<LandscaperTint>()?.Apply();
    }

    /// <summary>Applies the saved tint, if any. Also used to restore it after the removal highlight.</summary>
    public void Apply()
    {
        var view = GetComponent<ZNetView>();
        if (view == null || !view.IsValid() || MaterialMan.instance is null)
        {
            // Placement ghosts land here too: their ZNetView is removed when they are created.
            return;
        }

        // No tint is stored as zero; a real tint always keeps some brightness.
        var tint = view.GetZDO().GetVec3(ZdoKey, Vector3.zero);
        if (tint != Vector3.zero)
        {
            MaterialMan.instance.SetValue(gameObject, ColorId, new Color(tint.x, tint.y, tint.z));
        }
    }

    private void Start() => Apply();
}
