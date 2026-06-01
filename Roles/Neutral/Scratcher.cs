using System;
using System.Text;
using Hazel;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// スクラッチャー
/// タスクを終わらせる度にスクラッチ(削れる枚数)を入手する。
/// 会議で自投票することでスクラッチを削ることができ、
/// 設定された枚数の"当たり"を引くと乗っ取り単独勝利する。
/// </summary>
public sealed class Scratcher : RoleBase, ISelfVoter, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Scratcher),
            player => new Scratcher(player),
            CustomRoles.Scratcher,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            85000,
            SetupOptionItem,
            "scr",
            "#d4af37",
            (8, 1),
            true,
            from: From.TownOfHost_Pko
        );
    public Scratcher(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.True
    )
    {
        Scratches = 0;
        Hits = 0;
        ScratchedThisMeeting = 0;
        Seted = false;
        Won = false;
        AddWin = false;

        ScratchPerTask = OptionScratchPerTask.GetInt();
        MaxScratchPerMeeting = OptionMaxScratchPerMeeting.GetInt();
        WinHitCount = OptionWinHitCount.GetInt();
        HitProbability = OptionHitProbability.GetInt();
        WinAtMeetingEnd = OptionWinTiming.GetBool();
        IsAdditionalWin = OptionIsAdditionalWin.GetBool();
        CanWinAtDeath = OptionCanWinAtDeath.GetBool();
    }

    // 状態
    private int Scratches;            // 所持している削れる枚数
    private int Hits;                 // これまでに引いた当たりの数
    private int ScratchedThisMeeting; // この会議で削った枚数
    private bool Seted;               // この会議で自投票モードを確定したか(再操作抑制)
    private bool Won;                 // 単独勝利の予約フラグ(会議終了時勝利用)
    private bool AddWin;              // 追加勝利フラグ

    // オプション
    private static OptionItem OptionScratchPerTask; private static int ScratchPerTask;
    private static OptionItem OptionMaxScratchPerMeeting; private static int MaxScratchPerMeeting;
    private static OptionItem OptionWinHitCount; private static int WinHitCount;
    private static OptionItem OptionHitProbability; private static int HitProbability;
    private static OptionItem OptionWinTiming; private static bool WinAtMeetingEnd;
    private static OptionItem OptionIsAdditionalWin; private static bool IsAdditionalWin;
    private static OptionItem OptionCanWinAtDeath; private static bool CanWinAtDeath;

    enum OptionName
    {
        ScratcherScratchPerTask,
        ScratcherMaxScratchPerMeeting,
        ScratcherWinHitCount,
        ScratcherHitProbability,
        ScratcherWinTiming,
        ScratcherIsAdditionalWin,
        ScratcherCanWinAtDeath,
    }

    private static void SetupOptionItem()
    {
        OptionScratchPerTask = IntegerOptionItem.Create(RoleInfo, 10, OptionName.ScratcherScratchPerTask, new(1, 15, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionMaxScratchPerMeeting = IntegerOptionItem.Create(RoleInfo, 11, OptionName.ScratcherMaxScratchPerMeeting, new(1, 15, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionWinHitCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.ScratcherWinHitCount, new(1, 15, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionHitProbability = IntegerOptionItem.Create(RoleInfo, 13, OptionName.ScratcherHitProbability, new(1, 100, 1), 20, false)
            .SetValueFormat(OptionFormat.Percent);
        // false: 条件達成後すぐ勝利 / true: 条件達成後の会議終了後に勝利
        OptionWinTiming = BooleanOptionItem.Create(RoleInfo, 14, OptionName.ScratcherWinTiming, false, false);
        // 単独勝利ではなく追加勝利にする
        OptionIsAdditionalWin = BooleanOptionItem.Create(RoleInfo, 15, OptionName.ScratcherIsAdditionalWin, false, false);
        // 追加勝利の場合、死後でも条件達成済みなら勝利できる
        OptionCanWinAtDeath = BooleanOptionItem.Create(RoleInfo, 16, OptionName.ScratcherCanWinAtDeath, false, false)
            .SetParent(OptionIsAdditionalWin);
        // タスクを上書きする
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    // タスク完了でスクラッチ入手
    public override bool OnCompleteTask(uint taskid)
    {
        if (AmongUsClient.Instance.AmHost is false) return true;

        Scratches += ScratchPerTask;
        Logger.Info($"タスク完了: スクラッチ +{ScratchPerTask} (所持:{Scratches})", "Scratcher");
        UtilsGameLog.AddGameLog("Scratcher", string.Format(GetString("ScratcherGetScratchLog"), ScratchPerTask, Scratches, Player.Data.GetPlayerColor()));
        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);
        return true;
    }

    // 会議開始時に各種カウントをリセット
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ScratchedThisMeeting = 0;
        Seted = false;
    }

    // 会議終了時(追放処理時)に予約済み勝利を確定させる
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, System.Collections.Generic.Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (Won && WinAtMeetingEnd)
        {
            DoSoloWin();
        }
        return false;
    }

    // 自投票で削る
    bool ISelfVoter.CanUseVoted() => true;
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (Madmate.MadAvenger.Skill) return true;
        if (Impostor.Assassin.NowUse) return true;

        if (Is(voter))
        {
            if (CheckSelfVoteMode(Player, votedForId, out var status))
            {
                // 自投票モード中に自分へ投票 = スクラッチを1枚削る
                if (status is VoteStatus.Self)
                    ScratchOne();
                if (status is VoteStatus.Skip)
                    Utils.SendMessage(GetString("ScratcherInfoMeg"), Player.PlayerId);
                // status が Vote(他者投票)の場合は自投票モードを抜けるだけ
                SetMode(Player, status is VoteStatus.Self);
                return false;
            }
        }
        return true;
    }

    private void ScratchOne()
    {
        if (AmongUsClient.Instance.AmHost is false) return;

        // すでに勝利確定済みなら何もしない
        if (Won && !WinAtMeetingEnd) return;

        // 削れる条件チェック
        if (Scratches <= 0)
        {
            Utils.SendMessage(GetString("ScratcherNoScratch"), Player.PlayerId);
            return;
        }
        if (ScratchedThisMeeting >= MaxScratchPerMeeting)
        {
            Utils.SendMessage(string.Format(GetString("ScratcherMeetingLimit"), MaxScratchPerMeeting), Player.PlayerId);
            return;
        }

        Scratches--;
        ScratchedThisMeeting++;

        // 当たり判定
        var roll = IRandom.Instance.Next(100); // 0-99
        var isHit = roll < HitProbability;

        var sb = new StringBuilder();
        if (isHit)
        {
            Hits++;
            sb.Append(string.Format(GetString("ScratcherHit"), Hits, WinHitCount));
        }
        else
        {
            sb.Append(GetString("ScratcherMiss"));
        }
        // 残り情報
        sb.Append('\n');
        sb.Append(string.Format(GetString("ScratcherRemain"),
            Scratches,
            Math.Max(0, MaxScratchPerMeeting - ScratchedThisMeeting)));

        Utils.SendMessage(sb.ToString(), Player.PlayerId);
        Logger.Info($"スクラッチ削り: {(isHit ? "当たり" : "ハズレ")} 当たり数:{Hits}/{WinHitCount} 残り:{Scratches}", "Scratcher");

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);

        // 勝利条件達成
        if (Hits >= WinHitCount)
        {
            if (IsAdditionalWin)
            {
                AddWin = true;
                SendRPC();
                Utils.SendMessage(GetString("ScratcherAchieveAdd"), Player.PlayerId);
            }
            else
            {
                Won = true;
                SendRPC();
                if (WinAtMeetingEnd)
                {
                    // 会議終了後に勝利
                    Utils.SendMessage(GetString("ScratcherAchieveSoon"), Player.PlayerId);
                }
                else
                {
                    // 即時単独勝利
                    DoSoloWin();
                }
            }
        }
    }

    private void DoSoloWin()
    {
        if (AmongUsClient.Instance.AmHost is false) return;
        Logger.Info("スクラッチャー単独勝利", "Scratcher");
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Scratcher, Player.PlayerId, true))
        {
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Scratcher);
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
        Won = false;
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
        => AddWin && (CanWinAtDeath || Player.IsAlive());

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => $"<{RoleInfo.RoleColorCode}>({Hits}/{WinHitCount})♦{Scratches}</color>";

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seen != seer) return "";
        return AddWin ? Utils.AdditionalAliveWinnerMark : "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer) return "";
        return $"<size=80%><{RoleInfo.RoleColorCode}>{string.Format(GetString("ScratcherLower"), Scratches, Hits, WinHitCount)}</color></size>";
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(Scratches);
        sender.Writer.Write(Hits);
        sender.Writer.Write(Won);
        sender.Writer.Write(AddWin);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        Scratches = reader.ReadInt32();
        Hits = reader.ReadInt32();
        Won = reader.ReadBoolean();
        AddWin = reader.ReadBoolean();
    }
}
