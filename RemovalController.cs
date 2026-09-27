using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Lets the Cultivator and Hoe remove Landscaper pieces with the remove button (middle click), like
/// the Hammer. Vanilla turns removal off for those tools, so it is turned on for their piece tables
/// and a patch cancels any removal that doesn't target a Landscaper piece, leaving crops, buildings
/// and other pieces alone.
/// </summary>
internal static class RemovalController
{
    private static readonly HashSet<PieceTable> LandscaperOnlyTables = new();

    /// <summary>Turns on removal for the given tools. Safe to call repeatedly.</summary>
    public static void EnableFor(IEnumerable<BuildTool> tools)
    {
        foreach (var tool in tools)
        {
            if (tool == BuildTool.Hammer)
            {
                continue;
            }

            var table = ObjectDB.instance?.GetItemPrefab(tool.ToString())?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
            if (table is null || LandscaperOnlyTables.Contains(table) || table.m_canRemovePieces)
            {
                continue;
            }

            table.m_canRemovePieces = true;
            LandscaperOnlyTables.Add(table);
        }
    }

    [HarmonyPatch(typeof(Player), "RemovePiece")]
    private static class RemovePiecePatch
    {
        /// <summary>
        /// Repeats vanilla's target raycast and skips the removal unless it would hit a Landscaper
        /// piece. Only applies to tools whose removal this mod turned on.
        /// </summary>
        private static bool Prefix(Player __instance, ref bool __result, PieceTable ___m_buildPieces, int ___m_removeRayMask)
        {
            if (___m_buildPieces is null || !LandscaperOnlyTables.Contains(___m_buildPieces))
            {
                return true;
            }

            var camera = GameCamera.instance.transform;
            if (Physics.Raycast(camera.position, camera.forward, out var hit, 50f, ___m_removeRayMask) &&
                Vector3.Distance(hit.point, __instance.m_eye.position) < __instance.m_maxPlaceDistance)
            {
                var piece = hit.collider.GetComponentInParent<Piece>();
                if (piece != null && piece.gameObject.name.StartsWith("Landscaper_", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            __result = false;
            return false;
        }
    }
}
