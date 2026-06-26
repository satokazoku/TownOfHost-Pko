using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;
using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class MagicalGirl : RoleBase, ISelfVoter, IKiller, IUsePhantomButton,
    ISystemTypeUpdateHook, IKillFlashSeeable, IDeathReasonSeeable, IMeetingTimeAlterable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MagicalGirl),
            player => new MagicalGirl(player),
            CustomRoles.MagicalGirl,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            32400,
            SetupOptionItem,
            "mg",
            "#ff66cc",
            (2, 0)
        );

    private enum SlotMode
    {
        None = 0,
        Random = 1,
        AssignFilter = 2
    }

    private readonly struct CandidateEntry
    {
        public readonly byte PlayerId;
        public readonly byte SlotNumber;
        public readonly CustomRoles Role;
        public CandidateEntry(byte playerId, byte slotNumber, CustomRoles role)
        {
            PlayerId = playerId;
            SlotNumber = slotNumber;
            Role = role;
        }
    }

    private static readonly string[] SlotModeSelections =
    [
        "MagicalGirlSlotMode.None",
        "MagicalGirlSlotMode.Random",
        "MagicalGirlSlotMode.AssignFilter"
    ];

    private static readonly CustomRoles[] ForbiddenTransformRoles =
    [
        CustomRoles.MagicalGirl,
        CustomRoles.AllArounder,
        CustomRoles.SatsumatoImo,
        CustomRoles.SatsumatoImoC,
        CustomRoles.SatsumatoImoM,
        CustomRoles.Apprentice,
        CustomRoles.Walker,
        CustomRoles.Merlin
    ];

    private const int StateRpcTag = 0x4D47524C; // MGRL

    private static OptionItem OptionRequiredTasks;
    private static OptionItem OptionTransformDuration;
    private static StringOptionItem OptionRole1Mode;
    private static StringOptionItem OptionRole2Mode;
    private static StringOptionItem OptionRole3Mode;
    private static AssignOptionItem OptionRole1Assign;
    private static AssignOptionItem OptionRole2Assign;
    private static AssignOptionItem OptionRole3Assign;

    private readonly HashSet<CustomRoles> usedRoles = [];
    private readonly List<CandidateEntry> meetingCandidates = [];

    private RoleBase addRole;
    private CustomRoles transformedRole;
    private int remainingTurns;
    private bool transformedThisMeeting;
    private int requiredTasks;
    private int transformDuration;
    private bool hadGuesserBeforeTransform;

    private bool IsTransformed => transformedRole is not CustomRoles.NotAssigned && addRole != null;

    private enum OptionName
    {
        MagicalGirlRequiredTasks,
        MagicalGirlTransformDuration,
        MagicalGirlTransformRole1Mode,
        MagicalGirlTransformRole2Mode,
        MagicalGirlTransformRole3Mode,
        MagicalGirlTransformRole1Filter,
        MagicalGirlTransformRole2Filter,
        MagicalGirlTransformRole3Filter
    }

    public MagicalGirl(PlayerControl player)
        : base(RoleInfo, player)
    {
        hasTasks = GetCurrentHasTasks;
        transformedRole = CustomRoles.NotAssigned;
        remainingTurns = 0;
        transformedThisMeeting = false;
        hadGuesserBeforeTransform = false;
        requiredTasks = OptionRequiredTasks.GetInt();
        transformDuration = OptionTransformDuration.GetInt();
    }

    private static void SetupOptionItem()
    {
        OptionRequiredTasks = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MagicalGirlRequiredTasks, new(0, 99, 1), 3, false);
        OptionTransformDuration = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MagicalGirlTransformDuration, new(1, 99, 1), 2, false).SetValueFormat(OptionFormat.day);

        OptionRole1Mode = StringOptionItem.Create(RoleInfo, 12, OptionName.MagicalGirlTransformRole1Mode, SlotModeSelections, 0, false);
        OptionRole2Mode = StringOptionItem.Create(RoleInfo, 13, OptionName.MagicalGirlTransformRole2Mode, SlotModeSelections, 0, false);
        OptionRole3Mode = StringOptionItem.Create(RoleInfo, 14, OptionName.MagicalGirlTransformRole3Mode, SlotModeSelections, 0, false);

        OptionRole1Assign = AssignOptionItem.Create(RoleInfo, 20, OptionName.MagicalGirlTransformRole1Filter, 0, false, null, crew: true, notassing: ForbiddenTransformRoles);
        OptionRole2Assign = AssignOptionItem.Create(RoleInfo, 21, OptionName.MagicalGirlTransformRole2Filter, 0, false, null, crew: true, notassing: ForbiddenTransformRoles);
        OptionRole3Assign = AssignOptionItem.Create(RoleInfo, 22, OptionName.MagicalGirlTransformRole3Filter, 0, false, null, crew: true, notassing: ForbiddenTransformRoles);

        OptionRole1Assign.SetEnabled(() => OptionRole1Mode.GetValue() == (int)SlotMode.AssignFilter);
        OptionRole2Assign.SetEnabled(() => OptionRole2Mode.GetValue() == (int)SlotMode.AssignFilter);
        OptionRole3Assign.SetEnabled(() => OptionRole3Mode.GetValue() == (int)SlotMode.AssignFilter);
    }

    public override void Add()
    {
        usedRoles.Clear();
        meetingCandidates.Clear();
        transformedRole = CustomRoles.NotAssigned;
        addRole = null;
        remainingTurns = 0;
        transformedThisMeeting = false;
        hadGuesserBeforeTransform = Player.Is(CustomRoles.Guesser);
        SetMode(Player, false);
        RefreshMainRoleState();
        SendStateRPC();
    }

    public override void OnDestroy()
    {
        ClearTransform(logReturn: false);
        meetingCandidates.Clear();
        usedRoles.Clear();
        SetMode(Player, false);
    }

    private HasTask GetCurrentHasTasks()
    {
        if (IsTransformed)
        {
            return addRole?.HasTasks ?? HasTask.True;
        }
        return HasTask.True;
    }

    private bool CanUseTransformAbility()
    {
        if (!Player.IsAlive()) return false;
        if (!SelfVoteManager.Canuseability()) return false;
        if (IsTransformed) return false;
        if (!MyTaskState.HasCompletedEnoughCountOfTasks(requiredTasks)) return false;
        return HasAnyCandidateRole();
    }

    private bool HasAnyCandidateRole()
    {
        for (var i = 0; i < 3; i++)
        {
            var mode = GetSlotMode(i);
            if (mode is SlotMode.None) continue;

            if (mode is SlotMode.Random)
            {
                if (GetTransformableCrewRoles().Any(r => !usedRoles.Contains(r))) return true;
                continue;
            }

            var option = GetSlotAssignOption(i);
            if (option.GetNowRoleValue().Any(r => IsTransformableCrewRole(r) && !usedRoles.Contains(r)))
                return true;
        }
        return false;
    }

    bool ISelfVoter.CanUseVoted()
    {
        if (IsTransformed && addRole is ISelfVoter transformedSelfVoter)
        {
            return transformedSelfVoter.CanUseVoted();
        }
        return CanUseTransformAbility();
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (IsTransformed)
        {
            return addRole?.CheckVoteAsVoter(votedForId, voter) ?? true;
        }

        if (!Is(voter) || !CanUseTransformAbility()) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            switch (status)
            {
                case VoteStatus.Self:
                    if (PrepareMeetingCandidates())
                    {
                        ShowCandidateList();
                        Utils.SendMessage(GetString("MagicalGirlVoteModeOn"), Player.PlayerId);
                        SetMode(Player, true);
                    }
                    else
                    {
                        Utils.SendMessage(GetString("MagicalGirlNoCandidates"), Player.PlayerId);
                        SetMode(Player, false);
                    }
                    break;
                case VoteStatus.Skip:
                    meetingCandidates.Clear();
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    SetMode(Player, false);
                    break;
                case VoteStatus.Vote:
                    if (TryGetCandidateRole(votedForId, out var selectedRole))
                    {
                        TransformInto(selectedRole);
                    }
                    else
                    {
                        Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    }
                    SetMode(Player, false);
                    break;
            }
            SendStateRPC();
            return false;
        }
        return true;
    }

    public override void OnStartMeeting()
    {
        if (IsTransformed)
        {
            addRole?.OnStartMeeting();
        }
        meetingCandidates.Clear();
        SendStateRPC();
    }

    public override void AfterMeetingTasks()
    {
        if (IsTransformed && transformedRole is not CustomRoles.Bakery)
        {
            addRole?.AfterMeetingTasks();
        }

        if (!AmongUsClient.Instance.AmHost || !IsTransformed) return;

        if (transformedThisMeeting)
        {
            transformedThisMeeting = false;
            SendStateRPC();
            return;
        }

        remainingTurns--;
        if (remainingTurns <= 0)
        {
            ClearTransform();
            Utils.SendMessage(GetString("MagicalGirlTransformEnded"), Player.PlayerId);
            if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
            UtilsNotifyRoles.NotifyRoles();
        }
        SendStateRPC();
    }

    public override bool OnCompleteTask(uint taskid)
    {
        return IsTransformed ? addRole?.OnCompleteTask(taskid) ?? true : true;
    }

    public override bool CanTask() => !IsTransformed || (addRole?.CanTask() ?? true);

    public override void CheckWinner(GameOverReason reason)
    {
        if (IsTransformed)
        {
            addRole?.CheckWinner(reason);
            if (reason is GameOverReason.CrewmatesByTask or GameOverReason.CrewmatesByVote
                && transformedRole is CustomRoles.Staff
                && addRole is Staff staff
                && !staff.EndedTaskInAlive)
            {
                CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
            }
        }
    }

    public override bool? CheckGuess(PlayerControl killer)
        => IsTransformed ? addRole?.CheckGuess(killer) : true;

    public override CustomRoles TellResults(PlayerControl player)
    {
        if (!IsTransformed) return CustomRoles.NotAssigned;
        return addRole?.TellResults(player) ?? transformedRole;
    }

    public override RoleTypes? AfterMeetingRole
        => IsTransformed
            ? addRole?.AfterMeetingRole ?? transformedRole.GetRoleInfo()?.BaseRoleType?.Invoke()
            : RoleTypes.Crewmate;

    public override CustomRoles HaveAddRole() => CustomRoles.NotAssigned;

    public static bool CanWinAsCrewmateStaff(PlayerControl player)
    {
        if (!player.Is(CustomRoles.Staff)) return true;
        return player.GetRoleClass() switch
        {
            Staff staff => staff.EndedTaskInAlive,
            MagicalGirl { transformedRole: CustomRoles.Staff, addRole: Staff transformedStaff } => transformedStaff.EndedTaskInAlive,
            _ => false
        };
    }

    public static bool TryGetEffectiveRole<T>(PlayerControl player, out T role) where T : class
    {
        role = null;
        if (player?.GetRoleClass() is T directRole)
        {
            role = directRole;
            return true;
        }

        if (player?.GetRoleClass() is MagicalGirl magicalGirl && magicalGirl.IsTransformed && magicalGirl.addRole is T transformed)
        {
            role = transformed;
            return true;
        }

        return false;
    }

    public static bool IsMatchedGuess(PlayerControl target, CustomRoles guessedRole)
    {
        return target?.GetRoleClass() is MagicalGirl magicalGirl
            && magicalGirl.IsTransformed
            && (guessedRole == CustomRoles.MagicalGirl || guessedRole == magicalGirl.transformedRole);
    }

    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (!IsTransformed) return;
        var displayRole = GetDisplayRoleForCandidate(transformedRole);
        roleColor = UtilsRoleText.GetRoleColor(displayRole);
        roleText = $"{GetString($"{displayRole}")}<size=45%>{Utils.ColorString(RoleInfo.RoleColor, $"({GetString($"{CustomRoles.MagicalGirl}")})")}</size>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seen != seer) return IsTransformed ? addRole?.GetLowerText(seer, seen, isForMeeting, isForHud) ?? "" : "";

        if (IsTransformed)
        {
            var addText = addRole?.GetLowerText(seer, seen, isForMeeting, isForHud) ?? "";
            if (!isForMeeting) return addText;
            var roleText = UtilsRoleText.GetRoleColorAndtext(GetDisplayRoleForCandidate(transformedRole));
            var ownText = string.Format(GetString("MagicalGirlTransformedState"), roleText, remainingTurns);
            var wrapped = isForHud ? ownText : $"<size=60%>{ownText}</size>";
            return addText == "" ? wrapped : addText + "\n" + wrapped;
        }

        if (isForMeeting && Player.IsAlive() && MyTaskState.HasCompletedEnoughCountOfTasks(requiredTasks))
        {
            var modeOn = CheckVote.TryGetValue(Player.PlayerId, out var mode) && mode;
            var key = modeOn ? "MagicalGirlVoteModeSelect" : "MagicalGirlVoteModeOff";
            var text = $"<color={RoleInfo.RoleColorCode}>{GetString(key)}</color>";
            return isForHud ? text : $"<size=60%>{text}</size>";
        }
        return "";
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (!isForMeeting || !Is(seer) || IsTransformed) return "";
        seen ??= seer;
        if (seen == seer) return "";
        var candidate = meetingCandidates.FirstOrDefault(x => x.PlayerId == seen.PlayerId);
        if (candidate.PlayerId == seen.PlayerId)
        {
            return $" <color={RoleInfo.RoleColorCode}>[{candidate.SlotNumber}]</color>";
        }
        return "";
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var backup = MessageReader.Get(reader);
        var handled = false;
        try
        {
            if (reader.ReadPackedInt32() == StateRpcTag)
            {
                transformedRole = (CustomRoles)reader.ReadPackedInt32();
                remainingTurns = reader.ReadPackedInt32();
                transformedThisMeeting = reader.ReadBoolean();

                usedRoles.Clear();
                var usedCount = reader.ReadPackedInt32();
                for (var i = 0; i < usedCount; i++)
                {
                    usedRoles.Add((CustomRoles)reader.ReadPackedInt32());
                }

                meetingCandidates.Clear();
                var candidateCount = reader.ReadPackedInt32();
                for (var i = 0; i < candidateCount; i++)
                {
                    var targetId = reader.ReadByte();
                    var slot = reader.ReadByte();
                    var role = (CustomRoles)reader.ReadPackedInt32();
                    meetingCandidates.Add(new CandidateEntry(targetId, slot, role));
                }
                EnsureTransformedRoleInstance();
                RefreshMainRoleState();
                handled = true;
            }
        }
        catch
        {
            //
        }

        if (!handled)
        {
            try
            {
                addRole?.ReceiveRPC(backup);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex}", "MagicalGirl");
            }
        }

        backup.Recycle();
    }

    private void SendStateRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked(StateRpcTag);
        sender.Writer.WritePacked((int)transformedRole);
        sender.Writer.WritePacked(remainingTurns);
        sender.Writer.Write(transformedThisMeeting);

        sender.Writer.WritePacked(usedRoles.Count);
        foreach (var role in usedRoles)
        {
            sender.Writer.WritePacked((int)role);
        }

        sender.Writer.WritePacked(meetingCandidates.Count);
        foreach (var candidate in meetingCandidates)
        {
            sender.Writer.Write(candidate.PlayerId);
            sender.Writer.Write(candidate.SlotNumber);
            sender.Writer.WritePacked((int)candidate.Role);
        }
    }

    private bool PrepareMeetingCandidates()
    {
        meetingCandidates.Clear();

        var targets = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != Player.PlayerId)
            .OrderBy(pc => pc.PlayerId)
            .ToList();

        if (targets.Count <= 0) return false;

        var reserved = new HashSet<CustomRoles>(usedRoles);
        byte index = 0;
        for (var slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            if (index >= targets.Count) break;
            if (!TryResolveSlotRole(slotIndex, reserved, out var role)) continue;
            meetingCandidates.Add(new CandidateEntry(targets[index].PlayerId, (byte)(slotIndex + 1), role));
            index++;
        }
        return meetingCandidates.Count > 0;
    }

    private bool TryResolveSlotRole(int slotIndex, HashSet<CustomRoles> reserved, out CustomRoles role)
    {
        role = CustomRoles.NotAssigned;
        var mode = GetSlotMode(slotIndex);
        switch (mode)
        {
            case SlotMode.None:
                return false;
            case SlotMode.Random:
                {
                    var pool = GetTransformableCrewRoles().Where(r => !reserved.Contains(r)).ToList();
                    if (pool.Count <= 0) return false;
                    role = pool[IRandom.Instance.Next(pool.Count)];
                    reserved.Add(role);
                    return true;
                }
            case SlotMode.AssignFilter:
                {
                    var option = GetSlotAssignOption(slotIndex);
                    var pool = option.GetNowRoleValue()
                        .Where(IsTransformableCrewRole)
                        .Where(r => !reserved.Contains(r))
                        .Distinct()
                        .ToList();
                    if (pool.Count <= 0) return false;
                    role = pool[IRandom.Instance.Next(pool.Count)];
                    reserved.Add(role);
                    return true;
                }
            default:
                return false;
        }
    }

    private bool TryGetCandidateRole(byte votedForId, out CustomRoles role)
    {
        foreach (var candidate in meetingCandidates)
        {
            if (candidate.PlayerId == votedForId)
            {
                role = candidate.Role;
                return true;
            }
        }
        role = CustomRoles.NotAssigned;
        return false;
    }

    private void ShowCandidateList()
    {
        StringBuilder sb = new();
        sb.Append("<size=80%>");
        foreach (var candidate in meetingCandidates)
        {
            var info = PlayerCatch.GetPlayerInfoById(candidate.PlayerId);
            if (info == null) continue;
            var displayRole = GetDisplayRoleForCandidate(candidate.Role);
            var roleText = UtilsRoleText.GetRoleColorAndtext(displayRole);
            sb.Append($"{Palette.GetColorName(info.DefaultOutfit.ColorId)} - {roleText}\n");
        }
        Utils.SendMessage(sb.ToString(), Player.PlayerId, GetString("MagicalGirlCandidateTitle"));
    }

    private void TransformInto(CustomRoles role)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (role is CustomRoles.NotAssigned) return;
        if (!IsTransformableCrewRole(role))
        {
            Utils.SendMessage(GetString("MagicalGirlCannotTransformRole"), Player.PlayerId);
            return;
        }
        if (usedRoles.Contains(role))
        {
            Utils.SendMessage(GetString("MagicalGirlRoleAlreadyUsed"), Player.PlayerId);
            return;
        }

        ClearTransform(logReturn: false);
        if (!CustomRoleManager.AllRolesInfo.TryGetValue(role, out var roleInfo))
        {
            Utils.SendMessage(GetString("MagicalGirlCannotTransformRole"), Player.PlayerId);
            return;
        }

        hadGuesserBeforeTransform = Player.Is(CustomRoles.Guesser);
        addRole = roleInfo.CreateInstance(Player);
        addRole?.Add();
        if (addRole is ISelfVoter transformedSelfVoter) transformedSelfVoter.AddSelfVoter(Player);
        if (addRole is IRoomTasker roomTasker) roomTasker.AddRoomTaker(Player.PlayerId);

        transformedRole = role;
        remainingTurns = transformDuration;
        transformedThisMeeting = true;
        usedRoles.Add(role);
        meetingCandidates.Clear();
        RefreshMainRoleState();
        RecomputeTaskCountsForTransform();
        SyncCurrentRoleType();

        Player.SyncSettings();
        Logger.Info($"{Player?.Data?.GetLogPlayerName() ?? "???"}は{GetString($"{role}")}に変身した", "MagicalGirl");
        if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
        Utils.SendMessage(
            string.Format(GetString("MagicalGirlTransformSuccess"), UtilsRoleText.GetRoleColorAndtext(GetDisplayRoleForCandidate(role)), remainingTurns),
            Player.PlayerId,
            GetString("MagicalGirlTransformSuccessTitle"));
        UtilsNotifyRoles.NotifyRoles();
        SendStateRPC();
    }

    private void ClearTransform(bool logReturn = true)
    {
        var wasTransformed = transformedRole is not CustomRoles.NotAssigned || addRole is not null;
        if (addRole is not null)
        {
            addRole.OnDestroy();
        }
        if (wasTransformed) CleanupTransformSubRoles();
        addRole = null;
        transformedRole = CustomRoles.NotAssigned;
        remainingTurns = 0;
        transformedThisMeeting = false;
        RefreshMainRoleState();
        RecomputeTaskCountsForTransform();
        SyncCurrentRoleType();
        if (RoomTaskAssign.AllRoomTasker.ContainsKey(Player.PlayerId))
        {
            RoomTaskAssign.AllRoomTasker.Remove(Player.PlayerId);
        }
        if (AmongUsClient.Instance.AmHost)
            Player.SyncSettings();
        if (logReturn && wasTransformed)
            Logger.Info($"{Player?.Data?.GetLogPlayerName() ?? "???"}は{GetString($"{CustomRoles.MagicalGirl}")}に戻った", "MagicalGirl");
    }

    private void CleanupTransformSubRoles()
    {
        if (!hadGuesserBeforeTransform && MyState.SubRoles.Contains(CustomRoles.Guesser))
        {
            if (AmongUsClient.Instance.AmHost)
                Player.RpcReplaceSubRole(CustomRoles.Guesser, remove: true);
            else
                MyState.RemoveSubRole(CustomRoles.Guesser);
        }
        hadGuesserBeforeTransform = Player.Is(CustomRoles.Guesser);
    }

    private void RefreshMainRoleState()
    {
        var targetRole = transformedRole is CustomRoles.NotAssigned ? CustomRoles.MagicalGirl : transformedRole;
        if (MyState.MainRole == targetRole) return;
        MyState.SetMainRole(targetRole);
    }

    private void SyncCurrentRoleType()
    {
        if (!AmongUsClient.Instance.AmHost || Player?.GetClient() == null) return;
        AntiBlackout.ResetSetRole(Player);
    }

    private void RecomputeTaskCountsForTransform()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Player?.Data == null) return;
        MyTaskState.hasTasks = UtilsTask.HasTasks(Player.Data, false);
        GameData.Instance?.RecomputeTaskCounts();
    }

    private void EnsureTransformedRoleInstance()
    {
        if (transformedRole is CustomRoles.NotAssigned)
        {
            if (addRole is not null)
            {
                addRole.OnDestroy();
                addRole = null;
            }
            if (RoomTaskAssign.AllRoomTasker.ContainsKey(Player.PlayerId))
            {
                RoomTaskAssign.AllRoomTasker.Remove(Player.PlayerId);
            }
            return;
        }

        if (addRole is not null)
        {
            var classType = transformedRole.GetRoleInfo()?.ClassType;
            if (classType != null && classType == addRole.GetType()) return;
        }

        if (addRole is not null)
        {
            addRole.OnDestroy();
            addRole = null;
        }

        if (!CustomRoleManager.AllRolesInfo.TryGetValue(transformedRole, out var info)) return;
        addRole = info.CreateInstance(Player);
        addRole?.Add();
        if (addRole is ISelfVoter transformedSelfVoter) transformedSelfVoter.AddSelfVoter(Player);
        if (addRole is IRoomTasker roomTasker) roomTasker.AddRoomTaker(Player.PlayerId);
    }

    private SlotMode GetSlotMode(int slotIndex)
    {
        var value = slotIndex switch
        {
            0 => OptionRole1Mode.GetValue(),
            1 => OptionRole2Mode.GetValue(),
            2 => OptionRole3Mode.GetValue(),
            _ => 0
        };
        return (SlotMode)value;
    }

    private AssignOptionItem GetSlotAssignOption(int slotIndex)
    {
        return slotIndex switch
        {
            0 => OptionRole1Assign,
            1 => OptionRole2Assign,
            _ => OptionRole3Assign
        };
    }

    private static bool IsTransformableCrewRole(CustomRoles role)
    {
        if (role is CustomRoles.NotAssigned or CustomRoles.Crewmate) return false;
        if (ForbiddenTransformRoles.Contains(role)) return false;
        if (!Event.CheckRole(role)) return false;
        if (!CustomRoleManager.AllRolesInfo.TryGetValue(role, out var roleInfo)) return false;
        return roleInfo.CustomRoleType == CustomRoleTypes.Crewmate;
    }

    private static List<CustomRoles> GetTransformableCrewRoles()
    {
        return CustomRoleManager.AllRolesInfo
            .Keys
            .Where(IsTransformableCrewRole)
            .Distinct()
            .ToList();
    }

    private CustomRoles GetDisplayRoleForCandidate(CustomRoles role)
    {
        if (role is not CustomRoles.PonkotuTeller) return role;
        try
        {
            var fake = new PonkotuTeller(Player);
            var miss = fake.Misidentify();
            fake.OnDestroy();
            if (miss is CustomRoles.FortuneTeller)
            {
                return CustomRoles.FortuneTeller;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Ponkotu display fallback: {ex}", "MagicalGirl");
        }
        return role;
    }

    private static bool TryParseMediumCommand(string msg, out byte targetId, out bool invalidFormat)
    {
        targetId = byte.MaxValue;
        invalidFormat = false;
        if (string.IsNullOrWhiteSpace(msg)) return false;

        var args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2) return false;
        if (args[0] != "/cmd") return false;

        var command = args[1].StartsWith("/") ? args[1] : $"/{args[1]}";
        if (command != "/sp") return false;

        if (args.Length < 3 || !byte.TryParse(args[2], out targetId))
        {
            invalidFormat = true;
            return true;
        }
        return true;
    }

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GuesserMsg))]
    [HarmonyPriority(Priority.First)]
    private static class MediumCommandPatch
    {
        private static readonly System.Reflection.MethodInfo MediumUseAbilityMethod =
            AccessTools.Method(typeof(Medium), "UseAbility", [typeof(byte)]);

        private static bool Prefix(PlayerControl pc, string msg, ref bool __result)
        {
            if (!TryParseMediumCommand(msg, out var targetId, out var invalidFormat))
                return true;

            if (pc?.GetRoleClass() is not MagicalGirl magicalGirl || magicalGirl.addRole is not Medium medium)
                return true;

            __result = true;
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame)
                return false;

            const string title = "<#66a6ff>Medium</color>";
            if (invalidFormat)
            {
                Utils.SendMessage("Usage: /cmd sp (ID)", pc.PlayerId, title);
                return false;
            }

            if (MediumUseAbilityMethod == null)
            {
                Utils.SendMessage("Medium command is unavailable.", pc.PlayerId, title);
                return false;
            }

            MediumUseAbilityMethod.Invoke(medium, new object[] { targetId });
            return false;
        }
    }

    public override bool CanUseAbilityButton() => IsTransformed && (addRole?.CanUseAbilityButton() ?? false);
    public override void ApplyGameOptions(IGameOptions opt) => addRole?.ApplyGameOptions(opt);
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => addRole?.OnEnterVent(physics, ventId) ?? true;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => addRole?.CanVentMoving(physics, ventId) ?? true;
    public override bool OnCheckMurderAsTarget(MurderInfo info) => addRole?.OnCheckMurderAsTarget(info) ?? true;
    public override void OnMurderPlayerAsTarget(MurderInfo info) => addRole?.OnMurderPlayerAsTarget(info);
    public override void OnShapeshift(PlayerControl target) => addRole?.OnShapeshift(target);
    public override bool CheckShapeshift(PlayerControl target, ref bool shouldAnimate) => addRole?.CheckShapeshift(target, ref shouldAnimate) ?? true;
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => addRole?.OnReportDeadBody(reporter, target);
    public override bool CanClickUseVentButton => addRole?.CanClickUseVentButton ?? true;
    public override string MeetingAddMessage() => addRole?.MeetingAddMessage() ?? "";
    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
        => addRole?.ModifyVote(voterId, sourceVotedForId, isIntentional) ?? (null, null, true);
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner) => addRole?.OnExileWrapUp(exiled, ref DecidedWinner);
    public override void StartGameTasks() => addRole?.StartGameTasks();
    public override void OnSpawn(bool initialState = false) => addRole?.OnSpawn(initialState);
    public override void OnFixedUpdate(PlayerControl player) => addRole?.OnFixedUpdate(player);
    public override bool OnInvokeSabotage(SystemTypes systemType) => addRole?.OnInvokeSabotage(systemType) ?? true;
    public override bool OnSabotage(PlayerControl player, SystemTypes systemType) => addRole?.OnSabotage(player, systemType) ?? true;
    public override void AfterSabotage(SystemTypes systemType) => addRole?.AfterSabotage(systemType);
    public override bool NotifyRolesCheckOtherName => addRole?.NotifyRolesCheckOtherName ?? false;
    public override void OverrideProgressTextAsSeer(PlayerControl seen, ref bool enabled, ref string text)
        => addRole?.OverrideProgressTextAsSeer(seen, ref enabled, ref text);
    public override void OverrideProgressTextAsSeen(PlayerControl seer, ref bool enabled, ref string text)
        => addRole?.OverrideProgressTextAsSeen(seer, ref enabled, ref text);
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
        => addRole?.OverrideDisplayRoleNameAsSeer(seen, ref enabled, ref roleColor, ref roleText, ref addon);
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
        => addRole?.OverrideDisplayRoleNameAsSeen(seer, ref enabled, ref roleColor, ref roleText, ref addon);
    public override string GetProgressText(bool comms = false, bool GameLog = false) => addRole?.GetProgressText(comms, GameLog) ?? "";
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => addRole?.GetSuffix(seer, seen, isForMeeting) ?? "";
    public override string GetAbilityButtonText() => addRole?.GetAbilityButtonText() ?? base.GetAbilityButtonText();
    public override bool OverrideAbilityButton(out string text)
    {
        if (addRole?.OverrideAbilityButton(out var addText) ?? false)
        {
            text = addText;
            return true;
        }
        text = default;
        return false;
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
        => addRole?.CancelReportDeadBody(reporter, target, ref reason) ?? false;
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
        => addRole?.VotingResults(ref Exiled, ref IsTie, vote, mostVotedPlayers, ClearAndExile) ?? false;

    bool IKiller.CanKill => IsTransformed && addRole is IKiller killer && killer.CanKill;
    bool IKiller.IsKiller => IsTransformed && addRole is IKiller killer && killer.IsKiller;
    public bool CanUseKillButton() => IsTransformed && addRole is IKiller killer && killer.CanUseKillButton();
    public float CalculateKillCooldown() => IsTransformed && addRole is IKiller killer ? killer.CalculateKillCooldown() : Options.DefaultKillCooldown;
    public bool CanUseSabotageButton() => IsTransformed && addRole is IKiller killer && killer.CanUseSabotageButton();
    public bool CanUseImpostorVentButton() => IsTransformed && addRole is IKiller killer && killer.CanUseImpostorVentButton();
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (IsTransformed && addRole is IKiller killer)
            killer.OnCheckMurderAsKiller(info);
    }
    public void OnCheckMurderDontKill(MurderInfo info)
    {
        if (IsTransformed && addRole is IKiller killer)
            killer.OnCheckMurderDontKill(info);
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (IsTransformed && addRole is IKiller killer)
            killer.OnMurderPlayerAsKiller(info);
    }
    public bool OverrideKillButtonText(out string text)
    {
        if (IsTransformed && addRole is IKiller killer && killer.OverrideKillButtonText(out var value))
        {
            text = value;
            return true;
        }
        text = default;
        return false;
    }
    public bool OverrideKillButton(out string text)
    {
        if (IsTransformed && addRole is IKiller killer && killer.OverrideKillButton(out var value))
        {
            text = value;
            return true;
        }
        text = default;
        return false;
    }
    public bool OverrideImpVentButton(out string text)
    {
        if (IsTransformed && addRole is IKiller killer && killer.OverrideImpVentButton(out var value))
        {
            text = value;
            return true;
        }
        text = default;
        return false;
    }

    void IUsePhantomButton.Init(PlayerControl player)
    {
        if (IsTransformed && addRole is IUsePhantomButton phantomButton)
            phantomButton.Init(player);
    }

    void IUsePhantomButton.FixedUpdate(PlayerControl player)
    {
        if (IsTransformed && addRole is IUsePhantomButton phantomButton)
            phantomButton.FixedUpdate(player);
    }

    void IUsePhantomButton.CheckOnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (IsTransformed && addRole is IUsePhantomButton phantomButton)
        {
            phantomButton.CheckOnClick(ref AdjustKillCooldown, ref ResetCooldown);
            return;
        }

        AdjustKillCooldown = true;
    }

    bool IUsePhantomButton.UseOneclickButton => IsTransformed && addRole is IUsePhantomButton phantomButton && phantomButton.UseOneclickButton;
    bool IUsePhantomButton.IsPhantomRole => IsTransformed && addRole is IUsePhantomButton phantomButton && phantomButton.IsPhantomRole;
    bool IUsePhantomButton.IsresetAfterKill => IsTransformed && addRole is IUsePhantomButton phantomButton && phantomButton.IsresetAfterKill;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (IsTransformed && addRole is IUsePhantomButton phantomButton)
            phantomButton.OnClick(ref AdjustKillCooldown, ref ResetCooldown);
    }

    bool ISystemTypeUpdateHook.UpdateReactorSystem(ReactorSystemType reactorSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateReactorSystem(reactorSystem, amount) : true;
    bool ISystemTypeUpdateHook.UpdateHeliSabotageSystem(HeliSabotageSystem heliSabotageSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateHeliSabotageSystem(heliSabotageSystem, amount) : true;
    bool ISystemTypeUpdateHook.UpdateLifeSuppSystem(LifeSuppSystemType lifeSuppSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateLifeSuppSystem(lifeSuppSystem, amount) : true;
    bool ISystemTypeUpdateHook.UpdateHudOverrideSystem(HudOverrideSystemType hudOverrideSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateHudOverrideSystem(hudOverrideSystem, amount) : true;
    bool ISystemTypeUpdateHook.UpdateHqHudSystem(HqHudSystemType hqHudSystemType, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateHqHudSystem(hqHudSystemType, amount) : true;
    bool ISystemTypeUpdateHook.UpdateSwitchSystem(SwitchSystem switchSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateSwitchSystem(switchSystem, amount) : true;
    bool ISystemTypeUpdateHook.UpdateDoorsSystem(DoorsSystemType doorsSystem, byte amount)
        => IsTransformed && addRole is ISystemTypeUpdateHook hook ? hook.UpdateDoorsSystem(doorsSystem, amount) : true;

    bool? IKillFlashSeeable.CheckKillFlash(MurderInfo info)
        => IsTransformed && addRole is IKillFlashSeeable killFlashSeeable ? killFlashSeeable.CheckKillFlash(info) : false;

    bool? IDeathReasonSeeable.CheckSeeDeathReason(PlayerControl seen)
        => IsTransformed && addRole is IDeathReasonSeeable deathReasonSeeable ? deathReasonSeeable.CheckSeeDeathReason(seen) : false;

    bool IMeetingTimeAlterable.RevertOnDie => IsTransformed && addRole is IMeetingTimeAlterable meetingTimeAlterable && meetingTimeAlterable.RevertOnDie;
    int IMeetingTimeAlterable.CalculateMeetingTimeDelta()
        => IsTransformed && addRole is IMeetingTimeAlterable meetingTimeAlterable ? meetingTimeAlterable.CalculateMeetingTimeDelta() : 0;
}
