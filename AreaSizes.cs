using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// A piece's areas are trigger colliders on it (EffectArea): a fire's warmth, the base area that keeps
/// enemies from spawning, and so on. They'd grow and shrink with the piece, so a resized campfire
/// would warm a much bigger or smaller area than a vanilla one. Keep them at the vanilla size instead,
/// by undoing the piece's resize on their colliders. Burn areas are left alone: they follow the
/// flames you can see, so a small fire still only burns you when you touch it.
/// </summary>
internal static class AreaSizes
{
    /// <summary>A collider's vanilla dimensions, kept so it can be resized again.</summary>
    private sealed class VanillaSize : MonoBehaviour
    {
        public float Radius;
        public float Height;
        public Vector3 Size;
    }

    private static void Keep(GameObject root)
    {
        if (!root.TryGetComponent<LandscaperTint>(out _) || !root.TryGetComponent<ZNetView>(out var view) || !view.IsValid() ||
            SavedPieces.PrefabOf(view.GetZDO()) is not { } prefab)
        {
            return;
        }

        var baseScale = prefab.transform.localScale;
        var scale = root.transform.localScale;
        var resize = new Vector3(
            baseScale.x != 0f ? Mathf.Abs(scale.x / baseScale.x) : 1f,
            baseScale.y != 0f ? Mathf.Abs(scale.y / baseScale.y) : 1f,
            baseScale.z != 0f ? Mathf.Abs(scale.z / baseScale.z) : 1f);
        if (resize.x <= 0f || resize.y <= 0f || resize.z <= 0f)
        {
            return;
        }

        // At its normal size a piece's areas are already right, unless they were resized before.
        var resized = (resize - Vector3.one).sqrMagnitude > 0.0001f;

        // Spheres and capsules grow with the largest axis.
        var largest = Mathf.Max(resize.x, resize.y, resize.z);
        foreach (var area in root.GetComponentsInChildren<EffectArea>(includeInactive: true))
        {
            if ((area.m_type & EffectArea.Type.Burning) != 0)
            {
                continue;
            }

            foreach (var collider in area.GetComponents<Collider>())
            {
                if (!collider.TryGetComponent<VanillaSize>(out var original))
                {
                    if (!resized)
                    {
                        continue;
                    }

                    original = Record(collider);
                }

                switch (collider)
                {
                    case SphereCollider sphere:
                        sphere.radius = original.Radius / largest;
                        break;
                    case CapsuleCollider capsule:
                        capsule.radius = original.Radius / largest;
                        capsule.height = original.Height / largest;
                        break;
                    case BoxCollider box:
                        box.size = new Vector3(original.Size.x / resize.x, original.Size.y / resize.y, original.Size.z / resize.z);
                        break;
                }
            }
        }
    }

    private static VanillaSize Record(Collider collider)
    {
        var original = collider.gameObject.AddComponent<VanillaSize>();
        switch (collider)
        {
            case SphereCollider sphere:
                original.Radius = sphere.radius;
                break;
            case CapsuleCollider capsule:
                original.Radius = capsule.radius;
                original.Height = capsule.height;
                break;
            case BoxCollider box:
                original.Size = box.size;
                break;
        }

        return original;
    }

    /// <summary>A loaded piece already has its saved size.</summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    private static class KeepWhenLoadedPatch
    {
        private static void Postfix(GameObject? __result)
        {
            if (__result != null)
            {
                Keep(__result);
            }
        }
    }

    /// <summary>Placing a resized piece sets its size this way.</summary>
    [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.SetLocalScale))]
    private static class KeepWhenResizedPatch
    {
        private static void Postfix(ZNetView __instance) => Keep(__instance.gameObject);
    }
}
