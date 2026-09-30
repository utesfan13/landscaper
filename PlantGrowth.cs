using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Vanilla saplings and seed crops can be resized and tinted like other build pieces, but a plant
/// replaces itself when it grows: the sapling is removed and a new tree (or ripe crop) is created
/// at a random vanilla size. The grown object keeps the sapling's look: its size is the grown
/// object's own size times how much the sapling was resized, and it gets the sapling's tint.
/// </summary>
internal static class PlantGrowth
{
    [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
    private static class KeepLookWhenGrownPatch
    {
        /// <summary>Records the sapling's size and tint; growing removes the sapling.</summary>
        private static void Prefix(Plant __instance, out (Vector3 Scale, Color? Tint)? __state)
        {
            __state = null;
            var view = __instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || SavedPieces.PrefabOf(view.GetZDO()) is not { } prefab)
            {
                return;
            }

            var baseScale = prefab.transform.localScale;
            var scale = __instance.transform.localScale;
            var factor = new Vector3(
                baseScale.x != 0f ? scale.x / baseScale.x : 1f,
                baseScale.y != 0f ? scale.y / baseScale.y : 1f,
                baseScale.z != 0f ? scale.z / baseScale.z : 1f);
            var tint = LandscaperTint.Read(__instance.gameObject);
            if ((factor - Vector3.one).sqrMagnitude > 0.0001f || tint is not null)
            {
                __state = (factor, tint);
            }
        }

        private static void Postfix(GameObject? __result, (Vector3 Scale, Color? Tint)? __state)
        {
            if (__result == null || __state is not { } look || __result.GetComponent<ZNetView>() is not { } view || !view.IsValid())
            {
                return;
            }

            view.SetLocalScale(Vector3.Scale(__result.transform.localScale, look.Scale));
            if (look.Tint is { } tint)
            {
                if (!__result.TryGetComponent<LandscaperTint>(out _))
                {
                    __result.AddComponent<LandscaperTint>();
                }

                LandscaperTint.Save(__result, tint);
            }
        }
    }

    /// <summary>
    /// A grown tree or crop is a vanilla world object without Landscaper's tint component, so give
    /// it one whenever an object with a saved Landscaper tint is loaded. Only tinted objects get it.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    private static class TintLoadedObjectsPatch
    {
        private static void Postfix(GameObject? __result)
        {
            if (__result != null && !__result.TryGetComponent<LandscaperTint>(out _) &&
                __result.GetComponent<ZNetView>() is { } view && view.IsValid() && LandscaperTint.HasTint(view.GetZDO()))
            {
                __result.AddComponent<LandscaperTint>();
            }
        }
    }
}
