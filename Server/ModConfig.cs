namespace ScavPopulation;

public class ModConfig
{
    public bool Enabled { get; set; } = true;
    public bool Debug { get; set; }

    /// <summary>Seconds after raid start before reinforcement waves begin.</summary>
    public int StartAfterSeconds { get; set; } = 180;

    /// <summary>Seconds between reinforcement pulses.</summary>
    public int ReinforcementIntervalSeconds { get; set; } = 180;

    public bool ScavEnabled { get; set; } = true;
    public int ScavSlotsMin { get; set; } = 2;
    public int ScavSlotsMax { get; set; } = 4;
    public int ScavWavesPerInterval { get; set; } = 2;
    public string ScavDifficulty { get; set; } = "normal";

    public bool PmcEnabled { get; set; } = true;
    public int PmcSquadMinSize { get; set; } = 1;
    public int PmcSquadMaxSize { get; set; } = 4;
    public int PmcSquadsPerInterval { get; set; } = 1;
    public string PmcDifficulty { get; set; } = "normal";

    /// <summary>Push BotStop near the end of the raid so late waves can still fire.</summary>
    public bool ExtendBotStop { get; set; } = true;

    public List<string> SkipMaps { get; set; } =
    [
        "laboratory",
        "labyrinth",
        "hideout"
    ];
}
