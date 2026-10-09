using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Landscaper;

/// <summary>
/// Lets the size keys change the size of the Hoe's and Cultivator's ground tools (level, raise, path,
/// paved road, cultivate, replant): the width follows the size (the larger of width and depth), and
/// how far Raise ground lifts follows the height. Only the size: the ground tools don't take tint,
/// tilt or nudging.
/// The game applies a ground tool by sending its name to whoever owns that stretch of terrain, who
/// looks the settings up, so a resized one is sent with its size instead, through a message of its
/// own. The result is saved in the terrain like any other edit, so it shows without the mod too;
/// only placing it needs the mod (on whoever owns the terrain, which is nearly always the player
/// placing it).
/// </summary>
internal static class TerrainBrushes
{
    private const string ScaledOperationRpc = "LandscaperScaledTerrainOp";

    /// <summary>Limits on the brush size, so one click can't rework a whole hillside (or vanish).</summary>
    private const float MinFactor = 0.25f;
    private const float MaxFactor = 5f;

    private static readonly MethodInfo? DoOperation = AccessTools.Method(typeof(TerrainComp), "DoOperation");
    private static readonly MethodInfo CloneSettings = AccessTools.Method(typeof(object), "MemberwiseClone");

    /// <summary>The size of the ground tool being placed right now, if it's resized.</summary>
    private static (float Width, float Height)? _placing;

    /// <summary>Whether a placement ghost (or piece) is a ground tool.</summary>
    public static bool IsBrush(GameObject? gameObject) => gameObject != null && gameObject.GetComponent<TerrainOp>() != null;

    /// <summary>The brush size for a size setting: width from the larger of width and depth, height from height.</summary>
    public static (float Width, float Height) FactorsFor(Vector3 scale) =>
        (Mathf.Clamp(Mathf.Max(scale.x, scale.z), MinFactor, MaxFactor), Mathf.Clamp(scale.y, MinFactor, MaxFactor));

    /// <summary>A ground tool ghost's indicator particles as the game made them, so they can be resized again.</summary>
    private sealed class Indicator : MonoBehaviour
    {
        public float Width = 1f;
        public readonly List<(ParticleSystem System, float Size, Vector3 Size3D)> Originals = new();
    }

    /// <summary>
    /// Sizes the ring a ground tool's ghost shows to match the brush. Each particle is drawn as a
    /// whole ring, and particles ignore the ghost's own scale, so their size is scaled instead.
    /// </summary>
    public static void SizeIndicator(GameObject ghost, float width)
    {
        if (!ghost.TryGetComponent<Indicator>(out var indicator))
        {
            indicator = ghost.AddComponent<Indicator>();
            foreach (var system in ghost.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
            {
                var main = system.main;
                indicator.Originals.Add((system, main.startSizeMultiplier,
                    new Vector3(main.startSizeXMultiplier, main.startSizeYMultiplier, main.startSizeZMultiplier)));
            }
        }

        if (Mathf.Abs(indicator.Width - width) < 0.001f)
        {
            return;
        }

        indicator.Width = width;
        foreach (var (system, size, size3D) in indicator.Originals)
        {
            if (system == null)
            {
                continue;
            }

            var main = system.main;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier = size3D.x * width;
                main.startSizeYMultiplier = size3D.y * width;
                main.startSizeZMultiplier = size3D.z * width;
            }
            else
            {
                main.startSizeMultiplier = size * width;
            }

            // Rings already shown keep the size they were made with; restart the effect, so they're
            // replaced straight away (including an effect that only shows its ring once, as it starts).
            system.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(withChildren: false);
        }
    }

    /// <summary>Scales a brush's settings in place.</summary>
    private static void Scale(TerrainOp.Settings settings, float width, float height)
    {
        settings.m_levelRadius *= width;
        settings.m_raiseRadius *= width;
        settings.m_smoothRadius *= width;
        settings.m_paintRadius *= width;
        settings.m_raiseDelta *= height;
    }

