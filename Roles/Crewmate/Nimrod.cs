using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using static TownOfHost.Modules.MeetingTimeManager;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Nimrod : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Nimrod),
            player => new Nimrod(player),
            CustomRoles.Nimrod,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            33800,
            SetUpOptionItem,
            "nm",
            "#9fcc5b",
            (3, 6),
            from: From.TownOfHost_Y
        );

    public Nimrod(PlayerControl player)
        : base(RoleInfo, player)
    {
        ExecutionMeetingPlayerId = byte.MaxValue;
        PendingExecutionMeetingPlayerId = byte.MaxValue;
    }

    static byte ExecutionMeetingPlayerId = byte.MaxValue;
    static byte PendingExecutionMeetingPlayerId = byte.MaxValue;

    static void SetUpOptionItem() { }

    public static bool IsExecutionMeeting() => ExecutionMeetingPlayerId != byte.MaxValue;

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (Exiled == null) return false;
        if (Exiled.PlayerId != Player.PlayerId) return false;
        if (!Player.IsAlive()) return false;
        if (IsExecutionMeeting()) return false;
        if (PendingExecutionMeetingPlayerId != byte.MaxValue) return false;

        PendingExecutionMeetingPlayerId = Exiled.PlayerId;

        Exiled = null;
        IsTie = false;
        return true;
    }

    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        var baseVote = base.ModifyVote(voterId, sourceVotedForId, isIntentional);
        if (ExecutionMeetingPlayerId != Player.PlayerId || voterId != Player.PlayerId)
            return baseVote;

        if (sourceVotedForId < 15)
        {
            var target = GetPlayerById(sourceVotedForId);
            if (target != null)
            {
                target.SetRealKiller(Player);
                PlayerState.GetByPlayerId(sourceVotedForId).DeathReason = CustomDeathReason.Execution;
                Logger.Info($"{Player.GetNameWithRole()} : exile {target.GetNameWithRole()} by Nimrod", "Nimrod");
            }
        }

        MeetingVoteManager.Instance.ClearAndExile(Player.PlayerId, sourceVotedForId);
        return (baseVote.votedForId, baseVote.numVotes, false);
    }

    public override void OnStartMeeting()
    {
    }

    public override string MeetingAddMessage()
    {
        if (!Player.IsAlive()) return "";
        if (!IsExecutionMeeting() || ExecutionMeetingPlayerId != Player.PlayerId) return "";

        string c = RoleInfo.RoleColorCode;
        return $"<{c}><size=90%>☆ {GetString("IsNimrodMeetingTitle")} ☆</size></color>\n" +
               $"<size=70%>{GetString("IsNimrodMeetingText")}</size>\n";
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (IsExecutionMeeting() && ExecutionMeetingPlayerId == Player.PlayerId)
        {
            var execPc = GetPlayerById(ExecutionMeetingPlayerId);
            if (execPc != null && Main.AllPlayerNames.ContainsKey(ExecutionMeetingPlayerId))
                execPc.RpcSetName(Main.AllPlayerNames[ExecutionMeetingPlayerId]);

            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Vote, ExecutionMeetingPlayerId);

            ExecutionMeetingPlayerId = byte.MaxValue;
            PendingExecutionMeetingPlayerId = byte.MaxValue;
            SendRPC();

            _ = new LateTask(() =>
                UtilsNotifyRoles.NotifyRoles(ForceLoop: true, NoCache: true),
                Main.LagTime, "Nimrod.NotifyAfter");
            return;
        }

        if (PendingExecutionMeetingPlayerId == byte.MaxValue) return;

        var exiledId = PendingExecutionMeetingPlayerId;
        var exiledPlayer = GetPlayerById(exiledId);
        if (exiledPlayer == null || !exiledPlayer.IsAlive())
        {
            PendingExecutionMeetingPlayerId = byte.MaxValue;
            return;
        }

        ExecutionMeetingPlayerId = exiledId;

        exiledPlayer.RpcSetName(
            $"<{RoleInfo.RoleColorCode}><b>★ {Main.AllPlayerNames[exiledId]} ★</b></color>");

        _ = new LateTask(() =>
        {
            _ = new LateTask(() => Utils.AllPlayerKillFlash(), 1f, "Nimrod.KillFlash", true);
            ReportDeadBodyPatch.ExReportDeadBody(
                exiledPlayer, null, false,
                "Nimrod.meeting",
                RoleInfo.RoleColorCode);
        }, 2f, "Nimrod.Meeting");

        _ = new LateTask(() =>
        {
            exiledPlayer.RpcSetName(Main.AllPlayerNames[exiledId]);
            UtilsGameLog.AddGameLog("Meeting",
                $"ニムロッド処刑会議: {UtilsName.GetPlayerColor(exiledPlayer, true)}");
        }, 2.8f, "Nimrod.RestoreName");

        SendRPC();
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ExecutionMeetingPlayerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        ExecutionMeetingPlayerId = reader.ReadByte();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (IsExecutionMeeting() && ExecutionMeetingPlayerId == Player.PlayerId)
        {
            string prefix = isForHud ? "" : "<size=60%>";
            return $"{prefix}<color={RoleInfo.RoleColorCode}>{GetString("IsNimrodMeetingText")}</color>";
        }
        return "";
    }
}