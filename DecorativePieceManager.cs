using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

public sealed class DecorativePieceManager
{
    private readonly ManualLogSource _log;
    private readonly BepInPlugin _plugin;
    private readonly IReadOnlyCollection<BuildTool> _enabledTools;
    private readonly bool _decorativeOnly;
    private readonly List<DecorativePieceDefinition> _customDefinitions;
    private readonly HashSet<string> _registeredCloneNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _freePieceNames = new(StringComparer.Ordinal);
    private readonly List<(Piece Piece, GameObject Source)> _pendingIcons = new();

    public DecorativePieceManager(
        ManualLogSource log,
        BepInPlugin plugin,
        string customEntries,
        IReadOnlyCollection<BuildTool> enabledTools,
        bool decorativeOnly)
    {
        _log = log;
        _plugin = plugin;
        _enabledTools = enabledTools;
        _decorativeOnly = decorativeOnly;
        _customDefinitions = ParseCustomEntries(customEntries);
    }

    /// <summary>Display names of registered pieces that cost nothing, for silent unlocking.</summary>
    public IReadOnlyCollection<string> FreePieceNames => _freePieceNames;

    /// <summary>
    /// Registers every catalog and custom entry. Call once, from ZNetScene.Awake, so the clones exist
    /// before ZNetScene starts creating saved objects.
    /// </summary>
    public void RegisterAll()
    {
        var registered = 0;
        foreach (var definition in PieceCatalog.Entries().Concat(_customDefinitions))
        {
            if (!_enabledTools.Contains(definition.Tool))
            {
                continue;
            }

            try
            {
                if (TryRegister(definition))
                {
                    registered++;
                }
            }
            catch (Exception exception)
            {
                _log.LogError($"Failed to register '{definition.DisplayName}' ({definition.PrefabName}): {exception}");
            }
        }

        _log.LogInfo($"Registered {registered} decorative pieces.");
    }

    private bool TryRegister(DecorativePieceDefinition definition)
    {
        var source = FindSpawnablePrefab(definition.PrefabName);
        if (source is null)
        {
            _log.LogWarning($"Prefab '{definition.PrefabName}' for '{definition.DisplayName}' was not found or is not spawnable.");
            return false;
        }

        var tableName = definition.Tool.ToString();
        var cloneName = GetCloneName(source, definition);
        if (!_registeredCloneNames.Add(cloneName))
        {
            _log.LogWarning($"Skipping '{definition.DisplayName}': '{source.name}' is already on the {tableName}. Give it a scale to add it as a separate variant.");
            return false;
        }

        var clone = PrefabManager.Instance.CreateClonedPrefab(cloneName, source);
        if (clone is null)
        {
            _log.LogWarning($"Could not clone '{source.name}' for '{definition.DisplayName}'.");
            return false;
        }

        clone.transform.localRotation = Quaternion.Euler(definition.PlacementRotation);
        clone.transform.localScale = Vector3.Scale(source.transform.localScale, definition.Scale);

        // Save each placed piece's own size with the world, so pieces resized while placing keep
        // their size. Copies placed before this was enabled have no saved size and keep the default.
        clone.GetComponent<ZNetView>().m_syncInitialScale = true;
        StripComponents(clone);
        EnsureTargetable(clone, definition.Tool);
        clone.AddComponent<LandscaperTint>();

        var piece = clone.GetComponent<Piece>() ?? clone.AddComponent<Piece>();
        var vanillaIcon = piece.m_icon;
        piece.m_name = definition.DisplayName;
        piece.m_description = definition.Description;
        piece.m_enabled = true;
        piece.m_groundPiece = definition.Tool != BuildTool.Hammer;
        piece.m_groundOnly = false;
        piece.m_canBeRemoved = true;
        piece.m_craftingStation = null;
        piece.m_resources = BuildRequirements(definition);
        piece.m_icon = vanillaIcon ?? GetToolIcon(definition.Tool);
        if (piece.m_icon is null)
        {
            _log.LogWarning($"No icon available for '{definition.DisplayName}'.");
            return false;
        }

        if (!PieceManager.Instance.AddPiece(new CustomPiece(clone, tableName, fixReference: false) { Category = definition.Category }))
        {
            return false;
        }

        // Jotunn adds custom prefabs to ZNetScene when it wakes, which has already happened by the
        // time this runs, so add this one directly. The tool menus are usually not loaded yet; if
        // so, Jotunn adds the piece to them when ObjectDB wakes.
        PrefabManager.Instance.RegisterToZNetScene(clone);
        if (PieceManager.Instance.GetPieceTable(tableName) is not null)
        {
            PieceManager.Instance.RegisterPieceInPieceTable(clone, tableName, definition.Category);
        }

        if (vanillaIcon is null)
        {
            _pendingIcons.Add((piece, source));
        }

        if (piece.m_resources.Length == 0)
        {
            _freePieceNames.Add(piece.m_name);
        }

        return true;
    }

