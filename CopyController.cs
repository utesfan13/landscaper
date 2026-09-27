using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Extends Valheim's copy-piece shortcut (Shift + middle click by default) to world objects such as
/// rocks, trees and props, not just pieces from the current tool. The copy selects the matching
/// Landscaper piece and takes the object's rotation, size and, for Landscaper pieces, tint. Objects
/// that aren't in the catalog yet are added to the current tool's "Copied" tab and saved as custom
/// entries, but only when hosting or playing alone: a host deletes saved objects whose prefab it
/// doesn't have, so other players can't add pieces the host doesn't know about.
/// </summary>
internal static class CopyController
{
    private const string CopiedCategory = "Copied";
    private const float MaxDistance = 50f;

    private static ConfigEntry<string> _customEntries = null!;

    public static void Bind(ConfigEntry<string> customEntries) => _customEntries = customEntries;

    [HarmonyPatch(typeof(Player), "CopyPiece")]
    private static class CopyPiecePatch
    {
        private static void Postfix(Player __instance, ref bool __result, PieceTable ___m_buildPieces, int ___m_removeRayMask,
            ref int ___m_placeRotation, float ___m_placeRotationDegrees)
        {
            if (__instance != Player.m_localPlayer || Plugin.Pieces is null || ___m_buildPieces == null ||
                FindTarget(__instance, ___m_removeRayMask) is not { } target)
            {
                return;
            }

            var isLandscaper = PlacementInput.IsLandscaperPiece(target.gameObject);
            if (__result)
            {
                // Valheim copied a piece from the current tool; for Landscaper pieces also take their look.
                if (isLandscaper && ZNetScene.instance.GetPrefab(target.GetZDO().GetPrefab()) is { } clone)
                {
                    ScaleController.ApplyCopied(ScaleRatio(target.transform, clone.transform), LandscaperTint.Read(target.gameObject));
                }

                return;
            }

            if (Copy(__instance, ___m_buildPieces, target, isLandscaper))
            {
                ___m_placeRotation = (int)Math.Round(target.transform.rotation.eulerAngles.y / ___m_placeRotationDegrees);
                __result = true;
            }
        }
    }

    private static bool Copy(Player player, PieceTable table, ZNetView target, bool isLandscaper)
    {
        var pieces = Plugin.Pieces!;
        if (ToolFor(table) is not { } tool)
        {
            return false;
        }

        if (RefusalFor(target) is { } refusal)
        {
            player.Message(MessageHud.MessageType.Center, refusal);
            return false;
        }

        var prefab = ZNetScene.instance.GetPrefab(target.GetZDO().GetPrefab());
        if (prefab == null)
        {
            return false;
        }

        var source = pieces.SourcePrefabOf(prefab.name) ?? prefab.name;
        var piece = pieces.FindPiece(source, tool);
        var added = false;
        if (piece is null)
        {
            if (pieces.FindToolFor(source) is { } otherTool)
            {
                player.Message(MessageHud.MessageType.Center, $"{DisplayNameFor(target, source)} is on the {otherTool}; switch to it to copy this.");
                return false;
            }

            if (ZNet.instance is null || !ZNet.instance.IsServer())
            {
                player.Message(MessageHud.MessageType.Center, "Only the host can copy objects that aren't in the Landscaper catalog.");
                return false;
            }

            piece = AddCopiedPiece(target, source, tool);
            if (piece is null)
            {
                player.Message(MessageHud.MessageType.Center, $"Can't copy {source}.");
                return false;
            }

            added = true;
        }

        if (!SelectPiece(player, piece))
        {
            player.Message(MessageHud.MessageType.Center, $"Couldn't select {piece.m_name}.");
            return false;
        }

        ScaleController.ApplyCopied(ScaleRatio(target.transform, piece.transform), isLandscaper ? LandscaperTint.Read(target.gameObject) : null);
        player.Message(MessageHud.MessageType.Center,
            added ? $"Copied {piece.m_name}; added to the {tool}'s {CopiedCategory} tab" : $"Copied {piece.m_name}");
        return true;
    }

