using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;

namespace Landscaper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "landscaper.zackc";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.2.0";

    internal static ManualLogSource Log = null!;
    internal static DecorativePieceManager? Pieces;
    private static bool _iconsDone;
    private static List<BuildTool> _tools = new();
    private static Player? _lastLocalPlayer;

    private void Awake()
    {
        Log = Logger;

        var enabled = Config.Bind("General", "Enabled", true, "Master toggle for the decorative landscaping pieces.");
        var decorativeOnly = Config.Bind("General", "DecorativeOnly", false,
            "When true, placed trees, rocks and plants can't be chopped, mined or picked; remove them with the remove button (middle click) instead. Requires a restart.");
        var cultivator = Config.Bind("Tools", "CultivatorDecorEnabled", true, "Add decorative pieces to the Cultivator menu. Requires a restart.");
        var hoe = Config.Bind("Tools", "HoeDecorEnabled", true, "Add decorative pieces to the Hoe menu. Requires a restart.");
        var hammer = Config.Bind("Tools", "HammerDecorEnabled", true, "Add decorative pieces to the Hammer menu. Requires a restart.");
        var customEntries = Config.Bind("CustomEntries", "Entries", string.Empty,
            "Entries separated by \\n: Display Name|Prefab Name|Tool|Category|Rotation X,Y,Z|Item:Amount,Item:Amount|Scale X,Y,Z. " +
            "Tool is Cultivator, Hoe, or Hammer. Rotation, requirements and scale are optional (leave a field empty to skip it); " +
            "without requirements the piece is free. Scale is one number or X,Y,Z; a scaled entry is a separate variant. " +
            "Find prefab names in game with the landscaper_find console command.");

        CommandManager.Instance.AddConsoleCommand(new FindPrefabCommand());

        if (!enabled.Value)
        {
            Log.LogInfo("Disabled by configuration.");
            return;
        }

        if (cultivator.Value) _tools.Add(BuildTool.Cultivator);
        if (hoe.Value) _tools.Add(BuildTool.Hoe);
        if (hammer.Value) _tools.Add(BuildTool.Hammer);

        Pieces = new DecorativePieceManager(Log, Info.Metadata, customEntries.Value, _tools, decorativeOnly.Value);
        ScaleController.Bind(Config);
        PrefabManager.OnPrefabsRegistered += RegisterPieces;
        new Harmony(ModGuid).PatchAll();
    }

    private void Update()
    {
        if (Pieces is null)
        {
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer is not null && !ReferenceEquals(localPlayer, _lastLocalPlayer))
        {
            _lastLocalPlayer = localPlayer;
            RemovalController.EnableFor(_tools);
        }

        if (!_iconsDone && localPlayer is not null)
        {
            _iconsDone = !Pieces.RenderNextIcon();
        }

        ScaleController.Update();
    }

    /// <summary>
    /// Runs at the end of the first ZNetScene.Awake, before any saved objects are created, so pieces
    /// placed in an earlier session find their prefab. Jotunn re-adds the pieces on later world loads.
    /// </summary>
    private static void RegisterPieces()
    {
        PrefabManager.OnPrefabsRegistered -= RegisterPieces;
        Pieces?.RegisterAll();
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
