using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// An on/off mode while building: every piece placed while it is on, Landscaper or vanilla, is
/// flagged indestructible in its ZDO, so the flag is saved with the world and every player's game
/// honours it. Flagged building pieces ignore all damage, including the damage WearNTear deals when
/// a piece has too little support, so they can be placed anywhere without falling apart. Flagged
/// trees, rocks and saplings can't be chopped, mined or broken. Removing a piece doesn't go through
/// damage, so the remove button still works.
/// </summary>
internal static class IndestructibleController
{
    private const string ZdoKey = "LandscaperIndestructible";

    /// <summary>The key as Valheim stores it; looked up on every hit, so hashed once.</summary>
    private static readonly int ZdoKeyHash = ZdoKey.GetStableHashCode();

    private static ConfigEntry<KeyCode> _toggleKey = null!;
    private static Func<bool> _allowed = () => true;
    private static bool _on;

    public static void Bind(ConfigFile config, Func<bool> allowed)
    {
        _toggleKey = config.Bind("Indestructible", "ToggleKey", KeyCode.Backslash,
            "Turn indestructible placement on or off while building. Pieces placed while it's on never break.");
        _allowed = allowed;
    }

    /// <summary>Whether pieces placed now should be indestructible.</summary>
    public static bool On => _on && _allowed();

    public static string ToggleKeyName => PlacementInput.KeyName(_toggleKey.Value);

    public static string HintLabel => $"Indestructible: {(On ? "On" : "Off")}";

    /// <summary>Handles the toggle key. Call every frame while in build mode, with any piece selected.</summary>
    public static void HandleInput(Player player)
    {
        if (!UnityInput.Current.GetKeyDown(_toggleKey.Value))
        {
            return;
        }

        if (!_allowed())
        {
            player.Message(MessageHud.MessageType.Center, "Indestructible pieces are turned off on this server.");
            return;
        }

        _on = !_on;
        Plugin.Log.LogInfo($"Indestructible placement {(_on ? "on" : "off")}.");
        BuildKeyHints.RefreshIndestructibleLabel();
        player.Message(MessageHud.MessageType.Center, _on
            ? "Indestructible placement ON: pieces placed now won't break"
            : "Indestructible placement OFF");
    }

    /// <summary>Piece.SetCreator runs on any piece right after the local player places it.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    private static class MarkPlacedPatch
    {
        private static void Postfix(Piece __instance)
        {
            var view = __instance.GetComponent<ZNetView>();
            if (On && view != null && view.IsValid())
            {
                view.GetZDO().Set(ZdoKeyHash, true);
            }
        }
    }

    public static bool IsIndestructible(Component component)
    {
        var view = component.GetComponent<ZNetView>();
        return view != null && view.IsValid() && view.GetZDO().GetBool(ZdoKeyHash);
    }

    /// <summary>
    /// Every way a building piece breaks, apart from being removed, goes through ApplyDamage: lack of
    /// support, rain wear, events and attacks. Ignore it for flagged pieces.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    private static class ApplyDamagePatch
    {
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            if (!IsIndestructible(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
