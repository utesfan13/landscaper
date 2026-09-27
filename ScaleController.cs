using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Scales Landscaper pieces while placing them. The keys adjust a multiplier that is applied to the
/// placement ghost every frame and to each piece when it is placed. Clones have
/// ZNetView.m_syncInitialScale enabled, so each placed piece saves its own size with the world.
/// </summary>
internal static class ScaleController
{

    private static ConfigEntry<KeyCode> _upKey = null!;
    private static ConfigEntry<KeyCode> _downKey = null!;
    private static ConfigEntry<KeyCode> _resetKey = null!;
    private static ConfigEntry<KeyCode> _xModifier = null!;
    private static ConfigEntry<KeyCode> _yModifier = null!;
    private static ConfigEntry<KeyCode> _zModifier = null!;
    private static ConfigEntry<float> _step = null!;
    private static ConfigEntry<float> _repeatRate = null!;
    private static ConfigEntry<float> _minScale = null!;
    private static ConfigEntry<float> _maxScale = null!;

    /// <summary>Seconds a scale key must be held before it starts repeating.</summary>
    private const float RepeatDelay = 0.35f;

    private static Vector3 _scale = Vector3.one;
    private static GameObject? _ghost;
    private static string? _selectedPiece;
    private static int _heldDirection;
    private static float _nextRepeatTime;

