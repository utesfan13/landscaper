using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// With Landscaper, protected objects ignore damage: indestructible pieces, and Landscaper trees, logs,
/// rocks and props while DecorativeOnly is on. A game without the mod doesn't know that, so protected
/// objects are also saved with a health far beyond any damage, which every game reads: there they
/// lose health without ever running out, so they don't break. Objects that stop being protected
/// (DecorativeOnly turned off) get their normal health back.
/// Landscaper storage props get the same health while they hold anything: most are made from world
/// objects that aren't chests without the mod, so breaking one there (or after uninstalling) would
/// lose its contents for good. With the mod they can still be removed, which drops the contents.
/// Only the owner changes saved data, so this runs when a piece is placed, when its owner loads it,
/// and before its owner applies damage to it (which catches the objects that weren't owned here when
/// they loaded).
/// </summary>
internal static class SavedHealth
{
    /// <summary>
    /// The protected health. Floats keep about 7 digits, so at this size any single hit (up to about
    /// 30 million) rounds away: the saved health never changes, which also means it isn't resent to
    /// other players. Kept well short of the float limit, so nothing can overflow.
    /// </summary>
    private const float Protected = 1e15f;

    /// <summary>Mineable rocks with several pieces save each piece's health under "Health" and its number.</summary>
    private static readonly string AreaHealthKey = "Health";

    private static readonly Action<MineRock5> ReloadRockHealth = AccessTools.MethodDelegate<Action<MineRock5>>(AccessTools.Method(typeof(MineRock5), "LoadHealth"));

    /// <summary>Saves the health an object should have: protected or normal.</summary>
    public static void Sync(ZNetView? view)
    {
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            return;
        }

        var zdo = view.GetZDO();
        var indestructible = IndestructibleController.IsFlagged(zdo);
        var landscaperPiece = PlacementInput.IsLandscaperPiece(view.gameObject);
        if (!indestructible && !landscaperPiece)
        {
            return;
        }

        // DecorativeOnly covers the natural objects; build pieces are only protected when indestructible.
        var storage = landscaperPiece && HoldsItems(zdo);
        var natural = indestructible || storage || (landscaperPiece && DecorativeGuard.Enabled());
        if (view.TryGetComponent<WearNTear>(out var piece))
        {
            SyncSingle(zdo, indestructible || storage, piece.m_health);
        }

        if (view.TryGetComponent<TreeBase>(out var tree))
        {
            SyncSingle(zdo, natural, MineHealth(tree.m_health));
        }

        if (view.TryGetComponent<TreeLog>(out var log))
        {
            SyncSingle(zdo, natural, MineHealth(log.m_health));
        }

        if (view.TryGetComponent<Destructible>(out var destructible))
        {
            SyncSingle(zdo, natural, MineHealth(destructible.m_health));
        }

        // A rock's pieces are counted on the game object it's saved as, which is what a game without
        // the mod loads.
        var saved = ZNetScene.instance?.GetPrefab(zdo.GetPrefab());
        if (view.TryGetComponent<MineRock5>(out var rock5) && saved != null && saved.GetComponent<MineRock5>() != null)
        {
            SyncRock5(zdo, natural, saved.GetComponentsInChildren<Collider>().Length, rock5);
        }

