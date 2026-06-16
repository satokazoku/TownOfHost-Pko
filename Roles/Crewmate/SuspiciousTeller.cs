using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class SuspiciousTeller : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SuspiciousTeller),
            player => new SuspiciousTeller(player),
            CustomRoles.SuspiciousTeller,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            35800,
            SetupOptionItem,
            "spt",
            "#6b3ec3",
            (3, 2),
            introSound: () => GetIntroSound(RoleTypes.Scientist)
        );

    private static readonly CustomRoles[] ForbiddenChangeRoles =
    [
        CustomRoles.SuspiciousTeller,
        CustomRoles.GM,
        CustomRoles.HASFox,
        CustomRoles.HASTroll,
        CustomRoles.MMArcher,
        CustomRoles.TaskPlayerB,
        CustomRoles.SatsumatoImo,
        CustomRoles.Apprentice,
        CustomRoles.Walker,
        CustomRoles.Merlin,
    ];

    private static OptionItem optionChangeChance;
    private static AssignOptionItem optionChangeRoleFilter;
    private static OptionItem optionNoChangeImpostor;
    private static OptionItem optionNoChangeMadmate;
    private static OptionItem optionNoChangeCrewmate;
    private static OptionItem optionNoChangeNeutral;
    private static OptionItem optionNonAlignFortuneTeller;
    private static OptionItem optionMaximum;
    private static OptionItem optionVoteMode;
    private static OptionItem optionRoleName;
    private static OptionItem optionRole;
    private static OptionItem optionCanTaskCount;
    private static OptionItem optionOneMeetingMaximum;
    private static OptionItem optionAwakening;
    private static OptionItem optionStableResult;
    private static OptionItem optionRecognizeAsFortuneTeller;

    private int changeChance;
    private int maxUseCount;
    private int usedCount;
    private int taskCount;
    private int oneMeetingMaximum;
    private int meetingUsedCount;
    private AbilityVoteMode voteMode;
    private bool showRoleName;
    private bool tellRole;
    private bool awakened;

    private readonly Dictionary<byte, CustomRoles> divinations = new();
    private readonly Dictionary<byte, CustomRoles> stableResults = new();

    private enum OptionName
    {
        SuspiciousTellerChangeChance,
        SuspiciousTellerChangeRoleFilter,
        SuspiciousTellerNoChangeImpostor,
        SuspiciousTellerNoChangeMadmate,
        SuspiciousTellerNoChangeCrewmate,
        SuspiciousTellerNoChangeNeutral,
        SuspiciousTellerMyIsFT,
        SuspiciousTellerFTOption,
        TellMaximum,
        AbilityVotemode,
        TellerCanSeeRolename,
        TellRole,
        PonkotuDontChengeGame,
    }

    public SuspiciousTeller(PlayerControl player)
        : base(RoleInfo, player)
    {
        ReloadOptions();
        ResetRuntime();
    }

    private static void SetupOptionItem()
    {
        optionChangeChance = IntegerOptionItem.Create(RoleInfo, 10, OptionName.SuspiciousTellerChangeChance, new(0, 100, 1), 70, false)
            .SetValueFormat(OptionFormat.Percent);
        optionChangeRoleFilter = AssignOptionItem.Create(RoleInfo, 11, OptionName.SuspiciousTellerChangeRoleFilter, 0, false, optionChangeChance, imp: true, mad: true, crew: true, neu: true, notassing: ForbiddenChangeRoles);
        optionNoChangeImpostor = BooleanOptionItem.Create(RoleInfo, 12, OptionName.SuspiciousTellerNoChangeImpostor, true, false);
        optionNoChangeMadmate = BooleanOptionItem.Create(RoleInfo, 13, OptionName.SuspiciousTellerNoChangeMadmate, true, false);
        optionNoChangeCrewmate = BooleanOptionItem.Create(RoleInfo, 14, OptionName.SuspiciousTellerNoChangeCrewmate, false, false);
        optionNoChangeNeutral = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SuspiciousTellerNoChangeNeutral, true, false);
        optionRecognizeAsFortuneTeller = BooleanOptionItem.Create(RoleInfo, 17, OptionName.SuspiciousTellerMyIsFT, true, false);

        optionNonAlignFortuneTeller = BooleanOptionItem.Create(RoleInfo, 18, OptionName.SuspiciousTellerFTOption, false, false);
        optionMaximum = IntegerOptionItem.Create(RoleInfo, 19, OptionName.TellMaximum, new(1, 99, 1), 1, false, optionNonAlignFortuneTeller)
            .SetValueFormat(OptionFormat.Times);
        optionVoteMode = StringOptionItem.Create(RoleInfo, 20, OptionName.AbilityVotemode, EnumHelper.GetAllNames<AbilityVoteMode>(), 1, false, optionNonAlignFortuneTeller);
        optionRoleName = BooleanOptionItem.Create(RoleInfo, 21, OptionName.TellerCanSeeRolename, true, false, optionNonAlignFortuneTeller);
        optionRole = BooleanOptionItem.Create(RoleInfo, 22, OptionName.TellRole, true, false, optionNonAlignFortuneTeller);
        optionCanTaskCount = IntegerOptionItem.Create(RoleInfo, 23, GeneralOption.cantaskcount, new(0, 99, 1), 5, false, optionNonAlignFortuneTeller);
        optionOneMeetingMaximum = IntegerOptionItem.Create(RoleInfo, 24, GeneralOption.MeetingMaxTime, new(0, 99, 1), 0, false, optionNonAlignFortuneTeller)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        optionAwakening = BooleanOptionItem.Create(RoleInfo, 25, GeneralOption.AbilityAwakening, true, false, optionNonAlignFortuneTeller);
        optionStableResult = BooleanOptionItem.Create(RoleInfo, 26, OptionName.PonkotuDontChengeGame, true, false, optionNonAlignFortuneTeller);

    }

    public override void Add()
    {
        ReloadOptions();
        ResetRuntime();
        SendRPC();
    }

    public override void OnDestroy()
    {
        SetMode(Player, false);
    }

    private void ReloadOptions()
    {
        changeChance = optionChangeChance.GetInt();

        if (optionNonAlignFortuneTeller.GetBool())
        {
            maxUseCount = optionMaximum.GetInt();
            voteMode = (AbilityVoteMode)optionVoteMode.GetValue();
            showRoleName = optionRoleName.GetBool();
            tellRole = optionRole.GetBool();
            taskCount = optionCanTaskCount.GetInt();
            oneMeetingMaximum = optionOneMeetingMaximum.GetInt();
            awakened = !optionAwakening.GetBool() || taskCount < 1;
            return;
        }

        maxUseCount = FortuneTeller.OptionMaximum.GetInt();
        voteMode = (AbilityVoteMode)FortuneTeller.OptionVoteMode.GetValue();
        showRoleName = FortuneTeller.Optionrolename.GetBool();
        tellRole = FortuneTeller.OptionRole.GetBool();
        taskCount = FortuneTeller.OptionCanTaskcount.GetInt();
        oneMeetingMaximum = FortuneTeller.Option1MeetingMaximum.GetInt();
        awakened = !FortuneTeller.OptAwakening.GetBool() || taskCount < 1;
    }

    private void ResetRuntime()
    {
        usedCount = 0;
        meetingUsedCount = 0;
        divinations.Clear();
        stableResults.Clear();
        SetMode(Player, false);
    }

    public override void OnStartMeeting()
    {
        meetingUsedCount = 0;
        SetMode(Player, false);
        SendRPC();
    }

    private static void CleanupBeforeRoleChange(PlayerControl target, CustomRoles beforeRole, CustomRoles afterRole)
    {
        if (beforeRole == CustomRoles.Express && afterRole != CustomRoles.Express)
            Main.AllPlayerSpeed[target.PlayerId] = Main.NormalOptions.PlayerSpeedMod;

        if (beforeRole == CustomRoles.UltraStar && afterRole != CustomRoles.UltraStar && target.GetRoleClass() is UltraStar ultraStar)
        {
            var colorField = typeof(UltraStar).GetField("PlayerColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (colorField?.GetValue(ultraStar) is int originalColorId)
                target.RpcSetColor((byte)originalColorId);
        }
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!awakened && MyTaskState.HasCompletedEnoughCountOfTasks(taskCount))
        {
            awakened = true;
            if (!Utils.RoleSendList.Contains(Player.PlayerId))
                Utils.RoleSendList.Add(Player.PlayerId);
            SendRPC();
        }
        return true;
    }

    public override CustomRoles Misidentify()
    {
        if (!awakened) return CustomRoles.Crewmate;
        return optionRecognizeAsFortuneTeller.GetBool() ? CustomRoles.FortuneTeller : CustomRoles.NotAssigned;
    }

    private bool CanUseTellAbility()
    {
        if (!Canuseability()) return false;
        if (!Player.IsAlive()) return false;
        if (!awakened) return false;
        if (!MyTaskState.HasCompletedEnoughCountOfTasks(taskCount)) return false;
        if (usedCount >= maxUseCount) return false;
        if (oneMeetingMaximum != 0 && meetingUsedCount >= oneMeetingMaximum) return false;
        return true;
    }

    bool ISelfVoter.CanUseVoted() => CanUseTellAbility();

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter)) return true;
        if (!CanUseTellAbility()) return true;

        if (voteMode == AbilityVoteMode.NomalVote)
        {
            if (votedForId == Player.PlayerId || votedForId == SkipId || votedForId >= 253) return true;
            UseTellAbility(votedForId);
            return false;
        }

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
                Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.Divied"), GetString("Vote.Divied")) + GetString("VoteSkillMode"), Player.PlayerId);
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
                UseTellAbility(votedForId);

            SetMode(Player, status is VoteStatus.Self);
            return false;
        }

        return true;
    }

    private void UseTellAbility(byte votedForId)
    {
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (target == null || target.Data.Disconnected || !target.IsAlive() || target.PlayerId == Player.PlayerId) return;

        usedCount++;
        meetingUsedCount++;

        var beforeActualRole = target.GetCustomRole();
        var stableRole = CustomRoles.NotAssigned;
        var useStableResult = optionStableResult.GetBool() && stableResults.TryGetValue(votedForId, out stableRole);
        var toldRole = useStableResult ? stableRole : target.GetTellResults(Player);

        divinations[votedForId] = toldRole;
        if (!useStableResult) stableResults[votedForId] = toldRole;

        var willChange = TryChangeRole(target, beforeActualRole, out var afterRole);
        SendRPC();
        SendTellMessage(target, toldRole, willChange);

        Logger.Info($"Player: {Player.name}, Target: {target.name}, result: {toldRole}, change: {(willChange ? $"{beforeActualRole}->{afterRole}" : "none")}, count: {usedCount}", "SuspiciousTeller");
    }

    private bool TryChangeRole(PlayerControl target, CustomRoles beforeRole, out CustomRoles afterRole)
    {
        afterRole = CustomRoles.NotAssigned;
        if (!AmongUsClient.Instance.AmHost) return false;
        if (target == null || target.Data.Disconnected || !target.IsAlive()) return false;
        if (ShouldSkipRoleChange(beforeRole)) return false;
        if (changeChance <= 0) return false;
        if (IRandom.Instance.Next(0, 100) >= changeChance) return false;

        IEnumerable<CustomRoles> changeRoles = optionChangeRoleFilter.GetBool()
            ? optionChangeRoleFilter.GetNowRoleValue()
            : CustomRoleManager.AllRolesInfo.Keys;
        var candidates = changeRoles
            .Where(role => IsChangeRoleCandidate(role, beforeRole))
            .Distinct()
            .ToList();

        if (candidates.Count <= 0) return false;

        afterRole = candidates[IRandom.Instance.Next(0, candidates.Count)];
        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Crewmate)) return false;

        CleanupBeforeRoleChange(target, beforeRole, afterRole);

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(afterRole, log: null);

        if (afterRole == CustomRoles.UltraStar)
        {
            var field = typeof(UltraStar).GetField("CanseeAllplayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field?.SetValue(null, true);
        }

        UtilsOption.MarkEveryoneDirtySettings();
        GameData.Instance?.RecomputeTaskCounts();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);

        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} changed by SuspiciousTeller ({beforeRole} -> {afterRole})", "SuspiciousTeller");
        return true;
    }

    private bool ShouldSkipRoleChange(CustomRoles role)
    {
        return role.GetCustomRoleTypes() switch
        {
            CustomRoleTypes.Impostor => optionNoChangeImpostor.GetBool(),
            CustomRoleTypes.Madmate => optionNoChangeMadmate.GetBool(),
            CustomRoleTypes.Crewmate => optionNoChangeCrewmate.GetBool(),
            CustomRoleTypes.Neutral => optionNoChangeNeutral.GetBool(),
            _ => true,
        };
    }

    private static bool IsChangeRoleCandidate(CustomRoles role, CustomRoles beforeRole)
    {
        if (role is CustomRoles.NotAssigned) return false;
        if (role >= CustomRoles.NotAssigned) return false;
        if (role == beforeRole) return false;
        if (ForbiddenChangeRoles.Contains(role)) return false;
        if (!Event.CheckRole(role)) return false;
        return CustomRoleManager.AllRolesInfo.ContainsKey(role);
    }

    private void SendTellMessage(PlayerControl target, CustomRoles toldRole, bool willChange)
    {
        var roleText = tellRole
            ? "<b>" + GetString($"{toldRole}").Color(UtilsRoleText.GetRoleColor(toldRole)) + "</b>"
            : GetString($"{toldRole.GetCustomRoleTypes()}");
        var lastText = GetString("Skill.Tellerfin") + (toldRole.IsCrewmate() ? "!" : "...");
        var remainingText = oneMeetingMaximum != 0
            ? string.Format(GetString("RemainingOneMeetingCount"), Math.Min(oneMeetingMaximum - meetingUsedCount, maxUseCount - usedCount))
            : string.Format(GetString("RemainingCount"), maxUseCount - usedCount);

        var message = string.Format(GetString("Skill.Teller"), UtilsName.GetPlayerColor(target, true), roleText)
            + lastText
            + (willChange ? "\n" + GetString("SuspiciousTellerChangedRole") : "")
            + "\n\n"
            + remainingText
            + (voteMode == AbilityVoteMode.SelfVote ? "\n\n" + GetString("VoteSkillFin") : "");

        Utils.SendMessage(message, Player.PlayerId);
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var color = !awakened || !MyTaskState.HasCompletedEnoughCountOfTasks(taskCount) || usedCount >= maxUseCount
            ? Color.gray
            : Color.cyan;
        return Utils.ColorString(color, $"({maxUseCount - usedCount})");
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!isForMeeting || seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId) return "";
        if (!CanUseTellAbility()) return "";

        var mes = $"<color={RoleInfo.RoleColorCode}>{(voteMode == AbilityVoteMode.SelfVote ? GetString("SelfVoteRoleInfoMeg") : GetString("NomalVoteRoleInfoMeg"))}</color>";
        return isForHud ? mes : $"<size=40%>{mes}</size>";
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!showRoleName) return "";
        if (!divinations.TryGetValue(seen.PlayerId, out var role)) return "";
        return tellRole
            ? $"<color={UtilsRoleText.GetRoleColorCode(role)}>" + GetString(role.ToString())
            : GetString(role.GetCustomRoleTypes().ToString());
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(usedCount);
        sender.Writer.Write(meetingUsedCount);
        sender.Writer.Write(awakened);

        sender.Writer.Write(divinations.Count);
        foreach (var (playerId, role) in divinations)
        {
            sender.Writer.Write(playerId);
            sender.Writer.WritePacked((int)role);
        }

        sender.Writer.Write(stableResults.Count);
        foreach (var (playerId, role) in stableResults)
        {
            sender.Writer.Write(playerId);
            sender.Writer.WritePacked((int)role);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        usedCount = reader.ReadInt32();
        meetingUsedCount = reader.ReadInt32();
        awakened = reader.ReadBoolean();

        divinations.Clear();
        var divinationCount = reader.ReadInt32();
        for (var i = 0; i < divinationCount; i++)
        {
            var playerId = reader.ReadByte();
            var role = (CustomRoles)reader.ReadPackedInt32();
            divinations[playerId] = role;
        }

        stableResults.Clear();
        var stableCount = reader.ReadInt32();
        for (var i = 0; i < stableCount; i++)
        {
            var playerId = reader.ReadByte();
            var role = (CustomRoles)reader.ReadPackedInt32();
            stableResults[playerId] = role;
        }
    }
}
