using UnityEngine;

namespace Landscaper;

/// <summary>
/// Valheim's vegetation shader sways trees and plants by _SwayDistance, a fixed amount that doesn't
/// shrink with the object, so a tree scaled down sways as far as a full-size one and looks like it's
/// in a storm. This scales the sway with the object's height scale through MaterialMan, which only
/// affects this object, not the shared materials of other trees.
/// </summary>
internal static class VegetationSway
{
    private static readonly int SwayDistanceId = Shader.PropertyToID("_SwayDistance");

    /// <summary>
    /// Sets the sway for <paramref name="target"/>, whose materials come from <paramref name="prefab"/>,
    /// at <paramref name="heightScale"/> times the prefab's height.
    /// </summary>
    public static void Apply(GameObject target, GameObject prefab, float heightScale)
    {
        if (MaterialMan.instance is null || BaseSway(prefab) is not { } sway)
        {
            return;
        }

        if (Mathf.Abs(heightScale - 1f) < 0.01f)
        {
            MaterialMan.instance.ResetValue(target, SwayDistanceId);
        }
        else
        {
            MaterialMan.instance.SetValue(target, SwayDistanceId, sway * heightScale);
        }
    }

    private static readonly Dictionary<GameObject, float?> BaseSwayByPrefab = new();

    /// <summary>The prefab's own sway, from the first material that has one; cached per prefab.</summary>
    private static float? BaseSway(GameObject prefab)
    {
        if (BaseSwayByPrefab.TryGetValue(prefab, out var cached))
        {
            return cached;
        }

        float? sway = null;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            var material = renderer.sharedMaterials.FirstOrDefault(candidate => candidate != null && candidate.HasProperty(SwayDistanceId));
            if (material != null)
            {
                sway = material.GetFloat(SwayDistanceId);
                break;
            }
        }

        BaseSwayByPrefab[prefab] = sway;
        return sway;
    }
}
