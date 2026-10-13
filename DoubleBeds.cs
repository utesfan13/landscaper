using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// The dragon bed, and Landscaper's prop copy of it, sleep two. A vanilla bed has one owner and one
/// point to lie at, in the middle. Here a second player can claim the other side (if a ward covers
/// the bed, only players it permits): their name is saved on the bed as its partner, the bed becomes
/// their spawn point, and they sleep on it like the owner does. Once a bed has a partner, the owner
/// lies on one side and the partner on the other. The owner or the partner frees the second side
/// with the alternate use key (Shift + E).
/// Each player's game points the bed's sleeping spot at its own player's side, so the vanilla code
/// that sleeps, checks the spawn point and respawns uses the right side unchanged. Without the mod
/// the bed is the owner's alone again.
/// </summary>
internal static class DoubleBeds
{
    private static readonly int PartnerKey = "LandscaperBedPartner".GetStableHashCode();
    private static readonly int PartnerNameKey = "LandscaperBedPartnerName".GetStableHashCode();

    /// <summary>How far each side's sleeping spot is from the middle, in the bed's own units (the dragon bed is 2.3 wide).</summary>
    private const float SideOffset = 0.5f;

    private const string DragonBed = "piece_bed02";
    private const string DragonBedProp = "prop_bed02";

    private static readonly Func<Bed, Player, bool> CheckEnemies = AccessTools.MethodDelegate<Func<Bed, Player, bool>>(AccessTools.Method(typeof(Bed), "CheckEnemies"));
    private static readonly Func<Bed, Player, bool> CheckExposure = AccessTools.MethodDelegate<Func<Bed, Player, bool>>(AccessTools.Method(typeof(Bed), "CheckExposure"));
    private static readonly Func<Bed, Player, bool> CheckFire = AccessTools.MethodDelegate<Func<Bed, Player, bool>>(AccessTools.Method(typeof(Bed), "CheckFire"));
    private static readonly Func<Bed, Player, bool> CheckWet = AccessTools.MethodDelegate<Func<Bed, Player, bool>>(AccessTools.Method(typeof(Bed), "CheckWet"));

    /// <summary>The three sleeping spots of a double bed; added to the bed when it loads.</summary>
    private sealed class Sides : MonoBehaviour
    {
        public Transform Middle = null!;
        public Transform OwnerSide = null!;
        public Transform PartnerSide = null!;
        public ZNetView View = null!;
    }

    [HarmonyPatch(typeof(Bed), "Awake")]
    private static class AddSidesPatch
    {
        private static void Postfix(Bed __instance)
        {
            var view = __instance.GetComponentInParent<ZNetView>();
            if (view == null || !view.IsValid() || __instance.m_spawnPoint == null || !IsDoubleBed(view))
            {
                return;
            }

            var sides = __instance.gameObject.AddComponent<Sides>();
            sides.View = view;
            sides.Middle = __instance.m_spawnPoint;
            sides.OwnerSide = SidePoint(__instance.m_spawnPoint, view.transform, -SideOffset, "LandscaperOwnerSide");
            sides.PartnerSide = SidePoint(__instance.m_spawnPoint, view.transform, SideOffset, "LandscaperPartnerSide");
            UseLocalPlayersSide(__instance, sides);
        }
    }

    /// <summary>
    /// The vanilla test only counts the owner's bed, and respawning uses it to find the bed: without
    /// this the partner would respawn at the start, losing the bed as their spawn point.
    /// </summary>
    [HarmonyPatch(typeof(Bed), nameof(Bed.IsCurrent))]
    private static class IsCurrentPatch
    {
        private static void Prefix(Bed __instance)
        {
            if (__instance.TryGetComponent<Sides>(out var sides))
            {
                UseLocalPlayersSide(__instance, sides);
            }
        }

        private static void Postfix(Bed __instance, ref bool __result)
        {
            if (__result || !__instance.TryGetComponent<Sides>(out var sides) || !sides.View.IsValid() || Game.instance == null)
            {
                return;
            }

            var zdo = sides.View.GetZDO();
            var me = Game.instance.GetPlayerProfile().GetPlayerID();
            if (me == zdo.GetLong(ZDOVars.s_owner) || me == zdo.GetLong(PartnerKey))
            {
                __result = NearAnySpot(sides);
            }
        }
    }

