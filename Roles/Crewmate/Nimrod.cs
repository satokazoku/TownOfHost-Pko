using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
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
            63000,
            SetUpOptionItem,
            "nm",
            "#9fcc5b",
            from: From.TownOfHost_Y
        );

    public Nimrod(PlayerControl player)
        : base(RoleInfo, player)
    {
        ExecutionMeetingPlayerId = byte.MaxValue;
        PendingExecutionMeetingPlayerId = byte.MaxValue;
        KillImpostor = OptionKillImpostor.GetBool();
    }

    static byte ExecutionMeetingPlayerId = byte.MaxValue;
    static byte PendingExecutionMeetingPlayerId = byte.MaxValue;

    static OptionItem OptionKillImpostor;
    static bool KillImpostor;

    enum OptionName
    {
        NimrodKillImpostor,
    }

    static void SetUpOptionItem()
    {
        OptionKillImpostor = BooleanOptionItem.Create(RoleInfo, 10, OptionName.NimrodKillImpostor, false, false);
    }

    public static bool IsExecutionMeeting() => ExecutionMeetingPlayerId != byte.MaxValue;

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (Exiled == null) return false;
        if (Exiled.PlayerId != Player.PlayerId) return false;
        if (!Player.IsAlive()) return false;
        if (IsExecutionMeeting()) return false;
        if (PendingExecutionMeetingPlayerId != byte.MaxValue) return false;

        var exiledId = Exiled.PlayerId;
        PendingExecutionMeetingPlayerId = exiledId;

        _ = new LateTask(() =>
        {
            if (PendingExecutionMeetingPlayerId != exiledId) return;
            PendingExecutionMeetingPlayerId = byte.MaxValue;

            var exiledPlayer = GetPlayerById(exiledId);
            if (exiledPlayer == null || !exiledPlayer.IsAlive()) return;

            ExecutionMeetingPlayerId = exiledId;
            Logger.Info($"{exiledPlayer.GetNameWithRole()} : start Nimrod execution meeting", "Nimrod");
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            global::TownOfHost.ReportDeadBodyPatch.ExReportDeadBody(exiledPlayer, null, Cancelcheck: false);
            SendRPC();
        }, 14.5f, "NimrodExiled", true);

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
                if (!KillImpostor && target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode)
                    return baseVote;

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
        if (!IsExecutionMeeting() || ExecutionMeetingPlayerId != Player.PlayerId) return;
        if (AmongUsClient.Instance.AmHost)
        {
            Utils.SendMessage("Nimrod 会議");
        }
        Utils.SendMessage(
            GetString("IsNimrodMeetingText"),
            title: $"<color={RoleInfo.RoleColorCode}>{GetString("IsNimrodMeetingTitle")}</color>"
        );
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsExecutionMeeting()) return;

        MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Vote, ExecutionMeetingPlayerId);
        ExecutionMeetingPlayerId = byte.MaxValue;
        PendingExecutionMeetingPlayerId = byte.MaxValue;
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
            var prefix = isForHud ? "" : "<size=60%>";
            var suffix = isForHud ? "" : "</size>";
            return $"{prefix}<color={RoleInfo.RoleColorCode}>{GetString("IsNimrodMeetingText")}</color>{suffix}";
        }

        return "";
    }
}