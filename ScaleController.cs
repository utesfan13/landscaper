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
    private static (ZNetView View, Vector3 Scale)? _justPlaced;
    private static GameObject? _ghostRenderersOwner;
    private static Renderer[] _ghostRenderers = Array.Empty<Renderer>();
    private static readonly MaterialPropertyBlock GhostBlock = new();

    public static void Bind(ConfigFile config)
    {
        _upKey = config.Bind("Scaling", "ScaleUpKey", KeyCode.RightBracket,
            "Make the piece being placed bigger. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _downKey = config.Bind("Scaling", "ScaleDownKey", KeyCode.LeftBracket,
            "Make the piece being placed smaller. Alone it scales all axes; hold an axis modifier to scale one axis.");
        _resetKey = config.Bind("Scaling", "ScaleResetKey", KeyCode.End, "Reset the scale, tint, position offset and tilt of the piece being placed.");
        _xModifier = config.Bind("Scaling", "XAxisModifier", KeyCode.LeftAlt, "Hold with the scale keys to change only X (width). Either side of the keyboard works.");
        _yModifier = config.Bind("Scaling", "YAxisModifier", KeyCode.LeftShift, "Hold with the scale keys to change only Y (height). Either side of the keyboard works.");
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

        OffsetController.Reset();
        _copiedScale = _scale;
        _copiedTint = tint;
    }

    private static float ClampScale(float value)
    {
        var clamped = Mathf.Clamp(value, _minScale.Value, _maxScale.Value);
        // Snap float drift back to exactly normal size.
        return Mathf.Abs(clamped - 1f) < 0.001f ? 1f : clamped;
    }

    /// <summary>
    /// How much the scale keys multiply a pond's water: the combined scale of all three axes, so one
    /// step up on all axes pours about a third more.
    /// </summary>
    public static float WaterFactor => _scale.x * _scale.y * _scale.z;

    /// <summary>Ponds (Valheim's simulated liquid) break if resized, so the scale keys set how much
    /// water they pour instead, and they always keep their own size.</summary>
    private static bool IsLiquid(GameObject? gameObject) => gameObject != null && gameObject.GetComponent<LiquidVolume>() != null;

    private static Vector3 SizeScaleFor(GameObject gameObject) => IsLiquid(gameObject) ? Vector3.one : _scale;

    /// <summary>Scale-up key as shown in the build hints.</summary>
    public static string UpKeyName => PlacementInput.KeyName(_upKey.Value);

    /// <summary>Scale-down key as shown in the build hints.</summary>
    public static string DownKeyName => PlacementInput.KeyName(_downKey.Value);

    /// <summary>The X, Y and Z modifiers in order, e.g. "Alt/Shift/Alt+Shift"; Z is both held.</summary>
    public static string ModifierNames =>
        PlacementInput.ChordNames(_xModifier.Value, _yModifier.Value);

    /// <summary>Whether the local player is placing a piece that can be adjusted right now.</summary>
    public static bool PlacingAdjustablePiece
    {
        get
        {
            var player = Player.m_localPlayer;
            return player is not null && player.InPlaceMode() && PlacementInput.IsAdjustable(_ghost);
        }
    }

    /// <summary>Handles the scale, tint, height and reset keys. Call every frame.</summary>
    public static void Update()
    {
        var player = Player.m_localPlayer;
        if (!Plugin.ModEnabled())
        {
            BuildKeyHints.SetVisible(false, false);
            return;
        }

        var placingAdjustablePiece = PlacingAdjustablePiece;
        BuildKeyHints.SetVisible(placingAdjustablePiece, player is not null && player.InPlaceMode());

        // The indestructible mode stays on across pieces, so it can be toggled with any piece selected.
        if (player is not null && player.InPlaceMode() && !PlacementInput.IsTyping())
        {
            IndestructibleController.HandleInput(player);
        }

        if (!placingAdjustablePiece || PlacementInput.IsTyping())
        {
            return;
        }

        if (UnityInput.Current.GetKeyDown(_resetKey.Value))
        {
            _scale = Vector3.one;
            TintController.Reset();
            OffsetController.ResetForNewPiece(_ghost!.name);
            player!.Message(MessageHud.MessageType.Center, "Scale, tint, position and tilt reset");
            return;
        }

        TintController.HandleInput(player!);
        OffsetController.HandleInput(player!);

        var direction = Repeater.Poll(_downKey.Value, _upKey.Value, _repeatRate.Value);
        if (direction != 0)
        {
            Adjust(direction);
            player!.Message(MessageHud.MessageType.Center, IsLiquid(_ghost)
                ? $"Water per placement: {PondWater.CurrentVolume:0.##} cubic m"
                : $"Scale X {_scale.x:0.##}  Y {_scale.y:0.##}  Z {_scale.z:0.##}");
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

        switch (PlacementInput.Chord(_xModifier.Value, _yModifier.Value))
        {
            case PlacementInput.ChordState.First:
                _scale.x = Clamp(_scale.x);
                return;
            case PlacementInput.ChordState.Second:
                _scale.y = Clamp(_scale.y);
                return;
            case PlacementInput.ChordState.Both:
                _scale.z = Clamp(_scale.z);
                return;
        }

        _scale = new Vector3(Clamp(_scale.x), Clamp(_scale.y), Clamp(_scale.z));
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

                OffsetController.ResetForNewPiece(_ghost?.name);
            }

            _copiedScale = null;
            _copiedTint = null;
            OffsetController.ClearCopied();

            if (!PlacementInput.IsAdjustable(_ghost))
            {
                return;
            }

            var ghost = _ghost!;
            var prefab = ZNetScene.instance?.GetPrefab(ghost.name);
            if (prefab is not null)
            {
                ghost.transform.localScale = Vector3.Scale(prefab.transform.localScale, SizeScaleFor(ghost));
            }

            ExtendPlacementRange(__instance, ghost);

            // Valheim recalculates the ghost's position every update and places the piece where the
            // ghost is, so moving the ghost here moves the placed piece too.
            // Only while Valheim has just positioned the ghost: a hidden ghost keeps its last transform,
            // and adding to it every frame would pile up.
            if (ghost.activeSelf)
            {
                // Landscaper water pieces (lily pads) rest on the surface: the aim ray finds a pond's
                // collider 2 m below it, and the sea's surface moves with the waves.
                if (ghost.TryGetComponent<WaterFloat>(out _) && WaterFloat.SurfaceAt(ghost.transform.position) is { } surface)
                {
                    var position = ghost.transform.position;
                    position.y = surface;
                    ghost.transform.position = position;
                }

                ghost.transform.position += OffsetController.WorldOffset;
                ghost.transform.rotation *= OffsetController.Rotation;
            }

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
    /// Valheim only lets pieces be placed within a few meters, so a large or scaled-up piece can't be
    /// placed far enough away to fit. Piece.m_extraPlacementDistance adds to that range for the piece
    /// being placed; set it on the ghost so the reach is at least half the piece's scaled size plus a
    /// little, up to Valheim's 50 m placement ray.
    /// </summary>
    private static void ExtendPlacementRange(Player player, GameObject ghost)
    {
        var piece = ghost.GetComponent<Piece>();
        var size = Plugin.Pieces?.SizeOf(ghost.name);
        if (piece is null || size is null)
        {
            return;
        }

        var scaledSize = size.Value * Mathf.Max(_scale.x, _scale.y, _scale.z);
        var needed = scaledSize * 0.5f + 2f - player.m_maxPlaceDistance;
        piece.m_extraPlacementDistance = Mathf.Clamp(Mathf.CeilToInt(needed), 0, 45);
    }

    /// <summary>
    /// Valheim refuses a placement when any solid collider of the ghost overlaps a character, so that
    /// nobody gets trapped. The extra box EnsureTargetable adds to some pieces collides with nothing,
    /// so it can't trap anyone, but around a large or hollow piece (such as the sunken crypt tower
    /// wall) it would block placing it near yourself. Run the same check without those boxes.
    /// </summary>
    [HarmonyPatch(typeof(Player), "CheckPlacementGhostVSPlayers")]
    private static class IgnoreTargetBoxWhenBlockedPatch
    {
        // This runs every frame while placing, so the ghost's colliders are found once per ghost and
        // the list of nearby characters is reused.
        private static readonly List<Character> Characters = new();
        private static readonly List<Collider> GhostColliders = new();
        private static GameObject? _collidersOwner;

        private static bool Prefix(GameObject ___m_placementGhost, Player __instance, ref bool __result)
        {
            if (!PlacementInput.IsLandscaperPiece(___m_placementGhost))
            {
                return true;
            }

            if (!ReferenceEquals(___m_placementGhost, _collidersOwner))
            {
                _collidersOwner = ___m_placementGhost;
                GhostColliders.Clear();
                foreach (var collider in ___m_placementGhost.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    if (collider.gameObject != ___m_placementGhost && collider.gameObject.name != DecorativePieceManager.TargetBoxName &&
                        (collider is not MeshCollider mesh || mesh.convex))
                    {
                        GhostColliders.Add(collider);
                    }
                }
            }

            Characters.Clear();
            Character.GetCharactersInRange(__instance.transform.position, 30f, Characters);
            __result = Overlaps();
            return false;
        }

        private static bool Overlaps()
        {
            foreach (var collider in GhostColliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foreach (var character in Characters)
                {
                    var capsule = character.GetCollider();
                    if (capsule != null && Physics.ComputePenetration(
                            collider, collider.transform.position, collider.transform.rotation,
                            capsule, capsule.transform.position, capsule.transform.rotation, out _, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Sizes and tints the placement ghost again just before it renders. Doing it in the ghost update
    /// is enough on its own in a plain game, but other building mods can change the ghost's size or
    /// material values later in the same frame. LateUpdate runs after every Update, so this wins. The
    /// tint is written straight into the renderers' property blocks, keeping their other values.
    /// Call every frame from LateUpdate.
    /// </summary>
    public static void LateUpdate()
    {
        if (!PlacingAdjustablePiece)
        {
            return;
        }

        var ghost = _ghost!;

        // Read again: relaxed placement rules can make the ghost valid after it was sized above.
        _ghostValid = Player.m_localPlayer!.GetPlacementStatus() == Player.PlacementStatus.Valid;
        if (ZNetScene.instance?.GetPrefab(ghost.name) is { } prefab)
        {
            var sizeScale = SizeScaleFor(ghost);
            ghost.transform.localScale = Vector3.Scale(prefab.transform.localScale, sizeScale);
            VegetationSway.Apply(ghost, prefab, sizeScale.y);
        }

        // A new pond fills itself as soon as it's created, so keep the amount it will pour current.
        if (IsLiquid(ghost))
        {
            PondWater.RefreshVolume();
        }

        var tint = TintController.Current;
        if (tint is null || !_ghostValid)
        {
            return;
        }

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
            if (!PlacementInput.IsAdjustable(_ghost) || !PlacementInput.IsAdjustable(__instance.gameObject))
            {
                return;
            }

            var view = __instance.GetComponent<ZNetView>();
            if (view is null || !view.IsValid())
            {
                return;
            }

            // Set the size from the prefab, the same way the ghost is sized, rather than multiplying the
            // placed object's current size: some mods create the placed object from the already scaled
            // ghost or copy its size, and multiplying again would square the scale.
            var prefab = SavedPieces.PrefabOf(view.GetZDO());
            var baseScale = prefab != null ? prefab.transform.localScale : __instance.transform.localScale;
            var scale = Vector3.Scale(baseScale, SizeScaleFor(__instance.gameObject));
            if (__instance.transform.localScale != scale)
            {
                view.SetLocalScale(scale);
            }

            _justPlaced = (view, scale);

            if (TintController.Current is { } tint)
            {
                LandscaperTint.Save(__instance.gameObject, tint);
            }

            OffsetController.SaveHeight(__instance.gameObject);

            // A water piece keeps its nudged height relative to the moving surface.
            if (__instance.TryGetComponent<WaterFloat>(out _))
            {
                WaterFloat.SaveOffset(__instance.gameObject, OffsetController.WorldOffset.y);
            }
        }
    }

    /// <summary>
    /// Runs after every other mod's patches on placing a piece and re-applies the size chosen above,
    /// in case one of them resized the placed object afterwards.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    private static class EnforcePlacedScalePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            if (_justPlaced is not { } placed)
            {
                return;
            }

            _justPlaced = null;
            var (view, scale) = placed;
            if (view != null && view.IsValid() && view.transform.localScale != scale)
            {
                view.SetLocalScale(scale);
            }
        }
    }
}
