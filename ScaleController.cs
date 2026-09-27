using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Scales Landscaper pieces while placing them, and applies the scale and tint from
/// <see cref="TintController"/> to the placement ghost and to each placed piece. Clones have
/// ZNetView.m_syncInitialScale enabled, so each placed piece saves its own size with the world.
/// </summary>
internal static class ScaleController
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

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

    private static readonly HeldKeyRepeater Repeater = new();
    private static Vector3 _scale = Vector3.one;
    private static GameObject? _ghost;
    private static string? _selectedPiece;
    private static bool _ghostValid;
    private static Vector3? _copiedScale;
    private static Color? _copiedTint;
    private static GameObject? _ghostRenderersOwner;
    private static Renderer[] _ghostRenderers = Array.Empty<Renderer>();
    private static readonly MaterialPropertyBlock GhostBlock = new();

    public static void Bind(ConfigFile config)
    {
        _upKey = config.Bind("Scaling", "ScaleUpKey", KeyCode.RightBracket,
            "Make the piece being placed bigger. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _downKey = config.Bind("Scaling", "ScaleDownKey", KeyCode.LeftBracket,
            "Make the piece being placed smaller. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _resetKey = config.Bind("Scaling", "ScaleResetKey", KeyCode.End, "Reset the scale, tint and height of the piece being placed.");
        _xModifier = config.Bind("Scaling", "XAxisModifier", KeyCode.LeftAlt, "Hold with the scale keys to change only X (width). Either side of the keyboard works.");
        _yModifier = config.Bind("Scaling", "YAxisModifier", KeyCode.LeftShift, "Hold with the scale keys to change only Y (height). Either side of the keyboard works.");
        _zModifier = config.Bind("Scaling", "ZAxisModifier", KeyCode.LeftControl, "Hold with the scale keys to change only Z (depth). Either side of the keyboard works.");
        _step = config.Bind("Scaling", "ScaleStep", 0.1f,
            new ConfigDescription("How much each key press changes the scale, as a fraction of the current size (0.1 = 10%).", new AcceptableValueRange<float>(0.01f, 1f)));
        _repeatRate = config.Bind("Scaling", "ScaleRepeatRate", 15f,
            new ConfigDescription("Steps per second while a scale key is held down.", new AcceptableValueRange<float>(1f, 60f)));
        _minScale = config.Bind("Scaling", "MinScale", 0.01f,
            new ConfigDescription("Smallest scale allowed on any axis.", new AcceptableValueRange<float>(0.01f, 1f)));
        _maxScale = config.Bind("Scaling", "MaxScale", 20f,
            new ConfigDescription("Largest scale allowed on any axis. Capped at 20: much larger pieces can reach beyond the remove button's range.", new AcceptableValueRange<float>(1f, 20f)));
    }

    /// <summary>
    /// Uses a copied object's size and tint for the piece being placed. Copying usually selects a
    /// different piece, and selecting a piece resets these, so they are also kept until the ghost's
    /// next update and used in place of that reset.
    /// </summary>
    public static void ApplyCopied(Vector3 scale, Color? tint)
    {
        _scale = new Vector3(ClampScale(scale.x), ClampScale(scale.y), ClampScale(scale.z));
        TintController.Reset();
        if (tint is { } color)
        {
            TintController.SetFromColor(color);
        }

        HeightController.Reset();
        _copiedScale = _scale;
        _copiedTint = tint;
    }

    private static float ClampScale(float value)
    {
        var clamped = Mathf.Clamp(value, _minScale.Value, _maxScale.Value);
        // Snap float drift back to exactly normal size.
        return Mathf.Abs(clamped - 1f) < 0.001f ? 1f : clamped;
    }

    /// <summary>Scale-up key as shown in the build hints.</summary>
    public static string UpKeyName => PlacementInput.KeyName(_upKey.Value);

    /// <summary>Scale-down key as shown in the build hints.</summary>
    public static string DownKeyName => PlacementInput.KeyName(_downKey.Value);

    /// <summary>The X, Y and Z modifiers in order, e.g. "Alt/Shift/Ctrl".</summary>
    public static string ModifierNames =>
        $"{PlacementInput.KeyName(_xModifier.Value)}/{PlacementInput.KeyName(_yModifier.Value)}/{PlacementInput.KeyName(_zModifier.Value)}";

    /// <summary>Whether the local player is placing a Landscaper piece right now.</summary>
    public static bool PlacingLandscaperPiece
    {
        get
        {
            var player = Player.m_localPlayer;
            return player is not null && player.InPlaceMode() && PlacementInput.IsLandscaperPiece(_ghost);
        }
    }

    /// <summary>Handles the scale, tint, height and reset keys. Call every frame.</summary>
    public static void Update()
    {
        var player = Player.m_localPlayer;
        var placingLandscaperPiece = PlacingLandscaperPiece;
        BuildKeyHints.SetVisible(placingLandscaperPiece);
        if (!placingLandscaperPiece || PlacementInput.IsTyping())
        {
            return;
        }

        if (UnityInput.Current.GetKeyDown(_resetKey.Value))
        {
            _scale = Vector3.one;
            TintController.Reset();
            HeightController.Reset();
            player!.Message(MessageHud.MessageType.Center, "Scale, tint and height reset");
            return;
        }

        TintController.HandleInput(player!);
        HeightController.HandleInput(player!);

        var direction = Repeater.Poll(_downKey.Value, _upKey.Value, _repeatRate.Value);
        if (direction != 0)
        {
            Adjust(direction);
            player!.Message(MessageHud.MessageType.Center, $"Scale X {_scale.x:0.##}  Y {_scale.y:0.##}  Z {_scale.z:0.##}");
        }
    }

    /// <summary>
    /// Grows (direction 1) or shrinks (direction -1) by a percentage of the current size, so steps
    /// stay fine near normal size and fast at large sizes. Shrinking divides by the same factor
    /// growing multiplies by, so one step each way returns to the same size.
    /// </summary>
    private static void Adjust(int direction)
    {
        var factor = direction > 0 ? 1f + _step.Value : 1f / (1f + _step.Value);

        float Clamp(float value) => ClampScale(value * factor);

        if (PlacementInput.IsHeld(_xModifier.Value))
        {
            _scale.x = Clamp(_scale.x);
        }
        else if (PlacementInput.IsHeld(_yModifier.Value))
        {
            _scale.y = Clamp(_scale.y);
        }
        else if (PlacementInput.IsHeld(_zModifier.Value))
        {
            _scale.z = Clamp(_scale.z);
        }
        else
        {
            _scale = new Vector3(Clamp(_scale.x), Clamp(_scale.y), Clamp(_scale.z));
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static class GhostPatch
    {
        private static void Postfix(Player __instance, GameObject ___m_placementGhost, Player.PlacementStatus ___m_placementStatus)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            _ghost = ___m_placementGhost;

            // Each newly selected piece starts at normal size and colour, including when switching
            // back to a piece that was changed earlier. The ghost is named after the selected prefab.
            // A copied object's size and tint replace the reset; see ApplyCopied.
            if (_ghost != null && _ghost.name != _selectedPiece)
            {
                _selectedPiece = _ghost.name;
                _scale = _copiedScale ?? Vector3.one;
                TintController.Reset();
                if (_copiedTint is { } copiedTint)
                {
                    TintController.SetFromColor(copiedTint);
                }

                HeightController.Reset();
            }

            _copiedScale = null;
            _copiedTint = null;

            if (!PlacementInput.IsLandscaperPiece(_ghost))
            {
                return;
            }

            var ghost = _ghost!;
            var prefab = ZNetScene.instance?.GetPrefab(ghost.name);
            if (prefab is not null)
            {
                ghost.transform.localScale = Vector3.Scale(prefab.transform.localScale, _scale);
            }

            // Valheim recalculates the ghost's position every update and places the piece where the
            // ghost is, so raising the ghost here raises the placed piece too.
            ghost.transform.position += Vector3.up * HeightController.Offset;

            // Valheim clears the ghost's colour every frame and turns it red when placement is invalid;
            // only tint a valid ghost so that warning stays visible.
            _ghostValid = ___m_placementStatus == Player.PlacementStatus.Valid;
            var tint = TintController.Current;
            if (tint is not null && _ghostValid && MaterialMan.instance is not null)
            {
                MaterialMan.instance.SetValue(ghost, ColorId, tint.Value);
            }
        }
    }

    /// <summary>
    /// Tints the placement ghost again just before it renders. Setting it through MaterialMan in the
    /// ghost update is enough on its own in a plain game, but other mods can overwrite the ghost's
    /// material values later in the same frame. LateUpdate runs after every Update, so writing the
    /// colour straight into the renderers' property blocks here wins. Their other values are kept.
    /// Call every frame from LateUpdate.
    /// </summary>
    public static void LateUpdate()
    {
        var tint = TintController.Current;
        if (tint is null || !_ghostValid || !PlacingLandscaperPiece)
        {
            return;
        }

        var ghost = _ghost!;
        if (!ReferenceEquals(ghost, _ghostRenderersOwner))
        {
            _ghostRenderersOwner = ghost;
            _ghostRenderers = ghost.GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        foreach (var renderer in _ghostRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(GhostBlock);
            GhostBlock.SetColor(ColorId, tint.Value);
            renderer.SetPropertyBlock(GhostBlock);
        }
    }

    /// <summary>Piece.SetCreator runs on a piece right after the local player places it.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    private static class PlacePatch
    {
        private static void Postfix(Piece __instance)
        {
            if (!PlacementInput.IsLandscaperPiece(_ghost) || !PlacementInput.IsLandscaperPiece(__instance.gameObject))
            {
                return;
            }

            var view = __instance.GetComponent<ZNetView>();
            if (view is null || !view.IsValid())
            {
                return;
            }

            if (_scale != Vector3.one)
            {
                view.SetLocalScale(Vector3.Scale(__instance.transform.localScale, _scale));
            }

            if (TintController.Current is { } tint)
            {
                LandscaperTint.Save(__instance.gameObject, tint);
            }
        }
    }
}
