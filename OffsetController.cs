using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Nudges Landscaper pieces while placing them: hold a modifier and scroll to move the piece up and
/// down, side to side, or toward and away from you. Side to side and toward/away follow the direction
/// the camera faces (horizontally), so they match the screen. The offset is added to the placement
/// ghost's position, and Valheim places the piece where the ghost is. Scrolling normally rotates the
/// piece, so rotation is held still while any of the modifiers is down.
/// </summary>
internal static class OffsetController
{
    private static ConfigEntry<KeyCode> _upModifier = null!;
    private static ConfigEntry<KeyCode> _sideModifier = null!;
    private static ConfigEntry<KeyCode> _depthModifier = null!;
    private static ConfigEntry<float> _step = null!;

    /// <summary>Meters right (x), up (y) and away from the camera (z).</summary>
    private static Vector3 _offset;

    public static void Bind(ConfigFile config)
    {
        _upModifier = config.Bind("Offset", "UpDownModifier", KeyCode.LeftAlt,
            "Hold and scroll to raise or lower the piece being placed. Either side of the keyboard works.");
        _sideModifier = config.Bind("Offset", "SideModifier", KeyCode.LeftShift,
            "Hold and scroll to move the piece being placed left or right. Either side of the keyboard works.");
        _depthModifier = config.Bind("Offset", "ForwardBackModifier", KeyCode.LeftControl,
            "Hold and scroll to move the piece being placed toward or away from you. Either side of the keyboard works.");
        _step = config.Bind("Offset", "Step", 0.1f,
            new ConfigDescription("Meters per scroll step.", new AcceptableValueRange<float>(0.01f, 1f)));
    }

    /// <summary>The up/down, side and forward/back modifiers in order, e.g. "Alt/Shift/Ctrl".</summary>
    public static string ModifierNames =>
        $"{PlacementInput.KeyName(_upModifier.Value)}/{PlacementInput.KeyName(_sideModifier.Value)}/{PlacementInput.KeyName(_depthModifier.Value)}";

    /// <summary>Whether scrolling should move the piece instead of rotating it right now.</summary>
    public static bool Active => ScaleController.PlacingLandscaperPiece && AxisHeld() is not null;

    /// <summary>The offset in world space, turned to match the direction the camera faces.</summary>
    public static Vector3 WorldOffset
    {
        get
        {
            if (_offset == Vector3.zero || GameCamera.instance is null)
            {
                return Vector3.up * _offset.y;
            }

            var forward = Vector3.ProjectOnPlane(GameCamera.instance.transform.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            return right * _offset.x + Vector3.up * _offset.y + forward * _offset.z;
        }
    }

    public static void Reset() => _offset = Vector3.zero;

    /// <summary>Handles scrolling. Call every frame while a Landscaper piece is being placed.</summary>
    public static void HandleInput(Player player)
    {
        if (AxisHeld() is not { } axis)
        {
            return;
        }

        // Some systems report Shift + wheel as horizontal scrolling.
        var delta = UnityInput.Current.mouseScrollDelta;
        var scroll = delta.y != 0f ? delta.y : delta.x;
        if (scroll == 0f)
        {
            return;
        }

        _offset[axis] = (float)Math.Round(_offset[axis] + Math.Sign(scroll) * _step.Value, 2);
        player.Message(MessageHud.MessageType.Center,
            $"Offset  up {_offset.y:+0.00;-0.00;0.00}  right {_offset.x:+0.00;-0.00;0.00}  forward {_offset.z:+0.00;-0.00;0.00} m");
    }

    /// <summary>The axis whose modifier is held (0 side, 1 up, 2 forward), or null.</summary>
    private static int? AxisHeld()
    {
        if (PlacementInput.IsHeld(_upModifier.Value))
        {
            return 1;
        }

        if (PlacementInput.IsHeld(_sideModifier.Value))
        {
            return 0;
        }

        return PlacementInput.IsHeld(_depthModifier.Value) ? 2 : null;
    }

    /// <summary>
    /// Valheim rotates the piece on any scroll, whatever modifiers are held. While scrolling moves the
    /// piece instead, put the rotation back after Valheim's placement update.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static class HoldRotationPatch
    {
        private static void Prefix(int ___m_placeRotation, float ___m_scrollCurrAmount, out (int Rotation, float Scroll) __state) =>
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
