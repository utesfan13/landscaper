using UnityEngine;

namespace Landscaper;

public static class PrefabUtilities
{
    private static string SafeZNetViewId(GameObject runtimePrefab)
    {
        if (runtimePrefab is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        var zNetView = runtimePrefab.GetComponent<ZNetView>();
        if (zNetView is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        var zdo = zNetView.GetZDO();
        if (zdo is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        return zdo.GetHashCode().ToString();
    }
    private static readonly Dictionary<string, PrefabRecord> VanillaCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RaspberryBush"] = new PrefabRecord { Name = "RaspberryBush", PrefabName = "RaspberryBush" },
        ["BlueberryBush"] = new PrefabRecord { Name = "BlueberryBush", PrefabName = "BlueberryBush" },
        ["Pickable_Dandelion"] = new PrefabRecord { Name = "Pickable_Dandelion", PrefabName = "Pickable_Dandelion" },
        ["Thistle"] = new PrefabRecord { Name = "Thistle", PrefabName = "Thistle" },
        ["Mushroom"] = new PrefabRecord { Name = "Mushroom", PrefabName = "Mushroom" },
        ["DecorativeShrub"] = new PrefabRecord { Name = "DecorativeShrub", PrefabName = "DecorativeShrub" },
        ["Rock_1"] = new PrefabRecord { Name = "Rock_1", PrefabName = "Rock_1" },
        ["Rock_2"] = new PrefabRecord { Name = "Rock_2", PrefabName = "Rock_2" },
        ["Boulder"] = new PrefabRecord { Name = "Boulder", PrefabName = "Boulder" },
        ["TreeStump"] = new PrefabRecord { Name = "TreeStump", PrefabName = "TreeStump" },
        ["FallenLog"] = new PrefabRecord { Name = "FallenLog", PrefabName = "FallenLog" },
        ["Statue01"] = new PrefabRecord { Name = "Statue01", PrefabName = "Statue01" },
        ["StoneArch"] = new PrefabRecord { Name = "StoneArch", PrefabName = "StoneArch" }
    };

    public static bool TryResolvePrefab(string prefabName, out PrefabRecord prefab)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            prefab = new PrefabRecord { Name = "<invalid>", PrefabName = string.Empty };
            return false;
        }

        if (VanillaCatalog.TryGetValue(prefabName, out var resolved))
        {
            prefab = resolved;
            resolved.EnsureBuildComponents();
            return true;
        }

        if (ValheimRuntimeBridge.TryResolvePrefabName(prefabName, out var runtimePrefab) && runtimePrefab is not null)
        {
            var runtimeGameObject = runtimePrefab;
            prefab = new PrefabRecord
            {
                Name = runtimeGameObject.name,
                PrefabName = runtimeGameObject.name,
                ActiveSelf = runtimeGameObject.activeSelf,
                Piece = null,
                ZNetView = new NetworkViewInfo { ViewId = SafeZNetViewId(runtimeGameObject) }
            };

            prefab.EnsureBuildComponents();
            VanillaCatalog[prefabName] = prefab;
            return true;
        }

        prefab = new PrefabRecord { Name = prefabName, PrefabName = prefabName };
        return false;
    }

    public static PrefabRecord ClonePrefabForBuild(string prefabName, string cloneName)
    {
        var source = TryResolvePrefab(prefabName, out var original)
            ? original
            : throw new InvalidOperationException($"Could not resolve prefab '{prefabName}'.");

        var clone = new PrefabRecord
        {
            Name = cloneName,
            PrefabName = cloneName,
            ActiveSelf = source.ActiveSelf,
            Piece = source.Piece ?? new DecorativeBuildPiece { Name = cloneName, Identifier = cloneName, Buildable = true },
            ZNetView = source.ZNetView ?? new NetworkViewInfo { ViewId = Guid.NewGuid().ToString("N") }
        };

        clone.EnsureBuildComponents();
        return clone;
    }

    public static IEnumerable<string> GetCandidatePrefabs(string keyword = "")
    {
        var query = keyword.Trim();

        if (ValheimRuntimeBridge.TryGetObjectDbInstance(out var objectDb) && objectDb is not null)
        {
            var runtime = ValheimRuntimeBridge.GetPrefabNameCandidates(query);
            if (runtime.Count > 0)
            {
                return runtime;
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return VanillaCatalog.Keys.OrderBy(x => x);
        }

        return VanillaCatalog.Keys
            .Where(x => x.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => x);
    }
}
