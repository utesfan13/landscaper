using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Landscaper;

public static class ValheimRuntimeBridge
{
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

    public static bool TryRegisterBuildPiece(GameObject prefab, string displayName, string category, BuildTool tool)
    {
        if (prefab is null)
        {
            return false;
        }

        if (!TryGetObjectDbInstance(out var objectDb) || objectDb is null)
        {
            return false;
        }

        var objectDbType = objectDb.GetType();
        var buildPiecesField = objectDbType.GetField("m_buildPieces", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (buildPiecesField is null || buildPiecesField.GetValue(objectDb) is not System.Collections.IList buildPieces)
        {
            return false;
        }

        if (!TryEnsurePieceComponent(prefab, out var pieceComponent) || pieceComponent is null)
        {
            return false;
        }

        var pieceType = pieceComponent.GetType();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var nameField = pieceType.GetField("m_name", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (nameField is not null && nameField.FieldType == typeof(string))
            {
                nameField.SetValue(pieceComponent, displayName);
            }
        }

        var info = category;
        var descField = pieceType.GetField("m_description", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (descField is not null && descField.FieldType == typeof(string))
        {
            descField.SetValue(pieceComponent, info);
        }

        var categoryField = pieceType.GetField("m_category", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (categoryField is not null && categoryField.FieldType.IsEnum)
        {
            var enumType = categoryField.FieldType;
            var enumName = tool switch
            {
                BuildTool.Cultivator => "Cultivator",
                BuildTool.Hoe => "Hoe",
                BuildTool.Hammer => "Hammer",
                _ => "Misc"
            };

            try
            {
                var value = Enum.Parse(enumType, enumName, ignoreCase: true);
                categoryField.SetValue(pieceComponent, value);
            }
            catch
            {
                // Best effort only: the runtime category values vary by game version.
            }
        }

        if (!buildPieces.Contains(pieceComponent))
        {
            buildPieces.Add(pieceComponent);
        }

        return true;
    }

    public static List<string> GetPrefabNameCandidates(string keyword = "")
    {
        var items = new List<string>();

        if (!TryGetObjectDbInstance(out var objectDb))
        {
            return items;
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
