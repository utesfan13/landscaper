using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>The names placed pieces are known by: aliases for other tools and former names, and invisible stand-ins for pieces that can't be registered.</summary>
public sealed partial class DecorativePieceManager
{
    /// <summary>Other saved names mapped to registered pieces, by name hash; see RegisterAliases.</summary>
    private readonly Dictionary<int, string> _aliases = new();

    /// <summary>A plain entry, for the names Landscaper copies of vanilla pieces were saved under.</summary>
    private static readonly DecorativePieceDefinition PlainDefinition = new();

    /// <summary>Saved names kept loading by invisible stand-ins; see KeepPlacedPieces.</summary>
    private readonly SortedSet<string> _standIns = new(StringComparer.OrdinalIgnoreCase);

    private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedPrefabs =
        AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");

    /// <summary>Saved names kept loading by invisible stand-ins because their piece couldn't be registered.</summary>
    public IReadOnlyCollection<string> StandIns => _standIns;

    /// <summary>How many other saved names are mapped to registered pieces.</summary>
    public int AliasCount => _aliases.Count;

    /// <summary>
    /// A placed piece is saved under its prefab's name, and a host deletes saved objects whose prefab
    /// is missing. The name includes the tool, so moving a piece to another tool would lose every
    /// copy placed with the old one. So each piece is also registered under its name for the other
    /// tools, and under any former names from the catalog, unless another piece has that name. Copies
    /// placed under those names load as this piece. Runs whenever the world's prefabs are set up.
    /// </summary>
    private void RegisterAliases()
    {
        var scene = ZNetScene.instance;
        if (scene == null)
        {
            return;
        }

        var named = NamedPrefabs(scene);
        _aliases.Clear();
        foreach (var piece in _registered.Values.OrderBy(piece => piece.IsVariant))
        {
            if (piece.Clone == null)
            {
                continue;
            }

            var names = ((BuildTool[])Enum.GetValues(typeof(BuildTool)))
                .Where(tool => tool != piece.Tool)
                .Select(tool => GetCloneName(piece.SourcePrefab, piece.Definition, tool))
                .Concat(piece.Definition.FormerNames);
            foreach (var name in names)
            {
                var hash = name.GetStableHashCode();
                if (!named.ContainsKey(hash))
                {
                    named[hash] = piece.Clone;
                    _aliases[hash] = name;
                }
            }
        }

        // Earlier versions had Landscaper copies of vanilla build pieces; those load as the vanilla
        // piece now, and LandscaperTint saves them as it, so they no longer depend on this mod.
        foreach (var prefab in _adjustableVanilla.Where(prefab => prefab != null))
        {
            foreach (BuildTool tool in Enum.GetValues(typeof(BuildTool)))
            {
                var name = GetCloneName(prefab.name, PlainDefinition, tool);
                var hash = name.GetStableHashCode();
                if (!named.ContainsKey(hash))
                {
                    named[hash] = prefab;
                    _aliases[hash] = name;
                }
            }
        }
    }

    /// <summary>Frees a name taken by an alias, so a piece registered under it now can have it.</summary>
    private void ReleaseAlias(string cloneName)
    {
        var hash = cloneName.GetStableHashCode();
        if (_aliases.Remove(hash) && ZNetScene.instance != null)
        {
            NamedPrefabs(ZNetScene.instance).Remove(hash);
        }
    }

    /// <summary>
    /// When a piece can't be registered (its vanilla prefab was renamed or removed by a game update,
    /// or setting it up failed), makes sure a networked prefab with its saved name still exists.
    /// Otherwise a host would delete every copy placed in the world when it loads. The stand-in is
    /// invisible and has no collider, and the placed copies come back once the piece registers again.
    /// </summary>
    private void KeepPlacedPieces(string cloneName, string reason)
    {
        var scene = ZNetScene.instance;
        if (scene == null || _registered.ContainsKey(cloneName))
        {
            return;
        }

        _standIns.Add(cloneName);
        ReleaseAlias(cloneName);
        if (scene.GetPrefab(cloneName) != null)
        {
            return;
        }

        var prefab = PrefabManager.Instance.GetPrefab(cloneName);
        if (prefab == null || prefab.GetComponent<ZNetView>() == null)
        {
            prefab = PrefabManager.Instance.CreateEmptyPrefab(cloneName);
            if (prefab == null)
            {
                return;
            }

            // CreateEmptyPrefab makes a cube; keep only the transform and ZNetView.
            UnityEngine.Object.DestroyImmediate(prefab.GetComponent<Collider>());
            UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
            UnityEngine.Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());
        }

        // Keep the placed copies saved, with their size.
        var view = prefab.GetComponent<ZNetView>();
        view.m_persistent = true;
        view.m_syncInitialScale = true;
        // Added to Jotunn too, so it's set up again when the next world loads.
        if (PrefabManager.Instance.GetPrefab(cloneName) == null)
        {
            PrefabManager.Instance.AddPrefab(prefab);
        }

        PrefabManager.Instance.RegisterToZNetScene(prefab);
        _log.LogWarning($"Keeping placed '{cloneName}' pieces as invisible stand-ins because {reason}; they come back once it registers again.");
    }
}
