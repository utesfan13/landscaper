using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Placed Landscaper pieces are saved under the name of the game object they're made from (a placed
/// Landscaper beech is saved as "Beech1"), with a tag naming the Landscaper piece. With Landscaper
/// installed, the tag makes it load as the Landscaper piece; without it, the game loads the original
/// object in the same place instead of deleting it as an unknown object. Everything Landscaper saves
/// on the object (size, tint, the tag) is kept by the game, so it all comes back on reinstalling.
/// Pieces made from something that isn't a saved game object (cattails and lily pads, built from
/// ground clutter) keep their Landscaper name, since the game has nothing to load them as.
/// </summary>
internal static class SavedPieces
{
    /// <summary>Saved on the object: the name hash of the Landscaper piece it is.</summary>
    private static readonly int PieceTag = "LandscaperPiece".GetStableHashCode();

    /// <summary>
    /// The prefab an object is: its Landscaper piece if it's tagged, otherwise the prefab it's saved
    /// as. Use this rather than the saved prefab, which is the game object for Landscaper pieces.
    /// </summary>
    public static GameObject? PrefabOf(ZDO? zdo)
    {
        var scene = ZNetScene.instance;
        if (zdo == null || scene == null)
        {
            return null;
        }

        var tag = zdo.GetInt(PieceTag);
        return (tag != 0 ? scene.GetPrefab(tag) : null) ?? scene.GetPrefab(zdo.GetPrefab());
    }

    /// <summary>
    /// Saves a placed Landscaper piece under its game object's name, with the tag. Only the owner
    /// changes saved data, so each piece is converted the first time its owner loads it, including
    /// pieces placed before this was added.
    /// </summary>
    public static void SaveUnderGameName(ZNetView view)
    {
        if (!view.IsValid() || !view.IsOwner() || Plugin.Pieces is not { } pieces || ZNetScene.instance is not { } scene)
        {
            return;
        }

        var zdo = view.GetZDO();
        var piece = PrefabOf(zdo);
        if (piece == null || !PlacementInput.IsLandscaperPiece(piece) ||
            pieces.SourcePrefabOf(piece.name) is not { } source || source.StartsWith(ClutterPrefabs.Prefix, StringComparison.Ordinal) ||
            scene.GetPrefab(source) is not { } gameObject || PlacementInput.IsLandscaperPiece(gameObject))
        {
            return;
        }

        var pieceHash = piece.name.GetStableHashCode();
        if (zdo.GetInt(PieceTag) != pieceHash)
        {
            zdo.Set(PieceTag, pieceHash);
        }

        var gameHash = gameObject.name.GetStableHashCode();
        if (zdo.GetPrefab() != gameHash)
        {
            zdo.SetPrefab(gameHash);
        }
    }

    /// <summary>
    /// Loads a tagged object as its Landscaper piece: the same as the game's CreateObject, with the
    /// Landscaper piece in place of the saved prefab. Untagged objects, and tagged ones whose piece
    /// isn't registered any more, load as normal.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    private static class LoadAsLandscaperPiecePatch
    {
        private static bool Prefix(ZNetScene __instance, ZDO zdo, ref GameObject? __result)
        {
            var tag = zdo.GetInt(PieceTag);
            var piece = tag != 0 ? __instance.GetPrefab(tag) : null;
            if (piece == null)
            {
                return true;
            }

            ZNetView.m_useInitZDO = true;
            ZNetView.m_initZDO = zdo;
            __result = UnityEngine.Object.Instantiate(piece, zdo.GetPosition(), zdo.GetRotation());
            if (ZNetView.m_initZDO != null)
            {
                Plugin.Log.LogWarning($"Saved object {zdo.m_uid} wasn't used when loading {piece.name}.");
                ZNetView.m_initZDO = null;
            }

            ZNetView.m_useInitZDO = false;
            return false;
        }
    }
}
