using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Implements the DecorativeOnly setting: while it is on, placed Landscaper trees, logs, rocks and
/// plants ignore damage and picking, so they can't be chopped, mined or harvested. Any piece placed
/// as indestructible, vanilla ones included, also ignores damage but can still be picked. The remove
/// button still removes them.
/// Checked when it happens rather than baked into the prefabs, so a value synced from the server, or
/// changed in game, applies straight away.
/// </summary>
internal static class DecorativeGuard
{
    /// <summary>Reads the current DecorativeOnly value.</summary>
    public static Func<bool> Enabled { get; set; } = () => false;

    private static bool Blocks(Component component) =>
        (Enabled() && PlacementInput.IsLandscaperPiece(component.gameObject)) ||
        IndestructibleController.IsIndestructible(component);

    private static bool BlocksPicking(Component component) =>
        Enabled() && PlacementInput.IsLandscaperPiece(component.gameObject);

    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
    private static class TreeBasePatch
    {
        private static bool Prefix(TreeBase __instance) => !Blocks(__instance);
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
    private static class TreeLogPatch
    {
        private static bool Prefix(TreeLog __instance) => !Blocks(__instance);
    }

    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
    private static class DestructiblePatch
    {
        private static bool Prefix(Destructible __instance) => !Blocks(__instance);
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    private static class MineRockPatch
    {
        private static bool Prefix(MineRock __instance) => !Blocks(__instance);
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    private static class MineRock5Patch
    {
        private static bool Prefix(MineRock5 __instance) => !Blocks(__instance);
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    private static class PickablePatch
    {
        private static bool Prefix(Pickable __instance, ref bool __result)
        {
            if (!BlocksPicking(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