    /// <summary>
    /// A bigger brush reaches further than the spot it's placed on, which the game's ward check
    /// doesn't cover: refuse a resized one that would reach into a ward the player can't use.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    private static class WardPatch
    {
        private static bool Prefix(Player __instance, Piece piece, GameObject ___m_placementGhost, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || !Plugin.ModEnabled() || piece == null || ___m_placementGhost == null ||
                !piece.TryGetComponent<TerrainOp>(out var tool))
            {
                return true;
            }

            var (width, _) = FactorsFor(ScaleController.CurrentScale);
            if (width <= 1f || PrivateArea.CheckAccess(___m_placementGhost.transform.position, tool.GetRadius() * width))
            {
                return true;
            }

            __instance.Message(MessageHud.MessageType.Center, "A brush this size would reach into a ward you can't use");
            __result = false;
            return false;
        }
    }

    /// <summary>Notes the size while the local player places a resized ground tool.</summary>
    [HarmonyPatch(typeof(Player), "PlacePiece")]
    private static class PlacePatch
    {
        private static void Prefix(Player __instance, Piece piece)
        {
            _placing = null;
            if (__instance != Player.m_localPlayer || !Plugin.ModEnabled() || piece == null || !IsBrush(piece.gameObject))
            {
                return;
            }

            var factors = FactorsFor(ScaleController.CurrentScale);
            if (Mathf.Abs(factors.Width - 1f) > 0.001f || Mathf.Abs(factors.Height - 1f) > 0.001f)
            {
                _placing = factors;
            }
        }

        private static void Postfix() => _placing = null;
    }

    /// <summary>The placed ground tool applies itself as it's created; resize it first.</summary>
    [HarmonyPatch(typeof(TerrainOp), "Awake")]
    private static class ResizePlacedBrushPatch
    {
        private static void Prefix(TerrainOp __instance)
        {
            if (_placing is not { } size || TerrainOp.m_forceDisableTerrainOps)
            {
                return;
            }

            Scale(__instance.m_settings, size.Width, size.Height);
            var marker = __instance.gameObject.AddComponent<ResizedBrush>();
            marker.Width = size.Width;
            marker.Height = size.Height;
        }
    }

    /// <summary>On a placed ground tool that was resized: its size, for the message to the terrain's owner.</summary>
    private sealed class ResizedBrush : MonoBehaviour
    {
        public float Width;
        public float Height;
    }

    /// <summary>Sends a resized ground tool's edit with its size, instead of only its name.</summary>
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.ApplyOperation))]
    private static class SendWithSizePatch
    {
        private static bool Prefix(TerrainComp __instance, TerrainOp modifier)
        {
            if (!modifier.TryGetComponent<ResizedBrush>(out var size) || !__instance.TryGetComponent<ZNetView>(out var view) || !view.IsValid())
            {
                return true;
            }

            var package = new ZPackage();
            package.Write(modifier.transform.position);
            package.Write(modifier.m_settings.m_rotation);
            if (modifier.m_settings.m_rotation)
            {
                package.Write(modifier.transform.forward);
            }

            package.Write(Utils.GetPrefabName(modifier.gameObject.name).GetStableHashCode());
            package.Write(size.Width);
            package.Write(size.Height);
            view.InvokeRPC(ScaledOperationRpc, package);
            return false;
        }
    }

    [HarmonyPatch(typeof(TerrainComp), "Awake")]
    private static class RegisterPatch
    {
        private static void Postfix(TerrainComp __instance)
        {
            if (__instance.TryGetComponent<ZNetView>(out var view) && view.GetZDO() != null)
            {
                view.Register<ZPackage>(ScaledOperationRpc, (_, package) => ApplyResized(__instance, view, package));
            }
        }
    }

    /// <summary>The terrain's owner applies a resized ground tool: the tool's own settings, resized.</summary>
    private static void ApplyResized(TerrainComp terrain, ZNetView view, ZPackage package)
    {
        if (!view.IsOwner() || DoOperation == null || ObjectDB.instance == null)
        {
            return;
        }

        var position = package.ReadVector3();
        var rotation = package.ReadBool() ? package.ReadVector3() : Vector3.zero;
        var hash = package.ReadInt();
        var width = Mathf.Clamp(package.ReadSingle(), MinFactor, MaxFactor);
        var height = Mathf.Clamp(package.ReadSingle(), MinFactor, MaxFactor);
        if (!ObjectDB.instance.TryGetTerrainOp(hash, out var tool))
        {
            Plugin.Log.LogWarning($"Unknown ground tool {hash} in a resized ground edit; skipped.");
            return;
        }

        var settings = (TerrainOp.Settings)CloneSettings.Invoke(tool.m_settings, null);
        Scale(settings, width, height);
        DoOperation.Invoke(terrain, new object[] { position, rotation, settings });
    }
}
