using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

public sealed class DecorativePieceManager
{
    private readonly ManualLogSource _log;
    private readonly BepInPlugin _plugin;
    private readonly Func<string> _customEntries;
    private readonly Func<BuildTool, bool> _toolEnabled;
    private readonly Dictionary<string, RegisteredPiece> _registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Other saved names mapped to registered pieces, by name hash; see RegisterAliases.</summary>
    private readonly Dictionary<int, string> _aliases = new();

    /// <summary>Saved names kept loading by invisible stand-ins; see KeepPlacedPieces.</summary>
    private readonly SortedSet<string> _standIns = new(StringComparer.OrdinalIgnoreCase);

    private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedPrefabs =
        AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");
    private readonly HashSet<string> _pieceNames = new(StringComparer.Ordinal);
    private readonly List<(Piece Piece, GameObject Source)> _pendingIcons = new();

    private sealed class RegisteredPiece
    {
        public RegisteredPiece(GameObject clone, DecorativePieceDefinition definition, string sourcePrefab, PieceTraits traits)
        {
            Clone = clone;
            Definition = definition;
            SourcePrefab = sourcePrefab;
            Traits = traits;
        }

        public DecorativePieceDefinition Definition { get; }

        /// <summary>What the automatic cost is based on.</summary>
        public PieceTraits Traits { get; }

        public GameObject Clone { get; }
        public BuildTool Tool => Definition.Tool;
        public string Category => Definition.Category;

        /// <summary>The vanilla prefab this piece was cloned from.</summary>
        public string SourcePrefab { get; }

        /// <summary>Whether this is a scaled variant rather than the plain piece for its prefab.</summary>
        public bool IsVariant => Definition.Scale != Vector3.one;
    }

    /// <param name="customEntries">Reads the current custom entries; synced from the server.</param>
    /// <param name="toolEnabled">Reads whether a tool's menu should list pieces; synced from the server.</param>
    public DecorativePieceManager(ManualLogSource log, BepInPlugin plugin, Func<string> customEntries, Func<BuildTool, bool> toolEnabled)
    {
        _log = log;
        _plugin = plugin;
        _customEntries = customEntries;
        _toolEnabled = toolEnabled;
    }

    public bool HasRegistered { get; private set; }

    /// <summary>Display names of every registered piece, for silent unlocking.</summary>
    public IReadOnlyCollection<string> PieceNames => _pieceNames;

    /// <summary>
    /// Registers every catalog and custom entry. Call once, from ZNetScene.Awake, so the clones exist
    /// before ZNetScene starts creating saved objects. Every catalog piece is registered whatever
    /// the tool toggles say: when hosting, Valheim deletes saved objects whose prefab is missing, so
    /// turning a tool off must only hide its pieces from the menu.
    /// </summary>
    public void RegisterAll()
    {
        HasRegistered = true;
        ClutterPrefabs.Build(_log);
        var registered = PieceCatalog.Entries().Concat(ParseCustomEntries(_customEntries())).Count(Register);
        _log.LogInfo($"Registered {registered} decorative pieces.");
        RegisterAliases();
        UpdateMenus();
        if (_standIns.Count > 0)
        {
            _log.LogWarning($"{_standIns.Count} piece(s) couldn't be registered and are kept as invisible stand-ins. " +
                            "Run landscaper_check in the console (F5) for details.");
        }

        var costs = _registered.Values
            .Select(piece => piece.Clone != null ? piece.Clone.GetComponent<Piece>()?.m_resources : null)
            .Where(resources => resources is { Length: > 0 })
            .GroupBy(resources => $"{resources![0].m_amount} {resources[0].m_resItem.m_itemData.m_shared.m_name}")
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: {group.Count()}");
        _log.LogDebug($"Build costs: {string.Join(", ", costs)}");
    }

