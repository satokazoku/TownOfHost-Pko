using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using InnerNet;
using TownOfHost.Modules;

namespace TownOfHost.Patches;

public static class PreviousSessionDetector
{
    private static readonly HashSet<string> PreviousSessionKeys = new();
    private static readonly HashSet<string> CurrentSessionKeys = new();
    private static readonly Dictionary<string, int> ContinuousCount = new();
    private static readonly List<(int ClientId, string PlayerName)> DetectedPlayers = new();

    private static readonly HashSet<string> ExemptKeys = new();

    private static bool IsEnabled => Options.OptionJoinKick.GetBool();
    private static bool NotifyOnly => Options.OptionNotifyJoinKick.GetBool();
    private static bool SkipModerator => Options.OptionNotModeJoinKick.GetBool();
    private static bool SkipDraw => Options.OptionDrawJoinKick.GetBool();

    public static bool TemporaryAllowAll = false;

    private static string GetKey(ClientData client)
    {
        if (client == null) return "";
        string fc = client.FriendCode?.Trim().ToUpper() ?? "";
        if (!string.IsNullOrEmpty(fc)) return fc;
        return client.ProductUserId?.Trim() ?? "";
    }

    private static string GetKey(PlayerControl pc)
    {
        return GetKey(pc?.GetClient());
    }

    public static void OnGameStart()
    {
        CurrentSessionKeys.Clear();
        DetectedPlayers.Clear();

        foreach (var client in AmongUsClient.Instance.allClients)
        {
            var key = GetKey(client);
            if (!string.IsNullOrEmpty(key))
            {
                CurrentSessionKeys.Add(key);
                Logger.Info($"セッション記録: {client.PlayerName} ({key})", "PreviousSession");
            }
            else
            {
                Logger.Warn($"キー取得失敗（FriendCode/PUID 未ロード）: {client.PlayerName}", "PreviousSession");
            }
        }
        Logger.Info($"現在のセッション参加者: {CurrentSessionKeys.Count}人", "PreviousSession");
    }

    public static void OnGameEnd()
    {
        if (SkipDraw && CustomWinnerHolder.WinnerTeam == CustomWinner.Draw)
        {
            Logger.Info("廃村のため前セッション記録をスキップ", "PreviousSession");
            CurrentSessionKeys.Clear();
            PreviousSessionKeys.Clear();
            ContinuousCount.Clear();
            DetectedPlayers.Clear();
            return;
        }

        PreviousSessionKeys.Clear();
        foreach (var k in CurrentSessionKeys)
            PreviousSessionKeys.Add(k);
        CurrentSessionKeys.Clear();
        DetectedPlayers.Clear();
        Logger.Info($"前セッション記録: {PreviousSessionKeys.Count}人", "PreviousSession");
    }

