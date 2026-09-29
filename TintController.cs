using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Tints Landscaper pieces while placing them. The tint is a hue (a position on the colour wheel),
/// a strength (how much of that colour to blend in) and a brightness. It is multiplied with the
/// piece's textures through the _Color shader value, so it can colour and darken but not brighten.
/// The tint keys step through presets; hold a modifier with them to fine-tune hue, strength or brightness.
/// </summary>
internal static class TintController
{
    private sealed class Preset
    {
        public Preset(string name, float hue, float strength, float brightness)
        {
            Name = name;
            Hue = hue;
            Strength = strength;
            Brightness = brightness;
        }

        public string Name { get; }
        public float Hue { get; }
        public float Strength { get; }
        public float Brightness { get; }
    }

    private const float MinBrightness = 0.1f;

    private static ConfigEntry<KeyCode> _backKey = null!;
    private static ConfigEntry<KeyCode> _forwardKey = null!;
    private static ConfigEntry<KeyCode> _strengthModifier = null!;
    private static ConfigEntry<KeyCode> _hueModifier = null!;
    private static ConfigEntry<float> _hueStep = null!;
    private static ConfigEntry<float> _amountStep = null!;
    private static ConfigEntry<float> _repeatRate = null!;
    private static ConfigEntry<string> _presetsText = null!;

    private static readonly HeldKeyRepeater Repeater = new();
    private static List<Preset> _presets = new();

    private static float _hue;
    private static float _strength;
    private static float _brightness = 1f;
    private static int _presetIndex = -1;

    public static void Bind(ConfigFile config)
    {
        _backKey = config.Bind("Tint", "TintBackKey", KeyCode.Comma,
            "Tint the piece being placed. Alone it steps back through the presets; hold a modifier to fine-tune.");
        _forwardKey = config.Bind("Tint", "TintForwardKey", KeyCode.Period,
            "Tint the piece being placed. Alone it steps forward through the presets; hold a modifier to fine-tune.");
        _strengthModifier = config.Bind("Tint", "StrengthModifier", KeyCode.LeftShift,
            "Hold with the tint keys to change how strong the tint is. Either side of the keyboard works.");
        _hueModifier = config.Bind("Tint", "HueModifier", KeyCode.LeftAlt,
            "Hold with the tint keys to move the hue round the colour wheel. Either side of the keyboard works.");
        _hueStep = config.Bind("Tint", "HueStep", 10f,
            new ConfigDescription("Degrees round the colour wheel per key press.", new AcceptableValueRange<float>(1f, 90f)));
        _amountStep = config.Bind("Tint", "StrengthBrightnessStep", 0.1f,
            new ConfigDescription("How much each key press changes strength or brightness (0.1 = 10%).", new AcceptableValueRange<float>(0.01f, 0.5f)));
        _repeatRate = config.Bind("Tint", "TintRepeatRate", 15f,
            new ConfigDescription("Steps per second while a tint key is held down.", new AcceptableValueRange<float>(1f, 60f)));
        _presetsText = config.Bind("Tint", "Presets",
            "Red:0,70,100; Orange:28,70,100; Gold:48,65,100; Green:110,55,100; Teal:175,55,100; " +
            "Blue:215,60,100; Purple:275,55,100; Pink:330,45,100; Weathered:35,25,75; Charred:0,0,35",
            "Tint presets, separated by semicolons: Name:hue,strength,brightness. Hue is 0-360 degrees round the " +
            "colour wheel (0 red, 120 green, 240 blue); strength and brightness are 0-100 percent.");

        _presets = ParsePresets(_presetsText.Value);
    }

    /// <summary>The tint to apply, or null when the piece should keep its normal colours.</summary>
    public static Color? Current
    {
        get
        {
            if (_strength <= 0f && _brightness >= 1f)
            {
                return null;
            }

            var colour = Color.Lerp(Color.white, Color.HSVToRGB(_hue / 360f, 1f, 1f), _strength) * _brightness;
            colour.a = 1f;
            return colour;
        }
    }

    public static string BackKeyName => PlacementInput.KeyName(_backKey.Value);
    public static string ForwardKeyName => PlacementInput.KeyName(_forwardKey.Value);

    /// <summary>The hue, strength and brightness modifiers, e.g. "Alt/Shift/Alt+Shift"; brightness is both held.</summary>
    public static string ModifierNames => PlacementInput.ChordNames(_hueModifier.Value, _strengthModifier.Value);

