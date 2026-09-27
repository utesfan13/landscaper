using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Added to every Landscaper clone. A placed piece's tint is saved in its ZDO, so it is kept with
/// the world and shared with other players; this applies it on every player's game and keeps it
/// applied.
/// </summary>
internal sealed class LandscaperTint : MonoBehaviour
{
    private const string ZdoKey = "LandscaperTint";
    private const float RecheckSeconds = 3f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private ZNetView? _view;
    private Vector3 _applied;

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

    /// <summary>The tint saved on a placed piece, or null if it has none.</summary>
    public static Color? Read(GameObject piece)
    {
        var view = piece.GetComponent<ZNetView>();
        if (view == null || !view.IsValid())
        {
            return null;
        }

        var tint = view.GetZDO().GetVec3(ZdoKey, Vector3.zero);
        return tint == Vector3.zero ? null : new Color(tint.x, tint.y, tint.z);
    }

    /// <summary>Applies the saved tint, if any.</summary>
    public void Apply()
    {
        if (_view == null || !_view.IsValid() || MaterialMan.instance is null)
        {
            return;
        }

        // No tint is stored as zero; a real tint always keeps some brightness.
        var tint = _view.GetZDO().GetVec3(ZdoKey, Vector3.zero);
        _applied = tint;
        if (tint != Vector3.zero)
        {
            MaterialMan.instance.SetValue(gameObject, ColorId, new Color(tint.x, tint.y, tint.z));
        }
    }

    private void Start()
    {
        _view = GetComponent<ZNetView>();
        if (_view == null || !_view.IsValid())
        {
            // Placement ghosts land here: their ZNetView is removed when they are created.
            return;
        }

        Apply();

        // Pick up a tint that arrives or changes after the piece was created on this player's game.
        InvokeRepeating(nameof(Recheck), UnityEngine.Random.Range(0.5f, RecheckSeconds), RecheckSeconds);
    }

    private void Recheck()
    {
        if (_view != null && _view.IsValid() && _view.GetZDO().GetVec3(ZdoKey, Vector3.zero) != _applied)
        {
            Apply();
        }
    }

    /// <summary>
    /// Valheim clears a piece's colour when its hover highlight ends (for building pieces with
    /// WearNTear) and other code may clear it too; put the tint straight back whenever that happens.
    /// </summary>
    [HarmonyPatch(typeof(MaterialMan), nameof(MaterialMan.ResetValue), typeof(GameObject), typeof(int))]
    private static class RestoreAfterResetPatch
    {
        private static void Postfix(GameObject go, int nameID)
        {
            if (nameID == ColorId && go != null && go.TryGetComponent<LandscaperTint>(out var tint))
            {
                tint.Apply();
            }
        }
    }
}
