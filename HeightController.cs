using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Raises or lowers Landscaper pieces while placing them: hold the height modifier and scroll. The
/// offset is added to the placement ghost's position, and Valheim places the piece where the ghost is.
/// Scrolling normally rotates the piece, so rotation is held still while the modifier is down.
/// </summary>
internal static class HeightController
{
    private static ConfigEntry<KeyCode> _modifier = null!;
    private static ConfigEntry<float> _step = null!;

    private static float _offset;

    public static void Bind(ConfigFile config)
    {
        _modifier = config.Bind("Height", "HeightModifier", KeyCode.LeftAlt,
            "Hold and scroll to raise or lower the piece being placed. Either side of the keyboard works.");
        _step = config.Bind("Height", "HeightStep", 0.1f,
            new ConfigDescription("Meters per scroll step.", new AcceptableValueRange<float>(0.01f, 1f)));
    }

    /// <summary>Meters to raise the piece being placed; negative lowers it.</summary>
    public static float Offset => _offset;

    public static string ModifierName => PlacementInput.KeyName(_modifier.Value);

    /// <summary>Whether scrolling should change height instead of rotating right now.</summary>
    public static bool Active => ScaleController.PlacingLandscaperPiece && PlacementInput.IsHeld(_modifier.Value);

    public static void Reset() => _offset = 0f;

    /// <summary>Handles scrolling. Call every frame while a Landscaper piece is being placed.</summary>
    public static void HandleInput(Player player)
    {
        if (!PlacementInput.IsHeld(_modifier.Value))
        {
            return;
        }

        var scroll = UnityInput.Current.mouseScrollDelta.y;
        if (scroll == 0f)
        {
            return;
        }

        _offset = (float)Math.Round(_offset + Math.Sign(scroll) * _step.Value, 2);
        player.Message(MessageHud.MessageType.Center, $"Height {_offset:+0.00;-0.00;0.00} m");
    }

    /// <summary>
    /// Valheim rotates the piece on any scroll, whatever modifiers are held. While scrolling changes
    /// height instead, put the rotation back after Valheim's placement update.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static class HoldRotationPatch
    {
        private static void Prefix(Player __instance, int ___m_placeRotation, float ___m_scrollCurrAmount, out (int Rotation, float Scroll) __state) =>
            __state = (___m_placeRotation, ___m_scrollCurrAmount);

        private static void Postfix(Player __instance, ref int ___m_placeRotation, ref float ___m_scrollCurrAmount, (int Rotation, float Scroll) __state)
        {
            if (__instance == Player.m_localPlayer && Active)
            {
                ___m_placeRotation = __state.Rotation;
                ___m_scrollCurrAmount = __state.Scroll;
            }
        }
    }
}
