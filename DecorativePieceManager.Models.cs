using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>Working with a piece's model: its bounds, re-centring it, and making clones static and targetable.</summary>
public sealed partial class DecorativePieceManager
{
    /// <summary>
    /// Adds an extra box around the model on "piece_nonsolid" when a prefab's own colliders fall
    /// short. The remove and interact raycasts hit that layer but nothing collides with it, so the
    /// box never blocks movement, and the original colliders are left as they are. Two cases need it:
    /// <list type="bullet">
    /// <item>Removing needs a collider the remove raycast can hit. Some prefabs have none (the Jotun
    /// stair rug, cave webs) or only have them on layers it skips (pickable mushrooms use "item").</item>
    /// <item>Valheim positions Hammer placement ghosts using their solid colliders, ignoring triggers
    /// and non-convex mesh colliders. With none left the ghost is pushed far from the crosshair and
    /// can't be placed; the Jotun statue pieces and training dummies only have non-convex meshes.
    /// Cultivator and Hoe pieces are positioned differently, and a box around a large cliff would
    /// block placing plants on the terrain under it, so this only applies to the Hammer.</item>
    /// </list>
    /// The box is solid rather than a trigger so the ghost positioning can use it.
    /// </summary>
    private static void EnsureTargetable(GameObject clone, BuildTool tool)
    {
        var removeMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");
        var root = clone.transform;

        var colliders = clone.GetComponentsInChildren<Collider>(includeInactive: true)
            .Where(collider => collider.enabled && IsActiveWithin(collider.transform, root))
            .ToList();
        var removable = colliders.Any(collider => (removeMask & (1 << collider.gameObject.layer)) != 0);
        var placeable = tool != BuildTool.Hammer || colliders.Any(collider =>
            !collider.isTrigger && (collider is not MeshCollider mesh || mesh.convex));
        if (removable && placeable)
        {
            return;
        }

        if (!TryGetModelBounds(clone, out var combined))
        {
            return;
        }

        var target = new GameObject(TargetBoxName) { layer = LayerMask.NameToLayer("piece_nonsolid") };
        target.transform.SetParent(root, worldPositionStays: false);
        var box = target.AddComponent<BoxCollider>();
        box.center = combined.center;
        // Keep flat pieces such as rugs hittable.
        box.size = Vector3.Max(combined.size, new Vector3(0.1f, 0.1f, 0.1f));
    }

    /// <summary>
    /// The combined bounds of a clone's visible meshes, in the clone's local space, or in the space
    /// <paramref name="fromRoot"/> maps the clone's local space to.
    /// </summary>
    private static bool TryGetModelBounds(GameObject clone, out Bounds bounds, Matrix4x4? fromRoot = null)
    {
        var root = clone.transform;
        bounds = default;

        var meshes = clone.GetComponentsInChildren<MeshFilter>(includeInactive: true)
            .Where(filter => filter.sharedMesh is not null && IsActiveWithin(filter.transform, root))
            .Select(filter => (filter.transform, filter.sharedMesh.bounds))
            .Concat(clone.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                .Where(renderer => renderer.sharedMesh is not null && IsActiveWithin(renderer.transform, root))
                .Select(renderer => (renderer.transform, renderer.localBounds)))
            .ToList();
        if (meshes.Count == 0)
        {
            return false;
        }

        var toRoot = (fromRoot ?? Matrix4x4.identity) * root.worldToLocalMatrix;
        Bounds? combined = null;
        foreach (var (transform, meshBounds) in meshes)
        {
            var toRootFromMesh = toRoot * transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var point = toRootFromMesh.MultiplyPoint3x4(local);
                if (combined is { } existing)
                {
                    existing.Encapsulate(point);
                    combined = existing;
                }
                else
                {
                    combined = new Bounds(point, Vector3.zero);
                }
            }
        }

