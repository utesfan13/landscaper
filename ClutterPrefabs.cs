using BepInEx.Logging;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Builds placeable prefabs from Valheim's clutter, the plants it scatters in bulk with an
/// InstanceRenderer (grass, reeds, lily pads). Clutter isn't a saved object, so it can't be cloned
/// like other pieces. Each one here becomes a networked prefab named "LandscaperClutter_{name}"
/// that shows the clutter's mesh and material; the catalog then refers to it like any vanilla prefab.
/// Every player's game builds the same prefabs, so placed copies load for everyone.
/// </summary>
internal static class ClutterPrefabs
{
    public const string Prefix = "LandscaperClutter_";

    /// <summary>Clutter to turn into prefabs, by the clutter object's name.</summary>
    private static readonly string[] Sources =
    {
        "instanced_vass",          // cattails / reeds
        "instanced_waterlilies"    // lily pads
    };

    private static bool _built;

    /// <summary>Builds the prefabs once. Call before registering pieces, from ZNetScene.Awake.</summary>
    public static void Build(ManualLogSource log)
    {
        if (_built)
        {
            return;
        }

        _built = true;
        foreach (var sourceName in Sources)
        {
            try
            {
                if (!TryBuild(sourceName, out var reason))
                {
                    log.LogWarning($"Could not make a placeable piece from clutter '{sourceName}': {reason}");
                }
            }
            catch (Exception exception)
            {
                log.LogError($"Failed to make a placeable piece from clutter '{sourceName}': {exception}");
            }
        }
    }

    private static bool TryBuild(string sourceName, out string reason)
    {
        var source = PrefabManager.Instance.GetPrefab(sourceName);
        var renderer = source != null ? source.GetComponent<InstanceRenderer>() : null;
        if (renderer == null || renderer.m_mesh == null || renderer.m_material == null)
        {
            reason = "it isn't loaded or has no mesh";
            return false;
        }

        var name = Prefix + sourceName.Replace("instanced_", string.Empty);
        var prefab = PrefabManager.Instance.CreateEmptyPrefab(name);
        if (prefab == null)
        {
            reason = "the prefab couldn't be created";
            return false;
        }

        // CreateEmptyPrefab makes a cube; keep only the transform and ZNetView.
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
        UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());

        var visual = new GameObject("visual");
        visual.transform.SetParent(prefab.transform, worldPositionStays: false);
        visual.transform.localScale = renderer.m_scale;
        visual.AddComponent<MeshFilter>().sharedMesh = renderer.m_mesh;
        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = renderer.m_material;
        meshRenderer.shadowCastingMode = renderer.m_shadowCasting;

        PrefabManager.Instance.AddPrefab(prefab);
        PrefabManager.Instance.RegisterToZNetScene(prefab);
        reason = string.Empty;
        return true;
    }
}
