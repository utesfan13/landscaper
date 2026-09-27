using BepInEx;
using UnityEngine;

namespace Landscaper;

/// <summary>Keyboard helpers shared by the scale and tint controls.</summary>
internal static class PlacementInput
{
    /// <summary>Checks a modifier key, accepting its counterpart on the other side of the keyboard.</summary>
    public static bool IsHeld(KeyCode key) =>
        IsDown(key) || (TryGetCounterpart(key, out var other) && IsDown(other));

    private static readonly KeyCode[] Modifiers =
    {
        KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl
    };

    /// <summary>Modifiers seen being pressed while the game had focus and not released since.</summary>
    private static readonly HashSet<KeyCode> PressedModifiers = new();

    /// <summary>
    /// Tracks the modifier keys. Call every frame. If the window loses focus while a key is down, as
    /// with Alt+Tab, Windows never reports its release and Unity keeps treating it as held until it is
    /// pressed again. So a modifier only counts as held once it has been pressed while the game had
    /// focus, and all of them are forgotten when focus is lost.
    /// </summary>
    public static void Update()
    {
        if (!Application.isFocused)
        {
            PressedModifiers.Clear();
            return;
        }

        var input = UnityInput.Current;
        foreach (var key in Modifiers)
        {
            if (input.GetKeyDown(key))
            {
                PressedModifiers.Add(key);
            }
            else if (!input.GetKey(key))
            {
                PressedModifiers.Remove(key);
            }
        }
    }

    private static bool IsDown(KeyCode key) =>
        UnityInput.Current.GetKey(key) && (Array.IndexOf(Modifiers, key) < 0 || PressedModifiers.Contains(key));

    public static bool IsTyping() =>
        global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible() ||
        Minimap.InTextInput() || (Chat.instance is not null && Chat.instance.HasFocus());

    // Unity's == also treats destroyed objects as null; the placement ghost is destroyed whenever
    // the selected piece changes.
    public static bool IsLandscaperPiece(GameObject? gameObject) =>
        gameObject != null && gameObject.name.StartsWith("Landscaper_", StringComparison.Ordinal);

    /// <summary>A key as shown in the build hints.</summary>
    public static string KeyName(KeyCode key) => key switch
    {
        KeyCode.LeftBracket => "[",
        KeyCode.RightBracket => "]",
        KeyCode.Comma => ",",
        KeyCode.Period => ".",
        KeyCode.Semicolon => ";",
        KeyCode.Quote => "'",
        KeyCode.Slash => "/",
        KeyCode.Backslash => "\\",
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
}

/// <summary>
/// Turns a pair of keys into steps: one step when a key is pressed, then after a short delay a
/// steady stream of steps while it stays held.
/// </summary>
internal sealed class HeldKeyRepeater
{
    /// <summary>Seconds a key must be held before it starts repeating.</summary>
    private const float RepeatDelay = 0.35f;

    private int _heldDirection;
    private float _nextRepeatTime;

    /// <summary>Returns 1 or -1 when a step should happen this frame, otherwise 0.</summary>
    public int Poll(KeyCode downKey, KeyCode upKey, float stepsPerSecond)
    {
        var input = UnityInput.Current;
        var direction = input.GetKey(upKey) ? 1 : input.GetKey(downKey) ? -1 : 0;

        if (direction == 0)
        {
            _heldDirection = 0;
            return 0;
        }

        if (direction != _heldDirection)
        {
            _heldDirection = direction;
            _nextRepeatTime = Time.unscaledTime + RepeatDelay;
            return direction;
        }

        if (Time.unscaledTime >= _nextRepeatTime)
        {
            _nextRepeatTime = Time.unscaledTime + 1f / stepsPerSecond;
            return direction;
        }

        return 0;
    }
}
