using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Tilts and nudges Landscaper pieces while placing them. Hold a modifier and scroll: in rotate mode
/// (the default) to tilt the piece forward/back or sideways or spin it in fine steps, in move mode to
/// move it up and down, side to side, or toward and away from you. The mode key switches between them.
/// Side to side and toward/away follow the direction the camera faces (horizontally), so they match
/// the screen. The tilt is relative to the piece, so Valheim's own scroll rotation turns a tilted
/// piece around. Both are added to the placement ghost, and Valheim places the piece where the ghost
/// is and as it's turned; the game saves a piece's rotation itself. Scrolling normally rotates the
/// piece, so that rotation is held still while any of the modifiers is down.
/// </summary>
internal static class OffsetController
{
    private static ConfigEntry<KeyCode> _upModifier = null!;
    private static ConfigEntry<KeyCode> _sideModifier = null!;
    private static ConfigEntry<float> _step = null!;
    private static ConfigEntry<KeyCode> _modeKey = null!;
    private static ConfigEntry<float> _rotationStep = null!;

    /// <summary>Meters right (x), up (y) and away from the camera (z).</summary>
    private static Vector3 _offset;

    /// <summary>Degrees of tilt forward (x), fine spin (y) and tilt sideways (z), relative to the piece.</summary>
    private static Vector3 _tilt;

    /// <summary>A copied object's tilt, kept for the piece selected by the copy; see ScaleController.ApplyCopied.</summary>
    private static Vector3? _copiedTilt;

    /// <summary>Whether scrolling with a modifier moves the piece rather than tilting it.</summary>
    private static bool _moveMode;

    public static void Bind(ConfigFile config)
    {
        _upModifier = config.Bind("Offset", "UpDownModifier", KeyCode.LeftAlt,
            "Hold and scroll to raise or lower the piece being placed. Either side of the keyboard works.");
        _sideModifier = config.Bind("Offset", "SideModifier", KeyCode.LeftShift,
            "Hold and scroll to move the piece being placed left or right. Either side of the keyboard works.");
        _step = config.Bind("Offset", "Step", 0.1f,
            new ConfigDescription("Meters per scroll step.", new AcceptableValueRange<float>(0.01f, 1f)));
        _modeKey = config.Bind("Offset", "ModeKey", KeyCode.Slash,
            "Switch what scrolling with the modifiers does: rotate (tilt and fine spin, the default) or move.");
        _rotationStep = config.Bind("Offset", "RotationStep", 5f,
            new ConfigDescription("Degrees per scroll step when tilting or fine-spinning.", new AcceptableValueRange<float>(0.5f, 45f)));
    }

    /// <summary>The mode key as shown in the build hints.</summary>
    public static string ModeKeyName => PlacementInput.KeyName(_modeKey.Value);

    /// <summary>The build hint for scrolling with the modifiers in the current mode.</summary>
    public static string HintLabel => _moveMode
        ? $"Move ({ModifierNames}: up/sideways/forward), {ModeKeyName} to rotate"
        : $"Tilt ({ModifierNames}: forward/sideways/fine spin), {ModeKeyName} to move";

    /// <summary>The extra rotation to apply to the piece, after Valheim's own.</summary>
    public static Quaternion Rotation => Quaternion.Euler(_tilt);

    /// <summary>Uses the tilt of a copied object, relative to the rotation Valheim gives the copy.</summary>
    public static void ApplyCopied(Quaternion relativeRotation)
    {
        var angles = relativeRotation.eulerAngles;
        _tilt = new Vector3(Normalize(angles.x), Normalize(angles.y), Normalize(angles.z));
        _copiedTilt = _tilt;
    }

    /// <summary>Called once the ghost of the copied piece has been set up.</summary>
    public static void ClearCopied() => _copiedTilt = null;

    /// <summary>
    /// Resets the offset, and the tilt to the piece's starting tilt (a custom entry's rotation), or
    /// to a copied object's tilt when the piece was just selected by copying.
    /// </summary>
    public static void ResetForNewPiece(string? pieceName)
    {
        _offset = Vector3.zero;
        var start = pieceName is not null && Plugin.Pieces?.StartingTiltOf(pieceName) is { } tilt ? tilt : Vector3.zero;
        _tilt = _copiedTilt ?? new Vector3(Normalize(start.x), Normalize(start.y), Normalize(start.z));
    }

    /// <summary>Angle in -180 to 180, rounded to hundredths, so tiny float drift reads as 0.</summary>
    private static float Normalize(float degrees) => (float)Math.Round(Mathf.DeltaAngle(0f, degrees), 2);

    /// <summary>The up/down, side and forward/back modifiers, e.g. "Alt/Shift/Alt+Shift"; forward/back is both held.</summary>
    public static string ModifierNames => PlacementInput.ChordNames(_upModifier.Value, _sideModifier.Value);

    /// <summary>Whether scrolling should tilt or move the piece instead of Valheim rotating it right now.</summary>
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

    public static void Reset()
    {
        _offset = Vector3.zero;
        _tilt = Vector3.zero;
    }

    /// <summary>Handles scrolling. Call every frame while a Landscaper piece is being placed.</summary>
    public static void HandleInput(Player player)
    {
        if (UnityInput.Current.GetKeyDown(_modeKey.Value))
        {
            _moveMode = !_moveMode;
            BuildKeyHints.RefreshOffsetLabel();
            player.Message(MessageHud.MessageType.Center, _moveMode ? "Scroll mode: move" : "Scroll mode: rotate");
        }

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

        if (!_moveMode)
        {
            // Alt tilts forward/back (around the piece's X axis), Shift sideways (Z), both spin (Y).
            var tiltAxis = axis switch { 1 => 0, 0 => 2, _ => 1 };
            _tilt[tiltAxis] = Normalize(_tilt[tiltAxis] + Math.Sign(scroll) * _rotationStep.Value);
            player.Message(MessageHud.MessageType.Center,
                $"Tilt  forward {_tilt.x:+0.#;-0.#;0}°  sideways {_tilt.z:+0.#;-0.#;0}°  spin {_tilt.y:+0.#;-0.#;0}°");
            return;
        }

        _offset[axis] = (float)Math.Round(_offset[axis] + Math.Sign(scroll) * _step.Value, 2);
        player.Message(MessageHud.MessageType.Center,
            $"Offset  up {_offset.y:+0.00;-0.00;0.00}  right {_offset.x:+0.00;-0.00;0.00}  forward {_offset.z:+0.00;-0.00;0.00} m");
    }

    /// <summary>The axis whose modifier is held (0 side, 1 up, 2 forward), or null.</summary>
    private static int? AxisHeld() => PlacementInput.Chord(_upModifier.Value, _sideModifier.Value) switch
    {
        PlacementInput.ChordState.First => 1,
        PlacementInput.ChordState.Second => 0,
        PlacementInput.ChordState.Both => 2,
        _ => null
    };

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
