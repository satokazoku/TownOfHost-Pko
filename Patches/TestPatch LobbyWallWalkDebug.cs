#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    internal static class TestPatchLobbyWallWalkDebug
    {
        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]
        private static class AmongUsClientStartGamePatch
        {
            public static void Prefix()
            {
                ResetAllForGameStart();
            }
        }

        private enum TargetMode
        {
            NonModClients,
            AllRemotePlayers,
            FocusedPlayer,
        }

        private enum ScanPattern
        {
            GhostOnly,
            GhostReload002,
            GhostReload004,
            GhostReload006,
            GhostDoubleReload002,
        }

        private const string EmptySkinId = "skin_None";
        private const string FallbackNudgeSkinId = "skin_Astronaut";
        private const float GhostRoleVisibleDuration = 0.02f;
        private const float CosmeticNudgeDuration = 0.02f;
        private const float CosmeticRestoreRetryDelay = 0.06f;
        private const float CosmeticRestoreLateDelay = 0.14f;
        private static readonly float[] AutoJoinPulseAttemptDelays = { 0.80f, 1.30f, 1.90f, 2.70f, 3.70f };
        private const float AutoJoinStableDuration = 0.45f;
        private static readonly float[] ManualPulseDelays = { GhostRoleVisibleDuration };
        private const int MaxActionHistory = 100;

        private static readonly HashSet<string> knownLobbyPlayerKeys = new();
        private static readonly HashSet<string> pendingAutoJoinPlayerKeys = new();
        private static readonly Dictionary<string, AutoJoinReadinessState> autoJoinReadinessByKey = new();
        private static bool lobbyJoinWatchInitialized;
        private static TargetMode targetMode;
        private static int focusedIndex;
        private static int runId;
        private static int actionId;
        private static int scanIndex;
        private static int manualDelayIndex;
        private static readonly Queue<string> actionHistory = new();

        private sealed class AutoJoinReadinessState
        {
            public OutfitSnapshot Snapshot;
            public float StableSince;
        }

        private readonly struct OutfitSnapshot : IEquatable<OutfitSnapshot>
        {
            private readonly string playerName;
            private readonly string clientName;
            private readonly string outfitName;
            private readonly int colorId;
            private readonly string hatId;
            private readonly string skinId;
            private readonly string visorId;
            private readonly string petId;

            public OutfitSnapshot(PlayerControl player)
            {
                var outfit = player.Data.DefaultOutfit;
                playerName = player.Data.PlayerName ?? "";
                clientName = player.GetClient()?.PlayerName ?? "";
                outfitName = outfit.PlayerName ?? "";
                colorId = outfit.ColorId;
                hatId = outfit.HatId ?? "";
                skinId = outfit.SkinId ?? "";
                visorId = outfit.VisorId ?? "";
                petId = outfit.PetId ?? "";
            }

            public bool Equals(OutfitSnapshot other)
            {
                return playerName == other.playerName
                    && clientName == other.clientName
                    && outfitName == other.outfitName
                    && colorId == other.colorId
                    && hatId == other.hatId
                    && skinId == other.skinId
                    && visorId == other.visorId
                    && petId == other.petId;
            }

            public override bool Equals(object obj)
            {
                return obj is OutfitSnapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(playerName, clientName, outfitName, colorId, hatId, skinId, visorId, petId);
            }

            public override string ToString()
            {
                return $"{playerName}/{clientName}/{outfitName} color={colorId} hat={hatId} skin={skinId} visor={visorId} pet={petId}";
            }
        }

        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                ResetLobbyJoinWatch();
                return;
            }

            if (!GameStates.IsLobby || GameStates.IsFreePlay)
            {
                ResetLobbyJoinWatch();
                return;
            }

            if (!CanRunDebug()) return;

            DetectLobbyJoinAndAutoPulse();

            if (!Input.GetKey(KeyCode.RightControl)) return;

            if (Input.GetKeyDown(KeyCode.F1))
            {
                MarkObservedSuccess();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.F4))
            {
                RunNextScanPattern();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                ResetOwnerOnlyState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                CycleManualDelay();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                CycleTargetMode();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                CycleFocusedPlayer();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                SetOwnerOnlyRole(RoleTypes.CrewmateGhost, "Ghost only");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                SetOwnerOnlyRole(RoleTypes.Crewmate, "Crewmate only");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                ReloadOwnerOnlyCosmetics("Skin reload");
                return;
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                PulseGhostThenCosmeticReload(GhostRoleVisibleDuration);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                RecordAction("Key F12 Dump NetObjects");
                DumpLobbyNetObjects();
            }
        }

        private static bool CanRunDebug()
        {
            return DebugModeManager.IsDebugMode
                || (DebugModeManager.AmDebugger
                    && DebugModeManager.EnableTOHPDebugMode != null
                    && DebugModeManager.EnableTOHPDebugMode.GetBool());
        }

        private static void CycleTargetMode()
        {
            targetMode = (TargetMode)(((int)targetMode + 1) % Enum.GetValues<TargetMode>().Length);
            RecordAction($"TargetMode -> {targetMode}");
            Logger.seeingame($"[LobbyWallWalkDebug] TargetMode: {targetMode}");
        }

        private static void CycleFocusedPlayer()
        {
            var candidates = GetRemotePlayers().ToList();
            if (candidates.Count == 0)
            {
                Logger.seeingame("[LobbyWallWalkDebug] Focus target: none");
                return;
            }

            focusedIndex = (focusedIndex + 1) % candidates.Count;
            RecordAction($"Focus -> {FormatPlayer(candidates[focusedIndex])}");
            Logger.seeingame($"[LobbyWallWalkDebug] Focus target: {FormatPlayer(candidates[focusedIndex])}");
        }

        private static void CycleManualDelay()
        {
            manualDelayIndex = (manualDelayIndex + 1) % ManualPulseDelays.Length;
            RecordAction($"ManualDelay -> {ManualPulseDelays[manualDelayIndex]:F2}");
            Logger.seeingame($"[LobbyWallWalkDebug] Manual delay: {ManualPulseDelays[manualDelayIndex]:F2}s");
        }

        private static void MarkObservedSuccess()
        {
            var targets = GetTargets().ToList();
            RecordAction("Observed success marker", targets);
            Logger.seeingame("[LobbyWallWalkDebug] Success marked. Check LogOutput.");
            Logger.Info("==== Observed success marker ====", "LobbyWallWalkDebug");
            DumpActionHistory();
            LogTargetStates("Observed success state", targets);
        }

        private static void DetectLobbyJoinAndAutoPulse()
        {
            var currentPlayers = GetRemotePlayers().ToList();
            var currentKeys = currentPlayers
                .Select(GetLobbyPlayerKey)
                .ToHashSet();

            if (!lobbyJoinWatchInitialized)
            {
                knownLobbyPlayerKeys.Clear();
                foreach (var key in currentKeys)
                {
                    knownLobbyPlayerKeys.Add(key);
                }

                lobbyJoinWatchInitialized = true;
                return;
            }

            var joinedPlayers = currentPlayers
                .Where(player => !knownLobbyPlayerKeys.Contains(GetLobbyPlayerKey(player)))
                .ToList();

            knownLobbyPlayerKeys.Clear();
            foreach (var key in currentKeys)
            {
                knownLobbyPlayerKeys.Add(key);
            }

            if (joinedPlayers.Count == 0) return;

            QueueAutoJoinPulse(joinedPlayers);
        }

        private static void ResetLobbyJoinWatch()
        {
            knownLobbyPlayerKeys.Clear();
            pendingAutoJoinPlayerKeys.Clear();
            autoJoinReadinessByKey.Clear();
            lobbyJoinWatchInitialized = false;
        }

        private static void QueueAutoJoinPulse(IReadOnlyList<PlayerControl> joinedPlayers)
        {
            var acceptedKeys = joinedPlayers
                .Select(GetLobbyPlayerKey)
                .Where(key => pendingAutoJoinPlayerKeys.Add(key))
                .ToArray();

            if (acceptedKeys.Length == 0) return;

            RecordAction("AutoJoin detected", joinedPlayers);
            Logger.seeingame($"[LobbyWallWalkDebug] AutoJoin detected: {FormatTargets(joinedPlayers)}");

            for (var i = 0; i < AutoJoinPulseAttemptDelays.Length; i++)
            {
                var attempt = i + 1;
                var isLastAttempt = i == AutoJoinPulseAttemptDelays.Length - 1;
                var delay = AutoJoinPulseAttemptDelays[i];
                var keys = acceptedKeys.ToArray();

                _ = new LateTask(() =>
                {
                    TryAutoJoinPulse(keys, attempt, isLastAttempt);
                }, delay, $"LobbyWallWalkDebug.AutoJoinPulse.{attempt}", true);
            }
        }

        private static void TryAutoJoinPulse(IReadOnlyList<string> joinedKeys, int attempt, bool isLastAttempt)
        {
            if (!GameStates.IsLobby || GameStates.IsFreePlay)
            {
                ResetLobbyJoinWatch();
                return;
            }

            var activeKeys = joinedKeys
                .Where(key => pendingAutoJoinPlayerKeys.Contains(key))
                .ToArray();

            if (activeKeys.Length == 0) return;

            try
            {
                var activeKeySet = activeKeys.ToHashSet();
                var targets = GetRemotePlayers()
                    .Where(player => activeKeySet.Contains(GetLobbyPlayerKey(player)))
                    .Where(player => IsAutoJoinTargetReady(player, attempt, isLastAttempt))
                    .ToList();

                if (targets.Count == 0)
                {
                    RecordAction($"AutoJoin attempt {attempt}: target not ready keys={string.Join(",", activeKeys)}");
                    if (isLastAttempt)
                    {
                        foreach (var key in activeKeys)
                        {
                            pendingAutoJoinPlayerKeys.Remove(key);
                            autoJoinReadinessByKey.Remove(key);
                        }
                    }

                    return;
                }

                PulseGhostThenCosmeticReloadForTargets(
                    targets,
                    GhostRoleVisibleDuration,
                    $"Auto join ghost reload attempt {attempt}");

                foreach (var target in targets)
                {
                    var key = GetLobbyPlayerKey(target);
                    pendingAutoJoinPlayerKeys.Remove(key);
                    autoJoinReadinessByKey.Remove(key);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Auto join ghost reload attempt {attempt} failed: {ex}", "LobbyWallWalkDebug");
                if (isLastAttempt)
                {
                    foreach (var key in activeKeys)
                    {
                        pendingAutoJoinPlayerKeys.Remove(key);
                        autoJoinReadinessByKey.Remove(key);
                    }
                }
            }
        }

        private static void RunNextScanPattern()
        {
            var targets = GetTargets().ToList();
            if (targets.Count == 0)
            {
                RecordAction("Scan: target none");
                Logger.seeingame("[LobbyWallWalkDebug] Scan: target none");
                return;
            }

            var patterns = Enum.GetValues<ScanPattern>();
            var pattern = patterns[scanIndex % patterns.Length];
            scanIndex++;

            var currentRun = ++runId;
            RecordAction($"Scan #{currentRun}: {pattern}", targets);
            Logger.seeingame($"[LobbyWallWalkDebug] Scan #{currentRun}: {pattern} -> {FormatTargets(targets)}");
            LogTargetStates($"Scan #{currentRun} before {pattern}", targets);

            foreach (var target in targets)
            {
                try
                {
                    RunScanPatternForTarget(target, pattern, currentRun);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Scan #{currentRun} failed: {FormatPlayer(target)} {ex}", "LobbyWallWalkDebug");
                }
            }

            QueueStateSnapshots(targets, $"Scan #{currentRun} after {pattern}", 0.35f, 0.9f, 1.8f);
        }

        private static void RunScanPatternForTarget(PlayerControl target, ScanPattern pattern, int currentRun)
        {
            switch (pattern)
            {
                case ScanPattern.GhostOnly:
                    ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                    break;
                case ScanPattern.GhostReload002:
                    ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                    QueueCosmeticReload(target, currentRun, 0.02f);
                    break;
                case ScanPattern.GhostReload004:
                    ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                    QueueCosmeticReload(target, currentRun, 0.04f);
                    break;
                case ScanPattern.GhostReload006:
                    ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                    QueueCosmeticReload(target, currentRun, 0.06f);
                    break;
                case ScanPattern.GhostDoubleReload002:
                    ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                    QueueCosmeticReload(target, currentRun, 0.02f);
                    QueueCosmeticReload(target, currentRun, 0.10f);
                    break;
            }
        }

        private static void SetOwnerOnlyRole(RoleTypes role, string label)
        {
            RunForTargets(label, target => SendRoleRpcToOwner(target, role));
        }

        private static void RefreshOwnerOnlyCosmetics(string label)
        {
            RunForTargets(label, SendCosmeticRpcsToOwner);
        }

        private static void ReloadOwnerOnlyCosmetics(string label)
        {
            var targets = GetTargets().ToList();
            if (targets.Count == 0)
            {
                Logger.seeingame($"[LobbyWallWalkDebug] {label}: target none");
                return;
            }

            var currentRun = ++runId;
            RecordAction($"{label} #{currentRun}", targets);
            LogTargetStates($"{label} #{currentRun} before", targets);
            foreach (var target in targets)
            {
                try
                {
                    ExecuteStep(target, currentRun, "Cosmetics nudge", SendCosmeticNudgeRpcsToOwner);
                    QueueStep(target, currentRun, CosmeticNudgeDuration, "Cosmetics restore", SendCosmeticRpcsToOwner);
                    QueueStep(target, currentRun, CosmeticRestoreRetryDelay, "Cosmetics restore retry", SendCosmeticRpcsToOwner);
                    QueueStep(target, currentRun, CosmeticRestoreLateDelay, "Cosmetics restore late", SendCosmeticRpcsToOwner);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"{label} #{currentRun} failed: {FormatPlayer(target)} {ex}", "LobbyWallWalkDebug");
                }
            }

            QueueStateSnapshots(targets, $"{label} #{currentRun}", 0.35f, 0.9f);
            Logger.seeingame($"[LobbyWallWalkDebug] {label} #{currentRun} -> {FormatTargets(targets)}");
        }

        private static void PulseGhostThenCosmeticReload(float delay)
        {
            PulseGhostThenCosmeticReloadForTargets(
                GetTargets().ToList(),
                delay,
                "Ghost reload");
        }

        private static void PulseGhostThenCosmeticReloadForTargets(IReadOnlyList<PlayerControl> targets, float delay, string label)
        {
            if (targets.Count == 0)
            {
                Logger.seeingame($"[LobbyWallWalkDebug] {label}: target none");
                return;
            }

            var currentRun = ++runId;
            RecordAction($"{label} #{currentRun} delay={delay:F2}", targets);
            foreach (var target in targets)
            {
                ExecuteStep(target, currentRun, "Role Ghost", player => SendRoleRpcToOwner(player, RoleTypes.CrewmateGhost));
                QueueCosmeticReload(target, currentRun, delay);
            }

            QueueStateSnapshots(targets, $"{label} #{currentRun}", delay + 0.35f, delay + 0.9f);
            Logger.seeingame($"[LobbyWallWalkDebug] {label} #{currentRun} -> {FormatTargets(targets)}");
        }

        private static void ResetOwnerOnlyState()
        {
            RunForTargets("Reset", RestoreCrewmateAndCosmetics);
        }

        private static void ResetAllForGameStart()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!CanRunDebug()) return;

            var targets = GetRemotePlayers().ToList();
            ResetLobbyJoinWatch();

            if (targets.Count == 0)
            {
                Logger.Info("GameStart reset: target none", "LobbyWallWalkDebug");
                return;
            }

            var currentRun = ++runId;
            RecordAction($"GameStart reset #{currentRun}", targets);
            foreach (var target in targets)
            {
                try
                {
                    ExecuteStep(target, currentRun, "GameStart reset Crewmate + cosmetics", RestoreCrewmateAndCosmetics);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"GameStart reset #{currentRun} failed: {FormatPlayer(target)} {ex}", "LobbyWallWalkDebug");
                }
            }

            Logger.seeingame($"[LobbyWallWalkDebug] GameStart reset #{currentRun} -> {FormatTargets(targets)}");
        }

        private static void RunForTargets(string label, Action<PlayerControl> action)
        {
            var targets = GetTargets().ToList();
            if (targets.Count == 0)
            {
                Logger.seeingame($"[LobbyWallWalkDebug] {label}: target none");
                return;
            }

            var currentRun = ++runId;
            RecordAction($"{label} #{currentRun}", targets);
            LogTargetStates($"{label} #{currentRun} before", targets);
            foreach (var target in targets)
            {
                try
                {
                    ExecuteStep(target, currentRun, label, action);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"{label} #{currentRun} failed: {FormatPlayer(target)} {ex}", "LobbyWallWalkDebug");
                }
            }

            QueueStateSnapshots(targets, $"{label} #{currentRun}", 0.35f, 0.9f);
            Logger.seeingame($"[LobbyWallWalkDebug] {label} #{currentRun} -> {FormatTargets(targets)}");
        }

        private static void RestoreCrewmateAndCosmetics(PlayerControl target)
        {
            SendRoleRpcToOwner(target, RoleTypes.Crewmate);
            SendCosmeticRpcsToOwner(target);
        }

        private static void QueueCosmeticReload(PlayerControl target, int currentRun, float delay)
        {
            QueueStep(target, currentRun, delay, "Cosmetics nudge", SendCosmeticNudgeRpcsToOwner);
            QueueStep(target, currentRun, delay + CosmeticNudgeDuration, "Cosmetics restore", SendCosmeticRpcsToOwner);
            QueueStep(target, currentRun, delay + CosmeticRestoreRetryDelay, "Cosmetics restore retry", SendCosmeticRpcsToOwner);
            QueueStep(target, currentRun, delay + CosmeticRestoreLateDelay, "Cosmetics restore late", SendCosmeticRpcsToOwner);
        }

        private static void ExecuteStep(PlayerControl target, int currentRun, string step, Action<PlayerControl> action)
        {
            if (target == null || target.GetClient() == null)
            {
                Logger.Warn($"Run #{currentRun} skip {step}: target missing", "LobbyWallWalkDebug");
                return;
            }

            action(target);
            Logger.Info($"Run #{currentRun} {step}: {FormatPlayer(target)} | host={FormatHostState(target)}", "LobbyWallWalkDebug");
        }

        private static void QueueStep(PlayerControl target, int currentRun, float delay, string step, Action<PlayerControl> action)
        {
            _ = new LateTask(() =>
            {
                try
                {
                    if (!GameStates.IsLobby || GameStates.IsFreePlay)
                    {
                        Logger.Info($"Run #{currentRun} skip queued {step}: not lobby", "LobbyWallWalkDebug");
                        return;
                    }

                    ExecuteStep(target, currentRun, step, action);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Run #{currentRun} {step} failed: {FormatPlayer(target)} {ex}", "LobbyWallWalkDebug");
                }
            }, delay, $"LobbyWallWalkDebug.Run{currentRun}.{target?.PlayerId}.{step}", true);
        }

        private static void QueueStateSnapshots(IReadOnlyList<PlayerControl> targets, string label, params float[] delays)
        {
            foreach (var delay in delays)
            {
                var snapshotDelay = delay;
                _ = new LateTask(() =>
                {
                    try
                    {
                        LogTargetStates($"{label} +{snapshotDelay:F2}s", targets);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"{label} snapshot failed: {ex}", "LobbyWallWalkDebug");
                    }
                }, snapshotDelay, $"LobbyWallWalkDebug.State.{label}.{snapshotDelay:F2}", true);
            }
        }

        private static void RecordAction(string action, IEnumerable<PlayerControl> targets = null)
        {
            var targetText = targets == null ? "" : $" targets={FormatTargets(targets)}";
            var line = $"#{++actionId} t={Time.realtimeSinceStartup:F2} mode={targetMode} scan={scanIndex} delay={ManualPulseDelays[manualDelayIndex]:F2} focus={focusedIndex} {action}{targetText}";
            actionHistory.Enqueue(line);
            while (actionHistory.Count > MaxActionHistory)
            {
                actionHistory.Dequeue();
            }

            Logger.Info(line, "LobbyWallWalkDebug");
        }

        private static void DumpActionHistory()
        {
            if (actionHistory.Count == 0)
            {
                Logger.Info("Action history empty", "LobbyWallWalkDebug");
                return;
            }

            foreach (var line in actionHistory)
            {
                Logger.Info($"history {line}", "LobbyWallWalkDebug");
            }
        }

        private static void LogTargetStates(string label, IEnumerable<PlayerControl> targets)
        {
            var targetList = targets?.Where(target => target != null).ToList() ?? new List<PlayerControl>();
            if (targetList.Count == 0)
            {
                Logger.Info($"{label}: target none", "LobbyWallWalkDebug");
                return;
            }

            foreach (var target in targetList)
            {
                Logger.Info($"{label}: {FormatPlayer(target)} | {FormatPlayerPhysicsState(target)} | host={FormatHostState(target)}", "LobbyWallWalkDebug");
            }
        }

        private static void DumpLobbyNetObjects()
        {
            try
            {
                var collection = AmongUsClient.Instance?.allObjects;
                var objects = collection?.allObjects?.ToArray();
                if (objects == null)
                {
                    Logger.seeingame("[LobbyWallWalkDebug] NetObjects: null");
                    return;
                }

                Logger.seeingame($"[LobbyWallWalkDebug] NetObjects dumped: {objects.Length}");
                Logger.Info($"==== Lobby NetObjects count={objects.Length} ====", "LobbyWallWalkDebug");
                for (var i = 0; i < objects.Length; i++)
                {
                    var netObject = objects[i];
                    if (netObject == null)
                    {
                        Logger.Info($"[{i}] null", "LobbyWallWalkDebug");
                        continue;
                    }

                    Logger.Info($"[{i}] {FormatNetObject(netObject)}", "LobbyWallWalkDebug");
                    if (netObject.TryCast<PlayerControl>() is { } player)
                    {
                        Logger.Info($"    {FormatPlayerPhysicsState(player)}", "LobbyWallWalkDebug");
                    }
                }

                var ship = ShipStatus.Instance;
                Logger.Info(ship == null
                    ? "ShipStatus: null"
                    : $"ShipStatus: {ship.name} net={ship.NetId} systems={ship.Systems?.Count ?? -1}",
                    "LobbyWallWalkDebug");
            }
            catch (Exception ex)
            {
                Logger.Warn($"DumpLobbyNetObjects failed: {ex}", "LobbyWallWalkDebug");
            }
        }

        private static string FormatNetObject(InnerNet.InnerNetObject netObject)
        {
            var typeName = netObject.GetIl2CppType()?.Name ?? netObject.GetType().Name;
            var gameObject = netObject.gameObject;
            var active = gameObject != null && gameObject.activeInHierarchy;
            var layer = gameObject != null ? gameObject.layer : -1;
            var pos = netObject.transform != null ? netObject.transform.position : Vector3.zero;
            return $"{typeName} net={netObject.NetId} spawn={netObject.SpawnId} owner={netObject.OwnerId} mode={netObject.sendMode} active={active} layer={layer} pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) name={netObject.name}";
        }

        private static string FormatPlayerPhysicsState(PlayerControl player)
        {
            var collider = player.Collider;
            var body = player.MyPhysics?.body;
            var rigidbodyType = body != null ? body.bodyType.ToString() : "null";
            var simulated = body != null && body.simulated;
            var playerBodyType = player.BodyType.ToString();
            var physicsBodyType = player.MyPhysics != null ? player.MyPhysics.bodyType.ToString() : "null";
            var isDead = player.Data?.IsDead.ToString() ?? "null";
            var netTransform = player.NetTransform;
            var netInfo = netTransform == null
                ? "netTransform=null"
                : $"netPaused={netTransform.isPaused}, lastSid={netTransform.lastSequenceId}";
            var colliderInfo = collider == null
                ? "collider=null"
                : $"colliderEnabled={collider.enabled}, trigger={collider.isTrigger}, colliderLayer={collider.gameObject.layer}";

            return $"playerState id={player.PlayerId}, owner={player.OwnerId}, client={player.GetClientId()}, isDead={isDead}, moveable={player.moveable}, canMove={player.CanMove}, inVent={player.inVent}, walkingToVent={player.walkingToVent}, petting={player.petting}, inMovingPlat={player.inMovingPlat}, onLadder={player.onLadder}, playerBody={playerBodyType}, physicsBody={physicsBodyType}, rigidbody={rigidbodyType}, simulated={simulated}, {colliderInfo}, {netInfo}";
        }

        private static IEnumerable<PlayerControl> GetTargets()
        {
            var remotePlayers = GetRemotePlayers().ToList();

            return targetMode switch
            {
                TargetMode.NonModClients => remotePlayers.Where(player => !player.IsModClient()),
                TargetMode.AllRemotePlayers => remotePlayers,
                TargetMode.FocusedPlayer when remotePlayers.Count > 0 => new[] { remotePlayers[focusedIndex % remotePlayers.Count] },
                _ => Enumerable.Empty<PlayerControl>(),
            };
        }

        private static IEnumerable<PlayerControl> GetRemotePlayers()
        {
            return PlayerCatch.AllPlayerControls
                .Where(player => player != null
                    && !player.AmOwner
                    && !player.isDummy
                    && player.GetClient() != null);
        }

        private static bool IsAutoJoinTargetReady(PlayerControl player, int attempt, bool isLastAttempt)
        {
            if (player == null || player.GetClient() == null || player.GetClientId() < 0 || player.Data?.DefaultOutfit == null)
            {
                return false;
            }

            var key = GetLobbyPlayerKey(player);
            var data = player.Data;
            var outfit = data.DefaultOutfit;
            var clientName = player.GetClient()?.PlayerName ?? "";
            var playerName = data.PlayerName ?? "";
            var outfitName = outfit.PlayerName ?? "";

            if (string.IsNullOrWhiteSpace(clientName)
                || string.IsNullOrWhiteSpace(playerName)
                || string.IsNullOrWhiteSpace(outfitName)
                || outfit.ColorId < 0
                || outfit.ColorId >= Palette.PlayerColors.Length)
            {
                RecordAction($"AutoJoin attempt {attempt}: userdata incomplete {FormatPlayer(player)} clientName='{clientName}' playerName='{playerName}' outfitName='{outfitName}' color={outfit.ColorId}");
                if (isLastAttempt) autoJoinReadinessByKey.Remove(key);
                return false;
            }

            var snapshot = new OutfitSnapshot(player);
            if (!autoJoinReadinessByKey.TryGetValue(key, out var readiness)
                || !readiness.Snapshot.Equals(snapshot))
            {
                autoJoinReadinessByKey[key] = new AutoJoinReadinessState
                {
                    Snapshot = snapshot,
                    StableSince = Time.realtimeSinceStartup,
                };
                RecordAction($"AutoJoin attempt {attempt}: userdata sampled {FormatPlayer(player)} {snapshot}");
                return false;
            }

            var stableTime = Time.realtimeSinceStartup - readiness.StableSince;
            if (stableTime < AutoJoinStableDuration)
            {
                RecordAction($"AutoJoin attempt {attempt}: userdata stabilizing {stableTime:F2}s {FormatPlayer(player)} {snapshot}");
                return false;
            }

            RecordAction($"AutoJoin attempt {attempt}: userdata ready stable={stableTime:F2}s {FormatPlayer(player)} {snapshot}");
            return true;
        }

        private static string GetLobbyPlayerKey(PlayerControl player)
        {
            if (player == null) return "null";

            var clientId = player.GetClientId();
            if (clientId >= 0) return $"client:{clientId}";

            if (player.OwnerId >= 0) return $"owner:{player.OwnerId}";

            return $"player:{player.PlayerId}:net:{player.NetId}";
        }

        private static void SendRoleRpcToOwner(PlayerControl target, RoleTypes role)
        {
            var clientId = target.GetClientId();
            if (clientId < 0) return;

            var writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, clientId);
            writer.Write((ushort)role);
            writer.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static void SendCosmeticRpcsToOwner(PlayerControl target)
        {
            if (target?.Data?.DefaultOutfit == null) return;

            var clientId = target.GetClientId();
            if (clientId < 0) return;

            var outfit = target.Data.DefaultOutfit;
            var color = (byte)outfit.ColorId;
            SendOutfitRpcsToOwner(target, clientId, color, outfit.HatId, outfit.SkinId, outfit.VisorId, 2);
        }

        private static void SendCosmeticNudgeRpcsToOwner(PlayerControl target)
        {
            if (target?.Data?.DefaultOutfit == null) return;

            var clientId = target.GetClientId();
            if (clientId < 0) return;

            var outfit = target.Data.DefaultOutfit;
            var nudgeColor = (byte)((outfit.ColorId + 1) % Palette.PlayerColors.Length);
            var nudgeSkin = GetNudgeSkinId(outfit.SkinId);
            SendOutfitRpcsToOwner(target, clientId, nudgeColor, outfit.HatId, nudgeSkin, outfit.VisorId, 2);
        }

        private static string GetNudgeSkinId(string currentSkinId)
        {
            return string.Equals(currentSkinId, EmptySkinId, StringComparison.OrdinalIgnoreCase)
                ? FallbackNudgeSkinId
                : EmptySkinId;
        }

        private static void SendOutfitRpcsToOwner(PlayerControl target, int clientId, byte color, string hatId, string skinId, string visorId, int sequenceAdvance)
        {
            var hatSequenceId = AdvanceRpcSequenceId(target, RpcCalls.SetHatStr, sequenceAdvance);
            var skinSequenceId = AdvanceRpcSequenceId(target, RpcCalls.SetSkinStr, sequenceAdvance);
            var visorSequenceId = AdvanceRpcSequenceId(target, RpcCalls.SetVisorStr, sequenceAdvance);

            SendOutfitDataToOwner(target, clientId, color, hatId, skinId, visorId);

            SendRpcToOwner(target, RpcCalls.SetColor, clientId, writer =>
            {
                writer.Write(target.Data.NetId);
                writer.Write(color);
            });
            SendRpcToOwner(target, RpcCalls.SetHatStr, clientId, writer =>
            {
                writer.Write(hatId ?? "");
                writer.Write(hatSequenceId);
            });
            SendRpcToOwner(target, RpcCalls.SetSkinStr, clientId, writer =>
            {
                writer.Write(skinId ?? "");
                writer.Write(skinSequenceId);
            });
            SendRpcToOwner(target, RpcCalls.SetVisorStr, clientId, writer =>
            {
                writer.Write(visorId ?? "");
                writer.Write(visorSequenceId);
            });
        }

        private static void SendOutfitDataToOwner(PlayerControl target, int clientId, byte color, string hatId, string skinId, string visorId)
        {
            var data = target?.Data;
            var outfit = data?.DefaultOutfit;
            if (data == null || outfit == null) return;

            var originalColor = outfit.ColorId;
            var originalHat = outfit.HatId;
            var originalSkin = outfit.SkinId;
            var originalVisor = outfit.VisorId;

            try
            {
                outfit.ColorId = color;
                outfit.HatId = hatId ?? "";
                outfit.SkinId = skinId ?? "";
                outfit.VisorId = visorId ?? "";

                var sender = CustomRpcSender.Create("LobbyWallWalkDebug.OutfitData", SendOption.Reliable);
                sender.StartMessage(clientId);
                sender.Write(writer =>
                {
                    writer.StartMessage(1);
                    writer.WritePacked(data.NetId);
                    data.Serialize(writer, false);
                    writer.EndMessage();
                }, true);
                sender.EndMessage();
                sender.SendMessage();
            }
            finally
            {
                outfit.ColorId = originalColor;
                outfit.HatId = originalHat;
                outfit.SkinId = originalSkin;
                outfit.VisorId = originalVisor;
            }
        }

        private static byte AdvanceRpcSequenceId(PlayerControl target, RpcCalls rpcCall, int count)
        {
            var steps = Math.Max(1, count);
            byte sequenceId = 0;
            for (var i = 0; i < steps; i++)
            {
                sequenceId = target.GetNextRpcSequenceId(rpcCall);
            }

            return sequenceId;
        }

        private static void SendRpcToOwner(PlayerControl target, RpcCalls rpcCall, int clientId, Action<MessageWriter> write)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)rpcCall, SendOption.Reliable, clientId);
            write(writer);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static string FormatTargets(IEnumerable<PlayerControl> targets)
        {
            return string.Join(", ", targets.Select(FormatPlayer));
        }

        private static string FormatPlayer(PlayerControl player)
        {
            if (player == null) return "null";
            var clientName = player.GetClient()?.PlayerName ?? player.name ?? "unknown";
            var modState = player.IsModClient() ? "Mod" : "Vanilla";
            return $"{clientName.RemoveHtmlTags()}#{player.PlayerId}/{modState}";
        }

        private static string FormatHostState(PlayerControl player)
        {
            if (player?.Data == null) return "no data";
            return $"roleType={player.Data.RoleType}, role={player.Data.Role?.Role}";
        }
    }
}
#endif
