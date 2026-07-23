using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace TownOfHost
{
    public static class TestBotManager
    {
        private const byte MaxPlayerId = 15;
        public const int MaxLegacySnrBotCount = 10;
        private const string DefaultLegacySnrBotNamePrefix = "SNR TestBot";
        private static readonly Vector2 HiddenPosition = new(9999f, 9999f);
        private static readonly List<PlayerControl> allBots = new();
        private static readonly HashSet<byte> botPlayerIds = new();
        private static readonly HashSet<byte> spawnedBotPlayerIds = new();
        private static readonly HashSet<byte> hiddenBotPlayerIds = new();
        private static int pendingLegacySnrBotCount;
        private static string pendingLegacySnrBotNamePrefix = DefaultLegacySnrBotNamePrefix;
        private static bool pendingLegacySnrBotsHidden = true;
        private static float keepHiddenTimer;

        public static IReadOnlyList<PlayerControl> AllBots => allBots;
        public static IReadOnlyCollection<byte> BotPlayerIds => botPlayerIds;
        public static int PendingLegacySnrBotCount => pendingLegacySnrBotCount;

        public static bool IsTestBot(this PlayerControl player)
            => player != null && botPlayerIds.Contains(player.PlayerId);

        public static bool CanSpawnInCurrentRoom(out string reason)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                reason = "host only";
                return false;
            }
            if (!GameStates.IsLobby)
            {
                reason = "lobby only";
                return false;
            }
            if (GameStates.IsOnlineGame)
            {
                reason = "online servers reject fake PlayerControl spawns";
                return false;
            }

            reason = "";
            return true;
        }

        public static PlayerControl Spawn(string name = null, RoleTypes? role = null, bool hideInLobby = false)
        {
            if (!CanSpawnInCurrentRoom(out var reason))
            {
                Logger.Warn($"Test bot spawn was rejected: {reason}.", nameof(TestBotManager));
                return null;
            }
            if (AmongUsClient.Instance.PlayerPrefab == null || GameData.Instance == null)
            {
                Logger.Warn("Test bot spawn failed: PlayerPrefab or GameData is null.", nameof(TestBotManager));
                return null;
            }
            if (!TryGetAvailablePlayerId(out var id))
            {
                Logger.Warn("Test bot spawn failed: no free PlayerId.", nameof(TestBotManager));
                return null;
            }

            var bot = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
            bot.isDummy = true;
            bot.PlayerId = id;
            bot.NetTransform.enabled = true;

            var playerInfo = GameData.Instance.AddDummy(bot);
            AmongUsClient.Instance.Spawn(bot);
            var dummyBehaviour = bot.GetComponent<DummyBehaviour>();
            if (dummyBehaviour != null)
                dummyBehaviour.enabled = false;

            Register(bot, hideInLobby, spawned: true);

            var botName = string.IsNullOrWhiteSpace(name) ? $"TOHP TestBot {id}" : name.Trim();
            var colorId = GetUnusedColorId(id);

            var spawnPosition = hideInLobby ? HiddenPosition : GetVisibleSpawnPosition(id);
            bot.transform.position = spawnPosition;
            bot.NetTransform.SnapTo(spawnPosition);
            bot.RpcSetName(botName);
            bot.RpcSetColor(colorId);
            SetEmptyCosmetics(bot);
            playerInfo?.RpcSetTasks(Array.Empty<byte>());

            if (role.HasValue)
                bot.RpcSetRole(role.Value, true);

            SendSync(bot.PlayerId, true);
            Logger.Info($"Spawned test bot. PlayerId:{bot.PlayerId}, Name:{botName}, Hidden:{hideInLobby}", nameof(TestBotManager));
            return bot;
        }

        public static int ArmLegacySnrStartBots(int count = 1, string namePrefix = null, bool hidden = true)
        {
            if (count < 1)
                count = 1;
            else if (count > MaxLegacySnrBotCount)
                count = MaxLegacySnrBotCount;

            pendingLegacySnrBotCount = count;
            pendingLegacySnrBotNamePrefix = string.IsNullOrWhiteSpace(namePrefix)
                ? DefaultLegacySnrBotNamePrefix
                : namePrefix.Trim();
            pendingLegacySnrBotsHidden = hidden;

            Logger.Info($"Armed legacy SNR bot spawn. Count:{pendingLegacySnrBotCount}, Prefix:{pendingLegacySnrBotNamePrefix}, Hidden:{pendingLegacySnrBotsHidden}", nameof(TestBotManager));
            return pendingLegacySnrBotCount;
        }

        public static void CancelLegacySnrStartBots()
        {
            pendingLegacySnrBotCount = 0;
            pendingLegacySnrBotNamePrefix = DefaultLegacySnrBotNamePrefix;
            pendingLegacySnrBotsHidden = true;
            Logger.Info("Canceled legacy SNR bot spawn.", nameof(TestBotManager));
        }

        public static PlayerControl SpawnLegacySnr(string name = null, RoleTypes? role = null, bool hidden = true)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                Logger.Warn("Legacy SNR bot spawn failed: host only.", nameof(TestBotManager));
                return null;
            }
            if (AmongUsClient.Instance.PlayerPrefab == null || GameData.Instance == null)
            {
                Logger.Warn("Legacy SNR bot spawn failed: PlayerPrefab or GameData is null.", nameof(TestBotManager));
                return null;
            }
            if (!TryGetAvailablePlayerId(out var id))
            {
                Logger.Warn("Legacy SNR bot spawn failed: no free PlayerId.", nameof(TestBotManager));
                return null;
            }

            var bot = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
            bot.PlayerId = id;
            bot.NetTransform.enabled = true;

            NetworkedPlayerInfo playerInfo;
            try
            {
                playerInfo = GameData.Instance.AddPlayer(bot, null);
                AmongUsClient.Instance.Spawn(bot, -2, SpawnFlags.IsClientCharacter);
            }
            catch (Exception e)
            {
                Logger.Warn($"Legacy SNR bot spawn failed during AddPlayer/Spawn: {e}", nameof(TestBotManager));
                try
                {
                    GameData.Instance?.RemovePlayer(id);
                    UnityEngine.Object.Destroy(bot.gameObject);
                }
                catch { }
                return null;
            }

            Register(bot, hidden, spawned: true);

            var botName = string.IsNullOrWhiteSpace(name) ? $"{DefaultLegacySnrBotNamePrefix} {id}" : name.Trim();
            var colorId = GetUnusedColorId(id);
            var spawnPosition = hidden ? HiddenPosition : GetVisibleSpawnPosition(id);

            bot.transform.position = spawnPosition;
            bot.NetTransform.SnapTo(spawnPosition);
            bot.RpcSetName(botName);
            bot.RpcSetColor(colorId);
            SetEmptyCosmetics(bot);
            playerInfo?.RpcSetTasks(Array.Empty<byte>());

            bot.RpcSetRole(role ?? RoleTypes.Crewmate, true);

            SendSync(bot.PlayerId, true);
            Logger.Info($"Spawned legacy SNR test bot. PlayerId:{bot.PlayerId}, Name:{botName}, Hidden:{hidden}", nameof(TestBotManager));
            return bot;
        }

        public static void Despawn(PlayerControl bot)
        {
            if (bot == null) return;
            var playerId = bot.PlayerId;

            if (!spawnedBotPlayerIds.Contains(playerId))
            {
                Unregister(playerId);
                SendSync(playerId, false);
                Logger.Info($"Unmarked real-client bot. PlayerId:{playerId}", nameof(TestBotManager));
                return;
            }

            try
            {
                GameData.Instance?.RemovePlayer(playerId);
                AmongUsClient.Instance?.Despawn(bot);
            }
            catch (Exception e)
            {
                Logger.Warn($"Test bot despawn hit an exception: {e}", nameof(TestBotManager));
                try
                {
                    bot.Despawn();
                }
                catch (Exception inner)
                {
                    Logger.Warn($"Fallback test bot despawn failed: {inner}", nameof(TestBotManager));
                }
            }

            Unregister(playerId);
            SendSync(playerId, false);
            Logger.Info($"Despawned test bot. PlayerId:{playerId}", nameof(TestBotManager));
        }

        public static void DespawnAll()
        {
            foreach (var bot in allBots.ToArray())
                Despawn(bot);

            ClearLocal();
        }

        public static void ClearLocal()
        {
            allBots.Clear();
            botPlayerIds.Clear();
            spawnedBotPlayerIds.Clear();
            hiddenBotPlayerIds.Clear();
            CancelLegacySnrStartBots();
        }

        public static bool MarkRealClient(PlayerControl player, bool hideInLobby = false)
        {
            if (!AmongUsClient.Instance.AmHost || player == null) return false;
            if (player.Data == null || player.Data.Disconnected) return false;
            if (player.GetClient() == null) return false;

            Register(player, hideInLobby, spawned: false);
            SendSync(player.PlayerId, true);
            Logger.Info($"Marked real client as bot. PlayerId:{player.PlayerId}, Name:{player.Data.PlayerName}, Hidden:{hideInLobby}", nameof(TestBotManager));
            return true;
        }

        public static bool Unmark(byte playerId)
        {
            if (!botPlayerIds.Contains(playerId)) return false;

            Unregister(playerId);
            SendSync(playerId, false);
            Logger.Info($"Unmarked bot. PlayerId:{playerId}", nameof(TestBotManager));
            return true;
        }

        public static void ReceiveSync(MessageReader reader)
        {
            var playerId = reader.ReadByte();
            var isBot = reader.ReadBoolean();

            if (isBot)
            {
                botPlayerIds.Add(playerId);
                var bot = PlayerCatch.GetPlayerById(playerId);
                if (bot != null && allBots.All(x => x.PlayerId != playerId))
                    allBots.Add(bot);
            }
            else
            {
                Unregister(playerId);
            }
        }

        private static void Register(PlayerControl bot, bool hidden, bool spawned)
        {
            if (bot == null) return;
            botPlayerIds.Add(bot.PlayerId);
            if (spawned)
                spawnedBotPlayerIds.Add(bot.PlayerId);
            else
                spawnedBotPlayerIds.Remove(bot.PlayerId);

            if (hidden)
                hiddenBotPlayerIds.Add(bot.PlayerId);
            else
                hiddenBotPlayerIds.Remove(bot.PlayerId);

            if (allBots.All(x => x.PlayerId != bot.PlayerId))
                allBots.Add(bot);
        }

        private static void Unregister(byte playerId)
        {
            botPlayerIds.Remove(playerId);
            spawnedBotPlayerIds.Remove(playerId);
            hiddenBotPlayerIds.Remove(playerId);
            allBots.RemoveAll(bot => bot == null || bot.PlayerId == playerId);
        }

        private static Vector2 GetVisibleSpawnPosition(byte playerId)
        {
            var hostPosition = PlayerControl.LocalPlayer != null
                ? PlayerControl.LocalPlayer.GetTruePosition()
                : Vector2.zero;
            var index = allBots.Count;
            var xOffset = 1.1f + (index % 4) * 0.55f;
            var yOffset = -0.45f - ((index / 4) % 3) * 0.45f;
            return hostPosition + new Vector2(xOffset, yOffset);
        }

        private static bool TryGetAvailablePlayerId(out byte id)
        {
            var usedIds = new HashSet<byte>();

            foreach (var player in PlayerControl.AllPlayerControls.ToArray())
                if (player != null)
                    usedIds.Add(player.PlayerId);

            if (GameData.Instance != null)
            {
                foreach (var info in GameData.Instance.AllPlayers)
                    usedIds.Add(info.PlayerId);
            }

            for (byte candidate = 0; candidate <= MaxPlayerId; candidate++)
            {
                if (!usedIds.Contains(candidate))
                {
                    id = candidate;
                    return true;
                }
            }

            id = byte.MaxValue;
            return false;
        }

        private static byte GetUnusedColorId(byte fallback)
        {
            var usedColors = PlayerCatch.AllPlayerControls
                .Where(pc => pc?.Data?.DefaultOutfit != null)
                .Select(pc => pc.Data.DefaultOutfit.ColorId)
                .ToHashSet();

            for (byte colorId = 0; colorId < 18; colorId++)
            {
                if (!usedColors.Contains(colorId))
                    return colorId;
            }

            return (byte)(fallback % 18);
        }

        private static void SetEmptyCosmetics(PlayerControl bot)
        {
            bot.RpcSetHat("");
            bot.RpcSetSkin("");
            bot.RpcSetVisor("");
            bot.RpcSetPet("");
            bot.RpcSetNamePlate("");
        }

        private static void SendSync(byte playerId, bool isBot)
        {
            if (!AmongUsClient.Instance.AmHost || !PlayerCatch.AnyModClient()) return;

            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)CustomRPC.SyncTestBot,
                SendOption.Reliable,
                -1);
            writer.Write(playerId);
            writer.Write(isBot);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static void KeepBotsHidden()
        {
            if (!AmongUsClient.Instance.AmHost || allBots.Count == 0) return;

            keepHiddenTimer += Time.fixedDeltaTime;
            if (keepHiddenTimer < 1f) return;
            keepHiddenTimer = 0f;

            foreach (var bot in allBots.ToArray())
            {
                if (bot == null)
                {
                    allBots.Remove(bot);
                    continue;
                }

                if (hiddenBotPlayerIds.Contains(bot.PlayerId))
                    bot.RpcSnapToForced(HiddenPosition);
            }
        }

        public static void SpawnPendingLegacySnrBots(string source)
        {
            if (!AmongUsClient.Instance.AmHost || pendingLegacySnrBotCount <= 0) return;

            var count = pendingLegacySnrBotCount;
            var namePrefix = pendingLegacySnrBotNamePrefix;
            var hidden = pendingLegacySnrBotsHidden;
            pendingLegacySnrBotCount = 0;
            pendingLegacySnrBotNamePrefix = DefaultLegacySnrBotNamePrefix;
            pendingLegacySnrBotsHidden = true;

            Logger.Info($"Spawning pending legacy SNR bots from {source}. Count:{count}, Hidden:{hidden}", nameof(TestBotManager));

            for (var i = 0; i < count; i++)
            {
                var suffix = count == 1 ? "" : $" {i + 1}";
                SpawnLegacySnr($"{namePrefix}{suffix}", RoleTypes.Crewmate, hidden);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        private static class FixedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (__instance == null || PlayerControl.LocalPlayer == null) return;
                if (__instance.PlayerId != PlayerControl.LocalPlayer.PlayerId) return;

                KeepBotsHidden();
            }
        }

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
        private static class OnGameJoinedPatch
        {
            public static void Postfix()
                => ClearLocal();
        }

        [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
        private static class OnDisconnectedPatch
        {
            public static void Postfix()
                => ClearLocal();
        }

    }
}