    /// <summary>
    /// Sets the controls to reproduce a saved tint, for copying a tinted piece. Undoes <see cref="Current"/>:
    /// brightness scales every channel, so it is the largest one; after dividing it out, the colour is
    /// white blended with a full-strength hue, so strength is how far the smallest channel is below 1.
    /// </summary>
    public static void SetFromColor(Color tint)
    {
        var brightness = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        if (brightness <= 0f)
        {
            Reset();
            return;
        }

        var normalized = new Color(tint.r / brightness, tint.g / brightness, tint.b / brightness);
        Color.RGBToHSV(normalized, out var hue, out _, out _);
        _hue = Round(hue * 360f) % 360f;
        _strength = Mathf.Clamp01(Round(1f - Mathf.Min(normalized.r, Mathf.Min(normalized.g, normalized.b))));
        _brightness = Mathf.Clamp(Round(brightness), MinBrightness, 1f);
        _presetIndex = -1;
    }

    public static void Reset()
    {
        _hue = 0f;
        _strength = 0f;
        _brightness = 1f;
        _presetIndex = -1;
    }

    /// <summary>Handles the tint keys. Call every frame while a Landscaper piece is being placed.</summary>
    public static void HandleInput(Player player)
    {
        var direction = Repeater.Poll(_backKey.Value, _forwardKey.Value, _repeatRate.Value);
        if (direction == 0)
        {
            return;
        }

        string? presetName = null;
        var chord = PlacementInput.Chord(_hueModifier.Value, _strengthModifier.Value);
        if (chord == PlacementInput.ChordState.First)
        {
            _hue = ((_hue + direction * _hueStep.Value) % 360f + 360f) % 360f;
            // Turning the hue with no strength would show nothing, so start at a visible strength.
            if (_strength <= 0f)
            {
                _strength = 0.5f;
            }
        }
        else if (chord == PlacementInput.ChordState.Second)
        {
            _strength = Mathf.Clamp01(Round(_strength + direction * _amountStep.Value));
        }
        else if (chord == PlacementInput.ChordState.Both)
        {
            _brightness = Mathf.Clamp(Round(_brightness + direction * _amountStep.Value), MinBrightness, 1f);
        }
        else
        {
            if (_presets.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, "No tint presets configured");
                return;
            }

            // "No tint" is a stop in the cycle, just before the first preset, so stepping back the
            // other way from a preset that doesn't suit returns to the piece's own look.
            var stops = _presets.Count + 1;
            var position = (_presetIndex + 1 + direction + stops) % stops - 1;
            if (position < 0)
            {
                Reset();
            }
            else
            {
                var preset = _presets[position];
                _hue = preset.Hue;
                _strength = preset.Strength;
                _brightness = preset.Brightness;
                presetName = preset.Name;
                _presetIndex = position;
            }
        }

        player.Message(MessageHud.MessageType.Center, Describe(presetName));
    }

    private static string Describe(string? presetName)
    {
        var tint = Current;
        if (tint is null)
        {
            return "Tint: none";
        }

        var swatch = $"<color=#{ColorUtility.ToHtmlStringRGB(tint.Value)}>Tint</color>";
        var name = presetName is null ? string.Empty : $" {presetName}:";
        return $"{swatch}{name}  hue {_hue:0}  strength {_strength:P0}  brightness {_brightness:P0}";
    }

    private static float Round(float value) => (float)Math.Round(value, 2);

    /// <summary>
    /// The default tint keys (, and .) are also Valheim's minimap zoom keys. Minimap.UpdateMap applies
    /// that zoom to SmallZoom, so while the player is in build mode its value is put back afterwards.
    /// The large map's zoom and the minimap outside build mode are unaffected.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "UpdateMap")]
    private static class MinimapZoomPatch
    {
        private static void Prefix(Minimap __instance, out float __state) => __state = __instance.SmallZoom;

        private static void Postfix(Minimap __instance, float __state)
        {
            var player = Player.m_localPlayer;
            if (player != null && player.InPlaceMode())
            {
                __instance.SmallZoom = __state;
            }
        }
    }

    private static List<Preset> ParsePresets(string text)
    {
        var presets = new List<Preset>();
        foreach (var entry in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            var values = parts.Length == 2 ? parts[1].Split(',') : Array.Empty<string>();
            if (values.Length == 3 &&
                float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hue) &&
                float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var strength) &&
                float.TryParse(values[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var brightness))
            {
                presets.Add(new Preset(
                    parts[0].Trim(),
                    ((hue % 360f) + 360f) % 360f,
                    Mathf.Clamp01(strength / 100f),
                    Mathf.Clamp(brightness / 100f, MinBrightness, 1f)));
            }
            else if (entry.Trim().Length > 0)
            {
                Plugin.Log.LogWarning($"Ignoring invalid tint preset (use Name:hue,strength,brightness): {entry.Trim()}");
            }
        }

        return presets;
    }
}
