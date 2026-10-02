using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Landscaper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
// Pieces only exist for players who have the mod, so every player must have it, at the same minor
// version. A dedicated server doesn't need it: pieces are saved as the game objects they're made from
// (see SavedPieces), which a server without the mod keeps. If the server has it, versions must match.
[NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "utesfan13.landscaper";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.19.0";

    internal static ManualLogSource Log = null!;
    /// <summary>Reads the General.Enabled setting: off hides the pieces and turns off the controls.</summary>
    internal static Func<bool> ModEnabled { get; private set; } = () => true;

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

        var enabled = Config.Bind("General", "Enabled", true,
            "Master toggle. Off hides the pieces from the tool menus and turns off the building controls. Placed pieces stay in " +
            "the world and are still loaded, so turning this off never deletes them.");
        var costsEnabled = Config.Bind("Costs", "Enabled", true,
            Synced("Charge a small cost to place pieces: 5 of the item for pickables that grow back (what one pick gives for crops), 1 iron for metal pieces, otherwise 2 to 8 " +
                   "of the wood, stone or bone of the piece's biome by size, plus 1 resin for crafted light sources. Removing a piece refunds it. " +
                   "Custom entries with their own requirements keep them."));
        var costMultiplier = Config.Bind("Costs", "Multiplier", 1f, new ConfigDescription(
            "Multiplies the automatic costs, e.g. 0.5 for cheaper or 2 for more expensive. Synced from the server in multiplayer.",
            new AcceptableValueRange<float>(0.25f, 4f), synced));
        var allowIndestructible = Config.Bind("Indestructible", "Allowed", true,
            Synced("Allow placing indestructible pieces, which never break from lack of support, weather or attacks."));
        var decorativeOnly = Config.Bind("General", "DecorativeOnly", false,
            Synced("When true, placed trees, rocks and plants can't be chopped, mined or picked; remove them with the remove button (middle click) instead."));
        var ignorePlacementRules = Config.Bind("Placement", "IgnoreRules", true,
            Synced("Allow placing any piece, vanilla build pieces included, where Valheim normally wouldn't (clipping, unsupported, wrong biome, " +
                   "in dungeons, ...), and Hoe and Cultivator pieces on floors and objects as well as the ground. Overlapping a player or creature and other players' wards still block placing."));
        PlacementRules.IgnoreRules = () => ignorePlacementRules.Value;
        var cultivator = Config.Bind("Tools", "CultivatorDecorEnabled", true, Synced("List decorative pieces in the Cultivator menu."));
        var hoe = Config.Bind("Tools", "HoeDecorEnabled", true, Synced("List decorative pieces in the Hoe menu."));
        var hammer = Config.Bind("Tools", "HammerDecorEnabled", true, Synced("List decorative pieces in the Hammer menu."));
        var customEntries = Config.Bind("CustomEntries", "Entries", string.Empty, Synced(
            "Entries separated by \\n: Display Name|Prefab Name|Tool|Category|Rotation X,Y,Z|Item:Amount,Item:Amount|Scale X,Y,Z. " +
            "Tool is Cultivator, Hoe, or Hammer. Rotation, requirements and scale are optional (leave a field empty to skip it); " +
            "rotation is the starting tilt, and without requirements the piece gets the automatic cost. " +
            "Scale is one number or X,Y,Z; a scaled entry is a separate variant. " +
            "Find prefab names in game with the landscaper_find console command. " +
            "Removing an entry hides it from the menu; pieces already placed keep loading until the next restart."));

        CommandManager.Instance.AddConsoleCommand(new FindPrefabCommand());
        CommandManager.Instance.AddConsoleCommand(new RemoveNearbyCommand());
        CommandManager.Instance.AddConsoleCommand(new CheckCommand());
        CommandManager.Instance.AddConsoleCommand(new CostsCommand(costsEnabled));

        // Pieces are registered even when the mod is turned off: a host deletes saved objects whose
        // prefab is missing, so turning it off only hides the pieces and the controls.
        ModEnabled = () => enabled.Value;
        bool ToolEnabled(BuildTool tool) => enabled.Value && tool switch
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
        enabled.SettingChanged += RequestRefresh;
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
        LandscaperTint.CheckSome();
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
/// Unlocks Landscaper pieces like vanilla ones: a piece shows up in the build menu once the player
/// has found everything it's built with (and knows its crafting station, if it needs one). Valheim
/// would show a "new piece" message for each; finding one material can unlock dozens of Landscaper
/// pieces at once, so they're marked known quietly, with one message saying how many.
/// Valheim keeps known pieces by name. Earlier versions of Landscaper marked every piece known
/// straight away, so the first check after loading a character takes out the names of Landscaper
/// pieces whose materials aren't known yet. It never takes out a name another piece also uses (other
/// mods can have pieces called the same, such as a "Dvergr Banner"): Valheim would add it straight
/// back with a "new piece" message, on every check. After that first check names are only added.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
internal static class UnlockLikeVanillaPatch
{
    /// <summary>The player whose first check has run; that one catches up quietly.</summary>
    private static Player? _caughtUp;

    private static void Prefix(Player __instance, HashSet<string> ___m_knownRecipes)
    {
        if (Plugin.Pieces is null || __instance != Player.m_localPlayer)
        {
            return;
        }

        var firstCheck = !ReferenceEquals(_caughtUp, __instance);
        _caughtUp = __instance;
        var otherNames = firstCheck ? NamesOfOtherPieces(__instance) : null;

        var unlocked = 0;
        foreach (var piece in Plugin.Pieces.RegisteredPieces)
        {
            if (__instance.HaveRequirements(piece, Player.RequirementMode.IsKnown))
            {
                if (___m_knownRecipes.Add(piece.m_name))
                {
                    unlocked++;
                }
            }
            else if (otherNames is not null && !otherNames.Contains(piece.m_name))
            {
                ___m_knownRecipes.Remove(piece.m_name);
            }
        }

        if (!firstCheck && unlocked > 0)
        {
            __instance.Message(MessageHud.MessageType.TopLeft, $"{unlocked} new Landscaper piece{(unlocked == 1 ? "" : "s")}");
        }
    }

    /// <summary>Names of the pieces in the player's tools that aren't Landscaper's.</summary>
    private static HashSet<string> NamesOfOtherPieces(Player player)
    {
        var tables = new List<PieceTable>();
        player.GetInventory().GetAllPieceTables(tables);
        return new HashSet<string>(tables.SelectMany(table => table.m_pieces)
            .Where(prefab => prefab != null && !PlacementInput.IsLandscaperPiece(prefab))
            .Select(prefab => prefab.GetComponent<Piece>()?.m_name)
            .OfType<string>());
    }
}
