using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Makes Valheim's simulated water (LiquidVolume) work as a Landscaper piece:
/// <list type="bullet">
/// <item>Each placement pours a small, configurable amount (1 m³ by default, against Valheim's
/// 500 m³), times the combined scale set with the scale keys, so ponds are filled in steps.</item>
/// <item>Every pond is its own simulation that only sees the terrain, so a second pond in the same
/// hollow would settle to the same level instead of adding to it. Placing pond water inside an
/// existing pond therefore pours its water into that pond and removes the new one.</item>
/// <item>A pond's collider sits 2 m below its surface, under the ground in a shallow pond, so the
/// remove button can't hit it. Aiming at underwater ground in a pond removes that pond.</item>
/// </list>
/// </summary>
internal static class PondWater
{
    private static ConfigEntry<float> _volume = null!;

    /// <summary>The pond water prefabs, so a changed volume setting can be applied to them.</summary>
    private static readonly List<LiquidVolume> Prefabs = new();

    public static void Bind(ConfigFile config)
    {
        _volume = config.Bind("Water", "PondVolume", 1f, new ConfigDescription(
            "Cubic meters of water each Pond Water placement pours at normal scale; the scale keys multiply it while placing. " +
            "Place more in the same pond to fill it further.",
            new AcceptableValueRange<float>(0.1f, 500f)));
        _volume.SettingChanged += (_, _) => Prefabs.ForEach(ApplyVolume);
    }

    /// <summary>
    /// LiquidVolume stores water as a depth per grid cell, and a new pond spreads m_initialVolume
    /// over its starting cells, so the volume in cubic meters is that total times each cell's area
    /// (m_scale squared).
    /// </summary>
    private static void ApplyVolume(LiquidVolume liquid)
    {
        if (liquid != null)
        {
            liquid.m_initialVolume = CurrentVolume / Mathf.Max(0.01f, liquid.m_scale * liquid.m_scale);
        }
    }

    /// <summary>Cubic meters the next placement pours: the setting times the scale keys' factor.</summary>
    public static float CurrentVolume => Mathf.Clamp(_volume.Value * ScaleController.WaterFactor, 0.01f, 5000f);

    /// <summary>Updates the pond prefabs to <see cref="CurrentVolume"/>. Call while placing a pond.</summary>
    public static void RefreshVolume() => Prefabs.ForEach(ApplyVolume);

    private static readonly AccessTools.FieldRef<LiquidVolume, List<float>> Depths =
        AccessTools.FieldRefAccess<LiquidVolume, List<float>>("m_depths");
    private static readonly AccessTools.FieldRef<LiquidVolume, bool> NeedsSaving =
        AccessTools.FieldRefAccess<LiquidVolume, bool>("m_needsSaving");
    private static readonly AccessTools.FieldRef<LiquidVolume, object> MeshDataLock =
        AccessTools.FieldRefAccess<LiquidVolume, object>("m_meshDataLock");
    private static readonly Func<LiquidVolume, Vector3, Vector2> WorldToLocal =
        AccessTools.MethodDelegate<Func<LiquidVolume, Vector3, Vector2>>(AccessTools.Method(typeof(LiquidVolume), "WorldToLocal"));

    /// <summary>Call when registering a clone.</summary>
    public static void Prepare(GameObject clone)
    {
        if (clone.GetComponent<LiquidVolume>() is { } liquid)
        {
            Prefabs.Add(liquid);
            ApplyVolume(liquid);
        }
    }