        bounds = combined!.Value;
        return true;
    }

    /// <summary>
    /// Whether a transform is active up to <paramref name="root"/>. Clones sit under Jotunn's inactive
    /// prefab container, so activeInHierarchy is always false for them.
    /// </summary>
    private static bool IsActiveWithin(Transform transform, Transform root)
    {
        for (var current = transform; current is not null && current != root.parent; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Some prefabs are world loot or physics objects rather than scenery, such as the cargo crates
    /// that float at sea. Placed as pieces they should stay put and not hand out free loot:
    /// <list type="bullet">
    /// <item>Containers keep working as storage but get no default loot, and don't destroy themselves
    /// when empty (a new cargo crate is empty for a moment and would delete itself straight away).</item>
    /// <item>Floating, network movement sync and rigidbodies are removed so pieces don't roll, fall
    /// or bob. The placement ghost already has its rigidbodies removed, so this matches the preview.
    /// Fallen logs (TreeLog) use their rigidbody as soon as they're created and break without it, so
    /// theirs is kept but made kinematic, which holds it still just the same.</item>
    /// <item>StaticPhysics is removed. On trees, rocks and plants it snaps the object to the terrain
    /// 20 seconds after it loads: up if it's sunk into the ground, down if it's floating. That undid
    /// pieces placed partly underground, raised with the nudge keys, or placed on floors and rocks.</item>
    /// </list>
    /// </summary>
    private static void MakeStaticDecoration(GameObject clone)
    {
        foreach (var container in clone.GetComponentsInChildren<Container>(includeInactive: true))
        {
            container.m_autoDestroyEmpty = false;
            container.m_defaultItems = new DropTable();
        }

        // Components that use the rigidbody go first, or Unity refuses to remove it.
        DestroyAll<StaticPhysics>(clone);
        DestroyAll<Floating>(clone);
        DestroyAll<ZSyncTransform>(clone);
        foreach (var body in clone.GetComponentsInChildren<Rigidbody>(includeInactive: true))
        {
            if (body.GetComponent<TreeLog>() != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }
    }

    /// <summary>
    /// A few prefabs, such as the giant Jotun stairs, have a mesh collider with no mesh. Valheim still
    /// measures from it when positioning a Hammer ghost, gets the aim point back, and pushes the ghost
    /// 50 m up, so the piece can't be placed. Such colliders do nothing else, so remove them.
    /// </summary>
    private static void RemoveEmptyMeshColliders(GameObject clone)
    {
        // Liquids (pond water, tar) build their collider mesh while simulating; keep those.
        if (clone.GetComponent<LiquidVolume>() != null)
        {
            return;
        }

        foreach (var collider in clone.GetComponentsInChildren<MeshCollider>(includeInactive: true))
        {
            if (collider.sharedMesh == null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
    }

    private static void DestroyAll<T>(GameObject root) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    /// <summary>
    /// Moves everything under the clone so the bottom middle of the model is at the clone's origin.
    /// Pieces already placed keep their saved position, so they appear moved by the same amount.
    /// </summary>
    private static void RecenterModel(GameObject clone)
    {
        if (!TryGetVertexBounds(clone, out var bounds) && !TryGetModelBounds(clone, out bounds))
        {
            return;
        }

        var bottomMiddle = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (Transform child in clone.transform)
        {
            child.localPosition -= bottomMiddle;
        }
    }

    /// <summary>
    /// The combined bounds of the clone's mesh vertices, in the clone's local space. Tighter than
    /// TryGetModelBounds for rotated models: the horizontal web is a flat sheet turned 45 degrees, and
    /// its turned mesh box reaches from the ground to 33 m while the sheet itself is at 16.5 m. Needs
    /// meshes the game lets mods read; false if any isn't.
    /// </summary>
    private static bool TryGetVertexBounds(GameObject clone, out Bounds bounds)
    {
        var root = clone.transform;
        bounds = default;
        Bounds? combined = null;
        foreach (var filter in clone.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || !IsActiveWithin(filter.transform, root))
            {
                continue;
            }

            if (!mesh.isReadable)
            {
                return false;
            }

            var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (var vertex in mesh.vertices)
            {
                var point = toRoot.MultiplyPoint3x4(vertex);
                if (combined is { } existing)
                {
                    existing.Encapsulate(point);
                    combined = existing;
                }
                else
                {
                    combined = new Bounds(point, Vector3.zero);
                }
            }
        }

        if (combined is not { } result)
        {
            return false;
        }

        bounds = result;
        return true;
    }
}