    /// <summary>
    /// Placed pieces are saved under this name, so it must stay stable. Unscaled entries use
    /// "Landscaper_{prefab}_{tool}". Scaled entries add their display name, so a prefab can have
    /// several size variants and a scale can be adjusted without losing pieces already placed.
    /// Jotunn rejects names with spaces or parentheses, which a few vanilla prefabs have.
    /// </summary>
    private static string GetCloneName(GameObject source, DecorativePieceDefinition definition)
    {
        var prefabName = new string(source.name.Select(c => c is ' ' or '(' or ')' ? '_' : c).ToArray());
        var cloneName = $"Landscaper_{prefabName}_{definition.Tool}";
        if (definition.Scale == Vector3.one)
        {
            return cloneName;
        }

        var suffix = new string(definition.DisplayName.Where(char.IsLetterOrDigit).ToArray());
        return $"{cloneName}_{suffix}";
    }

    /// <summary>
    /// Looks up a prefab registered with ZNetScene. Only those have a ZNetView, so pieces built
    /// from them are saved with the world.
    /// </summary>
    private static GameObject? FindSpawnablePrefab(string prefabName)
    {
        var scene = ZNetScene.instance;
        if (scene is null || string.IsNullOrWhiteSpace(prefabName))
        {
            return null;
        }

        var prefab = scene.GetPrefab(prefabName)
            ?? scene.m_prefabs.FirstOrDefault(candidate =>
                candidate is not null && string.Equals(candidate.name, prefabName, StringComparison.OrdinalIgnoreCase));

        return prefab is not null && prefab.GetComponent<ZNetView>() is not null ? prefab : null;
    }

    private void StripComponents(GameObject clone)
    {
        // LodFadeInOut hides the LODGroup in Awake when spawned more than 20m from the camera and
        // restores it 0.1-0.3s later. Placement ghosts spawn at the prefab origin, so a ghost that
        // is recreated before the fade-in runs stays invisible.
        DestroyAll<LodFadeInOut>(clone);

        if (_decorativeOnly)
        {
            // Without these, placed trees, rocks and plants can't be chopped, mined or picked.
            // The remove button still removes them.
            DestroyAll<TreeBase>(clone);
            DestroyAll<TreeLog>(clone);
            DestroyAll<Destructible>(clone);
            DestroyAll<MineRock>(clone);
            DestroyAll<MineRock5>(clone);
            DestroyAll<Pickable>(clone);
            DestroyAll<DropOnDestroyed>(clone);
        }
    }

