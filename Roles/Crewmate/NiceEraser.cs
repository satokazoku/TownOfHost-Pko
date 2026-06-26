using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceEraser : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceEraser),
            player => new NiceEraser(player),
            CustomRoles.NiceEraser,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            33300,
            SetupOptionItem,
            "nicer",
            "#d0ff00",
            (1, 5)
        );

    private static OptionItem OptionMaxUseCount;
    private static OptionItem OptionVoteMode;
    private static OptionItem OptionTaskCount;
    private static OptionItem OptionOneMeetingMaximum;
    private static OptionItem OptionAwakening;

    private int maxUseCount;
    private int taskCount;
    private int oneMeetingMaximum;
    private AbilityVoteMode voteMode;

    private int usedCount;
    private int meetingUsedCount;
    private bool awakened;

    private readonly List<byte> queuedReformTargets = new();

    enum OptionName
    {
        NiceEraserMaxUseCount,
        NiceEraserVoteMode,
        NiceEraserTaskCount,
        NiceEraserOneMeetingMaximum,
    }

    private static void SetupOptionItem()
    {
        OptionMaxUseCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.NiceEraserMaxUseCount, new(1, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionVoteMode = StringOptionItem.Create(RoleInfo, 11, OptionName.NiceEraserVoteMode, EnumHelper.GetAllNames<AbilityVoteMode>(), 1, false);
        OptionTaskCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.NiceEraserTaskCount, new(0, 99, 1), 5, false);
        OptionOneMeetingMaximum = IntegerOptionItem.Create(RoleInfo, 13, OptionName.NiceEraserOneMeetingMaximum, new(1, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionAwakening = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.AbilityAwakening, true, false);
    }

    public NiceEraser(PlayerControl player)
        : base(RoleInfo, player)
    {
        ReloadOptions();
        ResetRuntime();
    }

    public override void Add()
    {
        ReloadOptions();
        ResetRuntime();
        SendRPC();
    }

    private void ReloadOptions()
    {
        maxUseCount = OptionMaxUseCount.GetInt();
        taskCount = OptionTaskCount.GetInt();
        oneMeetingMaximum = OptionOneMeetingMaximum.GetInt();
        voteMode = (AbilityVoteMode)OptionVoteMode.GetValue();
        awakened = !OptionAwakening.GetBool() || taskCount <= 0;
    }

    private void ResetRuntime()
    {
        usedCount = 0;
        meetingUsedCount = 0;
        queuedReformTargets.Clear();
        SetMode(Player, false);
    }

    public override void OnStartMeeting()
    {
        meetingUsedCount = 0;
        SetMode(Player, false);
        SendRPC();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            queuedReformTargets.Clear();
            return;
        }

        if (queuedReformTargets.Count <= 0) return;

        foreach (var targetId in queuedReformTargets.ToArray())
        {
            var target = PlayerCatch.GetPlayerById(targetId);
            if (target == null || target.Data.Disconnected || !target.IsAlive()) continue;

            var beforeRole = target.GetCustomRole();
            if (beforeRole == CustomRoles.Crewmate) continue;

            if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Crewmate)) break;

            target.RpcSetCustomRole(CustomRoles.Crewmate, true, null);
            UtilsGameLog.AddGameLog("NiceEraser", string.Format(GetString("NiceEraserReformLog"), UtilsName.GetPlayerColor(Player), UtilsName.GetPlayerColor(target)));
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} reformed => {target.GetNameWithRole().RemoveHtmlTags()} ({beforeRole} -> Crewmate)", "NiceEraser");
        }

        queuedReformTargets.Clear();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles();
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!awakened && MyTaskState.HasCompletedEnoughCountOfTasks(taskCount))
        {
            awakened = true;
            if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
            SendRPC();
        }
        return true;
    }

    public override CustomRoles Misidentify() => awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;

    private bool CanUseAbilityNow()
    {
        if (!Canuseability()) return false;
        if (!Player.IsAlive()) return false;
        if (!awakened) return false;
        if (!MyTaskState.HasCompletedEnoughCountOfTasks(taskCount)) return false;
        if (usedCount >= maxUseCount) return false;
        if (meetingUsedCount >= oneMeetingMaximum) return false;
        return true;
    }

    bool ISelfVoter.CanUseVoted() => CanUseAbilityNow();

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter)) return true;
        if (!CanUseAbilityNow()) return true;

        if (voteMode == AbilityVoteMode.NomalVote)
        {
            if (votedForId == Player.PlayerId || votedForId == SkipId || votedForId >= 253) return true;
            TryQueueReform(votedForId);
            return false;
        }

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
                Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.NiceEraser"), GetString("Vote.NiceEraser")) + GetString("VoteSkillMode"), Player.PlayerId);
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
                TryQueueReform(votedForId);

            SetMode(Player, status is VoteStatus.Self);
            return false;
        }

        return true;
    }

    private void TryQueueReform(byte votedForId)
    {
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (target == null || target.Data.Disconnected || !target.IsAlive() || target.PlayerId == Player.PlayerId)
        {
            Utils.SendMessage(GetString("NiceEraserInvalidTarget"), Player.PlayerId);
            return;
        }

        if (queuedReformTargets.Contains(target.PlayerId))
        {
            Utils.SendMessage(GetString("NiceEraserAlreadyQueued"), Player.PlayerId);
            return;
        }

        if (usedCount >= maxUseCount || meetingUsedCount >= oneMeetingMaximum) return;

        usedCount++;
        meetingUsedCount++;
        queuedReformTargets.Add(target.PlayerId);

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);

        var oneMeetingLeft = oneMeetingMaximum - meetingUsedCount;
        var totalLeft = maxUseCount - usedCount;
        var msg = string.Format(GetString("NiceEraserQueued"), UtilsName.GetPlayerColor(target, true))
            + "\n"
            + string.Format(GetString("RemainingOneMeetingCount"), oneMeetingLeft)
            + "\n"
            + string.Format(GetString("RemainingCount"), totalLeft)
            + (voteMode == AbilityVoteMode.SelfVote ? "\n\n" + GetString("VoteSkillFin") : "");
        Utils.SendMessage(msg, Player.PlayerId);
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (OptionAwakening.GetBool() && !awakened) return "";
        return Utils.ColorString(usedCount >= maxUseCount ? Color.gray : Color.cyan, $"({maxUseCount - usedCount})");
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!isForMeeting || seer.PlayerId != seen.PlayerId || seer.PlayerId != Player.PlayerId) return "";
        if (!CanUseAbilityNow()) return "";

        var key = voteMode == AbilityVoteMode.SelfVote ? "SelfVoteRoleInfoMeg" : "NomalVoteRoleInfoMeg";
        var mes = $"<color={RoleInfo.RoleColorCode}>{GetString(key)}</color>";
        return isForHud ? mes : $"<size=40%>{mes}</size>";
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.PlayerId != Player.PlayerId) return "";
        if (!queuedReformTargets.Contains(seen.PlayerId)) return "";
        return "<color=#79c36a>◎</color>";
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(usedCount);
        sender.Writer.Write(meetingUsedCount);
        sender.Writer.Write(awakened);
        sender.Writer.Write(queuedReformTargets.Count);
        foreach (var id in queuedReformTargets)
            sender.Writer.Write(id);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        usedCount = reader.ReadInt32();
        meetingUsedCount = reader.ReadInt32();
        awakened = reader.ReadBoolean();

        queuedReformTargets.Clear();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
            queuedReformTargets.Add(reader.ReadByte());
    }
}
