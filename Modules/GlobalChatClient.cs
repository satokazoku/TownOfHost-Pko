using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
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

    private const char Sep = '\x1E';

    private const string KindChat = "CHAT";
    private const string KindLink = "LINK";

    public static string MyLinkId { get; private set; } = GenerateLinkId();

    private static readonly HashSet<string> _linkedKeys = new();

    private static string GenerateLinkId()
    {
        // ソースは公開されるが、この値は各セッションで乱数生成されるためID自体は漏れない。
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var sb = new StringBuilder(16);
        for (int i = 0; i < 16; i++)
            sb.Append(chars[bytes[i] % chars.Length]);
        return sb.ToString();
    }

    public static void Initialize(string serverUrl)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        MyLinkId = GenerateLinkId();
        _linkedKeys.Clear();
        _linkedKeys.Add(MyLinkId);
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
                Logger.Info($"GlobalChat 接続成功: {url} (LinkId={MyLinkId})", "GlobalChatManager");

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

            try { await Task.Delay(5000, ct); } catch { break; }
        }
    }

    public static void Tick()
    {
        while (_pendingMessages.TryDequeue(out string raw))
        {
            try { Handle(raw); } catch { }
        }
    }

    private static void Handle(string raw)
    {
        int firstSep = raw.IndexOf(Sep);
        string kind = firstSep > 0 ? raw.Substring(0, firstSep) : "";

        if (kind == KindLink)
        {
            // LINK \x1E fromId \x1E toId
            var lp = raw.Split(Sep);
            if (lp.Length >= 3)
            {
                string fromId = lp[1];
                string toId = lp[2];
                if (toId == MyLinkId && !string.IsNullOrEmpty(fromId))
                {
                    if (_linkedKeys.Add(fromId))
                        Logger.Info($"GlobalChat 相互リンク成立: {fromId}", "GlobalChatManager");
                }
            }
            return;
        }

        if (!AmongUsClient.Instance.AmHost) return;
        DistributeChat(raw);
    }

    private static void DistributeChat(string raw)
    {
        // CHAT \x1E linkId \x1E hostName \x1E playerName \x1E friendCode \x1E message
        string linkId, hostName, playerName, friendCode, message;

        var parts = raw.Split(Sep);
        if (parts.Length >= 6 && parts[0] == KindChat)
        {
            linkId = parts[1];
            hostName = parts[2];
            playerName = parts[3];
            friendCode = parts[4];
            message = string.Join(Sep.ToString(), parts.Skip(5));
        }
        else if (parts.Length == 4)
        {
            linkId = null;
            hostName = parts[0];
            playerName = parts[1];
            friendCode = parts[2];
            message = parts[3];
        }
        else
        {
            return;
        }

        //招待制フィルタ：リンク済みの相手のメッセージのみ表示
        if (linkId == null || !_linkedKeys.Contains(linkId)) return;

        string title = $"<size=70%>[Global]</size> <size=70%>({hostName}村)-{playerName}</size> <size=40%>({friendCode})</size>";

        bool isInGame =
            (AmongUsClient.Instance != null
                && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            || (GameStates.IsInGame);

        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
            if (IgnoreList.Contains(pc.PlayerId)) continue;

            if (isInGame)
            {
                bool isConfirmedDead = pc.Data != null && pc.Data.IsDead;
                if (!isConfirmedDead) continue;
            }

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

        // CHAT \x1E linkId \x1E hostName \x1E playerName \x1E friendCode \x1E message
        string payload = string.Join(Sep.ToString(),
            KindChat, MyLinkId, hostName, playerName, friendCode, cleanMessage);

        RawSend(payload);
    }

    //相手IDを入力してリンク要求を送る（相手側も自動で相互リンクされる）。
    public static bool RequestLink(string targetId)
    {
        targetId = NormalizeId(targetId);
        if (string.IsNullOrEmpty(targetId)) return false;
        if (targetId == MyLinkId) return false;

        _linkedKeys.Add(targetId);
        string payload = string.Join(Sep.ToString(), KindLink, MyLinkId, targetId);
        RawSend(payload);
        Logger.Info($"GlobalChat リンク要求送信: {targetId}", "GlobalChatManager");
        return true;
    }

    public static int LinkedCount => Math.Max(0, _linkedKeys.Count - 1);

    public static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        return id.Trim().ToUpperInvariant();
    }

    private static void RawSend(string payload)
    {
        if (_socket == null || _socket.State != WebSocketState.Open) return;
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