    /// <summary>
    /// Recomputes every piece's cost from the current settings: custom entries with their own
    /// requirements keep them, everything else gets the automatic cost from <see cref="BuildCosts"/>
    /// (or none when costs are off).
    /// </summary>
    public void ApplyCosts()
    {
        foreach (var piece in _registered.Values)
        {
            var component = piece.Clone != null ? piece.Clone.GetComponent<Piece>() : null;
            if (component != null)
            {
                component.m_resources = CostFor(piece.Definition, piece.Traits);
                component.m_craftingStation = StationFor(piece.Definition, piece.Traits);
            }
        }
    }

    /// <summary>
    /// The crafting station needed nearby to place a piece: the vanilla piece's station when the piece
    /// uses the vanilla cost, otherwise none.
    /// </summary>
    private static CraftingStation? StationFor(DecorativePieceDefinition definition, PieceTraits traits) =>
        definition.Requirements.Count == 0 && BuildCosts.Enabled() && traits.VanillaResources is not null
            ? traits.VanillaStation
            : null;

    private Piece.Requirement[] CostFor(DecorativePieceDefinition definition, PieceTraits traits)
    {
        if (definition.Requirements.Count > 0)
        {
            return BuildRequirements(definition);
        }

        if (!BuildCosts.Enabled())
        {
            return Array.Empty<Piece.Requirement>();
        }

        // Pieces that are vanilla build pieces cost the same as the vanilla piece.
        if (traits.VanillaResources is { } vanilla)
        {
            return vanilla;
        }

        var requirements = new List<Piece.Requirement>();
        foreach (var (item, amount) in BuildCosts.CostFor(definition, traits))
        {
            if (item != null)
            {
                requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_recover = true });
            }
        }

        return requirements.ToArray();
    }

    /// <summary>
    /// Applies changed settings, such as those synced from a server when joining: registers custom
    /// entries that aren't registered yet and updates which pieces the tool menus list. Entries that
    /// were removed or changed keep their registered piece until restart, so placed copies still load.
    /// </summary>
    public void Refresh()
    {
        if (!HasRegistered)
        {
            return;
        }

        var added = ParseCustomEntries(_customEntries())
            .Where(definition => !IsRegistered(definition))
            .Count(Register);
        if (added > 0)
        {
            _log.LogInfo($"Registered {added} custom pieces from updated settings.");
        }

        ApplyCosts();
        RegisterAliases();
        UpdateMenus();
        RefreshPlayerPieces();
    }

    /// <summary>
    /// Lists each registered piece in its tool's menu if the tool is enabled and the piece is still
    /// in the catalog or the custom entries, and removes it otherwise. Jotunn re-adds every piece
    /// whenever ObjectDB loads, so this runs again after that.
    /// </summary>
    public void UpdateMenus()
    {
        if (!HasRegistered)
        {
            return;
        }

        var wanted = new HashSet<string>(
            PieceCatalog.Entries().Concat(ParseCustomEntries(_customEntries(), logErrors: false))
                .Select(definition => FindSpawnablePrefab(definition.PrefabName) is { } source ? GetCloneName(source.name, definition) : null)
                .OfType<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in _registered)
        {
            var piece = pair.Value;
            var table = PieceManager.Instance.GetPieceTable(piece.Tool.ToString());
            if (table is null || piece.Clone == null)
            {
                continue;
            }

            var listed = table.m_pieces.Contains(piece.Clone);
            var visible = _toolEnabled(piece.Tool) && wanted.Contains(pair.Key);
            if (visible && !listed)
            {
                PieceManager.Instance.RegisterPieceInPieceTable(piece.Clone, piece.Tool.ToString(), piece.Category);
            }
            else if (!visible && listed)
            {
                table.m_pieces.Remove(piece.Clone);
            }

            if (visible && piece.Definition.ListAfter is { } after)
            {
                MoveAfter(table.m_pieces, piece.Clone, after);
            }
        }
    }

    /// <summary>
    /// Moves a piece to right after another in its menu. Pieces are listed in their table's order and
    /// Jotunn appends new ones, so this runs again whenever the menus are rebuilt.
    /// </summary>
    private static void MoveAfter(List<GameObject> pieces, GameObject piece, string afterPrefab)
    {
        var anchor = pieces.FindIndex(candidate => candidate != null && candidate.name == afterPrefab);
        var current = pieces.IndexOf(piece);
        if (anchor < 0 || current < 0 || current == anchor + 1)
        {
            return;
        }

        pieces.RemoveAt(current);
        pieces.Insert(current < anchor ? anchor : anchor + 1, piece);
    }

    private static void RefreshPlayerPieces()
    {
        if (Player.m_localPlayer != null)
        {
            AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList")?.Invoke(Player.m_localPlayer, null);
        }
    }

    /// <summary>
    /// The piece for a vanilla prefab on a tool, preferring the plain piece over scaled variants, or
    /// null if the prefab isn't registered for that tool.
    /// </summary>
    public Piece? FindPiece(string sourcePrefab, BuildTool tool) =>
        _registered.Values
            .Where(piece => piece.Tool == tool && piece.Clone != null &&
                string.Equals(piece.SourcePrefab, sourcePrefab, StringComparison.OrdinalIgnoreCase))
            .OrderBy(piece => piece.IsVariant)
            .Select(piece => piece.Clone.GetComponent<Piece>())
            .FirstOrDefault();

    /// <summary>A tool that has a piece for this vanilla prefab, if any.</summary>
    public BuildTool? FindToolFor(string sourcePrefab) =>
        _registered.Values
            .Where(piece => string.Equals(piece.SourcePrefab, sourcePrefab, StringComparison.OrdinalIgnoreCase))
            .Select(piece => (BuildTool?)piece.Tool)
            .FirstOrDefault();

    /// <summary>Saved names kept loading by invisible stand-ins because their piece couldn't be registered.</summary>
    public IReadOnlyCollection<string> StandIns => _standIns;

    /// <summary>How many other saved names are mapped to registered pieces.</summary>
    public int AliasCount => _aliases.Count;

    /// <summary>Whether a vanilla prefab exists that pieces can be made from.</summary>
    public static bool PrefabExists(string prefabName) => FindSpawnablePrefab(prefabName) is not null;

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

    /// <summary>Name of the extra box added by EnsureTargetable, which collides with nothing.</summary>
    public const string TargetBoxName = "LandscaperRemoveTarget";

    /// <summary>The largest dimension of a registered piece's model in meters, or null if unknown.</summary>
    public float? SizeOf(string cloneName) =>
        _registered.TryGetValue(cloneName, out var piece) ? piece.Traits.Size : null;

    /// <summary>The vanilla prefab a Landscaper clone was made from, or null for other prefabs.</summary>
    public string? SourcePrefabOf(string cloneName) =>
        _registered.TryGetValue(cloneName, out var piece) ? piece.SourcePrefab : null;

    /// <summary>The tilt a piece starts with when selected: a custom entry's rotation, or null.</summary>
    public Vector3? StartingTiltOf(string cloneName) =>
        _registered.TryGetValue(cloneName, out var piece) && piece.Definition.PlacementRotation != Vector3.zero
            ? piece.Definition.PlacementRotation
            : null;

    /// <summary>Whether any registered piece already uses this display name.</summary>
    public bool IsNameUsed(string displayName) =>
        _registered.Values.Any(piece => piece.Clone != null && piece.Clone.GetComponent<Piece>()?.m_name == displayName);

    /// <summary>
    /// Registers a piece for a copied world object and lists it in its tool's menu. The caller also
    /// saves it as a custom entry so it is registered again after a restart.
    /// </summary>
    public Piece? RegisterCopied(DecorativePieceDefinition definition)
    {
        if (!Register(definition))
        {
            return null;
        }

        UpdateMenus();
        return FindPiece(FindSpawnablePrefab(definition.PrefabName)?.name ?? definition.PrefabName, definition.Tool);
    }

    private bool IsRegistered(DecorativePieceDefinition definition) =>
        FindSpawnablePrefab(definition.PrefabName) is { } source && _registered.ContainsKey(GetCloneName(source.name, definition));

    private bool Register(DecorativePieceDefinition definition)
    {
        try
        {
            return TryRegister(definition);
        }
        catch (Exception exception)
        {
            _log.LogError($"Failed to register '{definition.DisplayName}' ({definition.PrefabName}): {exception}");
            var source = FindSpawnablePrefab(definition.PrefabName);
            KeepPlacedPieces(GetCloneName(source != null ? source.name : definition.PrefabName, definition), "it failed to register");
            return false;
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

    private bool TryRegister(DecorativePieceDefinition definition)
    {
        var source = FindSpawnablePrefab(definition.PrefabName);
        if (source is null)
        {
            _log.LogWarning($"Prefab '{definition.PrefabName}' for '{definition.DisplayName}' was not found or is not spawnable.");
            KeepPlacedPieces(GetCloneName(definition.PrefabName, definition), "its prefab wasn't found");
            return false;
        }

        var tableName = definition.Tool.ToString();
        var cloneName = GetCloneName(source.name, definition);
        if (_registered.ContainsKey(cloneName))
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

        clone.transform.localScale = Vector3.Scale(source.transform.localScale, definition.Scale);

        // Save each placed piece's own size with the world, so pieces resized while placing keep
        // their size. Copies placed before this was enabled have no saved size and keep the default.
        clone.GetComponent<ZNetView>().m_syncInitialScale = true;
        // LodFadeInOut hides the LODGroup in Awake when spawned more than 20m from the camera and
        // restores it 0.1-0.3s later. Placement ghosts spawn at the prefab origin, so a ghost that
        // is recreated before the fade-in runs stays invisible.
        DestroyAll<LodFadeInOut>(clone);
        MakeStaticDecoration(clone);
        PondWater.Prepare(clone);
        if (definition.FunctionFrom is { } functionFrom)
        {
            FunctionalPieces.Apply(clone, functionFrom);
        }
        RemoveEmptyMeshColliders(clone);
        // Measured before EnsureTargetable adds its box, and including a scaled variant's scale.
        var modelSize = TryGetModelBounds(clone, out var modelBounds)
            ? Vector3.Scale(modelBounds.size, clone.transform.localScale)
            : Vector3.one;
        var pickable = clone.GetComponentInChildren<Pickable>(includeInactive: true);
        var vanillaPiece = clone.GetComponent<Piece>();
        var traits = new PieceTraits(
            Mathf.Max(modelSize.x, modelSize.y, modelSize.z),
            pickable != null && pickable.m_itemPrefab != null ? pickable.m_itemPrefab.GetComponent<ItemDrop>() : null,
            clone.GetComponentsInChildren<Light>(includeInactive: true).Length > 0,
            vanillaPiece != null && vanillaPiece.m_resources is { Length: > 0 } ? vanillaPiece.m_resources.ToArray() : null,
            vanillaPiece != null ? vanillaPiece.m_craftingStation : null,
            clone.GetComponent<LiquidVolume>() != null);
        EnsureTargetable(clone, definition.Tool);
        clone.AddComponent<LandscaperTint>();

        var piece = clone.GetComponent<Piece>() ?? clone.AddComponent<Piece>();
        var vanillaIcon = piece.m_icon;
        piece.m_name = definition.DisplayName;
        piece.m_description = definition.Description;
        piece.m_enabled = true;
        // Water pieces are placed where the crosshair meets the water surface instead of the ground.
        // Valheim lifts water pieces 3 m when it positions them by their colliders (for docks, which
        // hang down); clipEverything places them exactly at the crosshair instead.
        if (definition.OnWater)
        {
            piece.m_waterPiece = true;
            piece.m_noInWater = false;
            piece.m_clipEverything = true;
        }

        piece.m_groundPiece = definition.Tool != BuildTool.Hammer && !definition.OnWater;
        piece.m_groundOnly = false;
        piece.m_canBeRemoved = true;
        piece.m_craftingStation = StationFor(definition, traits);
        piece.m_resources = CostFor(definition, traits);
        piece.m_icon = vanillaIcon ?? GetToolIcon(definition.Tool);
        if (piece.m_icon is null)
        {
            _log.LogWarning($"No icon available for '{definition.DisplayName}'.");
            KeepPlacedPieces(cloneName, "it has no icon for the menu");
            return false;
        }

        if (!PieceManager.Instance.AddPiece(new CustomPiece(clone, tableName, fixReference: false) { Category = definition.Category }))
        {
            KeepPlacedPieces(cloneName, "it couldn't be added to the menu");
            return false;
        }

        _registered[cloneName] = new RegisteredPiece(clone, definition, source.name, traits);
        _standIns.Remove(cloneName);
        ReleaseAlias(cloneName);

        // Jotunn adds custom prefabs to ZNetScene when it wakes, which has already happened by the
        // time this runs, so add this one directly. UpdateMenus decides whether it is listed.
        PrefabManager.Instance.RegisterToZNetScene(clone);

        if (vanillaIcon is null)
        {
            _pendingIcons.Add((piece, source));
        }

        _pieceNames.Add(piece.m_name);

        return true;
    }

    /// <summary>
    /// Placed pieces are saved under this name, so it must stay stable. Unscaled entries use
    /// "Landscaper_{prefab}_{tool}". Scaled entries add their display name, so a prefab can have
    /// several size variants and a scale can be adjusted without losing pieces already placed.
    /// Jotunn rejects names with spaces or parentheses, which a few vanilla prefabs have.
    /// </summary>
    private static string GetCloneName(string sourceName, DecorativePieceDefinition definition, BuildTool? tool = null)
    {
        var prefabName = new string(sourceName.Select(c => c is ' ' or '(' or ')' ? '_' : c).ToArray());
        var cloneName = $"Landscaper_{prefabName}_{tool ?? definition.Tool}";
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

        var colliders = clone.GetComponentsInChildren<Collider>(includeInactive: true)
            .Where(collider => collider.enabled && IsActiveWithin(collider.transform, root))
            .ToList();
        var removable = colliders.Any(collider => (removeMask & (1 << collider.gameObject.layer)) != 0);
        var placeable = tool != BuildTool.Hammer || colliders.Any(collider =>
            !collider.isTrigger && (collider is not MeshCollider mesh || mesh.convex));
        if (removable && placeable)
        {
            return;
        }

        if (!TryGetModelBounds(clone, out var combined))
        {
            return;
        }

        var target = new GameObject(TargetBoxName) { layer = LayerMask.NameToLayer("piece_nonsolid") };
        target.transform.SetParent(root, worldPositionStays: false);
        var box = target.AddComponent<BoxCollider>();
        box.center = combined.center;
        // Keep flat pieces such as rugs hittable.
        box.size = Vector3.Max(combined.size, new Vector3(0.1f, 0.1f, 0.1f));
    }

    /// <summary>The combined bounds of a clone's visible meshes, in the clone's local space.</summary>
    private static bool TryGetModelBounds(GameObject clone, out Bounds bounds)
    {
        var root = clone.transform;
        bounds = default;

        var meshes = clone.GetComponentsInChildren<MeshFilter>(includeInactive: true)
            .Where(filter => filter.sharedMesh is not null && IsActiveWithin(filter.transform, root))
            .Select(filter => (filter.transform, filter.sharedMesh.bounds))
            .Concat(clone.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                .Where(renderer => renderer.sharedMesh is not null && IsActiveWithin(renderer.transform, root))
                .Select(renderer => (renderer.transform, renderer.localBounds)))
            .ToList();
        if (meshes.Count == 0)
        {
            return false;
        }

        var toRoot = root.worldToLocalMatrix;
        Bounds? combined = null;
        foreach (var (transform, meshBounds) in meshes)
        {
            var toRootFromMesh = toRoot * transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
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

        bounds = combined!.Value;
        return true;
    }

    /// <summary>
    /// Whether a transform is active up to <paramref name="root"/>. Clones sit under Jotunn's inactive
    /// prefab container, so activeInHierarchy is always false for them.
    /// </summary>
    private static bool IsActiveWithin(Transform transform, Transform root)
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

    /// <summary>
    /// Some prefabs are world loot or physics objects rather than scenery, such as the cargo crates
    /// that float at sea. Placed as pieces they should stay put and not hand out free loot:
    /// <list type="bullet">
    /// <item>Containers keep working as storage but get no default loot, and don't destroy themselves
    /// when empty (a new cargo crate is empty for a moment and would delete itself straight away).</item>
    /// <item>Floating, network movement sync and rigidbodies are removed so pieces don't roll, fall
    /// or bob. The placement ghost already has its rigidbodies removed, so this matches the preview.
    /// Fallen logs (TreeLog) use their rigidbody as soon as they're created and break without it, so
    /// theirs is kept but made kinematic, which holds it still just the same.</item>
    /// </list>
    /// </summary>
    private static void MakeStaticDecoration(GameObject clone)
    {
        foreach (var container in clone.GetComponentsInChildren<Container>(includeInactive: true))
        {
            container.m_autoDestroyEmpty = false;
            container.m_defaultItems = new DropTable();
        }

        // Components that use the rigidbody go first, or Unity refuses to remove it.
        DestroyAll<Floating>(clone);
        DestroyAll<ZSyncTransform>(clone);
        foreach (var body in clone.GetComponentsInChildren<Rigidbody>(includeInactive: true))
        {
            if (body.GetComponent<TreeLog>() != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }
    }

    /// <summary>
    /// A few prefabs, such as the giant Jotun stairs, have a mesh collider with no mesh. Valheim still
    /// measures from it when positioning a Hammer ghost, gets the aim point back, and pushes the ghost
    /// 50 m up, so the piece can't be placed. Such colliders do nothing else, so remove them.
    /// </summary>
    private static void RemoveEmptyMeshColliders(GameObject clone)
    {
        // Liquids (pond water, tar) build their collider mesh while simulating; keep those.
        if (clone.GetComponent<LiquidVolume>() != null)
        {
            return;
        }

        foreach (var collider in clone.GetComponentsInChildren<MeshCollider>(includeInactive: true))
        {
            if (collider.sharedMesh == null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
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

        // Liquids have no mesh until they simulate, so there's nothing to render.
        if (source.GetComponent<LiquidVolume>() != null)
        {
            piece.m_icon = WaterIcon() ?? piece.m_icon;
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

    /// <summary>
    /// An icon for pond water: the water drop Valheim shows for the Wet status effect, or failing
    /// that the ice pond rock's icon tinted blue.
    /// </summary>
    private Sprite? WaterIcon()
    {
        if (ObjectDB.instance?.GetStatusEffect(SEMan.s_statusEffectWet)?.m_icon is { } wet)
        {
            return wet;
        }

        var rock = ZNetScene.instance?.GetPrefab("IcePond_rock");
        if (rock == null)
        {
            return null;
        }

        try
        {
            var rendered = RenderManager.Instance.Render(new RenderManager.RenderRequest(rock)
            {
                Rotation = RenderManager.IsometricRotation,
                UseCache = true,
                TargetPlugin = _plugin
            });
            return rendered != null ? Tinted(rendered, new Color(0.35f, 0.6f, 1f)) : null;
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not render a water icon: {exception.Message}");
            return null;
        }
    }

    /// <summary>A copy of a sprite with its colours multiplied by <paramref name="tint"/>.</summary>
    private static Sprite Tinted(Sprite sprite, Color tint)
    {
        var source = sprite.texture;
        var pixels = source.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(pixels[i].r * tint.r, pixels[i].g * tint.g, pixels[i].b * tint.b, pixels[i].a);
        }

        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
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
    private List<DecorativePieceDefinition> ParseCustomEntries(string customEntries, bool logErrors = true)
    {
        void Warn(string message)
        {
            if (logErrors)
            {
                _log.LogWarning(message);
            }
        }

        var definitions = new List<DecorativePieceDefinition>();
        foreach (var line in customEntries.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('|').Select(field => field.Trim()).ToArray();
            if (fields.Length < 4 || !Enum.TryParse(fields[2], true, out BuildTool tool))
            {
                Warn($"Ignoring invalid custom entry: {line}");
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
                    Warn($"Ignoring invalid rotation in custom entry: {line}");
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
                        Warn($"Ignoring invalid requirement '{requirement}' in custom entry: {line}");
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
                    Warn($"Ignoring invalid scale in custom entry (use one number or X,Y,Z, all above 0): {line}");
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
