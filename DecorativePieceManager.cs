using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

public sealed partial class DecorativePieceManager
{
    private readonly ManualLogSource _log;

    private readonly BepInPlugin _plugin;

    private readonly Func<string> _customEntries;

    private readonly Func<BuildTool, bool> _toolEnabled;

    private readonly Dictionary<string, RegisteredPiece> _registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The webs. Their models reach tens of meters out to one side of their origin (the vertical web
    /// starts 30 m up), and the Hammer places a piece with the near face of its bounds on the aim
    /// point, so a web's preview started far from the crosshair. They're placed with the bottom
    /// middle of the model exactly at the crosshair instead (see RecenterModel). Their strands are
    /// too thin to show in a rendered icon, so their icon is the web texture itself.
    /// </summary>
    private static readonly HashSet<string> WebPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "horizontal_web", "vertical_web", "tunnel_web", "morkhalla_web_corner", "morkhalla_web_horisontal", "morkhalla_web_tunnel"
    };

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

    /// <summary>The Piece of every registered Landscaper piece, for unlocking them.</summary>
    public IEnumerable<Piece> RegisteredPieces =>
        _registered.Values.Select(piece => piece.Clone != null ? piece.Clone.GetComponent<Piece>() : null).OfType<Piece>();

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
        var adjustable = MakeVanillaPiecesAdjustable();
        // Retired pieces are registered but never listed: UpdateMenus only lists catalog and custom entries.
        var registered = PieceCatalog.Entries().Where(definition => !DuplicatesVanillaPiece(definition))
            .Concat(ParseCustomEntries(_customEntries())).Count(Register);
        PieceCatalog.Retired().Count(Register);
        _log.LogInfo($"Registered {registered} decorative pieces; {adjustable} vanilla build pieces can be adjusted too.");
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
                component.m_craftingStation = StationFor(piece.Definition, piece.Traits, component.m_resources);
            }
        }
    }

    /// <summary>
    /// The crafting station needed nearby to place a piece: the vanilla piece's station when the piece
    /// uses the vanilla cost, otherwise none.
    /// </summary>
    private static CraftingStation? StationFor(DecorativePieceDefinition definition, PieceTraits traits, Piece.Requirement[] resources)
    {
        // Custom entries with their own requirements, and free pieces, need no station.
        if (definition.Requirements.Count > 0 || !BuildCosts.Enabled())
        {
            return null;
        }

        // Pieces with the vanilla cost keep the vanilla station, whichever it is (or none).
        if (traits.VanillaResources is not null)
        {
            return traits.VanillaStation;
        }

        var name = BuildCosts.StationFor(definition, traits,
            resources.Where(requirement => requirement.m_resItem != null).Select(requirement => requirement.m_resItem.name));
        return name is null ? null : PrefabManager.Instance.GetPrefab(name)?.GetComponent<CraftingStation>();
    }

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

    /// <summary>Whether a vanilla prefab exists that pieces can be made from.</summary>
    public static bool PrefabExists(string prefabName) => FindSpawnablePrefab(prefabName) is not null;

    /// <summary>Name of the extra box added by EnsureTargetable, which collides with nothing.</summary>
    public const string TargetBoxName = "LandscaperRemoveTarget";

    /// <summary>The largest dimension of a registered piece's model in meters, or null if unknown.</summary>
    public float? SizeOf(string cloneName) =>
        _registered.TryGetValue(cloneName, out var piece) ? piece.Traits.Size
        : _vanillaSizes.TryGetValue(cloneName, out var size) ? size
        : null;

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
        if (WebPrefabs.Contains(definition.PrefabName))
        {
            RecenterModel(clone);
        }
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
            clone.GetComponent<LiquidVolume>() != null,
            pickable != null ? pickable.m_amount : 1,
            pickable == null || pickable.m_respawnTimeMinutes > 0f);
        EnsureTargetable(clone, definition.Tool);
        clone.AddComponent<LandscaperTint>();
        clone.AddComponent<LandscaperPiece>();

        var piece = clone.GetComponent<Piece>() ?? clone.AddComponent<Piece>();
        var vanillaIcon = piece.m_icon;
        // Leftover pieces the game ships but doesn't let you build have untranslated names, and often
        // borrow another piece's icon: the turf roofs use the thatch roofs' icons, so they looked like
        // thatch in the menu. Render their own icon instead.
        if (vanillaIcon != null && !piece.m_name.StartsWith("$", StringComparison.Ordinal))
        {
            vanillaIcon = null;
        }
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
            clone.AddComponent<WaterFloat>();
        }

        // Webs go exactly where the crosshair is, like water pieces; see WebPrefabs.
        if (WebPrefabs.Contains(definition.PrefabName))
        {
            piece.m_clipEverything = true;
        }

        piece.m_groundPiece = definition.Tool != BuildTool.Hammer && !definition.OnWater;
        piece.m_groundOnly = false;
        piece.m_canBeRemoved = true;
        piece.m_resources = CostFor(definition, traits);
        piece.m_craftingStation = StationFor(definition, traits, piece.m_resources);
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
}
