using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Lets any piece, vanilla build pieces included, be placed where Valheim would normally refuse:
/// clipping into other pieces, on unsupported spots, in the wrong biome, inside dungeons, off the water, on steep
/// ground and so on. Three things still block placing: overlapping a player or creature, another
/// player's ward, and aiming at nothing at all. Hoe and Cultivator pieces can also be placed on
/// building pieces, rocks and other objects, not only on the ground.
/// </summary>
internal static class PlacementRules
{
    private static readonly Func<Player, bool> OverlapsCharacter =
        AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "CheckPlacementGhostVSPlayers"));

    private delegate bool RayTest(Player player, out Vector3 point, out Vector3 normal, out Piece piece, out Heightmap heightmap,
        out Collider waterSurface, bool water);

    private static readonly RayTest PieceRayTest =
        AccessTools.MethodDelegate<RayTest>(AccessTools.Method(typeof(Player), "PieceRayTest"));

    private static readonly Action<Player, bool> SetGhostValid =
        AccessTools.MethodDelegate<Action<Player, bool>>(AccessTools.Method(typeof(Player), "SetPlacementGhostValid"));

    /// <summary>Reads whether the placement rules are relaxed.</summary>
    public static Func<bool> IgnoreRules { get; set; } = () => true;

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static class RelaxPlacementPatch
    {
        /// <summary>
        /// Valheim hides the ghost of a ground piece (every Hoe and Cultivator piece) when you aim at
        /// anything but the terrain. For that frame, make it a piece placed exactly where you aim
        /// instead, which is where a ground piece goes on the terrain too. Restored in the postfix.
        /// </summary>
        private static void Prefix(Player __instance, GameObject ___m_placementGhost, out Piece? __state)
        {
            __state = null;
            if (___m_placementGhost == null || !IgnoreRules() || !PlacementInput.IsLandscaperPiece(___m_placementGhost) ||
                ___m_placementGhost.GetComponent<Piece>() is not { m_groundPiece: true, m_waterPiece: false, m_clipEverything: false } piece)
            {
                return;
            }

            var water = piece.m_waterPiece || piece.m_noInWater;
            if (PieceRayTest(__instance, out _, out _, out _, out var heightmap, out _, water) && heightmap == null)
            {
                piece.m_groundPiece = false;
                piece.m_clipEverything = true;
                __state = piece;
            }
        }

        /// <summary>Runs after the other patches have scaled, moved and tilted the ghost, so the check
        /// against players uses where the piece will really be.</summary>
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Player __instance, GameObject ___m_placementGhost, ref Player.PlacementStatus ___m_placementStatus,
            Piece? __state)
        {
            if (__state != null)
            {
                __state.m_groundPiece = true;
                __state.m_clipEverything = false;
            }

            if (___m_placementStatus is Player.PlacementStatus.Valid or Player.PlacementStatus.NoRayHits or Player.PlacementStatus.PrivateZone ||
                ___m_placementGhost == null || !___m_placementGhost.activeSelf || !IgnoreRules())
            {
                return;
            }

            // A later check (such as the biome) can replace "blocked by player", so check it again.
            ___m_placementStatus = OverlapsCharacter(__instance) ? Player.PlacementStatus.BlockedbyPlayer : Player.PlacementStatus.Valid;
            SetGhostValid(__instance, ___m_placementStatus == Player.PlacementStatus.Valid);
        }
    }
}