    public static void OnPlayerJoined(ClientData client)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsEnabled) return;
        if (client == null || PreviousSessionKeys.Count == 0) return;
        if (TemporaryAllowAll)
        {
            Logger.Info($"一時許可中のためスキップ: {client.PlayerName}", "PreviousSession");
            return;
        }

        var key = GetKey(client);
        if (string.IsNullOrEmpty(key))
        {
            Logger.Warn($"キー取得失敗のためスキップ: {client.PlayerName}", "PreviousSession");
            return;
        }

        if (!PreviousSessionKeys.Contains(key)) return;

        if (ExemptKeys.Contains(key))
        {
            Logger.Info($"免除リストのためスキップ: {client.PlayerName}", "PreviousSession");
            return;
        }

        var pc = PlayerCatch.AllPlayerControls.FirstOrDefault(p => p.GetClientId() == client.Id);

        if (SkipModerator && pc != null && Moderator.IsModerator(pc))
        {
            Logger.Info($"モデレーターのためスキップ: {client.PlayerName}", "PreviousSession");
            return;
        }

        if (!ContinuousCount.ContainsKey(key)) ContinuousCount[key] = 0;
        ContinuousCount[key]++;

        string playerName = client.PlayerName ?? "???";
        int count = ContinuousCount[key];
        int clientId = client.Id;

        Logger.Warn($"前試合参加者が再参加: {playerName} ({key}) {count}回目", "PreviousSession");

        if (clientId >= 0 && !DetectedPlayers.Any(d => d.ClientId == clientId))
            DetectedPlayers.Add((clientId, playerName));

        if (NotifyOnly)
        {
            Utils.SendMessage(
                $"<color=#00c1ff>【再参加検知】</color>\n" +
                $"{playerName} は前の試合にも参加していました。\n" +
                $"FC: {key}\n" +
                $"<size=80%>/kp で一括キックできます。</size>",
                PlayerControl.LocalPlayer.PlayerId,
                "<color=#00c1ff>⚠ 再参加検知</color>");
            return;
        }

        Utils.SendMessage(
            $"<color=#ffaa00>【再参加検知】</color>\n" +
            $"{playerName} は前の試合にも参加していました。\n" +
            $"FC: {key}",
            PlayerControl.LocalPlayer.PlayerId,
            "<color=#ffaa00>⚠ 自動キック処理中...</color>");

        if (clientId < 0) return;

        _ = new LateTask(() =>
        {
            AmongUsClient.Instance.KickPlayer(clientId, false);
            Logger.Warn($"再参加のため {playerName} をキックしました", "PreviousSession");
            Utils.SendMessage(
                $"<color=#ff1919>【自動キック】{playerName} をキックしました。</color>",
                PlayerControl.LocalPlayer.PlayerId);
        }, 0.5f, "PreviousSession.Kick", true);
    }

    public static void OnPlayerJoined(PlayerControl pc)
    {
        OnPlayerJoined(pc?.GetClient());
    }

    public static bool AddExempt(PlayerControl pc)
    {
        var key = GetKey(pc);
        if (string.IsNullOrEmpty(key)) return false;
        ExemptKeys.Add(key);
        DetectedPlayers.RemoveAll(d => d.PlayerName == (pc.Data?.PlayerName ?? ""));
        Logger.Info($"免除追加: {pc.Data?.PlayerName} ({key})", "PreviousSession");
        return true;
    }

    public static bool RemoveExempt(PlayerControl pc)
    {
        var key = GetKey(pc);
        if (string.IsNullOrEmpty(key)) return false;
        bool removed = ExemptKeys.Remove(key);
        Logger.Info($"免除解除: {pc.Data?.PlayerName} ({key})", "PreviousSession");
        return removed;
    }

    public static string GetExemptList()
    {
        if (ExemptKeys.Count == 0) return "免除リストは空です。";
        return "免除リスト:\n" + string.Join("\n", ExemptKeys.Select(k => $"・{k}"));
    }

    public static void KickAllDetected()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (DetectedPlayers.Count == 0)
        {
            Utils.SendMessage(
                "<color=#00c1ff>キック対象の再参加プレイヤーはいません。</color>",
                PlayerControl.LocalPlayer.PlayerId);
            return;
        }

        int count = 0;
        foreach (var (clientId, playerName) in DetectedPlayers.ToArray())
        {
            var c = count;
            _ = new LateTask(() =>
            {
                AmongUsClient.Instance.KickPlayer(clientId, false);
                Logger.Warn($"手動キック: {playerName}", "PreviousSession");
            }, 0.2f * c, $"PreviousSession.ManualKick_{clientId}", true);

            Utils.SendMessage(
                $"<color=#ff1919>【手動キック】{playerName} をキックしました。</color>",
                PlayerControl.LocalPlayer.PlayerId);
            count++;
        }
        DetectedPlayers.Clear();
    }

    public static void EnableTemporaryAllow()
    {
        TemporaryAllowAll = true;
        Logger.Info("今回だけ全員の再参加を許可しました", "PreviousSession");
        Utils.SendMessage(
            "<color=#00ff88>【再参加許可】今回だけ全員の参加を許可しました。\n次の試合からは通常通りキックされます。</color>",
            PlayerControl.LocalPlayer.PlayerId);
    }

    public static PlayerControl FindTargetAuto(string key)
    {
        if (byte.TryParse(key, out var id))
        {
            var byId = PlayerCatch.GetPlayerById(id);
            if (byId != null) return byId;
        }

        var normalizedFc = key.Trim().ToUpper();
        var byFc = PlayerCatch.AllPlayerControls.FirstOrDefault(pc =>
        {
            var client = pc.GetClient();
            return client != null &&
                string.Equals(client.FriendCode?.Trim().ToUpper(), normalizedFc, System.StringComparison.OrdinalIgnoreCase);
        });
        if (byFc != null) return byFc;

        int colorId = key.ToLower() switch
        {
            "red" or "レッド" or "赤" => 0,
            "blue" or "ブルー" or "青" => 1,
            "green" or "グリーン" or "緑" => 2,
            "pink" or "ピンク" => 3,
            "orange" or "オレンジ" => 4,
            "yellow" or "イエロー" or "黄" => 5,
            "black" or "ブラック" or "黒" => 6,
            "white" or "ホワイト" or "白" => 7,
            "purple" or "パープル" or "紫" => 8,
            "brown" or "ブラウン" => 9,
            "cyan" or "シアン" => 10,
            "lime" or "ライム" => 11,
            "maroon" or "マルーン" => 12,
            "rose" or "ローズ" => 13,
            "banana" or "バナナ" => 14,
            "gray" or "グレー" => 15,
            "tan" or "タン" => 16,
            "coral" or "コーラル" => 17,
            _ => -1
        };
        if (colorId >= 0)
        {
            var byColor = PlayerCatch.AllPlayerControls
                .FirstOrDefault(pc => pc.Data?.DefaultOutfit.ColorId == colorId);
            if (byColor != null) return byColor;
        }

        return PlayerCatch.AllPlayerControls.FirstOrDefault(pc =>
            string.Equals(
                (pc.Data?.PlayerName ?? "").RemoveHtmlTags().Trim(),
                key.Trim(),
                System.StringComparison.OrdinalIgnoreCase));
    }

    public static void Reset()
    {
        PreviousSessionKeys.Clear();
        CurrentSessionKeys.Clear();
        ContinuousCount.Clear();
        DetectedPlayers.Clear();
        ExemptKeys.Clear();
        TemporaryAllowAll = false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]
public static class RecordSessionOnStartPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        PreviousSessionDetector.TemporaryAllowAll = false;
        PreviousSessionDetector.OnGameStart();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class RecordSessionOnEndPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        PreviousSessionDetector.OnGameEnd();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
public static class PreviousSessionJoinPatch
{
    public static void Postfix([HarmonyArgument(0)] ClientData client)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            try
            {
                PreviousSessionDetector.OnPlayerJoined(client);
            }
            catch (System.Exception e)
            {
                Logger.Error(e.ToString(), "PreviousSession");
            }
        }, 0.5f, "PreviousSession.Check", true);
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
public static class PreviousSessionResetPatch
{
    public static void Prefix() => PreviousSessionDetector.Reset();
}