    /// <summary>The world object under the crosshair, found the same way Valheim's copy does.</summary>
    private static ZNetView? FindTarget(Player player, int rayMask)
    {
        var camera = GameCamera.instance.transform;
        if (!Physics.Raycast(camera.position, camera.forward, out var hit, MaxDistance, rayMask) ||
            Vector3.Distance(hit.point, player.m_eye.position) >= player.m_maxPlaceDistance ||
            hit.collider.GetComponent<Heightmap>() != null)
        {
            return null;
        }

        var view = hit.collider.GetComponentInParent<ZNetView>();
        return view != null && view.IsValid() ? view : null;
    }

    private static string? RefusalFor(ZNetView target)
    {
        if (target.GetComponent<Character>() != null)
        {
            return "Creatures can't be copied.";
        }

        if (target.GetComponent<ItemDrop>() != null)
        {
            return "Items can't be copied.";
        }

        if (target.GetComponent<Ship>() != null || target.GetComponent<Vagon>() != null || target.GetComponent<TombStone>() != null)
        {
            return "That can't be copied.";
        }

        return null;
    }

    private static BuildTool? ToolFor(PieceTable table) =>
        ((BuildTool[])Enum.GetValues(typeof(BuildTool)))
            .Where(tool => PieceManager.Instance.GetPieceTable(tool.ToString()) == table)
            .Select(tool => (BuildTool?)tool)
            .FirstOrDefault();

    private static Piece? AddCopiedPiece(ZNetView target, string source, BuildTool tool)
    {
        var pieces = Plugin.Pieces!;
        var name = DisplayNameFor(target, source);
        if (pieces.IsNameUsed(name))
        {
            name = $"{name} ({source})";
        }

        var piece = pieces.RegisterCopied(new DecorativePieceDefinition
        {
            DisplayName = name,
            PrefabName = source,
            Tool = tool,
            Category = CopiedCategory
        });
        if (piece is null)
        {
            return null;
        }

        // Save it so it is registered again, before saved copies load, after a restart. As the host,
        // custom entries are also synced to other players.
        var entry = $"{name}|{source}|{tool}|{CopiedCategory}";
        _customEntries.Value = string.IsNullOrWhiteSpace(_customEntries.Value) ? entry : $"{_customEntries.Value.TrimEnd()}\n{entry}";

        // Jotunn only pushes changed settings to connected players when its config window closes or
        // the file reloads, so push this one now; otherwise they would not see the new piece until
        // they reconnect.
        try
        {
            AccessTools.Method(typeof(SynchronizationManager), "SynchronizeChangedConfig")?.Invoke(SynchronizationManager.Instance, null);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning($"Could not sync the new custom entry to other players: {exception.Message}");
        }

        return piece;
    }

    private static string DisplayNameFor(ZNetView target, string source)
    {
        var hoverName = target.GetComponentInChildren<Hoverable>()?.GetHoverName();
        var name = string.IsNullOrWhiteSpace(hoverName) || hoverName!.Contains("$") ? source : hoverName;
        // '|' and line breaks separate custom entry fields and entries.
        return new string(name.Where(c => c != '|' && c != '\n' && c != '\r').ToArray()).Trim();
    }

    /// <summary>Selects a piece, first making sure a newly added one is known and listed.</summary>
    private static bool SelectPiece(Player player, Piece piece)
    {
        if (player.SetSelectedPiece(piece))
        {
            return true;
        }

        AccessTools.Method(typeof(Player), "UpdateKnownRecipesList")?.Invoke(player, null);
        AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList")?.Invoke(player, null);
        return player.SetSelectedPiece(piece);
    }

    private static Vector3 ScaleRatio(Transform target, Transform prefab)
    {
        var baseScale = prefab.localScale;
        var scale = target.localScale;
        return new Vector3(
            baseScale.x != 0f ? scale.x / baseScale.x : 1f,
            baseScale.y != 0f ? scale.y / baseScale.y : 1f,
            baseScale.z != 0f ? scale.z / baseScale.z : 1f);
    }
}
