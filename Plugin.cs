using BepInEx;
using BepInEx.Configuration;

namespace Landscaper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "landscaper.zackc";
    public const string ModName = "Landscaper";
    public const string ModVersion = "0.1.0";

    private static Plugin? _instance;
    private static DecorativePieceManager? _pieceManager;

    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> FreePlacement = null!;
    internal static ConfigEntry<bool> CultivatorDecorEnabled = null!;
    internal static ConfigEntry<bool> HoeDecorEnabled = null!;
    internal static ConfigEntry<bool> HammerDecorEnabled = null!;

    private void Awake()
    {
        _instance = this;

        Enabled = Config.Bind("General", "Enabled", true, "Master toggle for the decorative landscaping pieces.");
        FreePlacement = Config.Bind("General", "FreePlacement", true, "Place decorative pieces without a cost requirement.");
        CultivatorDecorEnabled = Config.Bind("Tools", "CultivatorDecorEnabled", true, "Allow decorative pieces in the Cultivator menu.");
        HoeDecorEnabled = Config.Bind("Tools", "HoeDecorEnabled", true, "Allow decorative pieces in the Hoe menu.");
        HammerDecorEnabled = Config.Bind("Tools", "HammerDecorEnabled", true, "Allow decorative pieces in the Hammer menu.");

        Logger.LogInfo("Landscaper: initialising decorative piece registration.");

        if (!Enabled.Value)
        {
            Logger.LogInfo("Landscaper is disabled by configuration.");
            return;
        }

        _pieceManager = new DecorativePieceManager();
        _pieceManager.RegisterBuiltInCatalog();

        Logger.LogInfo($"Landscaper registered {_pieceManager.RegisteredCount} decorative pieces.");
    }

    public static DecorativePieceManager PieceManager => _pieceManager ?? throw new InvalidOperationException("Landscaper is not initialised yet.");

    public static void DumpPrefabSearch(string keyword = "")
    {
        _pieceManager?.DumpPrefabs(keyword);
    }
}
