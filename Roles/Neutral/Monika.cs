using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Neutral;

public sealed class Monika : RoleBase, ILNKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Monika),
            player => new Monika(player),
            CustomRoles.Monika,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            284800,
            SetupOptionItem,
            "mon",
            "#e5a497",
            (5, 7),
            true,
            countType: CountTypes.OutOfGame,
            from: From.ExtremeRoles
        );

    // ─── ゴミ箱レイヤー（全インスタンス共有）─────────────
    public static readonly HashSet<byte> MonikaTrashLayer = new();

    // ─── 勝利選択フェーズ管理（静的）────────────────────
    private static bool isSelectingAlly = false;
    private static float selectTimer = 0f;
    private const float SelectTimeout = 15f;

    // ─── オプション ──────────────────────────────────────
    static OptionItem OptionHasCustomVision;
    static OptionItem OptionVision;
    static OptionItem OptionCanVent;
    static OptionItem OptionCanSabotage;
    static OptionItem OptionConsumeButton;
    static OptionItem OptionKillCooldown;
    static OptionItem OptionGameContinues;
    static OptionItem OptionCanSeeTrash;

    public static float KillCooldownValue;
    public static bool GameContinues => OptionGameContinues?.GetBool() ?? true;
    public static bool CanSeeTrashOpt => OptionCanSeeTrash?.GetBool() ?? false;
    public static bool ConsumeButton => OptionConsumeButton?.GetBool() ?? false;

    enum OptionName
    {
        MonikaHasCustomVision,
        MonikaVision,
        MonikaCanVent,
        MonikaCanSabotage,
        MonikaConsumeButton,
        MonikaKillCooldown,
        MonikaGameContinues,
        MonikaCanSeeTrash,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MonikaKillCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionHasCustomVision = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MonikaHasCustomVision, false, false);
        OptionVision = FloatOptionItem.Create(RoleInfo, 12, OptionName.MonikaVision,
            new(0.1f, 5f, 0.05f), 1f, false, OptionHasCustomVision).SetValueFormat(OptionFormat.Multiplier);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MonikaCanVent, false, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 14, OptionName.MonikaCanSabotage, false, false);
        OptionConsumeButton = BooleanOptionItem.Create(RoleInfo, 15, OptionName.MonikaConsumeButton, false, false);
        OptionGameContinues = BooleanOptionItem.Create(RoleInfo, 16, OptionName.MonikaGameContinues, true, false);
        OptionCanSeeTrash = BooleanOptionItem.Create(RoleInfo, 17, OptionName.MonikaCanSeeTrash, false, false);
    }

    public Monika(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        KillCooldownValue = OptionKillCooldown.GetFloat();
    }

    // ─── ILNKiller ────────────────────────────────────────
    public float CalculateKillCooldown() => KillCooldownValue;
    public bool CanUseKillButton() => Player.IsAlive(); // ★ キルボタン有効
    public bool CanUseImpostorVentButton() => OptionCanVent.GetBool();
    public bool CanUseSabotageButton() => OptionCanSabotage.GetBool();
    public override bool CanClickUseVentButton => OptionCanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics p, int id) => OptionCanVent.GetBool();

    public override void Add()
    {
        MonikaTrashLayer.Clear();
        isSelectingAlly = false;
        selectTimer = 0f;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (OptionHasCustomVision.GetBool())
            opt.SetVision(false);
    }

    // ─── 抹消（キルボタン）───────────────────────────────
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        (_, var target) = info.AttemptTuple;

        // ★ モニカ同士は通常キル
        if (target.Is(CustomRoles.Monika))
        {
            UtilsGameLog.AddGameLog("Monika",
                $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)}(Monika) を抹消(キル)");
            return;
        }

        // ★ ゴミ箱レイヤーに送る（通常キルはしない）
        info.DoKill = false;
        // ★ 抹消エフェクトを再現
        Player.RpcProtectedMurderPlayer(target);
        Player.RpcProtectedMurderPlayer(Player);
        SendToTrash(target);
    }

    private void SendToTrash(PlayerControl target)
    {
        if (MonikaTrashLayer.Contains(target.PlayerId)) return;

        MonikaTrashLayer.Add(target.PlayerId);

        SendMessage(
            $"<color={RoleInfo.RoleColorCode}>【×抹消×】\nあなたはゴミ箱レイヤーに送られました。\n会議での投票・発言・能力は使えません。</color>",
            target.PlayerId);

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} をゴミ箱に送った");

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
    }

    // ─── ゴミ箱プレイヤーが実際に死亡→レイヤー解除 ─────
    public static void OnPlayerActuallyDied(byte playerId)
    {
        if (MonikaTrashLayer.Remove(playerId))
            Logger.Info($"[Monika] {playerId} がゴミ箱から死亡レイヤーへ移行", "Monika");
    }

    // ─── 緊急会議ボタン消費オプション ────────────────────
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
    public static class MonikaEmergencyConsumePatch
    {
        public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!ConsumeButton) return;
            if (target != null) return;
            if (!__instance.Is(CustomRoles.Monika)) return;

            var candidates = AllAlivePlayerControls
                .Where(pc => pc.PlayerId != __instance.PlayerId
                          && !pc.GetCustomRole().IsImpostor()
                          && !MonikaTrashLayer.Contains(pc.PlayerId))
                .ToList();

            if (candidates.Count == 0) return;
            var victim = candidates[IRandom.Instance.Next(candidates.Count)];
            SendMessage(
                $"<color={RoleInfo.RoleColorCode}>【緊急会議】\nあなたの緊急会議ボタン使用回数が1消費されました。</color>",
                victim.PlayerId);
        }
    }

    // ─── OnFixedUpdate：勝利条件チェック ─────────────────
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (player != Player || !Player.IsAlive()) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;

        if (isSelectingAlly)
        {
            selectTimer -= Time.fixedDeltaTime;
            if (selectTimer <= 0f)
            {
                isSelectingAlly = false;
                AutoSelectAlly(Player);
            }
            return;
        }

        CheckWinConditions();
    }

    private void CheckWinConditions()
    {
        var aliveMonikas = AllAlivePlayerControls
            .Where(pc => pc.Is(CustomRoles.Monika) && !MonikaTrashLayer.Contains(pc.PlayerId))
            .ToList();
        if (aliveMonikas.Count != 1) return;

        var nonTrashAlive = AllAlivePlayerControls
            .Where(pc => !MonikaTrashLayer.Contains(pc.PlayerId) && !pc.Is(CustomRoles.Monika))
            .ToList();

        if (nonTrashAlive.Count == 2) TriggerSelectPhase(aliveMonikas[0], nonTrashAlive);
        else if (nonTrashAlive.Count <= 1) TriggerImmediateWin(aliveMonikas[0], nonTrashAlive);
    }

    private static void TriggerSelectPhase(PlayerControl monika, List<PlayerControl> candidates)
    {
        if (isSelectingAlly) return;
        isSelectingAlly = true;
        selectTimer = SelectTimeout;

        string list = string.Join("\n", candidates.Select(pc =>
            $"  [{pc.PlayerId}] {UtilsName.GetPlayerColor(pc, true)}"));

        SendMessage(
            $"<color={RoleInfo.RoleColorCode}>【モニカ勝利条件達成！】\n生存者が2名になりました。\n/cmd mselect [ID] で1人を選んでください\n（{(int)SelectTimeout}秒後に自動選択）\n\n対象:\n{list}</color>",
            monika.PlayerId);

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(monika)} の勝利条件が達成。選択フェーズ開始");
    }

    public static bool TryHandleSelectCommand(PlayerControl sender, string idStr)
    {
        if (!isSelectingAlly) return false;
        if (!sender.Is(CustomRoles.Monika)) return false;
        if (!byte.TryParse(idStr, out byte targetId)) return false;

        var ally = GetPlayerById(targetId);
        if (ally == null || !ally.IsAlive() || MonikaTrashLayer.Contains(targetId)) return false;

        isSelectingAlly = false;
        ExecuteWin(sender, new List<PlayerControl> { ally });
        return true;
    }

    private static void AutoSelectAlly(PlayerControl monika)
    {
        var candidates = AllAlivePlayerControls
            .Where(pc => !pc.Is(CustomRoles.Monika) && !MonikaTrashLayer.Contains(pc.PlayerId))
            .ToList();
        var ally = candidates.Count > 0 ? candidates[IRandom.Instance.Next(candidates.Count)] : null;
        ExecuteWin(monika, ally != null ? new List<PlayerControl> { ally } : new());
    }

    private static void TriggerImmediateWin(PlayerControl monika, List<PlayerControl> remaining)
    {
        isSelectingAlly = false;
        ExecuteWin(monika, remaining);
    }

    private static void ExecuteWin(PlayerControl monika, List<PlayerControl> allies)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Monika, monika.PlayerId, AddWin: false);
        CustomWinnerHolder.NeutralWinnerIds.Add(monika.PlayerId);
        CustomWinnerHolder.WinnerIds.Add(monika.PlayerId);

        foreach (var ally in allies)
            CustomWinnerHolder.WinnerIds.Add(ally.PlayerId);

        string allyNames = allies.Count > 0
            ? string.Join(", ", allies.Select(pc => UtilsName.GetPlayerColor(pc, true)))
            : "なし";

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(monika)} 勝利！ 同伴勝利: {allyNames}");

        GameManager.Instance.enabled = false;
        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive() || MonikaTrashLayer.Contains(Player.PlayerId)) return;
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        isSelectingAlly = false;
        selectTimer = 0f;
    }

    // ─── RPC ─────────────────────────────────────────────
    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(MonikaTrashLayer.Count);
        foreach (var id in MonikaTrashLayer) sender.Writer.Write(id);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        int count = reader.ReadInt32();
        MonikaTrashLayer.Clear();
        for (int i = 0; i < count; i++) MonikaTrashLayer.Add(reader.ReadByte());
    }

    // ─── 表示 ─────────────────────────────────────────────
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        int trashed = MonikaTrashLayer.Count;
        return trashed > 0 ? $"<color={RoleInfo.RoleColorCode}>(×{trashed})</color>" : "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (isSelectingAlly)
        {
            int sec = Mathf.CeilToInt(selectTimer);
            return $"{size}<color={color}>【選択フェーズ】/cmd mselect [ID] で選択 ({sec}s)</color>";
        }
        return $"{size}<color={color}>キルで抹消 → ゴミ箱レイヤーへ (抹消数:{MonikaTrashLayer.Count})</color>";
    }

    public bool OverrideKillButton(out string text) { text = "Monika_Erase"; return true; }
    public bool OverrideKillButtonText(out string text) { text = "抹消"; return true; }
}

