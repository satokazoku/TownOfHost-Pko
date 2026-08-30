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
        GameEndTriggered = false;

        ScratchPerTask = OptionScratchPerTask.GetInt();
        MaxScratchPerMeeting = OptionMaxScratchPerMeeting.GetInt();
        WinHitCount = OptionWinHitCount.GetInt();
        HitProbability = OptionHitProbability.GetInt();
        WinAtMeetingEnd = OptionWinTiming.GetBool();
        IsAdditionalWin = OptionIsAdditionalWin.GetBool();
        CanWinAtDeath = OptionCanWinAtDeath.GetBool();
        AddWinToSoloWin = OptionAddWinToSoloWin.GetBool();
        SoloWinHitCount = OptionSoloWinHitCount.GetInt();

        RedistributeTasksOnFinish = OptionRedistributeTasksOnFinish.GetBool();
        RedistributeNormalTasks = OptionRedistributeNormalTasks.GetInt();
        RedistributeShortTasks = OptionRedistributeShortTasks.GetInt();
        RedistributeLongTasks = OptionRedistributeLongTasks.GetInt();
        if (Main.NormalOptions.NumCommonTasks < RedistributeNormalTasks)
            RedistributeNormalTasks = Main.NormalOptions.NumCommonTasks;
        if (Main.NormalOptions.NumShortTasks < RedistributeShortTasks)
            RedistributeShortTasks = Main.NormalOptions.NumShortTasks;
        if (Main.NormalOptions.NumLongTasks < RedistributeLongTasks)
            RedistributeLongTasks = Main.NormalOptions.NumLongTasks;
    }

    private int Scratches;
    private int Hits;
    private int ScratchedThisMeeting;
    private bool Won;
    private bool AddWin;
    private bool GameEndTriggered;

    private static OptionItem OptionScratchPerTask; private static int ScratchPerTask;
    private static OptionItem OptionMaxScratchPerMeeting; private static int MaxScratchPerMeeting;
    private static OptionItem OptionWinHitCount; private static int WinHitCount;
    private static OptionItem OptionHitProbability; private static int HitProbability;
    private static OptionItem OptionWinTiming; private static bool WinAtMeetingEnd;
    private static OptionItem OptionIsAdditionalWin; private static bool IsAdditionalWin;
    private static OptionItem OptionCanWinAtDeath; private static bool CanWinAtDeath;
    private static OptionItem OptionAddWinToSoloWin; private static bool AddWinToSoloWin;
    private static OptionItem OptionSoloWinHitCount; private static int SoloWinHitCount;
    private static OptionItem OptionRedistributeTasksOnFinish; private static bool RedistributeTasksOnFinish;
    private static OptionItem OptionRedistributeNormalTasks; private static int RedistributeNormalTasks;
    private static OptionItem OptionRedistributeShortTasks; private static int RedistributeShortTasks;
    private static OptionItem OptionRedistributeLongTasks; private static int RedistributeLongTasks;

    enum OptionName
    {
        ScratcherScratchPerTask,
        ScratcherMaxScratchPerMeeting,
        ScratcherWinHitCount,
        ScratcherHitProbability,
        ScratcherWinTiming,
        ScratcherIsAdditionalWin,
        ScratcherCanWinAtDeath,
        ScratcherAddWinToSoloWin,
        ScratcherSoloWinHitCount,
        ScratcherRedistributeTasksOnFinish,
        ScratcherRedistributeNormalTasks,
        ScratcherRedistributeShortTasks,
        ScratcherRedistributeLongTasks,
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
        OptionAddWinToSoloWin = BooleanOptionItem.Create(RoleInfo, 17, OptionName.ScratcherAddWinToSoloWin, false, false, OptionIsAdditionalWin);
        OptionSoloWinHitCount = IntegerOptionItem.Create(RoleInfo, 18, OptionName.ScratcherSoloWinHitCount,
            new(1, 100, 1), 3, false, OptionAddWinToSoloWin).SetValueFormat(OptionFormat.Pieces);
        OptionRedistributeTasksOnFinish = BooleanOptionItem.Create(RoleInfo, 19, OptionName.ScratcherRedistributeTasksOnFinish, false, false);
        OptionRedistributeNormalTasks = IntegerOptionItem.Create(RoleInfo, 30, OptionName.ScratcherRedistributeNormalTasks,
            new(0, 15, 1), 1, false, OptionRedistributeTasksOnFinish).SetValueFormat(OptionFormat.Pieces);
        OptionRedistributeShortTasks = IntegerOptionItem.Create(RoleInfo, 35, OptionName.ScratcherRedistributeShortTasks,
            new(0, 15, 1), 1, false, OptionRedistributeTasksOnFinish).SetValueFormat(OptionFormat.Pieces);
        OptionRedistributeLongTasks = IntegerOptionItem.Create(RoleInfo, 40, OptionName.ScratcherRedistributeLongTasks,
            new(0, 15, 1), 1, false, OptionRedistributeTasksOnFinish).SetValueFormat(OptionFormat.Pieces);
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

        if (RedistributeTasksOnFinish && IsTaskFinished)
        {
            MyTaskState.AllTasksCount += RedistributeNormalTasks + RedistributeShortTasks + RedistributeLongTasks;
            Player.Data.RpcSetTasks(Array.Empty<byte>());
            Player.SyncSettings();
        }

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

    private bool CanScratch(out string errorMessage)
    {
        errorMessage = null;

        if (GameEndTriggered) return false;

        if (!Player.IsAlive())
        {
            errorMessage = GetString("ScratcherDead");
            return false;
        }

        if (!GameStates.IsMeeting)
        {
            errorMessage = GetString("ScratcherNotMeeting");
            return false;
        }

        if (Scratches <= 0)
        {
            errorMessage = GetString("ScratcherNoScratch");
            return false;
        }

        if (ScratchedThisMeeting >= MaxScratchPerMeeting)
        {
            errorMessage = string.Format(GetString("ScratcherMeetingLimit"), MaxScratchPerMeeting);
            return false;
        }

        return true;
    }

    private void ScratchOne()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!CanScratch(out var error))
        {
            if (error != null) Utils.SendMessage(error, Player.PlayerId);
            return;
        }

        var isHit = RollScratch();

        var sb = new StringBuilder();
        sb.Append(isHit
            ? string.Format(GetString("ScratcherHit"), Hits, GetCurrentWinTarget())
            : GetString("ScratcherMiss"));
        sb.Append('\n');
        sb.Append(string.Format(GetString("ScratcherRemain"),
            Scratches,
            Math.Max(0, MaxScratchPerMeeting - ScratchedThisMeeting)));

        Utils.SendMessage(sb.ToString(), Player.PlayerId);

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);

        CheckWinCondition();
    }

    private void ScratchAll()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!CanScratch(out var error))
        {
            if (error != null) Utils.SendMessage(error, Player.PlayerId);
            return;
        }

        var hitCount = 0;
        var totalCount = 0;

        while (Scratches > 0 && ScratchedThisMeeting < MaxScratchPerMeeting && !GameEndTriggered)
        {
            if (RollScratch()) hitCount++;
            totalCount++;
            CheckWinCondition();
        }

        var sb = new StringBuilder();
        sb.Append(string.Format(GetString("ScratcherHitAll"), totalCount, hitCount, Hits, GetCurrentWinTarget()));
        sb.Append('\n');
        sb.Append(string.Format(GetString("ScratcherRemain"),
            Scratches,
            Math.Max(0, MaxScratchPerMeeting - ScratchedThisMeeting)));

        Utils.SendMessage(sb.ToString(), Player.PlayerId);

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: [Player]);
    }

    private bool RollScratch()
    {
        Scratches--;
        ScratchedThisMeeting++;

        var roll = IRandom.Instance.Next(100);
        var isHit = roll < HitProbability;
        if (isHit) Hits++;

        Logger.Info($"スクラッチ削り: {(isHit ? "当たり" : "ハズレ")} 当たり数:{Hits}/{GetCurrentWinTarget()} 残り:{Scratches}", "Scratcher");

        return isHit;
    }

    private void CheckWinCondition()
    {
        if (GameEndTriggered) return;
        if (Hits < WinHitCount) return;

        if (IsAdditionalWin)
        {
            if (!AddWin)
            {
                AddWin = true;
                SendRPC();
                Utils.SendMessage(GetString("ScratcherAchieveAdd"), Player.PlayerId);
            }

            if (AddWinToSoloWin && !Won && Hits >= GetSecondStageWinHitCount())
            {
                Won = true;
                SendRPC();
                if (WinAtMeetingEnd)
                    Utils.SendMessage(GetString("ScratcherAchieveSoon"), Player.PlayerId);
                else
                    DoSoloWin();
            }
        }
        else if (!Won)
        {
            Won = true;
            SendRPC();
            if (WinAtMeetingEnd)
                Utils.SendMessage(GetString("ScratcherAchieveSoon"), Player.PlayerId);
            else
                DoSoloWin();
        }
    }

    private static int GetSecondStageWinHitCount() => Math.Max(WinHitCount + 1, SoloWinHitCount);

    private int GetCurrentWinTarget()
        => (IsAdditionalWin && AddWinToSoloWin && AddWin) ? GetSecondStageWinHitCount() : WinHitCount;

    private void DoSoloWin()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameEndTriggered) return;
        GameEndTriggered = true;

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
        => AddWin && !GameEndTriggered && (CanWinAtDeath || Player.IsAlive());

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => $"<{RoleInfo.RoleColorCode}>({Hits}/{GetCurrentWinTarget()})♦{Scratches}</color>";

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

        var lower = $"<size=80%><{RoleInfo.RoleColorCode}>{string.Format(GetString("ScratcherLower"), Scratches, Hits, GetCurrentWinTarget())}</color></size>";

        if (isForMeeting && Player.IsAlive() && Scratches > 0 && ScratchedThisMeeting < MaxScratchPerMeeting)
            lower += $"\n<size=70%><color={RoleInfo.RoleColorCode}>/cmd /sh で1回、/cmd /sha で一括削り</color></size>";

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
        private enum ScratchCommandType { None, Single, All }

        private static bool Prefix(PlayerControl pc, string msg, ref bool __result)
        {
            var command = TryParseStCommand(msg);
            if (command == ScratchCommandType.None) return true;

            __result = true;
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || pc == null) return false;

            if (pc.GetRoleClass() is not Scratcher scratcher)
            {
                Utils.SendMessage("/cmd /sh・/cmd /sha はスクラッチャー専用コマンドです。", pc.PlayerId,
                    $"<{RoleInfo.RoleColorCode}>スクラッチャー</color>");
                return false;
            }

            if (command == ScratchCommandType.All)
                scratcher.ScratchAll();
            else
                scratcher.ScratchOne();
            return false;
        }

        private static ScratchCommandType TryParseStCommand(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return ScratchCommandType.None;
            var args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2) return ScratchCommandType.None;
            if (args[0] != "/cmd") return ScratchCommandType.None;
            var cmd = args[1].StartsWith("/") ? args[1] : $"/{args[1]}";
            return cmd switch
            {
                "/sh" => ScratchCommandType.Single,
                "/sha" => ScratchCommandType.All,
                _ => ScratchCommandType.None
            };
        }
    }
}