    /// <summary>
    /// If the remove button is aimed at underwater ground in a Landscaper pond, removes that pond and
    /// returns true. Returns false when it hits a piece, so normal removal handles those.
    /// </summary>
    public static bool TryRemoveAimedPond(Player player, int rayMask)
    {
        var camera = GameCamera.instance.transform;
        if (!Physics.Raycast(camera.position, camera.forward, out var hit, 50f, rayMask) ||
            Vector3.Distance(hit.point, player.m_eye.position) >= player.m_maxPlaceDistance ||
            hit.collider.GetComponentInParent<Piece>() != null)
        {
            return false;
        }

        var pond = PondsAround(hit.point).FirstOrDefault(liquid => liquid.GetSurface(hit.point) > hit.point.y + 0.05f);
        if (pond == null || !PrivateArea.CheckAccess(pond.transform.position))
        {
            return false;
        }

        var view = pond.GetComponent<ZNetView>();
        var piece = pond.GetComponent<Piece>();
        if (view == null || !view.IsValid() || piece == null)
        {
            return false;
        }

        view.ClaimOwnership();
        piece.DropResources();
        ZNetScene.instance.Destroy(pond.gameObject);
        return true;
    }

    /// <summary>Landscaper ponds whose area contains the point, nearest first.</summary>
    private static IEnumerable<LiquidVolume> PondsAround(Vector3 point, GameObject? except = null)
    {
        var pieces = new List<Piece>();
        Piece.GetAllPiecesInRadius(point, 64f, pieces);
        return pieces
            .Where(piece => piece != null && piece.gameObject != except && PlacementInput.IsLandscaperPiece(piece.gameObject))
            .Select(piece => piece.GetComponent<LiquidVolume>())
            .Where(liquid => liquid != null && Contains(liquid, point))
            .OrderBy(liquid => Vector3.Distance(liquid.transform.position, point));
    }

    private static bool Contains(LiquidVolume liquid, Vector3 point)
    {
        var local = WorldToLocal(liquid, point);
        return local.x >= 0f && local.y >= 0f && local.x <= liquid.m_width && local.y <= liquid.m_width;
    }

    /// <summary>Piece.SetCreator runs on a piece right after the local player places it.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    private static class MergeIntoExistingPondPatch
    {
        private static void Postfix(Piece __instance)
        {
            var placed = __instance.GetComponent<LiquidVolume>();
            if (placed == null || !PlacementInput.IsLandscaperPiece(__instance.gameObject))
            {
                return;
            }

            var pond = PondsAround(__instance.transform.position, __instance.gameObject).FirstOrDefault();
            var pondView = pond != null ? pond.GetComponent<ZNetView>() : null;
            if (pond == null || pondView == null || !pondView.IsValid())
            {
                return;
            }

            // Only the owner simulates and saves a pond, and the others reload what it saves.
            pondView.ClaimOwnership();
            AddWater(pond, __instance.transform.position, placed.m_initialVolume, placed.m_initialArea);

            var placedView = __instance.GetComponent<ZNetView>();
            if (placedView != null && placedView.IsValid())
            {
                ZNetScene.instance.Destroy(__instance.gameObject);
            }
        }

        /// <summary>
        /// Adds volume to the grid cells around the point the same way a new pond starts: spread
        /// evenly over an area-by-area block of cells. The simulation empties the outermost ring of
        /// cells every step, so the block is kept inside it, and the whole volume goes into the cells
        /// that remain.
        /// </summary>
        private static void AddWater(LiquidVolume pond, Vector3 point, float volume, int area)
        {
            var local = WorldToLocal(pond, point);
            var size = pond.m_width + 1;
            area = Mathf.Clamp(area, 1, pond.m_width - 1);
            var startX = Mathf.Clamp(Mathf.RoundToInt(local.x) - area / 2, 1, pond.m_width - area);
            var startY = Mathf.Clamp(Mathf.RoundToInt(local.y) - area / 2, 1, pond.m_width - area);
            var perCell = volume / (area * area);

            lock (MeshDataLock(pond))
            {
                var depths = Depths(pond);
                for (var y = startY; y < startY + area; y++)
                {
                    for (var x = startX; x < startX + area; x++)
                    {
                        depths[y * size + x] += perCell;
                    }
                }

                NeedsSaving(pond) = true;
            }
        }
    }
}
