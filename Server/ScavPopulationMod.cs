using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using Path = System.IO.Path;

namespace ScavPopulation;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "gadjed.scavpopulation";
    public override string Name { get; init; } = "Scav Population";
    public override string Author { get; init; } = "gadjed";
    public override List<string>? Contributors { get; init; } = null;
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; } = null;
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
    public override string? Url { get; init; } = "https://github.com/gadjed/Scav-population-SPT-mod";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class ScavPopulationMod(
    ISptLogger<ScavPopulationMod> logger,
    ModHelper modHelper,
    DatabaseService databaseService,
    RandomUtil randomUtil
) : IOnLoad
{
    private const string Tag = "[ScavPopulation]";

    public Task OnLoad()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "config.json");
        var config = File.Exists(configPath)
            ? modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json")
            : new ModConfig();

        if (!config.Enabled)
        {
            logger.Warning($"{Tag} Disabled via config.");
            return Task.CompletedTask;
        }

        Sanitize(config);

        var skip = new HashSet<string>(
            config.SkipMaps.Select(m => m.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase
        );

        var mapsTouched = 0;
        var scavWavesAdded = 0;
        var pmcSquadsAdded = 0;

        foreach (var (locationId, location) in databaseService.GetLocations().GetDictionary())
        {
            var id = locationId.ToLowerInvariant();
            if (skip.Contains(id) || location?.Base is null)
            {
                continue;
            }

            var locationBase = location.Base;
            if (locationBase.EscapeTimeLimit is null or <= 0)
            {
                continue;
            }

            var raidSeconds = (int)Math.Round(locationBase.EscapeTimeLimit.Value * 60.0);
            if (raidSeconds <= config.StartAfterSeconds)
            {
                continue;
            }

            if (config.ExtendBotStop)
            {
                // Keep wave activation window open almost until extract time.
                var botStop = locationBase.BotStop ?? 0;
                locationBase.BotStop = Math.Max(botStop, raidSeconds - 60);
            }

            var zones = GetZones(locationBase);
            var times = BuildPulseTimes(
                config.StartAfterSeconds,
                config.ReinforcementIntervalSeconds,
                raidSeconds
            );

            if (times.Count == 0)
            {
                continue;
            }

            var nextWaveNumber = (locationBase.Waves?.Count ?? 0) + 1000;
            var scavAdded = 0;
            var pmcAdded = 0;

            foreach (var time in times)
            {
                if (config.ScavEnabled)
                {
                    scavAdded += AddScavReinforcements(
                        locationBase,
                        zones,
                        time,
                        config,
                        ref nextWaveNumber
                    );
                }

                if (config.PmcEnabled)
                {
                    pmcAdded += AddPmcSquads(locationBase, zones, time, config);
                }
            }

            if (scavAdded > 0 || pmcAdded > 0)
            {
                mapsTouched++;
                scavWavesAdded += scavAdded;
                pmcSquadsAdded += pmcAdded;

                logger.LogWithColor(
                    $"{Tag} {id}: +{scavAdded} scav wave(s), +{pmcAdded} PMC squad(s) across {times.Count} pulse(s) (raid {raidSeconds}s).",
                    LogTextColor.Cyan
                );

                if (config.Debug)
                {
                    logger.Info($"{Tag} {id} pulse times: {string.Join(", ", times)}");
                }
            }
        }

        logger.Success(
            $"{Tag} Ready on {mapsTouched} map(s): {scavWavesAdded} scav reinforcement wave(s), {pmcSquadsAdded} PMC squad spawn(s)."
        );
        return Task.CompletedTask;
    }

    private static void Sanitize(ModConfig config)
    {
        config.StartAfterSeconds = Math.Max(60, config.StartAfterSeconds);
        config.ReinforcementIntervalSeconds = Math.Max(60, config.ReinforcementIntervalSeconds);
        config.ScavSlotsMin = Math.Max(1, config.ScavSlotsMin);
        config.ScavSlotsMax = Math.Max(config.ScavSlotsMin, config.ScavSlotsMax);
        config.ScavWavesPerInterval = Math.Max(1, config.ScavWavesPerInterval);
        config.PmcSquadMinSize = Math.Clamp(config.PmcSquadMinSize, 1, 4);
        config.PmcSquadMaxSize = Math.Clamp(config.PmcSquadMaxSize, config.PmcSquadMinSize, 4);
        config.PmcSquadsPerInterval = Math.Max(1, config.PmcSquadsPerInterval);

        if (string.IsNullOrWhiteSpace(config.ScavDifficulty))
        {
            config.ScavDifficulty = "normal";
        }

        if (string.IsNullOrWhiteSpace(config.PmcDifficulty))
        {
            config.PmcDifficulty = "normal";
        }
    }

    private static List<int> BuildPulseTimes(int startAfter, int interval, int raidSeconds)
    {
        var times = new List<int>();
        var lastSafe = raidSeconds - 90;
        for (var t = startAfter; t <= lastSafe; t += interval)
        {
            times.Add(t);
        }

        return times;
    }

    private static List<string> GetZones(LocationBase location)
    {
        var zones = new List<string>();

        if (!string.IsNullOrWhiteSpace(location.OpenZones))
        {
            zones.AddRange(
                location.OpenZones
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(z => !string.IsNullOrWhiteSpace(z))
            );
        }

        if (zones.Count == 0 && location.Waves is not null)
        {
            zones.AddRange(
                location.Waves
                    .Select(w => w.SpawnPoints)
                    .Where(z => !string.IsNullOrWhiteSpace(z))!
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            );
        }

        if (zones.Count == 0)
        {
            zones.Add("BotZone");
        }

        return zones;
    }

    private int AddScavReinforcements(
        LocationBase location,
        List<string> zones,
        int time,
        ModConfig config,
        ref int waveNumber
    )
    {
        location.Waves ??= [];
        var added = 0;

        for (var i = 0; i < config.ScavWavesPerInterval; i++)
        {
            var zone = zones[randomUtil.GetInt(0, zones.Count - 1)];
            var slots = randomUtil.GetInt(config.ScavSlotsMin, config.ScavSlotsMax);

            location.Waves.Add(
                new Wave
                {
                    BotPreset = config.ScavDifficulty,
                    BotSide = "Savage",
                    KeepZoneOnSpawn = false,
                    SpawnPoints = zone,
                    WildSpawnType = WildSpawnType.assault,
                    IsPlayers = false,
                    Number = waveNumber++,
                    SlotsMin = Math.Max(0, slots - 1),
                    SlotsMax = slots,
                    TimeMin = time,
                    TimeMax = time + 90,
                    ChanceGroup = 100,
                    SpawnMode = new HashSet<string> { "regular", "pve" },
                    OpenZones = zone,
                    SptId = $"scavpop-scav-{location.Id}-{time}-{i}"
                }
            );
            added++;
        }

        return added;
    }

    private int AddPmcSquads(
        LocationBase location,
        List<string> zones,
        int time,
        ModConfig config
    )
    {
        location.BossLocationSpawn ??= [];
        var added = 0;

        for (var i = 0; i < config.PmcSquadsPerInterval; i++)
        {
            var zone = zones[randomUtil.GetInt(0, zones.Count - 1)];
            var squadSize = randomUtil.GetInt(config.PmcSquadMinSize, config.PmcSquadMaxSize);
            location.BossLocationSpawn.Add(CreatePmcSquad(squadSize, config.PmcDifficulty, zone, time));
            added++;
        }

        return added;
    }

    private BossLocationSpawn CreatePmcSquad(int squadSize, string difficulty, string zone, int time)
    {
        var type = randomUtil.GetBool() ? "pmcBEAR" : "pmcUSEC";
        var escortAmount = Math.Max(0, squadSize - 1).ToString();
        List<BossSupport>? supports = null;

        if (squadSize > 1)
        {
            supports =
            [
                new BossSupport
                {
                    BossEscortType = type,
                    BossEscortDifficulty = new ListOrT<string>([difficulty], null),
                    BossEscortAmount = escortAmount
                }
            ];
        }

        return new BossLocationSpawn
        {
            BossChance = 100,
            BossDifficulty = difficulty,
            BossEscortAmount = escortAmount,
            BossEscortDifficulty = difficulty,
            BossEscortType = type,
            BossName = type,
            IsBossPlayer = true,
            BossZone = zone,
            IsRandomTimeSpawn = true,
            ShowOnTarkovMap = false,
            ShowOnTarkovMapPvE = false,
            Time = time,
            TriggerId = "",
            TriggerName = "",
            ForceSpawn = true,
            IgnoreMaxBots = true,
            Supports = supports,
            SptId = $"scavpop-pmc-{type}-{zone}-{time}-{squadSize}",
            SpawnMode = new List<string> { "regular", "pve" }
        };
    }
}
