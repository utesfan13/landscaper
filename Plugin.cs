using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Landscaper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "landscaper.zackc";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.1.0";

    private static Plugin? _instance;
    private static DecorativePieceManager? _pieceManager;
    private static bool _registrationAttempted;
    private static Player? _lastLocalPlayer;
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> FreePlacement = null!;
    internal static ConfigEntry<bool> CultivatorDecorEnabled = null!;
    internal static ConfigEntry<bool> HoeDecorEnabled = null!;
    internal static ConfigEntry<bool> HammerDecorEnabled = null!;
    internal static ConfigEntry<string> CustomEntries = null!;

    private void Awake()
    {
        _instance = this;

        Enabled = Config.Bind("General", "Enabled", true, "Master toggle for the decorative landscaping pieces.");
        FreePlacement = Config.Bind("General", "FreePlacement", true, "Place decorative pieces without a cost requirement.");
        CultivatorDecorEnabled = Config.Bind("Tools", "CultivatorDecorEnabled", true, "Allow decorative pieces in the Cultivator menu.");
        HoeDecorEnabled = Config.Bind("Tools", "HoeDecorEnabled", true, "Allow decorative pieces in the Hoe menu.");
        HammerDecorEnabled = Config.Bind("Tools", "HammerDecorEnabled", true, "Allow decorative pieces in the Hammer menu.");
        CustomEntries = Config.Bind("CustomEntries", "Entries", string.Empty, "One entry per line: Display Name|Prefab Name|Tool|Category|Rotation X,Y,Z. Tool is Cultivator, Hoe, or Hammer.");

        Logger.LogInfo("Landscaper: initialising decorative piece registration.");

        if (!Enabled.Value)
        {
            Logger.LogInfo("Landscaper is disabled by configuration.");
            return;
        }

        _pieceManager = new DecorativePieceManager(CustomEntries.Value);
        Logger.LogInfo("Landscaper waiting for Valheim runtime registration tables to become available.");
    }

    private void Update()
    {
        if (_pieceManager is null)
        {
            return;
        }

        if (!_registrationAttempted)
        {
            if (ObjectDB.instance is null || ObjectDB.instance.m_items.Count == 0 || ZNetScene.instance is null)
            {
                return;
            }

            // Register exactly once. Jotunn keeps the custom pieces and re-adds them to the piece
            // tables on later world loads. Retrying every frame makes Jotunn refresh the build
            // categories, which rebuilds the placement ghost every frame and leaves trees invisible.
            _registrationAttempted = true;
            var registeredCount = _pieceManager.TryRegisterBuiltInCatalog();
            Logger.LogInfo($"Landscaper registered {registeredCount} decorative pieces.");
            _lastLocalPlayer = null;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer is not null && !ReferenceEquals(localPlayer, _lastLocalPlayer))
        {
            _lastLocalPlayer = localPlayer;
            _pieceManager.RefreshKnownPieces();
        }
    }

    public static DecorativePieceManager PieceManager => _pieceManager ?? throw new InvalidOperationException("Landscaper is not initialised yet.");

    public static void DumpPrefabSearch(string keyword = "")
    {
        _pieceManager?.DumpPrefabs(keyword);
    }
}
