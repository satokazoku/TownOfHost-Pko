using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HarmonyLib;
using InnerNet;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    internal static class MakePublicDiscordBotPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(GameStartManager __instance)
        {
            // バニラの公開状態は変更せず、同じボタンをMOD募集のON/OFFとして使用する。
            DiscordMatchmakingRelayService.ToggleRecruitment(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
    internal static class UpdateDiscordMatchmakingRelayPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DiscordMatchmakingRelayService.Tick();
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    internal static class DeleteDiscordMatchmakingOnExitPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            DiscordMatchmakingRelayService.TryDelete("ExitGame");
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    internal static class DeleteDiscordMatchmakingOnDisconnectPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            DiscordMatchmakingRelayService.TryDelete("OnDisconnected");
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]
    internal static class UpdateDiscordMatchmakingOnStartGamePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DiscordMatchmakingRelayService.RequestImmediateUpdate("StartGame");
        }
    }

    internal static class DiscordMatchmakingRelayService
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(3.0) };
        private static readonly object Sync = new();

        private static string _lastRoomCode = "";
        private static string _lastMessageId = "";
        private static string _lastSnapshot = "";
        private static bool _activeRecruitment;
        private static bool _forceUpdateRequested;
        private static long _nextUpdateAtMs;

        private const int UpdateIntervalMs = 6000;

        public static void ToggleRecruitment(GameStartManager gameStartManager)
        {
            if (!CanSend()) return;

            bool enable;
            lock (Sync)
                enable = !_activeRecruitment;

            if (enable)
                enable = StartRecruitment();
            else
                TryDelete("RecruitmentDisabled");

            UpdateRecruitmentButtons(gameStartManager, enable);
            Logger.Info($"MODマッチメイキング募集: {(enable ? "ON" : "OFF")}（バニラ部屋は非公開のまま）", nameof(DiscordMatchmakingRelayService));
        }

        private static bool StartRecruitment()
        {
            try
            {
                if (!CanSend()) return false;
                if (!TryCollectLobby(out var hostName, out var roomCode, out var state, out var players, out var maxPlayers)) return false;
                var snapshot = $"{hostName}|{roomCode}|{state}|{players}/{maxPlayers}";
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                lock (Sync)
                {
                    _activeRecruitment = true;
                    _forceUpdateRequested = false;
                    _nextUpdateAtMs = nowMs + UpdateIntervalMs;
                    _lastSnapshot = snapshot;
                }

                SendUpsert(hostName, roomCode, state, players, maxPlayers, "MakePublic");
                return true;
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
                return false;
            }
        }

        public static void Tick()
        {
            try
            {
                if (!CanSend()) return;

                lock (Sync)
                {
                    if (!_activeRecruitment) return;
                }

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var forceUpdate = false;
                lock (Sync)
                {
                    forceUpdate = _forceUpdateRequested;
                    if (!forceUpdate && nowMs < _nextUpdateAtMs) return;
                    _forceUpdateRequested = false;
                    _nextUpdateAtMs = nowMs + UpdateIntervalMs;
                }

                if (!TryCollectLobby(out var hostName, out var roomCode, out var state, out var players, out var maxPlayers)) return;

                var snapshot = $"{hostName}|{roomCode}|{state}|{players}/{maxPlayers}";
                lock (Sync)
                {
                    if (string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal)) return;
                    _lastSnapshot = snapshot;
                }

                SendUpsert(hostName, roomCode, state, players, maxPlayers, "Tick");
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
            }
        }

        public static void TryDelete(string reason)
        {
            try
            {
                if (Main.IsAndroid()) return;

                var roomCode = GetCurrentRoomCode();
                if (string.IsNullOrWhiteSpace(roomCode))
                    roomCode = GetLastRoomCode();

                if (string.IsNullOrWhiteSpace(roomCode)) return;

                lock (Sync)
                {
                    _activeRecruitment = false;
                    _forceUpdateRequested = false;
                    _lastSnapshot = "";
                }

                var messageId = GetLastMessageId(roomCode);
                SendCore(
                    action: "delete",
                    hostName: "Unknown Host",
                    roomCode: roomCode,
                    state: "Closed",
                    players: 0,
                    maxPlayers: 0,
                    messageId: messageId,
                    reason: reason ?? ""
                );
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
            }
        }

        private static void SendUpsert(string hostName, string roomCode, string state, int players, int maxPlayers, string reason)
        {
            var messageId = GetLastMessageId(roomCode);
            SendCore(
                action: "upsert",
                hostName: hostName,
                roomCode: roomCode,
                state: state,
                players: players,
                maxPlayers: maxPlayers,
                messageId: messageId,
                reason: reason
            );
        }

        private static void SendCore(string action, string hostName, string roomCode, string state, int players, int maxPlayers, string messageId, string reason)
        {
            var relayUrl = Main.MatchmakingRelayUrl;
            var relaySecret = Main.MatchmakingRelaySecret;

            if (string.IsNullOrWhiteSpace(relayUrl) || relayUrl.Equals("none", StringComparison.OrdinalIgnoreCase))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.Info($"Relay send: action={action}, room={roomCode}, state={state}, players={players}/{maxPlayers}, reason={reason}", nameof(DiscordMatchmakingRelayService));

                    using var req = new HttpRequestMessage(HttpMethod.Post, relayUrl);
                    if (!string.IsNullOrWhiteSpace(relaySecret) && !relaySecret.Equals("none", StringComparison.OrdinalIgnoreCase))
                        req.Headers.TryAddWithoutValidation("X-Relay-Secret", relaySecret);

                    req.Content = new StringContent(
                        BuildPayload(action, hostName, roomCode, state, players, maxPlayers, messageId, reason),
                        Encoding.UTF8,
                        "application/json"
                    );

                    using var res = await Client.SendAsync(req);
                    var body = await res.Content.ReadAsStringAsync();

                    if (!res.IsSuccessStatusCode)
                    {
                        Logger.Warn($"Discord relay failed: {(int)res.StatusCode} {res.StatusCode} / {body}", nameof(DiscordMatchmakingRelayService));
                        return;
                    }

                    Logger.Info($"Relay success: action={action}, room={roomCode}, state={state}, reason={reason}", nameof(DiscordMatchmakingRelayService));

                    if (action.Equals("upsert", StringComparison.OrdinalIgnoreCase))
                    {
                        var newMessageId = TryReadJsonString(body, "messageId");
                        if (!string.IsNullOrWhiteSpace(newMessageId))
                            SetLastRecruitment(roomCode, newMessageId);
                    }
                    else if (action.Equals("delete", StringComparison.OrdinalIgnoreCase))
                    {
                        ClearLastRecruitment(roomCode, messageId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, nameof(DiscordMatchmakingRelayService));
                }
            });
        }

        private static bool CanSend()
        {
            if (Main.IsAndroid()) return false;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
            return true;
        }

        public static void RequestImmediateUpdate(string reason)
        {
            try
            {
                if (!CanSend()) return;
                lock (Sync)
                {
                    if (!_activeRecruitment) return;
                    _forceUpdateRequested = true;
                    _nextUpdateAtMs = 0;
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
            }
        }

        private static void UpdateRecruitmentButtons(GameStartManager gameStartManager, bool recruiting)
        {
            if (gameStartManager == null) return;
            gameStartManager.HostPublicButton?.SelectButton(recruiting);
            gameStartManager.HostPrivateButton?.SelectButton(!recruiting);
        }

        private static bool TryCollectLobby(out string hostName, out string roomCode, out string state, out int players, out int maxPlayers)
        {
            hostName = PlayerControl.LocalPlayer?.Data?.PlayerName ?? "Unknown Host";
            roomCode = GetCurrentRoomCode();
            var gameStarted = AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted;
            state = gameStarted || GameStates.IsInGame ? "InGame" : (GameStates.IsLobby ? "Lobby" : "Unknown");
            players = AmongUsClient.Instance?.allClients?.Count ?? 0;
            maxPlayers = Main.NormalOptions?.MaxPlayers ?? 15;
            return !string.IsNullOrWhiteSpace(roomCode);
        }

        private static string BuildPayload(string action, string hostName, string roomCode, string state, int players, int maxPlayers, string messageId, string reason)
        {
            var nowUtc = DateTime.UtcNow.ToString("o");
            var stateLabel = GetStateLabel(state);
            var content = BuildRecruitmentContent(hostName, roomCode, stateLabel, players, maxPlayers);
            var threadComment = TownOfHost.Modules.MatchmakingWordManager.GetCurrentWord();
            if (threadComment.Length > TownOfHost.Modules.MatchmakingWordManager.MaxCommentLength)
                threadComment = threadComment[..TownOfHost.Modules.MatchmakingWordManager.MaxCommentLength];
            var requestThread = action.Equals("upsert", StringComparison.OrdinalIgnoreCase)
                                && string.IsNullOrWhiteSpace(messageId)
                                && !string.IsNullOrWhiteSpace(threadComment);
            return "{"
                + $"\"action\":\"{EscapeJson(action ?? "upsert")}\","
                + $"\"hostName\":\"{EscapeJson(hostName)}\","
                + $"\"roomCode\":\"{EscapeJson(roomCode)}\","
                + $"\"state\":\"{EscapeJson(state ?? "Unknown")}\","
                + $"\"stateLabel\":\"{EscapeJson(stateLabel)}\","
                + $"\"players\":{players},"
                + $"\"maxPlayers\":{maxPlayers},"
                + $"\"content\":\"{EscapeJson(content)}\","
                + $"\"messageId\":\"{EscapeJson(messageId ?? "")}\","
                + $"\"reason\":\"{EscapeJson(reason ?? "")}\","
                + $"\"threadRequested\":{(requestThread ? "true" : "false")},"
                + $"\"threadComment\":\"{EscapeJson(threadComment)}\","
                + $"\"mod\":\"{EscapeJson(Main.ModName)}\","
                + $"\"forkId\":\"{EscapeJson(Main.ForkId)}\","
                + $"\"sentAtUtc\":\"{EscapeJson(nowUtc)}\""
                + "}";
        }

        private static string BuildRecruitmentContent(string hostName, string roomCode, string stateLabel, int players, int maxPlayers)
        {
            var host = string.IsNullOrWhiteSpace(hostName) ? "Unknown Host" : hostName;
            var code = string.IsNullOrWhiteSpace(roomCode) ? "------" : roomCode;
            var state = string.IsNullOrWhiteSpace(stateLabel) ? "不明" : stateLabel;
            var playersText = $"{Math.Max(players, 0)}/{Math.Max(maxPlayers, 0)}";

            return "⠀⠀⠀【募集情報】\n"
                + $"★ホスト★:  **{host}**\n"
                + $"▲コード▲: **{code}**\n"
                + $"♦現在♦: **{state}**\n"
                + $"♠人数♠: **{playersText}**\n"
                + "ーーーーーーーーーーーーー";
        }

        private static string GetStateLabel(string state)
        {
            if (string.IsNullOrWhiteSpace(state)) return "Unknown";

            return state switch
            {
                "Lobby" => "Lobby",
                "InGame" => "In Game",
                "Closed" => "Closed",
                _ => state
            };
        }

        private static string TryReadJsonString(string json, string propName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propName)) return "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(propName, out var elem)) return "";
                return elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : "";
            }
            catch
            {
                return "";
            }
        }

        private static void SetLastRecruitment(string roomCode, string messageId)
        {
            lock (Sync)
            {
                _lastRoomCode = roomCode ?? "";
                _lastMessageId = messageId ?? "";
            }
        }

        private static void ClearLastRecruitment(string roomCode, string messageId)
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(roomCode) && !string.Equals(roomCode, _lastRoomCode, StringComparison.Ordinal)) return;
                if (!string.IsNullOrWhiteSpace(messageId) && !string.Equals(messageId, _lastMessageId, StringComparison.Ordinal)) return;

                _lastRoomCode = "";
                _lastMessageId = "";
            }
        }

        private static string GetLastRoomCode()
        {
            lock (Sync) return _lastRoomCode;
        }

        private static string GetLastMessageId(string roomCode)
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(roomCode)) return _lastMessageId;
                if (!string.Equals(roomCode, _lastRoomCode, StringComparison.Ordinal)) return "";
                return _lastMessageId;
            }
        }

        private static string GetCurrentRoomCode()
        {
            try
            {
                return GameCode.IntToGameName(AmongUsClient.Instance.GameId);
            }
            catch
            {
                return "";
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
