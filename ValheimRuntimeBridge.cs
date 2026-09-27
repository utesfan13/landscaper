using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

public static class ValheimRuntimeBridge
{
    private static readonly Dictionary<string, GameObject> BuildPrefabClones = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> RegisteredCloneNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedMissingPrefabs = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] KnownAssemblyNames =
    {
        "assembly_valheim",
        "Assembly-CSharp"
    };

    public static Type? ResolveType(string typeName)
    {
        foreach (var assemblyName in KnownAssemblyNames)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);

            if (assembly is null)
            {
                continue;
            }

            var type = assembly.GetType(typeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    public static bool TryGetObjectDbInstance(out object? instance)
    {
        instance = null;

        var objectDbType = ResolveType("ObjectDB");
        if (objectDbType is null)
        {
            return false;
        }

        var property = objectDbType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
        if (property is null)
        {
            return false;
        }

        instance = property.GetValue(null, null);
        return instance is not null;
    }

    public static bool TryResolvePrefabName(string prefabName, out GameObject? prefab)
    {
        prefab = null;

        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return false;
        }

        try
        {
            prefab = PrefabManager.Instance.GetPrefab(prefabName)
                ?? PrefabManager.Instance.CreateClonedPrefab($"Landscaper_Source_{prefabName}", prefabName);
            if (prefab is not null)
            {
                return true;
            }
        }
        catch (Exception exception)
        {
            if (LoggedMissingPrefabs.Add(prefabName))
            {
                System.Console.WriteLine($"[Landscaper] Jotunn could not resolve '{prefabName}': {exception.Message}");
            }
        }

        if (!TryGetObjectDbInstance(out var objectDb))
        {
            return false;
        }

        var method = objectDb!.GetType().GetMethod("GetItemPrefab", new[] { typeof(string) });
        if (method is null)
        {
            return false;
        }

        prefab = method.Invoke(objectDb, new object[] { prefabName }) as GameObject;
        if (prefab is not null)
        {
            return true;
        }

        if (global::ZNetScene.instance is not null)
        {
            prefab = global::ZNetScene.instance.GetPrefab(prefabName);
            if (prefab is not null)
            {
                return true;
            }

            prefab = global::ZNetScene.instance.m_prefabs.FirstOrDefault(candidate =>
                candidate is not null && string.Equals(candidate.name, prefabName, StringComparison.OrdinalIgnoreCase));
            if (prefab is not null)
            {
                return true;
            }

            if (LoggedMissingPrefabs.Add(prefabName))
            {
                var matches = global::ZNetScene.instance.m_prefabs
                    .Where(candidate => candidate is not null && candidate.name.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(candidate => candidate.name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .Take(40)
                    .ToList();
                System.Console.WriteLine($"[Landscaper] No exact runtime prefab '{prefabName}'. Matching names: {string.Join(", ", matches)}");
            }
        }

        try
        {
            prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab is null)
            {
                prefab = PrefabManager.Instance.CreateClonedPrefab($"Landscaper_Source_{prefabName}", prefabName);
            }
        }
        catch (Exception exception)
        {
            System.Console.WriteLine($"[Landscaper] Jotunn prefab lookup failed for '{prefabName}': {exception.Message}");
        }

        return prefab is not null;
    }

    public static bool TryEnsurePieceComponent(GameObject prefab, out Component? piece)
    {
        piece = null;

        if (prefab is null)
        {
            return false;
        }

        var pieceType = ResolveType("Piece");
        if (pieceType is null)
        {
            return false;
        }

        piece = prefab.GetComponent(pieceType);
        if (piece is null)
        {
            piece = prefab.AddComponent(pieceType);
        }

        return piece is not null;
    }

    public static bool TryRegisterWithJotunn(GameObject prefab, string displayName, string category, BuildTool tool, Vector3 placementRotation)
    {
        if (prefab is null)
        {
            return false;
        }

        var tableName = tool switch
        {
            BuildTool.Cultivator => "Cultivator",
            BuildTool.Hoe => "Hoe",
            BuildTool.Hammer => "Hammer",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(tableName))
        {
            return false;
        }

        var cloneName = $"Landscaper_{prefab.name}_{tool}";
        if (RegisteredCloneNames.Contains(cloneName))
        {
            System.Console.WriteLine($"[Landscaper] Skipping '{displayName}': '{prefab.name}' is already registered for {tableName} by another entry.");
            return false;
        }

        if (!BuildPrefabClones.TryGetValue(cloneName, out var buildPrefab) || buildPrefab is null)
        {
            buildPrefab = PrefabManager.Instance.CreateClonedPrefab(cloneName, prefab);
            if (buildPrefab is null)
            {
                System.Console.WriteLine($"[Landscaper] Could not clone '{prefab.name}' for building.");
                return false;
            }

            BuildPrefabClones[cloneName] = buildPrefab;
        }

        buildPrefab.transform.localRotation = Quaternion.Euler(placementRotation);
        DisableWorldOnlyBehaviours(buildPrefab);

        if (!TryEnsurePieceComponent(buildPrefab, out var pieceComponent) || pieceComponent is not global::Piece runtimePiece)
        {
            System.Console.WriteLine($"[Landscaper] Could not prepare Piece component for '{buildPrefab.name}'.");
            return false;
        }

        runtimePiece.m_name = string.IsNullOrWhiteSpace(displayName) ? prefab.name : displayName;
        runtimePiece.m_description = displayName;
        runtimePiece.m_enabled = true;
        runtimePiece.m_groundPiece = true;
        runtimePiece.m_groundOnly = false;
        runtimePiece.m_canBeRemoved = true;
        runtimePiece.m_canRotate = true;
        runtimePiece.m_canRockJade = true;
        runtimePiece.m_resources = Array.Empty<global::Piece.Requirement>();
        runtimePiece.m_icon ??= FindFallbackPieceIcon(tool);

        if (buildPrefab.GetComponent<global::ZNetView>() is null)
        {
            buildPrefab.AddComponent<global::ZNetView>();
        }

        if (runtimePiece.m_icon is null)
        {
            System.Console.WriteLine($"[Landscaper] Could not find an icon for '{prefab.name}'.");
            return false;
        }

        var customPiece = new CustomPiece(buildPrefab, tableName, fixReference: true)
        {
            Category = category
        };

        var registered = PieceManager.Instance.AddPiece(customPiece) || PieceManager.Instance.GetPiece(cloneName) is not null;
        if (registered)
        {
            RegisteredCloneNames.Add(cloneName);
            PieceManager.Instance.RegisterPieceInPieceTable(buildPrefab, tableName, category);
            var piece = buildPrefab.GetComponent<global::Piece>();
            if (piece is not null && !string.IsNullOrWhiteSpace(displayName))
            {
                piece.m_name = displayName;
            }

            TryUnlockPieceForLocalPlayer(piece);
            var hasZNetView = buildPrefab.GetComponent<global::ZNetView>() is not null;
            System.Console.WriteLine($"[Landscaper] Jotunn accepted '{displayName}' for {tableName}; free placement enabled, resources={runtimePiece.m_resources.Length}, canRockJade={runtimePiece.m_canRockJade}, hasZNetView={hasZNetView}.");
        }

        return registered;
    }

    private static void DisableWorldOnlyBehaviours(GameObject prefab)
    {
        foreach (var tree in prefab.GetComponentsInChildren<global::TreeBase>(includeInactive: true))
        {
            tree.enabled = false;
        }

        foreach (var pickable in prefab.GetComponentsInChildren<global::Pickable>(includeInactive: true))
        {
            pickable.enabled = false;
        }

        // LodFadeInOut hides the LODGroup in Awake when spawned more than 20m from the camera and
        // restores it 0.1-0.3s later. Placement ghosts spawn at the prefab origin, so a ghost that
        // is recreated before the fade-in runs stays invisible.
        foreach (var lodFade in prefab.GetComponentsInChildren<global::LodFadeInOut>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(lodFade);
        }
    }

    public static void TryUnlockPieceForLocalPlayer(global::Piece? piece)
    {
        if (piece is null || global::Player.m_localPlayer is null)
        {
            return;
        }

        var knownRecipesField = typeof(global::Player).GetField(
            "m_knownRecipes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (knownRecipesField?.GetValue(global::Player.m_localPlayer) is HashSet<string> knownRecipes)
        {
            knownRecipes.Add(piece.m_name);
        }

        var addKnownPiece = typeof(global::Player).GetMethod(
            "AddKnownPiece",
            BindingFlags.Instance | BindingFlags.NonPublic);
        addKnownPiece?.Invoke(global::Player.m_localPlayer, new object[] { piece });
    }

    public static void RefreshKnownPieces()
    {
        foreach (var prefab in BuildPrefabClones.Values)
        {
            TryUnlockPieceForLocalPlayer(prefab.GetComponent<global::Piece>());
        }
    }

    private static Sprite? FindFallbackPieceIcon(BuildTool tool)
    {
        if (global::ObjectDB.instance is null)
        {
            return null;
        }

        var toolName = tool.ToString();

        foreach (var item in global::ObjectDB.instance.m_items)
        {
            if (item is null)
            {
                continue;
            }

            var itemDrop = item.GetComponent<global::ItemDrop>();
            var itemName = itemDrop?.m_itemData?.m_shared?.m_name ?? item.name ?? string.Empty;
            if (itemName.IndexOf(toolName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var icons = itemDrop?.m_itemData?.m_shared?.m_icons;
            if (icons is not null)
            {
                var icon = icons.FirstOrDefault(candidate => candidate is not null);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }

        foreach (var piece in global::ObjectDB.instance.GetAllBuildPieces(includeHidden: true))
        {
            if (piece is not null && piece.m_icon is not null)
            {
                return piece.m_icon;
            }
        }

        return null;
    }

    public static List<string> GetPrefabNameCandidates(string keyword = "")
    {
        var items = new List<string>();

        if (global::ZNetScene.instance is not null)
        {
            items.AddRange(global::ZNetScene.instance.m_prefabs
                .Where(prefab => prefab is not null)
                .Select(prefab => prefab.name)
                .Where(name => string.IsNullOrEmpty(keyword) || name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        if (!TryGetObjectDbInstance(out var objectDb))
        {
            return items.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
        }

        var allPieces = objectDb!.GetType().GetMethod("GetAllBuildPieces");
        if (allPieces is not null)
        {
            var result = allPieces.Invoke(objectDb, new object[] { true });
            if (result is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is GameObject go && go != null)
                    {
                        if (string.IsNullOrEmpty(keyword) || go.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            items.Add(go.name);
                        }
                    }
                    else if (item is Component component && component.gameObject != null)
                    {
                        var name = component.gameObject.name;
                        if (string.IsNullOrEmpty(keyword) || name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            items.Add(name);
                        }
                    }
                }
            }
        }

        return items
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }
}
