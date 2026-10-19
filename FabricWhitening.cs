using UnityEngine;

namespace Landscaper;

/// <summary>
/// Turns a piece's coloured fabric (or a whole stone) white, so a tint can make it any colour: a tint
/// multiplies the colours it's drawn with, so red cloth only ever comes out red or dark. Each texture
/// is copied (game textures can't be read directly, so through the graphics card) with the chosen
/// pixels made a light grey of the same shading: the strongly red ones by default, the blue ones for
/// blue cloth, or all of them for grey stone such as the grausten roofs. Wood, which is browner, is
/// left as it is.
/// </summary>
internal static class FabricWhitening
{
    /// <summary>Which pixels are made white.</summary>
    public enum Mode
    {
        Red,
        Blue,
        All
    }

    /// <summary>Pieces whose fabric isn't red, by prefab name.</summary>
    private static readonly Dictionary<string, Mode> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dvergrprops_banner"] = Mode.Blue, ["dvergrprops_curtain"] = Mode.Blue,
        ["piece_cloth_hanging_door_blue"] = Mode.Blue, ["piece_cloth_hanging_door_blue2"] = Mode.Blue, ["jute_carpet_blue"] = Mode.Blue,
        ["piece_grausten_roof_45"] = Mode.All, ["piece_grausten_roof_45_arch"] = Mode.All,
        ["piece_grausten_roof_45_arch_corner"] = Mode.All, ["piece_grausten_roof_45_arch_corner2"] = Mode.All,
        ["piece_grausten_roof_45_corner"] = Mode.All, ["piece_grausten_roof_45_corner2"] = Mode.All
    };

    /// <summary>
    /// How light the whitened fabric is on average, from 0 to 1: a light grey rather than white, which
    /// in direct sunlight glares like snow.
    /// </summary>
    private const float Lightness = 0.65f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly Dictionary<(Texture, Mode), Texture2D> Whitened = new();

    /// <summary>Which pixels a piece made from this prefab has whitened.</summary>
    public static Mode ModeFor(string prefabName) => Modes.TryGetValue(prefabName, out var mode) ? mode : Mode.Red;

    /// <summary>Gives every renderer of a prefab copies of its materials with whitened fabric.</summary>
    public static void Apply(GameObject prefab, Mode mode = Mode.Red)
    {
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || !material.HasProperty(MainTexId) || material.mainTexture is not { } texture)
                {
                    continue;
                }

                var copy = new Material(material) { name = material.name + " (white)", mainTexture = WhitenedCopy(texture, mode) };
                if (mode == Mode.All)
                {
                    WhitenEverything(copy);
                }

                Dull(copy);
                Uncolour(copy);
                materials[i] = copy;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static Texture2D WhitenedCopy(Texture texture, Mode mode)
    {
        if (!Whitened.TryGetValue((texture, mode), out var whitened))
        {
            whitened = Whiten(texture, mode);
            Whitened[(texture, mode)] = whitened;
        }

        return whitened;
    }

    /// <summary>Texture and colour properties that aren't colour: surface detail, shine and glow.</summary>
    private static readonly string[] NotColour = { "bump", "normal", "metal", "gloss", "smooth", "spec", "emission", "occlusion", "height", "parallax", "mask", "noise" };

    /// <summary>
    /// For a whole stone: every other colour texture whitened too, and the colours the textures are
    /// multiplied by made white, so no part keeps its colour (the grausten roofs' shingles are tinted
    /// brown by their material, not their texture).
    /// </summary>
    private static void WhitenEverything(Material material)
    {
        foreach (var name in material.GetTexturePropertyNames())
        {
            if (name != "_MainTex" && !NotColour.Any(name.ToLowerInvariant().Contains) && material.GetTexture(name) is { } texture)
            {
                material.SetTexture(name, WhitenedCopy(texture, Mode.All));
            }
        }

        var shader = material.shader;
        for (var i = 0; i < shader.GetPropertyCount(); i++)
        {
            var name = shader.GetPropertyName(i);
            if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color && !NotColour.Any(name.ToLowerInvariant().Contains))
            {
                var colour = material.GetColor(name);
                material.SetColor(name, new Color(1f, 1f, 1f, colour.a));
            }
        }
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// A coloured base colour made white, so it doesn't colour the whitened texture (the blue jute
    /// carpet's frayed edge is tinted blue by its material).
    /// </summary>
    private static void Uncolour(Material material)
    {
        if (material.HasProperty(ColorId))
        {
            var colour = material.GetColor(ColorId);
            Color.RGBToHSV(colour, out _, out var saturation, out _);
            if (saturation > 0.15f)
            {
                material.SetColor(ColorId, new Color(1f, 1f, 1f, colour.a));
            }
        }
    }

    /// <summary>How much of its shine a whitened material keeps.</summary>
    private const float Shine = 0.25f;

    /// <summary>
    /// Takes most of the shine off: what was a faint sheen on dark cloth or stone looks glossy, almost
    /// wet, once it's white.
    /// </summary>
    private static void Dull(Material material)
    {
        var shader = material.shader;
        for (var i = 0; i < shader.GetPropertyCount(); i++)
        {
            var name = shader.GetPropertyName(i);
            var type = shader.GetPropertyType(i);
            var lower = name.ToLowerInvariant();
            if ((type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range) &&
                (lower.Contains("gloss") || lower.Contains("smooth") || lower.Contains("metal") || lower.Contains("spec")))
            {
                material.SetFloat(name, material.GetFloat(name) * Shine);
            }
        }
    }

    private static bool Chosen(Mode mode, float hue, float saturation, float value) => mode switch
    {
        // Red, from a little towards magenta to a little towards orange; not brown wood.
        Mode.Red => (hue < 0.035f || hue > 0.95f) && saturation > 0.35f && value > 0.08f,
        // Blue, from teal to violet.
        Mode.Blue => hue > 0.48f && hue < 0.75f && saturation > 0.2f && value > 0.05f,
        _ => true
    };

    private static Texture2D Whiten(Texture texture, Mode mode)
    {
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Graphics.Blit(texture, target);
        RenderTexture.active = target;
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, mipChain: true) { name = texture.name + " (white)" };
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        var pixels = copy.GetPixels();
        var chosen = new bool[pixels.Length];
        var brightness = 0f;
        var count = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            Color.RGBToHSV(pixels[i], out var hue, out var saturation, out var value);
            if (Chosen(mode, hue, saturation, value))
            {
                chosen[i] = true;
                brightness += value;
                count++;
            }
        }

        if (count > 0 && brightness > 0f)
        {
            // The shading kept, lifted so it's light on average.
            var lift = Lightness / (brightness / count);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (chosen[i])
                {
                    Color.RGBToHSV(pixels[i], out _, out _, out var value);
                    var grey = Mathf.Clamp01(value * lift);
                    pixels[i] = new Color(grey, grey, grey, pixels[i].a);
                }
            }
        }

        copy.SetPixels(pixels);
        copy.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        return copy;
    }
}