        if (view.TryGetComponent<MineRock>(out var rock) && saved != null && saved.GetComponent<MineRock>() is { } savedRock)
        {
            var areas = savedRock.m_areaRoot != null ? savedRock.m_areaRoot.GetComponentsInChildren<Collider>() : saved.GetComponentsInChildren<Collider>();
            SyncRock(zdo, natural, areas.Length, MineHealth(rock.m_health));
        }
    }

    /// <summary>
    /// Whether a chest's saved contents hold any items. Read from the saved data rather than the
    /// chest, which only loads its contents a moment after the object is created.
    /// </summary>
    private static bool HoldsItems(ZDO zdo)
    {
        var saved = zdo.GetByteArray(ZDOVars.s_items);
        if (saved == null || saved.Length < 6)
        {
            return false;
        }

        try
        {
            // The inventory format: its version, then the number of items (smaller in older versions).
            var package = new ZPackage(saved);
            var version = package.ReadInt();
            return (version >= 108 ? package.ReadUShort() : package.ReadInt()) > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The health trees, logs, props and rocks start with: more in higher world levels.</summary>
    private static float MineHealth(float health) =>
        health + Game.m_worldLevel * health * (Game.instance != null ? Game.instance.m_worldLevelMineHPMultiplier : 0f);

    private static void SyncSingle(ZDO zdo, bool protect, float normal)
    {
        var health = zdo.GetFloat(ZDOVars.s_health, normal);
        if (protect && health < Protected)
        {
            zdo.Set(ZDOVars.s_health, Protected);
        }
        else if (!protect && health >= Protected)
        {
            zdo.Set(ZDOVars.s_health, normal);
        }
    }

    /// <summary>Big rocks save every piece's health together, as a count then each health.</summary>
    private static void SyncRock5(ZDO zdo, bool protect, int areas, MineRock5 rock)
    {
        var saved = zdo.GetString(ZDOVars.s_health);
        var isProtected = saved.Length > 0 && FirstAreaHealth(saved) >= Protected;
        if (protect == isProtected)
        {
            return;
        }

        if (protect)
        {
            var package = new ZPackage();
            package.Write(areas);
            for (var i = 0; i < areas; i++)
            {
                package.Write(Protected);
            }

            zdo.Set(ZDOVars.s_health, Convert.ToBase64String(package.GetArray()));
        }
        else
        {
            // Without a saved value, every piece starts at full health.
            zdo.Set(ZDOVars.s_health, "");
        }

        // The rock keeps the pieces' health in memory and saves it back after a hit; reload it.
        ReloadRockHealth(rock);
    }

    private static float FirstAreaHealth(string saved)
    {
        try
        {
            var package = new ZPackage(Convert.FromBase64String(saved));
            return package.ReadInt() > 0 ? package.ReadSingle() : 0f;
        }
        catch (Exception)
        {
            return 0f;
        }
    }

    /// <summary>Other rocks save each piece's health separately.</summary>
    private static void SyncRock(ZDO zdo, bool protect, int areas, float normal)
    {
        for (var i = 0; i < areas; i++)
        {
            var key = AreaHealthKey + i;
            var health = zdo.GetFloat(key, normal);
            if (protect && health < Protected)
            {
                zdo.Set(key, Protected);
            }
            else if (!protect && health >= Protected)
            {
                zdo.Set(key, normal);
            }
        }
    }

    /// <summary>Gives loaded objects the health they should have, such as ones placed before this was added.</summary>
    [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
    private static class SyncWhenLoadedPatch
    {
        private static void Postfix(GameObject? __result)
        {
            if (__result != null && __result.TryGetComponent<ZNetView>(out var view))
            {
                Sync(view);
            }
        }
    }

    /// <summary>A chest saves its contents whenever they change; protect it or not to match.</summary>
    [HarmonyPatch(typeof(Container), "Save")]
    private static class SyncWhenContentsChangePatch
    {
        private static void Postfix(Container __instance) =>
            Sync(__instance.m_rootObjectOverride != null ? __instance.m_rootObjectOverride.GetComponent<ZNetView>() : __instance.GetComponent<ZNetView>());
    }

    /// <summary>Damage is applied by the owner; make sure the health is right before it is.</summary>
    [HarmonyPatch]
    private static class SyncBeforeDamagePatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(TreeBase), "RPC_Damage"),
            AccessTools.Method(typeof(TreeLog), "RPC_Damage"),
            AccessTools.Method(typeof(Destructible), "RPC_Damage"),
            AccessTools.Method(typeof(MineRock), "RPC_Hit"),
            AccessTools.Method(typeof(MineRock5), "RPC_Damage")
        };

        private static void Prefix(Component __instance) => Sync(__instance.GetComponent<ZNetView>());
    }
}
