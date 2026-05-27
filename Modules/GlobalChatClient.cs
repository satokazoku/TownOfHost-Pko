using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using TownOfHost;

namespace TownOfHost.Modules;

public static class GlobalChatManager
{
    private static ClientWebSocket _socket;
    private static CancellationTokenSource _cts;

    private static readonly ConcurrentQueue<string> _pendingMessages = new();

    public static List<byte> IgnoreList = new();

    // ★ フィールド区切り文字（プレイヤー名/フレコに含まれない制御文字）
    //    hostName \x1E playerName \x1E friendCode \x1E message
    private const char Sep = '\x1E';

    public static void Initialize(string serverUrl)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        Task.Run(async () => await ConnectAsync(serverUrl, _cts.Token));
    }

    private static async Task ConnectAsync(string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("ngrok-skip-browser-warning", "true");
                _socket = socket;
                await socket.ConnectAsync(new Uri(url), ct);
                Logger.Info($"GlobalChat 接続成功: {url}", "GlobalChatManager");

                byte[] buffer = new byte[4096];
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string raw = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (!string.IsNullOrWhiteSpace(raw))
                        _pendingMessages.Enqueue(raw);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e)
            {
                Logger.Warn($"GlobalChat 切断: {e.Message} → 5秒後に再接続", "GlobalChatManager");
                _socket = null;
            }

            try { await Task.Delay(5000, ct); } catch { break; }
        }
    }

    public static void Tick()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        while (_pendingMessages.TryDequeue(out string raw))
            Distribute(raw);
    }

    private static void Distribute(string raw)
    {
        var parts = raw.Split(Sep, 4);

        string hostName, playerName, friendCode, message;
        if (parts.Length == 4)
        {
            hostName = parts[0];
            playerName = parts[1];
            friendCode = parts[2];
            message = parts[3];
        }
        else
        {
            hostName = playerName = friendCode = "???";
            message = raw;
        }

        //[Global](ホスト名)-(プレイヤー名) (フレコ)
        string title = $"<size=70%>[Global]</size> <size=70%>({hostName}村)-{playerName}</size> <size=40%>({friendCode})</size>";

        bool isInGame = AmongUsClient.Instance != null
            && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;

        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
            if (IgnoreList.Contains(pc.PlayerId)) continue;
            if (isInGame && pc.IsAlive()) continue;

            Main.MessagesToSend.Add((message, pc.PlayerId, title));
        }
    }

    public static void SendMessage(string message, PlayerControl sender = null)
    {
        if (_socket == null || _socket.State != WebSocketState.Open) return;

        var hostPc = PlayerCatch.AllPlayerControls
            .FirstOrDefault(pc => pc.GetClientId() == AmongUsClient.Instance.HostId);
        string hostName = hostPc?.Data?.PlayerName ?? "???";

        var senderPc = sender ?? PlayerControl.LocalPlayer;
        string playerName = senderPc?.Data?.PlayerName ?? "???";
        string friendCode = senderPc?.GetClient()?.FriendCode ?? "???";

        string cleanMessage = message;
        string prefix = playerName + ": ";
        if (cleanMessage.StartsWith(prefix))
            cleanMessage = cleanMessage[prefix.Length..];

        string payload = string.Join(Sep.ToString(), hostName, playerName, friendCode, cleanMessage);

        byte[] buffer = Encoding.UTF8.GetBytes(payload);
        _socket.SendAsync(
            new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    public static bool IsConnected => _socket?.State == WebSocketState.Open;

    public static void Disconnect()
    {
        _cts?.Cancel();
        _pendingMessages.Clear();
        _socket = null;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class GlobalChatTickPatch
{
    private static float _timer = 0f;
    public static void Postfix()
    {
        _timer += UnityEngine.Time.deltaTime;
        if (_timer < 0.2f) return;
        _timer = 0f;
        try { GlobalChatManager.Tick(); } catch { }
    }
}