    [HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText))]
    private static class HoverTextPatch
    {
        private static bool Prefix(Bed __instance, ref string __result)
        {
            if (!__instance.TryGetComponent<Sides>(out var sides) || !sides.View.IsValid() || Game.instance == null)
            {
                return true;
            }

            UseLocalPlayersSide(__instance, sides);
            var zdo = sides.View.GetZDO();
            var owner = zdo.GetLong(ZDOVars.s_owner);
            var partner = zdo.GetLong(PartnerKey);
            if (owner == 0L)
            {
                return true;
            }

            var me = Game.instance.GetPlayerProfile().GetPlayerID();
            var names = zdo.GetString(ZDOVars.s_ownerName);
            if (partner != 0L)
            {
                names += " & " + zdo.GetString(PartnerNameKey);
            }

            var text = names + "'s $piece_bed";
            const string use = "\n[<color=yellow><b>$KEY_Use</b></color>] ";
            const string altUse = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] ";
            if (me == owner || me == partner)
            {
                text += use + (IsCurrent(__instance) ? "$piece_bed_sleep" : "$piece_bed_setspawn");
                if (partner != 0L)
                {
                    text += altUse + "Free the other side";
                }
            }
            else if (partner == 0L)
            {
                text += use + "Claim the other side";
            }

            __result = Localization.instance.Localize(text);
            return false;
        }
    }

    [HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
    private static class InteractPatch
    {
        private static bool Prefix(Bed __instance, Humanoid human, bool repeat, bool alt, ref bool __result)
        {
            if (repeat || human is not Player player || !__instance.TryGetComponent<Sides>(out var sides) || !sides.View.IsValid())
            {
                return true;
            }

            UseLocalPlayersSide(__instance, sides);
            var zdo = sides.View.GetZDO();
            var me = Game.instance.GetPlayerProfile().GetPlayerID();
            var owner = zdo.GetLong(ZDOVars.s_owner);
            var partner = zdo.GetLong(PartnerKey);
            __result = false;

            if (alt && partner != 0L && (me == owner || me == partner))
            {
                SetPartner(sides, 0L, "");
                UseLocalPlayersSide(__instance, sides);
                player.Message(MessageHud.MessageType.Center, "The other side of the bed is free");
                return false;
            }

            // Claiming an unowned bed, and the owner's own use, are vanilla; the owner now just lies on
            // their side once there's a partner.
            if (owner == 0L || me == owner)
            {
                return true;
            }

            if (partner == 0L)
            {
                if (!PrivateArea.CheckAccess(__instance.transform.position))
                {
                    player.Message(MessageHud.MessageType.Center, "This bed is in someone else's ward");
                    return false;
                }

                if (!CheckExposure(__instance, player))
                {
                    return false;
                }

                SetPartner(sides, me, Game.instance.GetPlayerProfile().GetName());
                UseLocalPlayersSide(__instance, sides);
                Game.instance.GetPlayerProfile().SetCustomSpawnPoint(__instance.GetSpawnPoint());
                player.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
                return false;
            }

            if (me != partner)
            {
                // Someone else's bed, both sides taken: vanilla does nothing either.
                return false;
            }

            // The partner: the same as the vanilla owner's use.
            if (IsCurrent(__instance))
            {
                if (!EnvMan.CanSleep())
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_cantsleep");
                    return false;
                }

                if (CheckEnemies(__instance, player) && CheckExposure(__instance, player) && CheckFire(__instance, player) && CheckWet(__instance, player))
                {
                    player.AttachStart(__instance.m_spawnPoint, __instance.gameObject, hideWeapons: true, isBed: true, onShip: false, "attach_bed", new Vector3(0f, 0.5f, 0f));
                }

                return false;
            }

            if (CheckExposure(__instance, player))
            {
                Game.instance.GetPlayerProfile().SetCustomSpawnPoint(__instance.GetSpawnPoint());
                player.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
            }

            return false;
        }
    }

    private static bool IsDoubleBed(ZNetView view)
    {
        if (SavedPieces.PrefabOf(view.GetZDO()) is not { } prefab)
        {
            return false;
        }

        return prefab.name == DragonBed || Plugin.Pieces?.SourcePrefabOf(prefab.name) == DragonBedProp;
    }

    /// <summary>A sleeping spot beside the middle one, across the bed's width, as a child of the bed so it follows its size.</summary>
    private static Transform SidePoint(Transform middle, Transform root, float offset, string name)
    {
        var point = new GameObject(name).transform;
        point.SetParent(root, worldPositionStays: false);
        point.localPosition = root.InverseTransformPoint(middle.position) + new Vector3(offset, 0f, 0f);
        point.rotation = middle.rotation;
        return point;
    }

    /// <summary>Points the bed's sleeping spot at this player's side: the middle unless the bed has a partner.</summary>
    private static void UseLocalPlayersSide(Bed bed, Sides sides)
    {
        if (Game.instance == null || !sides.View.IsValid())
        {
            return;
        }

        var zdo = sides.View.GetZDO();
        var me = Game.instance.GetPlayerProfile().GetPlayerID();
        bed.m_spawnPoint = zdo.GetLong(PartnerKey) == 0L ? sides.Middle
            : me == zdo.GetLong(PartnerKey) ? sides.PartnerSide
            : me == zdo.GetLong(ZDOVars.s_owner) ? sides.OwnerSide
            : sides.Middle;
    }

    /// <summary>
    /// Whether the player's spawn point is at one of the bed's sleeping spots, within the vanilla 1 m.
    /// Any of them counts: the owner's spot moves to the side when a partner claims the other side,
    /// and back when it's freed, which on a bed resized much bigger is more than 1 m.
    /// </summary>
    private static bool NearAnySpot(Sides sides)
    {
        var spawn = Game.instance.GetPlayerProfile().GetCustomSpawnPoint();
        return Vector3.Distance(sides.Middle.position, spawn) < 1f || Vector3.Distance(sides.OwnerSide.position, spawn) < 1f ||
            Vector3.Distance(sides.PartnerSide.position, spawn) < 1f;
    }

    /// <summary>Whether this bed is the player's spawn point (Bed.IsCurrent, which counts the partner too; see IsCurrentPatch).</summary>
    private static bool IsCurrent(Bed bed) => bed.IsCurrent();

    private static void SetPartner(Sides sides, long id, string name)
    {
        // Only the bed's owner in the network sense can change saved data; take that over, as
        // nothing else about a bed depends on it.
        sides.View.ClaimOwnership();
        var zdo = sides.View.GetZDO();
        zdo.Set(PartnerKey, id);
        zdo.Set(PartnerNameKey, name);
    }
}
