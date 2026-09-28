using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// What breaking a placed Landscaper piece gives. Building a piece shouldn't turn its cost into
/// something better, so:
/// <list type="bullet">
/// <item>If everything it naturally drops is what it cost, plus harmless byproducts such as resin
/// and seeds, it drops as normal. A rock bought with stone gives stone and a beech bought with wood
/// gives wood, bonus included.</item>
/// <item>Otherwise (a copper deposit bought with stone, a birch that gives fine wood, a crate that
/// drops loot, or anything that drops nothing) its own drops, fragments and fallen logs are
/// skipped and it gives back exactly its build cost once it's fully broken.</item>
/// </list>
/// Hammer building pieces (with WearNTear) already give back their cost when they break, and picking
/// is untouched: spending a few berries to plant a bush that keeps growing more is intended.
/// </summary>
internal static class BreakRefund
{
    /// <summary>Drops that are fine alongside any cost: small byproducts of trees.</summary>
    private static readonly HashSet<string> Byproducts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resin", "BeechSeeds", "BirchSeeds", "FirCone", "PineCone", "Acorn"
    };

    private static readonly DropTable NoDrops = new();

    /// <summary>Natural drops of each Landscaper prefab, found the first time one is broken.</summary>
    private static readonly Dictionary<int, HashSet<string>> NaturalDrops = new();

    /// <summary>The Landscaper piece <paramref name="component"/> belongs to, if breaking it should
    /// give back the build cost instead of its natural drops.</summary>
    private static Piece? RefundOnlyPiece(Component component)
    {
        var piece = component.GetComponentInParent<Piece>();
        if (piece == null || !PlacementInput.IsLandscaperPiece(piece.gameObject))
        {
            return null;
        }

        var view = piece.GetComponent<ZNetView>();
        if (view == null || !view.IsValid())
        {
            return null;
        }

        var drops = DropsOf(view.GetZDO().GetPrefab());
        if (drops.Count == 0)
        {
            return piece;
        }

        foreach (var drop in drops)
        {
            if (!Byproducts.Contains(drop) && !piece.m_resources.Any(requirement =>
                    requirement.m_resItem != null && string.Equals(requirement.m_resItem.name, drop, StringComparison.OrdinalIgnoreCase)))
            {
                return piece;
            }
        }

        return null;
    }

    private static HashSet<string> DropsOf(int prefabHash)
    {
        if (NaturalDrops.TryGetValue(prefabHash, out var drops))
        {
            return drops;
        }

        drops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefab = ZNetScene.instance?.GetPrefab(prefabHash);
        if (prefab != null)
        {
            CollectDrops(prefab, drops, new HashSet<GameObject>());
        }

        NaturalDrops[prefabHash] = drops;
        Plugin.Log.LogDebug($"{(prefab != null ? prefab.name : prefabHash.ToString())} drops: {(drops.Count == 0 ? "nothing" : string.Join(", ", drops))}");
        return drops;
    }

    /// <summary>Everything breaking <paramref name="prefab"/> can drop, including from the fragments
    /// and logs it turns into.</summary>
    private static void CollectDrops(GameObject? prefab, HashSet<string> drops, HashSet<GameObject> visited)
    {
        if (prefab == null || !visited.Add(prefab))
        {
            return;
        }

        void Add(DropTable? table)
        {
            foreach (var drop in table?.m_drops ?? Enumerable.Empty<DropTable.DropData>())
            {
                if (drop.m_item != null)
                {
                    drops.Add(drop.m_item.name);
                }
            }
        }

        foreach (var dropper in prefab.GetComponentsInChildren<DropOnDestroyed>(includeInactive: true))
        {
            Add(dropper.m_dropWhenDestroyed);
        }

        foreach (var rock in prefab.GetComponentsInChildren<MineRock>(includeInactive: true))
        {
            Add(rock.m_dropItems);
        }

        foreach (var rock in prefab.GetComponentsInChildren<MineRock5>(includeInactive: true))
        {
            Add(rock.m_dropItems);
        }

        foreach (var tree in prefab.GetComponentsInChildren<TreeBase>(includeInactive: true))
        {
            Add(tree.m_dropWhenDestroyed);
            CollectDrops(tree.m_logPrefab, drops, visited);
            CollectDrops(tree.m_stubPrefab, drops, visited);
        }

        foreach (var log in prefab.GetComponentsInChildren<TreeLog>(includeInactive: true))
        {
            Add(log.m_dropWhenDestroyed);
            CollectDrops(log.m_subLogPrefab, drops, visited);
        }

        foreach (var destructible in prefab.GetComponentsInChildren<Destructible>(includeInactive: true))
        {
            CollectDrops(destructible.m_spawnWhenDestroyed, drops, visited);
        }
    }

    /// <summary>Drops the build cost if the piece's network object went away during the hit.</summary>
    private static void RefundIfDestroyed(Piece? piece, ZNetView? view, HitData? hit)
    {
        if (piece != null && view != null && !view.IsValid())
        {
            piece.DropResources(hit);
        }
    }

    // Props, barrels, small rocks and the like break in one go. Skip what they turn into (such as
    // an ore deposit's minable fragments) and give back the cost instead.
    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
    private static class DestructiblePatch
    {
        private static void Prefix(Destructible __instance, HitData? hit)
        {
            if (RefundOnlyPiece(__instance) is { } piece)
            {
                __instance.m_spawnWhenDestroyed = null;
                piece.DropResources(hit);
            }
        }
    }

    // Loot and other drops of destructibles, and of building pieces that have their own drops.
    [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
    private static class DropOnDestroyedPatch
    {
        private static bool Prefix(DropOnDestroyed __instance) => RefundOnlyPiece(__instance) is null;
    }

    // Large rocks drop items for each part mined, and are gone when every part is.
    [HarmonyPatch(typeof(MineRock5), "DamageArea")]
    private static class MineRock5Patch
    {
        private static void Prefix(MineRock5 __instance, out (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            __state = default;
            if (RefundOnlyPiece(__instance) is { } piece && __instance.GetComponent<ZNetView>() is { } view && view.IsValid())
            {
                __state = (piece, view, __instance.m_dropItems);
                __instance.m_dropItems = NoDrops;
            }
        }

        private static void Postfix(MineRock5 __instance, HitData hit, (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            if (__state.Piece != null)
            {
                __instance.m_dropItems = __state.Drops;
                RefundIfDestroyed(__state.Piece, __state.View, hit);
            }
        }
    }

    [HarmonyPatch(typeof(MineRock), "RPC_Hit")]
    private static class MineRockPatch
    {
        private static void Prefix(MineRock __instance, out (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            __state = default;
            if (RefundOnlyPiece(__instance) is { } piece && __instance.GetComponent<ZNetView>() is { } view && view.IsValid())
            {
                __state = (piece, view, __instance.m_dropItems);
                __instance.m_dropItems = NoDrops;
            }
        }

        private static void Postfix(MineRock __instance, HitData hit, (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            if (__state.Piece != null)
            {
                __instance.m_dropItems = __state.Drops;
                RefundIfDestroyed(__state.Piece, __state.View, hit);
            }
        }
    }

    // Trees fall into logs that are chopped into wood; a refund-only tree just disappears when felled.
    [HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
    private static class TreePatch
    {
        private static void Prefix(TreeBase __instance, out (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            __state = default;
            if (RefundOnlyPiece(__instance) is { } piece && __instance.GetComponent<ZNetView>() is { } view && view.IsValid() &&
                view.GetZDO().GetFloat(ZDOVars.s_health, __instance.m_health) > 0f)
            {
                __state = (piece, view, __instance.m_dropWhenDestroyed);
                __instance.m_dropWhenDestroyed = NoDrops;
            }
        }

        private static void Postfix(TreeBase __instance, HitData hit, (Piece? Piece, ZNetView? View, DropTable? Drops) __state)
        {
            if (__state.Piece != null)
            {
                __instance.m_dropWhenDestroyed = __state.Drops!;
                RefundIfDestroyed(__state.Piece, __state.View, hit);
            }
        }
    }

    [HarmonyPatch(typeof(TreeBase), "SpawnLog")]
    private static class TreeLogPatch
    {
        private static bool Prefix(TreeBase __instance) => RefundOnlyPiece(__instance) is null;
    }
}
