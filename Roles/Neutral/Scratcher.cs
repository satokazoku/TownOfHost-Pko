using System;
using System.Text;
using Hazel;
using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Scratcher : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Scratcher),
            player => new Scratcher(player),
            CustomRoles.Scratcher,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            54200,
            SetupOptionItem,
            "scr",
            "#d4af37",
            (4, 8),
            true,
            from: From.TownOfHost_Pko
        );

    public Scratcher(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        Scratches = 0;
        Hits = 0;
        ScratchedThisMeeting = 0;
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

    private int Scratches;
    private int Hits;
    private int ScratchedThisMeeting;
    private bool Won;
    private bool AddWin;

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
        SoloWinOption.Create(RoleInfo, 9, defo: 1);

        OptionScratchPerTask = IntegerOptionItem.Create(RoleInfo, 10, OptionName.ScratcherScratchPerTask,
            new(1, 100, 1), 2, false).SetValueFormat(OptionFormat.Pieces);
        OptionMaxScratchPerMeeting = IntegerOptionItem.Create(RoleInfo, 11, OptionName.ScratcherMaxScratchPerMeeting,
            new(1, 100, 1), 3, false).SetValueFormat(OptionFormat.Pieces);
        OptionWinHitCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.ScratcherWinHitCount,
            new(1, 100, 1), 1, false).SetValueFormat(OptionFormat.Pieces);
        OptionHitProbability = IntegerOptionItem.Create(RoleInfo, 13, OptionName.ScratcherHitProbability,
            new(1, 100, 1), 20, false).SetValueFormat(OptionFormat.Percent);
        OptionWinTiming = BooleanOptionItem.Create(RoleInfo, 14, OptionName.ScratcherWinTiming, false, false);
        OptionIsAdditionalWin = BooleanOptionItem.Create(RoleInfo, 15, OptionName.ScratcherIsAdditionalWin, false, false);
        OptionCanWinAtDeath = BooleanOptionItem.Create(RoleInfo, 16, OptionName.ScratcherCanWinAtDeath, false, false, OptionIsAdditionalWin);
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        Scratches += ScratchPerTask;
        Logger.Info($"タスク完了: スクラッチ +{ScratchPerTask} (所持:{Scratches})", "Scratcher");
        UtilsGameLog.AddGameLog("Scratcher",
            string.Format(GetString("ScratcherGetScratchLog"), ScratchPerTask, Scratches, Player.Data.GetPlayerColor()));
        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);
        return true;
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ScratchedThisMeeting = 0;
    }

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie,
        System.Collections.Generic.Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (Won && WinAtMeetingEnd)
            DoSoloWin();
        return false;
    }

    private void ScratchOne()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Won && !WinAtMeetingEnd) return;

        if (!Player.IsAlive())
        {
            Utils.SendMessage(GetString("ScratcherDead"), Player.PlayerId);
            return;
        }

        if (!GameStates.IsMeeting)
        {
            Utils.SendMessage(GetString("ScratcherNotMeeting"), Player.PlayerId);
            return;
        }
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

        var roll = IRandom.Instance.Next(100);
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
        sb.Append('\n');
        sb.Append(string.Format(GetString("ScratcherRemain"),
            Scratches,
            Math.Max(0, MaxScratchPerMeeting - ScratchedThisMeeting)));

        Utils.SendMessage(sb.ToString(), Player.PlayerId);
        Logger.Info($"スクラッチ削り: {(isHit ? "当たり" : "ハズレ")} 当たり数:{Hits}/{WinHitCount} 残り:{Scratches}", "Scratcher");

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);

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
                    Utils.SendMessage(GetString("ScratcherAchieveSoon"), Player.PlayerId);
                else
                    DoSoloWin();
            }
        }
    }

    private void DoSoloWin()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Logger.Info("スクラッチャー単独勝利", "Scratcher");

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Scratcher, Player.PlayerId, true))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }

        Won = false;
        SendRPC();

        _ = new LateTask(() =>
        {
            GameManager.Instance.enabled = false;
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
        }, 0.5f, "Scratcher.EndGame", true);
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

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer) return "";

        var lower = $"<size=80%><{RoleInfo.RoleColorCode}>{string.Format(GetString("ScratcherLower"), Scratches, Hits, WinHitCount)}</color></size>";

        if (isForMeeting && Player.IsAlive() && Scratches > 0 && ScratchedThisMeeting < MaxScratchPerMeeting)
            lower += $"\n<size=70%><color={RoleInfo.RoleColorCode}>/cmd sh でスクラッチを削る</color></size>";

        return lower;
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

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GuesserMsg))]
    private static class ScratcherCommandPatch
    {
        private static bool Prefix(PlayerControl pc, string msg, ref bool __result)
        {
            if (!TryParseStCommand(msg)) return true;

            __result = true;
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || pc == null) return false;

            if (pc.GetRoleClass() is not Scratcher scratcher)
            {
                Utils.SendMessage("/cmd st はスクラッチャー専用コマンドです。", pc.PlayerId,
                    $"<{RoleInfo.RoleColorCode}>スクラッチャー</color>");
                return false;
            }

            scratcher.ScratchOne();
            return false;
        }

        private static bool TryParseStCommand(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return false;
            var args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2) return false;
            if (args[0] != "/cmd") return false;
            var cmd = args[1].StartsWith("/") ? args[1] : $"/{args[1]}";
            return cmd == "/sh";
        }
    }
}
