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

    /// <summary>Clutter to turn into prefabs, by the clutter object's name: one plant each.</summary>
    private static readonly string[] Sources =
    {
        "instanced_vass",          // cattails / reeds
        "instanced_waterlilies"    // lily pads
    };

    /// <summary>
    /// Clutter turned into ground cover: a patch of the plant scattered over about a metre, as the game
    /// scatters it, named "{Prefix}{name}_patch".
    /// </summary>
    private static readonly string[] PatchSources =
    {
        "instanced_meadows_grass", "instanced_meadows_grass_short", "instanced_forest_groundcover",
        "instanced_forest_groundcover_brown", "instanced_forest_groundcover_snow", "instanced_heathgrass",
        "instanced_heathflowers", "instanced_swamp_grass", "instanced_swamp_ormbunke", "instanced_ormbunke",
        "instanced_shrub", "instanced_mistlands_grass_short", "instanced_mistlands_rockplant",
        "instanced_ashlands_grass_short", "instanced_ashlands_grass_long", "instanced_small_rock1"
    };

    /// <summary>How many plants a patch has, and how far they're scattered from its middle.</summary>
    private const int PatchCount = 12;
    private const float PatchRadius = 1f;

    private static bool _built;

    /// <summary>Builds the prefabs once. Call before registering pieces, from ZNetScene.Awake.</summary>
    public static void Build(ManualLogSource log)
    {
        if (_built)
        {
            return;
        }

        _built = true;
        foreach (var (sourceName, patch) in Sources.Select(name => (name, false)).Concat(PatchSources.Select(name => (name, true))))
        {
            try
            {
                if (!TryBuild(sourceName, patch, out var reason))
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

    private static bool TryBuild(string sourceName, bool patch, out string reason)
    {
        var source = PrefabManager.Instance.GetPrefab(sourceName);
        var renderer = source != null ? source.GetComponent<InstanceRenderer>() : null;
        if (renderer == null || renderer.m_mesh == null || renderer.m_material == null)
        {
            reason = "it isn't loaded or has no mesh";
            return false;
        }

        var name = Prefix + sourceName.Replace("instanced_", string.Empty) + (patch ? "_patch" : string.Empty);
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

        if (!patch)
        {
            AddPlant(prefab.transform, renderer, Vector3.zero, 0f, 1f);
        }
        else
        {
            // The same scatter on every player's game, so a placed patch looks the same to everyone.
            var random = new System.Random(name.GetStableHashCode());
            for (var i = 0; i < PatchCount; i++)
            {
                var angle = (float)(random.NextDouble() * Math.PI * 2);
                var distance = (float)Math.Sqrt(random.NextDouble()) * PatchRadius;
                var position = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                AddPlant(prefab.transform, renderer, position, (float)(random.NextDouble() * 360), 0.8f + (float)random.NextDouble() * 0.4f);
            }
        }

        PrefabManager.Instance.AddPrefab(prefab);
        PrefabManager.Instance.RegisterToZNetScene(prefab);
        reason = string.Empty;
        return true;
    }

    /// <summary>One plant: the clutter's mesh and material, as the game draws it.</summary>
    private static void AddPlant(Transform parent, InstanceRenderer renderer, Vector3 position, float turn, float size)
    {
        var visual = new GameObject("visual");
        visual.transform.SetParent(parent, worldPositionStays: false);
        visual.transform.localPosition = position;
        visual.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
        visual.transform.localScale = renderer.m_scale * size;
        visual.AddComponent<MeshFilter>().sharedMesh = renderer.m_mesh;
        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = renderer.m_material;
        meshRenderer.shadowCastingMode = renderer.m_shadowCasting;
    }
}
