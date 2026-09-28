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

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int MainColor = Shader.PropertyToID("_Color");

    /// <summary>Same light blue the Hammer uses for pieces without a support value.</summary>
    private static readonly Color HighlightColor = new(0.6f, 0.8f, 1f);

    private static GameObject? _highlighted;

    private static void SetHighlight(GameObject? target)
    {
        if (target == _highlighted)
        {
            return;
        }

        // Unity's == treats destroyed objects as null, so a removed piece isn't touched again.
        if (_highlighted != null && MaterialMan.instance is not null)
        {
            // Resetting the colour also puts back the piece's tint, see LandscaperTint.
            MaterialMan.instance.ResetValue(_highlighted, MainColor);
            MaterialMan.instance.ResetValue(_highlighted, EmissionColor);
        }

        _highlighted = target;
        if (target != null && MaterialMan.instance is not null)
        {
            MaterialMan.instance.SetValue(target, EmissionColor, HighlightColor * 0.4f);
            MaterialMan.instance.SetValue(target, MainColor, HighlightColor);
        }
    }

    /// <summary>
    /// Valheim finds the piece under the crosshair every frame with the remove raycast, but only
    /// highlights pieces that have building support (WearNTear). This highlights Landscaper pieces
    /// too, whenever the current tool can remove them, so it's clear what middle click will remove.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateWearNTearHover")]
    private static class HoverHighlightPatch
    {
        private static void Postfix(Player __instance, Piece ___m_hoveringPiece, PieceTable ___m_buildPieces)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            var piece = ___m_hoveringPiece;
            var canRemove = ___m_buildPieces != null && ___m_buildPieces.m_canRemovePieces;
            var isLandscaper = piece != null && piece.gameObject.name.StartsWith("Landscaper_", StringComparison.Ordinal);

            // Pieces with WearNTear are already highlighted by Valheim.
            SetHighlight(canRemove && isLandscaper && piece!.GetComponent<WearNTear>() == null ? piece.gameObject : null);
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
            // Ponds can't be hit by the remove ray directly; see PondWater.
            if (PondWater.TryRemoveAimedPond(__instance, ___m_removeRayMask))
            {
                __result = true;
                return false;
            }

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
