using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Item displays on walls: every display also has a version on the game's upright item stand, kept
/// out of the menus. While placing a display, aiming at a surface 45° or steeper (a wall, a post)
/// swaps it for that version, facing out from the surface (turn it with the usual rotate keys);
/// anything flatter takes the flat one, as on a table. The game builds the ghost and places the
/// piece from the build menu's selected piece, so that's where the swap is made.
/// </summary>
internal static class WallDisplays
{
    public const string FlatPrefix = "Landscaper_Display_";
    public const string WallPrefix = "Landscaper_DisplayWall_";

    /// <summary>How far the upright stand's back sits in front of its middle.</summary>
    private const float BackDepth = 0.045f;

    private static readonly MethodInfo PieceRayTest = AccessTools.Method(typeof(Player), "PieceRayTest");
    private static readonly MethodInfo SetupPlacementGhost = AccessTools.Method(typeof(Player), "SetupPlacementGhost");

    private static bool _onWall;
    private static Vector3 _point;
    private static Vector3 _normal;

    private static bool IsDisplay(GameObject? piece) =>
        piece != null && (piece.name.StartsWith(FlatPrefix, StringComparison.Ordinal) || piece.name.StartsWith(WallPrefix, StringComparison.Ordinal));

    private static bool IsWall(GameObject piece) => piece.name.StartsWith(WallPrefix, StringComparison.Ordinal);

    /// <summary>The wall version of a flat display, while aiming at a wall; otherwise the piece as it is.</summary>
    private static GameObject? Swap(GameObject? prefab) =>
        _onWall && prefab != null && prefab.name.StartsWith(FlatPrefix, StringComparison.Ordinal) && Plugin.Pieces?.Clone(WallPrefix + prefab.name.Substring(FlatPrefix.Length)) is { } wall
            ? wall
            : prefab;

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static class GhostPatch
    {
        /// <summary>Works out whether a display is aimed at a wall, and rebuilds the ghost when that changes.</summary>
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, GameObject ___m_placementGhost)
        {
            if (__instance != Player.m_localPlayer || !IsDisplay(___m_placementGhost) || !Plugin.ModEnabled())
            {
                _onWall = false;
                return;
            }

            var args = new object?[] { null, null, null, null, null, false };
            var hit = (bool)PieceRayTest.Invoke(__instance, args);
            var onWall = false;
            if (hit)
            {
                _point = (Vector3)args[0]!;
                _normal = (Vector3)args[1]!;
                var angle = Vector3.Angle(_normal, Vector3.up);
                onWall = angle >= 45f && angle <= 135f;
            }

            _onWall = onWall;
            if (onWall != IsWall(___m_placementGhost))
            {
                SetupPlacementGhost.Invoke(__instance, null);
            }
        }

        /// <summary>Puts a wall display's ghost flat against the wall, facing out.</summary>
        [HarmonyPriority(Priority.High)]
        private static void Postfix(Player __instance, GameObject ___m_placementGhost, int ___m_placeRotation, float ___m_placeRotationDegrees)
        {
            var ghost = ___m_placementGhost;
            if (!_onWall || __instance != Player.m_localPlayer || ghost == null || !IsWall(ghost))
            {
                return;
            }

            var rotation = Quaternion.LookRotation(_normal, Vector3.up) * Quaternion.Euler(0f, 0f, ___m_placeRotationDegrees * ___m_placeRotation);
            ghost.transform.SetPositionAndRotation(_point + _normal * BackDepth, rotation);
        }
    }

    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetSelectedPrefab))]
    private static class SelectedPrefabPatch
    {
        private static void Postfix(ref GameObject __result) => __result = Swap(__result)!;
    }

    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetSelectedPiece))]
    private static class SelectedPiecePatch
    {
        private static void Postfix(ref Piece __result)
        {
            if (__result != null && Swap(__result.gameObject) is { } swapped && swapped != __result.gameObject)
            {
                __result = swapped.GetComponent<Piece>();
            }
        }
    }
}
