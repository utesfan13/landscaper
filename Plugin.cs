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
    public const string ModGuid = "landscaper.zackc";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.3.0";

    internal static ManualLogSource Log = null!;
    internal static DecorativePieceManager? Pieces;
    private static bool _iconsDone;
    private static bool _refreshPending;
    private static Player? _lastLocalPlayer;

    private void Awake()
    {
        Log = Logger;

        // Settings that must match between players are admin-only: Jotunn syncs them from the server
        // when joining, and only admins can change them in game.
        var synced = new ConfigurationManagerAttributes { IsAdminOnly = true };
        ConfigDescription Synced(string description) => new($"{description} Synced from the server in multiplayer.", null, synced);

        var enabled = Config.Bind("General", "Enabled", true, "Master toggle for the decorative landscaping pieces. Requires a restart.");
        var decorativeOnly = Config.Bind("General", "DecorativeOnly", false,
            Synced("When true, placed trees, rocks and plants can't be chopped, mined or picked; remove them with the remove button (middle click) instead."));
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

        // Synced values arrive after the world has started loading, and admins can change them in
        // game, so apply changes as they come instead of only at startup.
        void RequestRefresh(object sender, EventArgs args) => _refreshPending = true;
        cultivator.SettingChanged += RequestRefresh;
        hoe.SettingChanged += RequestRefresh;
        hammer.SettingChanged += RequestRefresh;
        customEntries.SettingChanged += RequestRefresh;

        ScaleController.Bind(Config);
        TintController.Bind(Config);
        HeightController.Bind(Config);
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

        ScaleController.Update();
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
/// Marks free Landscaper pieces as known before Valheim checks for new recipes. Valheim would unlock
/// them anyway, but it would show a "new piece" message for every one of them on a new character.
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

        foreach (var name in Plugin.Pieces.FreePieceNames)
        {
            ___m_knownRecipes.Add(name);
        }
    }
}
