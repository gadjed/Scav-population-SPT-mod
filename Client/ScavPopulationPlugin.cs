using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ScavPopulation.Client.Patches;

namespace ScavPopulation.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class ScavPopulationPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "gadjed.scavpopulation";
    public const string PluginName = "Scav Population";
    public const string PluginVersion = "1.0.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static ConfigEntry<bool> Debug { get; private set; } = null!;
    public static ConfigEntry<float> StartAfterSeconds { get; private set; } = null!;
    public static ConfigEntry<float> CheckIntervalSeconds { get; private set; } = null!;

    public static ConfigEntry<bool> AfkEnabled { get; private set; } = null!;
    public static ConfigEntry<float> AfkSeconds { get; private set; } = null!;
    public static ConfigEntry<float> AfkMoveThresholdMeters { get; private set; } = null!;
    public static ConfigEntry<float> AfkMinDistanceFromPlayer { get; private set; } = null!;
    public static ConfigEntry<float> TeleportMinDistance { get; private set; } = null!;
    public static ConfigEntry<float> TeleportMaxDistance { get; private set; } = null!;
    public static ConfigEntry<int> MaxTeleportsPerCheck { get; private set; } = null!;
    public static ConfigEntry<bool> IncludeScavs { get; private set; } = null!;
    public static ConfigEntry<bool> IncludePmcs { get; private set; } = null!;
    public static ConfigEntry<bool> ExcludeBosses { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind("1. General", "Enabled", true, "Enable AFK bot relocator.");
        Debug = Config.Bind("1. General", "Debug", false, "Verbose logging.");
        StartAfterSeconds = Config.Bind(
            "1. General",
            "StartAfterSeconds",
            180f,
            new ConfigDescription(
                "Wait this many seconds after raid start before relocating AFK bots.",
                new AcceptableValueRange<float>(30f, 1800f)
            )
        );
        CheckIntervalSeconds = Config.Bind(
            "1. General",
            "CheckIntervalSeconds",
            45f,
            new ConfigDescription(
                "How often to scan for AFK bots.",
                new AcceptableValueRange<float>(10f, 300f)
            )
        );

        AfkEnabled = Config.Bind("2. AFK Relocate", "Enabled", true, "Teleport idle bots closer to the player.");
        AfkSeconds = Config.Bind(
            "2. AFK Relocate",
            "AfkSeconds",
            60f,
            new ConfigDescription(
                "How long a bot must stay nearly still to count as AFK.",
                new AcceptableValueRange<float>(15f, 300f)
            )
        );
        AfkMoveThresholdMeters = Config.Bind(
            "2. AFK Relocate",
            "MoveThresholdMeters",
            2f,
            new ConfigDescription(
                "Max movement distance during AfkSeconds to still count as AFK.",
                new AcceptableValueRange<float>(0.5f, 10f)
            )
        );
        AfkMinDistanceFromPlayer = Config.Bind(
            "2. AFK Relocate",
            "MinDistanceFromPlayer",
            100f,
            new ConfigDescription(
                "Only relocate AFK bots farther than this from the player.",
                new AcceptableValueRange<float>(40f, 500f)
            )
        );
        TeleportMinDistance = Config.Bind(
            "2. AFK Relocate",
            "TeleportMinDistance",
            80f,
            new ConfigDescription(
                "Minimum distance from the player for the teleport destination.",
                new AcceptableValueRange<float>(30f, 200f)
            )
        );
        TeleportMaxDistance = Config.Bind(
            "2. AFK Relocate",
            "TeleportMaxDistance",
            140f,
            new ConfigDescription(
                "Maximum distance from the player for the teleport destination.",
                new AcceptableValueRange<float>(50f, 300f)
            )
        );
        MaxTeleportsPerCheck = Config.Bind(
            "2. AFK Relocate",
            "MaxTeleportsPerCheck",
            2,
            new ConfigDescription(
                "Max bots relocated per check tick.",
                new AcceptableValueRange<int>(1, 8)
            )
        );
        IncludeScavs = Config.Bind("2. AFK Relocate", "IncludeScavs", true, "Relocate AFK scavs.");
        IncludePmcs = Config.Bind("2. AFK Relocate", "IncludePmcs", true, "Relocate AFK PMC bots.");
        ExcludeBosses = Config.Bind("2. AFK Relocate", "ExcludeBosses", true, "Never relocate bosses / followers.");

        new GameWorldPatch().Enable();
        new BotsControllerPatch().Enable();

        Log.LogInfo($"{PluginName} v{PluginVersion} loaded (AFK relocator).");
    }
}
