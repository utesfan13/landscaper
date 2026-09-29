using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>Vanilla build pieces made adjustable in place.</summary>
public sealed partial class DecorativePieceManager
{
    /// <summary>Vanilla build pieces made adjustable; see MakeVanillaPiecesAdjustable.</summary>
    private readonly List<GameObject> _adjustableVanilla = new();

    /// <summary>The largest dimension of each adjustable vanilla piece, by prefab name.</summary>
    private readonly Dictionary<string, float> _vanillaSizes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Vanilla pieces listed in each tool's menu, by prefab name.</summary>
    private readonly Dictionary<BuildTool, HashSet<string>> _vanillaInMenus = new();

    /// <summary>
    /// Vanilla pieces with these components aren't made adjustable: ships and carts are physics
    /// objects that resizing would break, and planting and the terrain tools aren't objects at all.
    /// </summary>
    private static readonly HashSet<string> NotAdjustableComponents = new(StringComparer.Ordinal)
    {
        "Ship", "Vagon", "Plant", "TerrainOp", "TerrainModifier"
    };

    /// <summary>The tool whose menu lists this vanilla build piece, or null if none does.</summary>
    public BuildTool? VanillaToolFor(string prefabName) =>
        _vanillaInMenus.Where(pair => pair.Value.Contains(prefabName)).Select(pair => (BuildTool?)pair.Key).FirstOrDefault();

    /// <summary>
    /// Makes the vanilla build pieces on the Cultivator, Hoe and Hammer adjustable in place, so they
    /// can be resized, tinted, tilted and nudged like Landscaper's own pieces, with nothing else about
    /// them changed: same menu, order, name, cost, unlock and crafting station. Each placed piece saves
    /// its own size (ZNetView.m_syncInitialScale) and tint (LandscaperTint). They stay vanilla
    /// objects, so without this mod they'd just be normal size and colour again, not deleted. Pieces
    /// from other mods are left alone, so every player with the same game version gets the same set.
    /// Returns how many there are.
    /// </summary>
    private int MakeVanillaPiecesAdjustable()
    {
        foreach (BuildTool tool in Enum.GetValues(typeof(BuildTool)))
        {
            // The tool's piece table, straight from the item, before Jotunn adds mod pieces to it.
            var table = ZNetScene.instance?.GetPrefab(tool.ToString())?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
            if (table == null)
            {
                continue;
            }

            if (!_vanillaInMenus.TryGetValue(tool, out var inMenu))
            {
                _vanillaInMenus[tool] = inMenu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var prefab in table.m_pieces)
            {
                if (prefab == null || PlacementInput.IsLandscaperPiece(prefab) || PieceManager.Instance.GetPiece(prefab.name) != null ||
                    !prefab.TryGetComponent<Piece>(out var piece) || piece.m_repairPiece ||
                    !prefab.TryGetComponent<ZNetView>(out var view) ||
                    prefab.GetComponentsInChildren<Component>(includeInactive: true).Any(component =>
                        component != null && NotAdjustableComponents.Contains(component.GetType().Name)))
                {
                    continue;
                }

                if (piece.m_enabled)
                {
                    inMenu.Add(prefab.name);
                }

                if (_adjustableVanilla.Contains(prefab))
                {
                    continue;
                }

                view.m_syncInitialScale = true;
                if (!prefab.TryGetComponent<LandscaperTint>(out _))
                {
                    prefab.AddComponent<LandscaperTint>();
                }

                var size = TryGetModelBounds(prefab, out var bounds) ? Vector3.Scale(bounds.size, prefab.transform.localScale) : Vector3.one;
                _vanillaSizes[prefab.name] = Mathf.Max(size.x, size.y, size.z);
                _adjustableVanilla.Add(prefab);
            }
        }

        return _adjustableVanilla.Count;
    }

    /// <summary>
    /// Whether a catalog entry is just a vanilla piece already in its tool's menu, which is adjustable
    /// itself now, so the entry isn't registered and its saved name loads as the vanilla piece.
    /// Scaled variants, furniture made functional, and pieces the menu doesn't list (such as the turf
    /// roofs and out-of-season festive pieces) are still Landscaper pieces.
    /// </summary>
    private bool DuplicatesVanillaPiece(DecorativePieceDefinition definition) =>
        definition.Scale == Vector3.one && definition.FunctionFrom is null && !definition.OnWater &&
        _vanillaInMenus.TryGetValue(definition.Tool, out var inMenu) &&
        FindSpawnablePrefab(definition.PrefabName) is { } source && inMenu.Contains(source.name);
}
