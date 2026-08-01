using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace ScavPopulation.Client;

public class PopulationMaintainerComponent : MonoBehaviour
{
    public static BotsController? BotsController { get; set; }

    private GameWorld? _gameWorld;
    private float _raidStartTime;
    private float _nextCheckTime;
    private readonly Dictionary<string, AfkTracker> _trackers = new();

    public void Init(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
        _raidStartTime = Time.time;
        _nextCheckTime = _raidStartTime + ScavPopulationPlugin.StartAfterSeconds.Value;
    }

    private void OnDestroy()
    {
        _trackers.Clear();
        BotsController = null;
    }

    private void Update()
    {
        if (!ScavPopulationPlugin.Enabled.Value || !ScavPopulationPlugin.AfkEnabled.Value)
        {
            return;
        }

        if (_gameWorld == null || Time.time < _nextCheckTime)
        {
            return;
        }

        _nextCheckTime = Time.time + ScavPopulationPlugin.CheckIntervalSeconds.Value;

        try
        {
            RelocateAfkBots();
        }
        catch (System.Exception ex)
        {
            ScavPopulationPlugin.Log.LogError($"[ScavPopulation] AFK tick failed: {ex}");
        }
    }

    private void RelocateAfkBots()
    {
        var player = _gameWorld?.MainPlayer;
        if (player == null || !player.HealthController.IsAlive)
        {
            return;
        }

        var botsController = BotsController ?? TryFindBotsController();
        if (botsController?.BotSpawner == null)
        {
            return;
        }

        var playerPos = player.Position;
        var relocated = 0;
        var max = ScavPopulationPlugin.MaxTeleportsPerCheck.Value;
        var afkSeconds = ScavPopulationPlugin.AfkSeconds.Value;
        var moveThreshold = ScavPopulationPlugin.AfkMoveThresholdMeters.Value;
        var minDist = ScavPopulationPlugin.AfkMinDistanceFromPlayer.Value;

        foreach (var botPlayer in _gameWorld!.AllAlivePlayersList)
        {
            if (relocated >= max)
            {
                break;
            }

            if (botPlayer == null || !botPlayer.IsAI || botPlayer.IsYourPlayer || !botPlayer.HealthController.IsAlive)
            {
                continue;
            }

            var botOwner = botPlayer.AIData?.BotOwner;
            if (botOwner == null || botOwner.BotState != EBotState.Active)
            {
                continue;
            }

            if (!IsEligibleRole(botOwner))
            {
                continue;
            }

            if (IsInCombat(botOwner))
            {
                ResetTracker(botOwner);
                continue;
            }

            var profileId = botOwner.Profile.ProfileId;
            var pos = botOwner.Position;
            var distToPlayer = Vector3.Distance(pos, playerPos);
            if (distToPlayer < minDist)
            {
                ResetTracker(botOwner);
                continue;
            }

            if (!_trackers.TryGetValue(profileId, out var tracker))
            {
                tracker = new AfkTracker { LastPosition = pos, StillSince = Time.time };
                _trackers[profileId] = tracker;
                continue;
            }

            if (Vector3.Distance(pos, tracker.LastPosition) > moveThreshold)
            {
                tracker.LastPosition = pos;
                tracker.StillSince = Time.time;
                continue;
            }

            var standbyAfk =
                botOwner.StandBy != null
                && (botOwner.StandBy.StandByType == BotStandByType.active
                    || botOwner.StandBy.StandByType == BotStandByType.paused);

            var idleLongEnough = Time.time - tracker.StillSince >= afkSeconds;
            if (!idleLongEnough && !standbyAfk)
            {
                continue;
            }

            // Standby alone is enough only if they have also been still for half the AFK window.
            if (standbyAfk && Time.time - tracker.StillSince < afkSeconds * 0.5f)
            {
                continue;
            }

            if (!TryFindTeleportPoint(playerPos, out var destination))
            {
                if (ScavPopulationPlugin.Debug.Value)
                {
                    ScavPopulationPlugin.Log.LogWarning("[ScavPopulation] No NavMesh teleport point found.");
                }
                continue;
            }

            botsController.DevelopmentTeleportBot(botOwner, destination);
            tracker.LastPosition = destination;
            tracker.StillSince = Time.time;
            relocated++;

            if (ScavPopulationPlugin.Debug.Value)
            {
                ScavPopulationPlugin.Log.LogInfo(
                    $"[ScavPopulation] Relocated AFK bot {botOwner.Profile.Nickname} ({distToPlayer:F0}m -> {Vector3.Distance(destination, playerPos):F0}m)."
                );
            }
        }

        PruneTrackers();
    }

    private static bool IsInCombat(BotOwner bot)
    {
        var memory = bot.Memory;
        if (memory == null)
        {
            return false;
        }

        return memory.HaveEnemy || memory.IsUnderFire || !memory.IsPeace;
    }

    private static bool IsEligibleRole(BotOwner bot)
    {
        var role = bot.Profile.Info.Settings.Role;
        var roleName = role.ToString();

        if (ScavPopulationPlugin.ExcludeBosses.Value)
        {
            if (roleName.StartsWith("boss", System.StringComparison.OrdinalIgnoreCase)
                || roleName.StartsWith("follower", System.StringComparison.OrdinalIgnoreCase)
                || roleName.StartsWith("sectant", System.StringComparison.OrdinalIgnoreCase)
                || role == WildSpawnType.pmcBot
                || role == WildSpawnType.exUsec
                || role == WildSpawnType.arenaFighter)
            {
                return false;
            }
        }

        var isPmc = role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR;
        if (isPmc)
        {
            return ScavPopulationPlugin.IncludePmcs.Value;
        }

        return ScavPopulationPlugin.IncludeScavs.Value;
    }

    private bool TryFindTeleportPoint(Vector3 playerPos, out Vector3 destination)
    {
        var min = ScavPopulationPlugin.TeleportMinDistance.Value;
        var max = Mathf.Max(min + 1f, ScavPopulationPlugin.TeleportMaxDistance.Value);

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            var radius = Random.Range(min, max);
            var candidate = playerPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (NavMesh.SamplePosition(candidate, out var hit, 12f, NavMesh.AllAreas))
            {
                destination = hit.position;
                return true;
            }
        }

        destination = Vector3.zero;
        return false;
    }

    private void ResetTracker(BotOwner bot)
    {
        var id = bot.Profile.ProfileId;
        if (_trackers.TryGetValue(id, out var tracker))
        {
            tracker.LastPosition = bot.Position;
            tracker.StillSince = Time.time;
        }
    }

    private void PruneTrackers()
    {
        if (_gameWorld == null)
        {
            _trackers.Clear();
            return;
        }

        var alive = new HashSet<string>();
        foreach (var p in _gameWorld.AllAlivePlayersList)
        {
            if (p?.Profile?.ProfileId != null)
            {
                alive.Add(p.Profile.ProfileId);
            }
        }

        var stale = new List<string>();
        foreach (var id in _trackers.Keys)
        {
            if (!alive.Contains(id))
            {
                stale.Add(id);
            }
        }

        foreach (var id in stale)
        {
            _trackers.Remove(id);
        }
    }

    private BotsController? TryFindBotsController()
    {
        if (Singleton<IBotGame>.Instantiated)
        {
            return Singleton<IBotGame>.Instance.BotsController;
        }

        return null;
    }

    private sealed class AfkTracker
    {
        public Vector3 LastPosition;
        public float StillSince;
    }
}