    public static void Bind(ConfigFile config)
    {
        _upKey = config.Bind("Scaling", "ScaleUpKey", KeyCode.RightBracket,
            "Make the piece being placed bigger. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _downKey = config.Bind("Scaling", "ScaleDownKey", KeyCode.LeftBracket,
            "Make the piece being placed smaller. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _resetKey = config.Bind("Scaling", "ScaleResetKey", KeyCode.End, "Reset the scale to normal size.");
        _xModifier = config.Bind("Scaling", "XAxisModifier", KeyCode.LeftAlt, "Hold with the scale keys to change only X (width). Either side of the keyboard works.");
        _yModifier = config.Bind("Scaling", "YAxisModifier", KeyCode.LeftShift, "Hold with the scale keys to change only Y (height). Either side of the keyboard works.");
        _zModifier = config.Bind("Scaling", "ZAxisModifier", KeyCode.LeftControl, "Hold with the scale keys to change only Z (depth). Either side of the keyboard works.");
        _step = config.Bind("Scaling", "ScaleStep", 0.1f,
            new ConfigDescription("How much each key press changes the scale, as a fraction of the current size (0.1 = 10%).", new AcceptableValueRange<float>(0.01f, 1f)));
        _repeatRate = config.Bind("Scaling", "ScaleRepeatRate", 15f,
            new ConfigDescription("Steps per second while a scale key is held down.", new AcceptableValueRange<float>(1f, 60f)));
        _minScale = config.Bind("Scaling", "MinScale", 0.1f,
            new ConfigDescription("Smallest scale allowed on any axis.", new AcceptableValueRange<float>(0.01f, 1f)));
        _maxScale = config.Bind("Scaling", "MaxScale", 10f,
            new ConfigDescription("Largest scale allowed on any axis.", new AcceptableValueRange<float>(1f, 100f)));
    }

    /// <summary>Handles the scale keys. Call every frame.</summary>
    public static void Update()
    {
        var player = Player.m_localPlayer;
        var placingLandscaperPiece = player is not null && player.InPlaceMode() && IsLandscaperPiece(_ghost);
        ScaleKeyHint.SetVisible(placingLandscaperPiece);
        if (!placingLandscaperPiece || IsTyping())
        {
            return;
        }

        var input = UnityInput.Current;
        var direction = input.GetKey(_upKey.Value) ? 1 : input.GetKey(_downKey.Value) ? -1 : 0;

        if (input.GetKeyDown(_resetKey.Value))
        {
            _scale = Vector3.one;
        }
        else if (direction == 0)
        {
            _heldDirection = 0;
            return;
        }
        else if (direction != _heldDirection)
        {
            // Newly pressed: one step now, then repeat after a short delay while held.
            _heldDirection = direction;
            _nextRepeatTime = Time.unscaledTime + RepeatDelay;
            Adjust(direction);
        }
        else if (Time.unscaledTime >= _nextRepeatTime)
        {
            _nextRepeatTime = Time.unscaledTime + 1f / _repeatRate.Value;
            Adjust(direction);
        }
        else
        {
            return;
        }

        player!.Message(MessageHud.MessageType.Center, $"Scale X {_scale.x:0.##}  Y {_scale.y:0.##}  Z {_scale.z:0.##}");
    }

    /// <summary>
    /// Grows (direction 1) or shrinks (direction -1) by a percentage of the current size, so steps
    /// stay fine near normal size and fast at large sizes. Shrinking divides by the same factor
    /// growing multiplies by, so one step each way returns to the same size.
    /// </summary>
    private static void Adjust(int direction)
    {
        var factor = direction > 0 ? 1f + _step.Value : 1f / (1f + _step.Value);

        float Clamp(float value)
        {
            var scaled = Mathf.Clamp(value * factor, _minScale.Value, _maxScale.Value);
            // Snap float drift back to exactly normal size.
            return Mathf.Abs(scaled - 1f) < 0.001f ? 1f : scaled;
        }

        if (IsHeld(_xModifier.Value))
        {
            _scale.x = Clamp(_scale.x);
        }
        else if (IsHeld(_yModifier.Value))
        {
            _scale.y = Clamp(_scale.y);
        }
        else if (IsHeld(_zModifier.Value))
        {
            _scale.z = Clamp(_scale.z);
        }
        else
        {
            _scale = new Vector3(Clamp(_scale.x), Clamp(_scale.y), Clamp(_scale.z));
        }
    }

    /// <summary>Checks a modifier key, accepting its counterpart on the other side of the keyboard.</summary>
    /// <summary>Scale-up key as shown in the build hints.</summary>
    public static string UpKeyName => KeyName(_upKey.Value);

    /// <summary>Scale-down key as shown in the build hints.</summary>
    public static string DownKeyName => KeyName(_downKey.Value);

    /// <summary>The X, Y and Z modifiers in order, e.g. "Alt/Shift/Ctrl".</summary>
    public static string ModifierNames => $"{KeyName(_xModifier.Value)}/{KeyName(_yModifier.Value)}/{KeyName(_zModifier.Value)}";

    private static string KeyName(KeyCode key) => key switch
    {
        KeyCode.LeftBracket => "[",
        KeyCode.RightBracket => "]",
        KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
        KeyCode.LeftShift or KeyCode.RightShift => "Shift",
        KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
        KeyCode.PageUp => "PgUp",
        KeyCode.PageDown => "PgDn",
        KeyCode.Equals => "=",
        KeyCode.Minus => "-",
        KeyCode.KeypadPlus => "Num+",
        KeyCode.KeypadMinus => "Num-",
        _ => key.ToString()
    };

    private static bool IsHeld(KeyCode key)
    {
        var input = UnityInput.Current;
        return input.GetKey(key) || (TryGetCounterpart(key, out var other) && input.GetKey(other));
    }

    private static bool TryGetCounterpart(KeyCode key, out KeyCode counterpart)
    {
        counterpart = key switch
        {
            KeyCode.LeftAlt => KeyCode.RightAlt,
            KeyCode.RightAlt => KeyCode.LeftAlt,
            KeyCode.LeftShift => KeyCode.RightShift,
            KeyCode.RightShift => KeyCode.LeftShift,
            KeyCode.LeftControl => KeyCode.RightControl,
            KeyCode.RightControl => KeyCode.LeftControl,
            _ => KeyCode.None
        };
        return counterpart != KeyCode.None;
    }

    // Unity's == also treats destroyed objects as null; the ghost is destroyed whenever the
    // selected piece changes.
    private static bool IsLandscaperPiece(GameObject? gameObject) =>
        gameObject != null && gameObject.name.StartsWith("Landscaper_", StringComparison.Ordinal);

    private static bool IsTyping() =>
        global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible() ||
        Minimap.InTextInput() || (Chat.instance is not null && Chat.instance.HasFocus());

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static class GhostPatch
    {
        private static void Postfix(Player __instance, GameObject ___m_placementGhost)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            _ghost = ___m_placementGhost;

            // Each newly selected piece starts at normal size, including when switching back to a
            // piece that was scaled earlier. The ghost is named after the selected prefab.
            if (_ghost != null && _ghost.name != _selectedPiece)
            {
                _selectedPiece = _ghost.name;
                _scale = Vector3.one;
            }

            if (!IsLandscaperPiece(_ghost))
            {
                return;
            }

            var ghost = _ghost!;
            var prefab = ZNetScene.instance?.GetPrefab(ghost.name);
            if (prefab is not null)
            {
                ghost.transform.localScale = Vector3.Scale(prefab.transform.localScale, _scale);
            }
        }
    }

    /// <summary>Piece.SetCreator runs on a piece right after the local player places it.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    private static class PlacePatch
    {
        private static void Postfix(Piece __instance)
        {
            if (_scale == Vector3.one || !IsLandscaperPiece(_ghost) || !IsLandscaperPiece(__instance.gameObject))
            {
                return;
            }

            var view = __instance.GetComponent<ZNetView>();
            if (view is not null && view.IsValid())
            {
                view.SetLocalScale(Vector3.Scale(__instance.transform.localScale, _scale));
            }
        }
    }
}