    /// <summary>
    /// Adds an extra box around the model on "piece_nonsolid" when a prefab's own colliders fall
    /// short. The remove and interact raycasts hit that layer but nothing collides with it, so the
    /// box never blocks movement, and the original colliders are left as they are. Two cases need it:
    /// <list type="bullet">
    /// <item>Removing needs a collider the remove raycast can hit. Some prefabs have none (the Jotun
    /// stair rug, cave webs) or only have them on layers it skips (pickable mushrooms use "item").</item>
    /// <item>Valheim positions Hammer placement ghosts using their solid colliders, ignoring triggers
    /// and non-convex mesh colliders. With none left the ghost is pushed far from the crosshair and
    /// can't be placed; the Jotun statue pieces and training dummies only have non-convex meshes.
    /// Cultivator and Hoe pieces are positioned differently, and a box around a large cliff would
    /// block placing plants on the terrain under it, so this only applies to the Hammer.</item>
    /// </list>
    /// The box is solid rather than a trigger so the ghost positioning can use it.
    /// </summary>
    private static void EnsureTargetable(GameObject clone, BuildTool tool)
    {
        var removeMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");
        var root = clone.transform;

        // The clone sits under Jotunn's inactive prefab container, so check activity up to the clone
        // itself rather than using activeInHierarchy.
        bool IsActive(Transform transform)
        {
            for (var current = transform; current is not null && current != root.parent; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        var colliders = clone.GetComponentsInChildren<Collider>(includeInactive: true)
            .Where(collider => collider.enabled && IsActive(collider.transform))
            .ToList();
        var removable = colliders.Any(collider => (removeMask & (1 << collider.gameObject.layer)) != 0);
        var placeable = tool != BuildTool.Hammer || colliders.Any(collider =>
            !collider.isTrigger && (collider is not MeshCollider mesh || mesh.convex));
        if (removable && placeable)
        {
            return;
        }

        var meshes = clone.GetComponentsInChildren<MeshFilter>(includeInactive: true)
            .Where(filter => filter.sharedMesh is not null && IsActive(filter.transform))
            .Select(filter => (filter.transform, filter.sharedMesh.bounds))
            .Concat(clone.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                .Where(renderer => renderer.sharedMesh is not null && IsActive(renderer.transform))
                .Select(renderer => (renderer.transform, renderer.localBounds)))
            .ToList();
        if (meshes.Count == 0)
        {
            return;
        }

        // Combine every mesh's bounds in the clone's local space.
        var toRoot = root.worldToLocalMatrix;
        Bounds? combined = null;
        foreach (var (transform, bounds) in meshes)
        {
            var toRootFromMesh = toRoot * transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var point = toRootFromMesh.MultiplyPoint3x4(local);
                if (combined is { } existing)
                {
                    existing.Encapsulate(point);
                    combined = existing;
                }
                else
                {
                    combined = new Bounds(point, Vector3.zero);
                }
            }
        }

        var target = new GameObject("LandscaperRemoveTarget") { layer = LayerMask.NameToLayer("piece_nonsolid") };
        target.transform.SetParent(root, worldPositionStays: false);
        var box = target.AddComponent<BoxCollider>();
        box.center = combined!.Value.center;
        // Keep flat pieces such as rugs hittable.
        box.size = Vector3.Max(combined.Value.size, new Vector3(0.1f, 0.1f, 0.1f));
    }

    private static void DestroyAll<T>(GameObject root) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(includeInactive: true))
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private Piece.Requirement[] BuildRequirements(DecorativePieceDefinition definition)
    {
        var requirements = new List<Piece.Requirement>();
        foreach (var requirement in definition.Requirements)
        {
            var item = PrefabManager.Instance.GetPrefab(requirement.ItemName)?.GetComponent<ItemDrop>();
            if (item is null)
            {
                _log.LogWarning($"Unknown item '{requirement.ItemName}' in requirements for '{definition.DisplayName}'.");
                continue;
            }

            requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = requirement.Amount, m_recover = true });
        }

        return requirements.ToArray();
    }

    /// <summary>
    /// Renders the icon for one piece that has no vanilla icon; returns false when none are left.
    /// Call once per frame after the local player has spawned: Valheim's vegetation and rock
    /// shaders read environment values that aren't set while the world is loading, and icons
    /// rendered then come out red.
    /// </summary>
    public bool RenderNextIcon()
    {
        if (_pendingIcons.Count == 0)
        {
            return false;
        }

        var (piece, source) = _pendingIcons[_pendingIcons.Count - 1];
        _pendingIcons.RemoveAt(_pendingIcons.Count - 1);
        if (piece is null || source is null)
        {
            return true;
        }

        try
        {
            var sprite = RenderManager.Instance.Render(new RenderManager.RenderRequest(source)
            {
                Rotation = RenderManager.IsometricRotation,
                UseCache = true,
                TargetPlugin = _plugin
            });
            if (sprite is not null)
            {
                piece.m_icon = sprite;
            }
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not render an icon for '{source.name}': {exception.Message}");
        }

        return true;
    }

    private static Sprite? GetToolIcon(BuildTool tool)
    {
        var icons = PrefabManager.Instance.GetPrefab(tool.ToString())?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
        return icons?.FirstOrDefault(icon => icon is not null);
    }

    /// <summary>
    /// Parses entries of the form
    /// Display Name|Prefab|Tool|Category[|Rotation X,Y,Z[|Item:Amount,...[|Scale X,Y,Z or Scale]]].
    /// </summary>
    private List<DecorativePieceDefinition> ParseCustomEntries(string customEntries)
    {
        var definitions = new List<DecorativePieceDefinition>();
        foreach (var line in customEntries.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('|').Select(field => field.Trim()).ToArray();
            if (fields.Length < 4 || !Enum.TryParse(fields[2], true, out BuildTool tool))
            {
                _log.LogWarning($"Ignoring invalid custom entry: {line}");
                continue;
            }

            var definition = new DecorativePieceDefinition
            {
                DisplayName = fields[0],
                PrefabName = fields[1],
                Tool = tool,
                Category = fields[3]
            };

            if (fields.Length >= 5 && fields[4].Length > 0)
            {
                var values = fields[4].Split(',');
                if (values.Length == 3 &&
                    float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(values[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    definition.PlacementRotation = new Vector3(x, y, z);
                }
                else
                {
                    _log.LogWarning($"Ignoring invalid rotation in custom entry: {line}");
                }
            }

            if (fields.Length >= 6 && fields[5].Length > 0)
            {
                foreach (var requirement in fields[5].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = requirement.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var amount) && amount > 0)
                    {
                        definition.Requirements.Add(new PieceRequirement { ItemName = parts[0].Trim(), Amount = amount });
                    }
                    else
                    {
                        _log.LogWarning($"Ignoring invalid requirement '{requirement}' in custom entry: {line}");
                    }
                }
            }

            if (fields.Length >= 7 && fields[6].Length > 0)
            {
                if (TryParseScale(fields[6], out var scale))
                {
                    definition.Scale = scale;
                }
                else
                {
                    _log.LogWarning($"Ignoring invalid scale in custom entry (use one number or X,Y,Z, all above 0): {line}");
                }
            }

            definitions.Add(definition);
        }

        return definitions;
    }

    private static bool TryParseScale(string text, out Vector3 scale)
    {
        scale = Vector3.one;
        var values = text.Split(',')
            .Select(value => float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : float.NaN)
            .ToArray();

        if (values.Length == 1)
        {
            values = new[] { values[0], values[0], values[0] };
        }

        if (values.Length != 3 || values.Any(value => float.IsNaN(value) || value <= 0f))
        {
            return false;
        }

        scale = new Vector3(values[0], values[1], values[2]);
        return true;
    }
}
