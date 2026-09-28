using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Added to every Landscaper clone. A placed piece's tint is saved in its ZDO, so it is kept with
/// the world and shared with other players; this applies it on every player's game and keeps it
/// applied. It also scales vegetation sway with the piece's size (see <see cref="VegetationSway"/>).
/// </summary>
internal sealed class LandscaperTint : MonoBehaviour
{
    private const string ZdoKey = "LandscaperTint";
    private const float RecheckSeconds = 3f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Placed pieces (not ghosts), checked a few at a time by <see cref="CheckSome"/>.</summary>
    private static readonly List<LandscaperTint> Placed = new();
    private static int _nextToCheck;
    private static float _checkBudget;

    private ZNetView? _view;
    private Vector3 _applied;
    private float _appliedHeightScale = 1f;
    private int _placedIndex = -1;
    private uint _seenRevision;
    private float _seenScaleY;

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

        if (ZNetScene.instance?.GetPrefab(_view.GetZDO().GetPrefab()) is { } prefab && prefab.transform.localScale.y > 0f)
        {
            _appliedHeightScale = transform.localScale.y / prefab.transform.localScale.y;
            VegetationSway.Apply(gameObject, prefab, _appliedHeightScale);
        }

        _seenRevision = _view.GetZDO().DataRevision;
        _seenScaleY = transform.localScale.y;
    }

    /// <summary>
    /// Rechecks some of the placed pieces, so that each one is checked about every few seconds
    /// however many there are. Call every frame.
    /// </summary>
    public static void CheckSome()
    {
        if (Placed.Count == 0)
        {
            return;
        }

        // Capped so a long frame (a loading hitch) doesn't leave a backlog.
        _checkBudget = Mathf.Min(_checkBudget + Placed.Count * Time.deltaTime / RecheckSeconds, Placed.Count);
        var count = (int)_checkBudget;
        _checkBudget -= count;
        for (var i = 0; i < count; i++)
        {
            if (_nextToCheck >= Placed.Count)
            {
                _nextToCheck = 0;
            }

            Placed[_nextToCheck++].Recheck();
        }
    }

    private float CurrentHeightScale()
    {
        var prefab = ZNetScene.instance?.GetPrefab(_view!.GetZDO().GetPrefab());
        return prefab != null && prefab.transform.localScale.y > 0f
            ? transform.localScale.y / prefab.transform.localScale.y
            : 1f;
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
        _placedIndex = Placed.Count;
        Placed.Add(this);
    }

    private void OnDestroy()
    {
        if (_placedIndex < 0)
        {
            return;
        }

        // Swap the last piece into this one's place, so removing is quick.
        var last = Placed[Placed.Count - 1];
        Placed[_placedIndex] = last;
        last._placedIndex = _placedIndex;
        Placed.RemoveAt(Placed.Count - 1);
        _placedIndex = -1;
    }

    private void Recheck()
    {
        if (_view == null || !_view.IsValid())
        {
            return;
        }

        // Nothing to do unless the piece's saved data or its size changed since the last look;
        // the ZDO's revision goes up whenever any of its data changes.
        var zdo = _view.GetZDO();
        if (zdo.DataRevision == _seenRevision && transform.localScale.y == _seenScaleY)
        {
            return;
        }

        _seenRevision = zdo.DataRevision;
        _seenScaleY = transform.localScale.y;
        if (zdo.GetVec3(ZdoKey, Vector3.zero) != _applied || Mathf.Abs(CurrentHeightScale() - _appliedHeightScale) > 0.001f)
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