// ══════════════════════════════════════════════════════════════
// ★ 会議でゴミ箱プレイヤーの名前を ×名前× にする（全クライアント共通）
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class MonikaTrashMeetingNameMarkPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        // ★ 名前 RPC が確定するまで少し待つ
        _ = new LateTask(() =>
        {
            if (__instance == null) return;
            string color = Monika.RoleInfo.RoleColorCode;
            string cross = $"<color={color}>×</color>";

            foreach (var voteArea in __instance.playerStates)
            {
                if (voteArea?.NameText == null) continue;
                if (!Monika.MonikaTrashLayer.Contains(voteArea.TargetPlayerId)) continue;

                // 二重追加防止（両側をしっかり囲む形に修正）
                if (voteArea.NameText.text.StartsWith(cross)) continue;
                voteArea.NameText.text = cross + voteArea.NameText.text + cross;
            }
        }, 0.4f, "Monika.VoteAreaNameMark", null);
    }
}

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーの投票ブロック（ホスト処理）
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
public static class MonikaTrashedVoteBlockPatch
{
    public static bool Prefix([HarmonyArgument(0)] byte srcPlayerId)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!Monika.MonikaTrashLayer.Contains(srcPlayerId)) return true;
        Logger.Info($"[Monika] ゴミ箱プレイヤー {srcPlayerId} の投票をブロック", "Monika");
        return false;
    }
}

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーの死体通報ブロック
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
public static class MonikaTrashedReportBlockPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!Monika.MonikaTrashLayer.Contains(__instance.PlayerId)) return true;
        return false;
    }
}

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーが実際に死亡した時、ゴミ箱レイヤーを解除
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class MonikaTrashDeathPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        Monika.OnPlayerActuallyDied(__instance.PlayerId);
    }
}