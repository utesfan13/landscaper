using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Landscaper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
// Pieces only exist for players who have the mod, so everyone must have it, at the same minor version.
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "landscaper.valheim";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.13.2";

    internal static ManualLogSource Log = null!;
    internal static DecorativePieceManager? Pieces;
    private static bool _iconsDone;
    private static bool _refreshPending;
    private static Player? _lastLocalPlayer;

    private void Awake()
    {
        Log = Logger;
        MigrateOldConfig();

        // Settings that must match between players are admin-only: Jotunn syncs them from the server
        // when joining, and only admins can change them in game.
        var synced = new ConfigurationManagerAttributes { IsAdminOnly = true };
        ConfigDescription Synced(string description) => new($"{description} Synced from the server in multiplayer.", null, synced);

        var enabled = Config.Bind("General", "Enabled", true, "Master toggle for the decorative landscaping pieces. Requires a restart.");
        var costsEnabled = Config.Bind("Costs", "Enabled", true,
            Synced("Charge a small cost to place pieces: 5 of the item for pickables, 1 iron for metal pieces, otherwise 2 to 8 " +
                   "wood or stone by size, plus 1 resin for crafted light sources. Removing a piece refunds it. " +
                   "Custom entries with their own requirements keep them."));
        var costMultiplier = Config.Bind("Costs", "Multiplier", 1f, new ConfigDescription(
            "Multiplies the automatic costs, e.g. 0.5 for cheaper or 2 for more expensive. Synced from the server in multiplayer.",
            new AcceptableValueRange<float>(0.25f, 4f), synced));
        var allowIndestructible = Config.Bind("Indestructible", "Allowed", true,
            Synced("Allow placing indestructible pieces, which never break from lack of support, weather or attacks."));
        var decorativeOnly = Config.Bind("General", "DecorativeOnly", false,
            Synced("When true, placed trees, rocks and plants can't be chopped, mined or picked; remove them with the remove button (middle click) instead."));
        var ignorePlacementRules = Config.Bind("Placement", "IgnoreRules", true,
            Synced("Allow placing Landscaper pieces where Valheim normally wouldn't (clipping, unsupported, wrong biome, " +
                   "in dungeons, ...), and Hoe and Cultivator pieces on floors and objects as well as the ground. Overlapping a player or creature and other players' wards still block placing."));
        PlacementRules.IgnoreRules = () => ignorePlacementRules.Value;
        var cultivator = Config.Bind("Tools", "CultivatorDecorEnabled", true, Synced("List decorative pieces in the Cultivator menu."));
        var hoe = Config.Bind("Tools", "HoeDecorEnabled", true, Synced("List decorative pieces in the Hoe menu."));
        var hammer = Config.Bind("Tools", "HammerDecorEnabled", true, Synced("List decorative pieces in the Hammer menu."));
        var customEntries = Config.Bind("CustomEntries", "Entries", string.Empty, Synced(
            "Entries separated by \\n: Display Name|Prefab Name|Tool|Category|Rotation X,Y,Z|Item:Amount,Item:Amount|Scale X,Y,Z. " +
            "Tool is Cultivator, Hoe, or Hammer. Rotation, requirements and scale are optional (leave a field empty to skip it); " +
            "without requirements the piece is free. Scale is one number or X,Y,Z; a scaled entry is a separate variant. " +
            "Find prefab names in game with the landscaper_find console command. " +
            "Removing an entry hides it from the menu; pieces already placed keep loading until the next restart."));

        CommandManager.Instance.AddConsoleCommand(new FindPrefabCommand());
        CommandManager.Instance.AddConsoleCommand(new RemoveNearbyCommand());

        if (!enabled.Value)
        {
            Log.LogInfo("Disabled by configuration.");
            return;
        }

        bool ToolEnabled(BuildTool tool) => tool switch
        {
            BuildTool.Cultivator => cultivator.Value,
            BuildTool.Hoe => hoe.Value,
            BuildTool.Hammer => hammer.Value,
            _ => false
        };

        Pieces = new DecorativePieceManager(Log, Info.Metadata, () => customEntries.Value, ToolEnabled);
        DecorativeGuard.Enabled = () => decorativeOnly.Value;
        BuildCosts.Enabled = () => costsEnabled.Value;
        BuildCosts.Multiplier = () => costMultiplier.Value;

        // Synced values arrive after the world has started loading, and admins can change them in
        // game, so apply changes as they come instead of only at startup.
        void RequestRefresh(object sender, EventArgs args) => _refreshPending = true;
        cultivator.SettingChanged += RequestRefresh;
        hoe.SettingChanged += RequestRefresh;
        hammer.SettingChanged += RequestRefresh;
        customEntries.SettingChanged += RequestRefresh;
        costsEnabled.SettingChanged += RequestRefresh;
        costMultiplier.SettingChanged += RequestRefresh;

        ScaleController.Bind(Config);
        TintController.Bind(Config);
        OffsetController.Bind(Config);
        PondWater.Bind(Config);
        CopyController.Bind(customEntries);
        IndestructibleController.Bind(Config, () => allowIndestructible.Value);
        PrefabManager.OnPrefabsRegistered += OnPrefabsRegistered;
        PieceManager.OnPiecesRegistered += () => Pieces?.UpdateMenus();
        new Harmony(ModGuid).PatchAll();
    }

    private void Update()
    {
        if (Pieces is null)
        {
            return;
        }

        if (_refreshPending && ZNetScene.instance is not null)
        {
            _refreshPending = false;
            Pieces.Refresh();
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer is not null && !ReferenceEquals(localPlayer, _lastLocalPlayer))
        {
            _lastLocalPlayer = localPlayer;
            RemovalController.EnableFor((BuildTool[])Enum.GetValues(typeof(BuildTool)));
        }

        if (!_iconsDone && localPlayer is not null)
        {
            _iconsDone = !Pieces.RenderNextIcon();
        }

        PlacementInput.Update();
        ScaleController.Update();
    }

    private void LateUpdate()
    {
        if (Pieces is not null)
        {
            ScaleController.LateUpdate();
        }
    }

    /// <summary>
    /// BepInEx names the config file after the mod's GUID, so changing the GUID would start from a
    /// fresh config. On the first launch with a new GUID, copy the settings from the previous
    /// Landscaper config file (any other landscaper.*.cfg) and rename that file to *.migrated.
    /// </summary>
    private void MigrateOldConfig()
    {
        var newPath = Config.ConfigFilePath;
        var folder = Path.GetDirectoryName(newPath);
        if (File.Exists(newPath) || folder is null || !Directory.Exists(folder))
        {
            return;
        }

        var oldPath = Directory.GetFiles(folder, "landscaper.*.cfg")
            .FirstOrDefault(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase));
        if (oldPath is null)
        {
            return;
        }

        try
        {
            File.Copy(oldPath, newPath);
            File.Move(oldPath, oldPath + ".migrated");
            Config.Reload();
            Log.LogInfo($"Moved settings from {Path.GetFileName(oldPath)} to {Path.GetFileName(newPath)}.");
        }
        catch (Exception exception)
        {
            Log.LogWarning($"Could not move settings from {Path.GetFileName(oldPath)}: {exception.Message}");
        }
    }

    /// <summary>
    /// Runs at the end of every ZNetScene.Awake, before any saved objects are created, so pieces
    /// placed in an earlier session find their prefab. The first time registers everything; later
    /// world loads pick up custom entries added since. Jotunn re-adds existing pieces itself.
    /// </summary>
    private static void OnPrefabsRegistered()
    {
        if (Pieces is null)
        {
            return;
        }

        if (Pieces.HasRegistered)
        {
            Pieces.Refresh();
        }
        else
        {
            Pieces.RegisterAll();
        }

        _refreshPending = false;
    }
}

/// <summary>
/// Marks Landscaper pieces as known before Valheim checks for new recipes. Valheim would unlock them
/// anyway once the player knows wood and stone, but it would show a "new piece" message for every
/// one of them on a new character.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
internal static class SilentUnlockPatch
{
    private static void Prefix(HashSet<string> ___m_knownRecipes)
    {
        if (Plugin.Pieces is null)
        {
            return;
        }

        foreach (var name in Plugin.Pieces.PieceNames)
        {
            ___m_knownRecipes.Add(name);
        }
    }
}
