using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Jotun statue parts fit together on their own: the game models every part of a statue in one shared
/// space, so put together, every part sits at the same position, rotation and size (that's how the
/// whole statues are made; see DecorativePieceDefinition.ExtraParts). So while placing a statue part,
/// aiming at a placed part of a statue the same size (or a whole statue) puts the new part exactly
/// where that statue has it. Holding the game's alternate place key (Shift) places freely instead,
/// like vanilla snapping.
/// </summary>
internal static class StatueSnapping
{
    private const float MaxAimDistance = 50f;

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static class SnapPatch
    {
        /// <summary>Last, so it has the final say on where the ghost goes (after tilt and nudge).</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance, GameObject ___m_placementGhost, int ___m_placeRayMask)
        {
            var ghost = ___m_placementGhost;
            if (__instance != Player.m_localPlayer || ghost == null || !ghost.activeSelf || !Plugin.ModEnabled() ||
                Plugin.Pieces is not { } pieces || ZInput.GetButton("AltPlace") ||
                pieces.StatueSizeOf(ghost.name) is not { } size || AimedStatue(__instance, ___m_placeRayMask, pieces, size) is not { } target)
            {
                return;
            }

            ghost.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

            // The same size too, so the part fits a resized statue; this is also the size it's placed at.
            if (SavedPieces.PrefabOf(target.GetZDO()) is { } prefab)
            {
                var baseScale = prefab.transform.localScale;
                var scale = target.transform.localScale;
                ScaleController.MatchSize(new Vector3(
                    baseScale.x != 0f ? scale.x / baseScale.x : 1f,
                    baseScale.y != 0f ? scale.y / baseScale.y : 1f,
                    baseScale.z != 0f ? scale.z / baseScale.z : 1f));
            }
        }
    }

    /// <summary>The placed statue part (or whole statue) under the crosshair, if it's the same size of statue.</summary>
    private static ZNetView? AimedStatue(Player player, int rayMask, DecorativePieceManager pieces, bool big)
    {
        var camera = GameCamera.instance.transform;
        if (!Physics.Raycast(camera.position, camera.forward, out var hit, MaxAimDistance, rayMask) ||
            Vector3.Distance(hit.point, player.m_eye.position) >= player.m_maxPlaceDistance)
        {
            return null;
        }

        var view = hit.collider.GetComponentInParent<ZNetView>();
        return view != null && view.IsValid() && SavedPieces.PrefabOf(view.GetZDO()) is { } prefab && pieces.StatueSizeOf(prefab.name) == big
            ? view
            : null;
    }
}
