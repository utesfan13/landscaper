using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>Menu icons for pieces without a vanilla one.</summary>
public sealed partial class DecorativePieceManager
{
    private readonly List<(Piece Piece, GameObject Source)> _pendingIcons = new();

    /// <summary>
    /// Renders the icon for one piece that has no vanilla icon; returns false when none are left.
    /// Call once per frame after the local player has spawned: Valheim's vegetation and rock
    /// shaders read environment values that aren't set while the world is loading, and icons
    /// rendered then come out red.
    /// </summary>
    public bool RenderNextIcon()
    {
        if (_pendingIcons.Count == 0)
        {
            return false;
        }

        var (piece, source) = _pendingIcons[_pendingIcons.Count - 1];
        _pendingIcons.RemoveAt(_pendingIcons.Count - 1);
        if (piece is null || source is null)
        {
            return true;
        }

        // Liquids have no mesh until they simulate, so there's nothing to render.
        if (source.GetComponent<LiquidVolume>() != null)
        {
            piece.m_icon = WaterIcon() ?? piece.m_icon;
            return true;
        }

        // Web strands don't show in a rendered icon; the web texture itself does.
        if (WebPrefabs.Contains(source.name) && TextureIcon(source) is { } webIcon)
        {
            piece.m_icon = webIcon;
            return true;
        }

        try
        {
            var sprite = RenderManager.Instance.Render(new RenderManager.RenderRequest(source)
            {
                Rotation = RenderManager.IsometricRotation,
                DistanceMultiplier = IconDistanceFor(source, RenderManager.IsometricRotation),
                UseCache = true,
                TargetPlugin = _plugin
            });
            if (sprite is not null)
            {
                piece.m_icon = sprite;
            }
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not render an icon for '{source.name}': {exception.Message}");
        }

        return true;
    }

    /// <summary>An icon from the main texture of the model's first material, or null if it has none.</summary>
    private static Sprite? TextureIcon(GameObject model)
    {
        var texture = model.GetComponentsInChildren<Renderer>(includeInactive: true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null && material.HasProperty("_MainTex"))
            .Select(material => material.mainTexture as Texture2D)
            .FirstOrDefault(candidate => candidate != null);
        return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Jotunn frames an icon by the model's size, but measures it from the model's own origin: the
    /// distance to the far edge plus the distance to the near edge. That's right for models around
    /// their origin, but a model that sits away from it (such as the big Jotun statue horns) measures
    /// far bigger than it is, the camera backs off, and the icon comes out tiny. Returns how much
    /// nearer the camera should be, from the real size; only when Jotunn is more than 10% off, so the
    /// icons that are framed right already don't change.
    /// </summary>
    private static float IconDistanceFor(GameObject model, Quaternion rotation)
    {
        // The model as Jotunn renders it: turned to the icon angle, at its own scale.
        if (!TryGetModelBounds(model, out var bounds, Matrix4x4.TRS(Vector3.zero, rotation, model.transform.localScale)))
        {
            return 1f;
        }

        var measured = Mathf.Max(Mathf.Abs(bounds.min.x) + Mathf.Abs(bounds.max.x), Mathf.Abs(bounds.min.y) + Mathf.Abs(bounds.max.y));
        var actual = Mathf.Max(bounds.size.x, bounds.size.y);
        var ratio = (actual + 0.1f) / (measured + 0.1f);
        return ratio < 0.9f ? ratio : 1f;
    }

    /// <summary>
    /// An icon for pond water: the water drop Valheim shows for the Wet status effect, or failing
    /// that the ice pond rock's icon tinted blue.
    /// </summary>
    private Sprite? WaterIcon()
    {
        if (ObjectDB.instance?.GetStatusEffect(SEMan.s_statusEffectWet)?.m_icon is { } wet)
        {
            return wet;
        }

        var rock = ZNetScene.instance?.GetPrefab("IcePond_rock");
        if (rock == null)
        {
            return null;
        }

        try
        {
            var rendered = RenderManager.Instance.Render(new RenderManager.RenderRequest(rock)
            {
                Rotation = RenderManager.IsometricRotation,
                UseCache = true,
                TargetPlugin = _plugin
            });
            return rendered != null ? Tinted(rendered, new Color(0.35f, 0.6f, 1f)) : null;
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not render a water icon: {exception.Message}");
            return null;
        }
    }

    /// <summary>A copy of a sprite with its colours multiplied by <paramref name="tint"/>.</summary>
    private static Sprite Tinted(Sprite sprite, Color tint)
    {
        var source = sprite.texture;
        var pixels = source.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(pixels[i].r * tint.r, pixels[i].g * tint.g, pixels[i].b * tint.b, pixels[i].a);
        }

        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite? GetToolIcon(BuildTool tool)
    {
        var icons = PrefabManager.Instance.GetPrefab(tool.ToString())?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
        return icons?.FirstOrDefault(icon => icon is not null);
    }
}
