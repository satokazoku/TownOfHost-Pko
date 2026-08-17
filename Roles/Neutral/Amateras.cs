using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Modules.SelfVoteManager;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.UtilsRoleText;

namespace TownOfHost.Roles.Neutral;

public sealed class Amateras : RoleBase, ISelfVoter, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Amateras),
            player => new Amateras(player),
            CustomRoles.Amateras,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            56000,
            SetupOptionItem,
            "Am",
            "#ffd34d",
            (5, 6),
            true,
            countType: CountTypes.Crew,
            introSound: () => GetIntroSound(RoleTypes.Scientist),
            from: From.TownOfHost_Pko
        );

    static OptionItem OptionAddWin;
    static OptionItem OptionRequiredWishCount;
    static OptionItem OptionShowAliveNotice;
    static OptionItem OptionMustBeAliveToWin;
    static OptionItem OptionDecreaseNeedOnRequesterDeath;
    static OptionItem OptionAllowSamePlayer;

    static readonly HashSet<Amateras> Instances = new();

    byte wishTargetId;
    WishType currentWish;
    int fulfilledWishCount;
    int requiredWishCount;
    readonly HashSet<byte> fulfilledTargetIds;
    readonly Dictionary<byte, GrantedWishEffect> grantedEffects;
    readonly Dictionary<byte, Vector3> deathArrowPositions;

    enum OptionName
    {
        AmaterasAddWin,
        AmaterasRequiredWishCount,
        AmaterasShowAliveNotice,
        AmaterasMustBeAliveToWin,
        AmaterasDecreaseNeedOnRequesterDeath,
        AmaterasAllowSamePlayer
    }

    enum RPCType
    {
        SyncState
    }

    enum WishType
    {
        None = 0,
        GiveBuff = 1,
        RemoveDebuff = 2,
        RevealImpostorCount = 3,
        RevealMadmateCount = 4,
        RevealCrewCount = 5,
        RevealNeutralCount = 6,
        OneTimeGuard = 7,
        RevealRandomRole = 8,
        RevealRandomFaction = 9,
        DeathArrow = 10
    }

    [Flags]
    enum GrantedWishEffect
    {
        None = 0,
        RevealImpostorCount = 1 << 0,
        RevealMadmateCount = 1 << 1,
        RevealCrewCount = 1 << 2,
        RevealNeutralCount = 1 << 3,
        DeathArrow = 1 << 4
    }

    public Amateras(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        wishTargetId = byte.MaxValue;
        currentWish = WishType.None;
        fulfilledWishCount = 0;
        requiredWishCount = OptionRequiredWishCount?.GetInt() ?? 1;
        fulfilledTargetIds = new();
        grantedEffects = new();
        deathArrowPositions = new();
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, CustomRoles.Amateras, () => !OptionAddWin.GetBool(), defo: 15);
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 10, OptionName.AmaterasAddWin, false, false);
        OptionRequiredWishCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AmaterasRequiredWishCount, new(1, 20, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionShowAliveNotice = BooleanOptionItem.Create(RoleInfo, 12, OptionName.AmaterasShowAliveNotice, true, false);
        OptionMustBeAliveToWin = BooleanOptionItem.Create(RoleInfo, 13, OptionName.AmaterasMustBeAliveToWin, false, false);
        OptionDecreaseNeedOnRequesterDeath = BooleanOptionItem.Create(RoleInfo, 14, OptionName.AmaterasDecreaseNeedOnRequesterDeath, false, false);
        OptionAllowSamePlayer = BooleanOptionItem.Create(RoleInfo, 15, OptionName.AmaterasAllowSamePlayer, false, false);
        OverrideTasksData.Create(RoleInfo, 20, tasks: (true, 1, 1, 1));
    }

    [Attributes.GameModuleInitializer]
    public static void Init()
    {
        Instances.Clear();
    }

    public override void Add()
    {
        Instances.Add(this);
        SendRPC();
    }

    public override void OnDestroy()
    {
        ClearDeathArrows();
        Instances.Remove(this);
    }

    bool ISelfVoter.CanUseVoted() => Player.IsAlive() && Canuseability() && wishTargetId == byte.MaxValue;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !Player.IsAlive()) return true;
        if (!CheckSelfVoteMode(Player, votedForId, out var status)) return true;

        switch (status)
        {
            case VoteStatus.Self:
                Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("AmaterasVoteMode"), GetString("Vote.Divied")) + GetString("VoteSkillMode"), Player.PlayerId);
                SetMode(Player, true);
                return false;
            case VoteStatus.Skip:
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                SetMode(Player, false);
                return false;
            case VoteStatus.Vote:
                var target = GetPlayerById(votedForId);
                SelectWishTarget(target);
                SetMode(Player, false);
                return false;
            default:
                return true;
        }
    }

    public override void OnStartMeeting()
    {
        ClearDeathArrows();

        if (!AmongUsClient.Instance.AmHost) return;

        TryGrantPendingWish();
        SendGrantedMeetingInfo();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ClearDeathArrows();
    }

    public override void OnDead(PlayerControl player)
    {
        if (player == null) return;

        if (HasGrantedEffect(player.PlayerId, GrantedWishEffect.DeathArrow))
            AddDeathArrow(player);

        if (AmongUsClient.Instance.AmHost && player.PlayerId == wishTargetId)
            CancelCurrentWish(resetTasks: currentWish != WishType.None, decreaseRequiredCount: true);
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost && player?.PlayerId == wishTargetId)
            CancelCurrentWish(resetTasks: currentWish != WishType.None, decreaseRequiredCount: true);
    }

    public override string MeetingAddMessage()
    {
        if (!OptionShowAliveNotice.GetBool() || !Player.IsAlive()) return "";
        return MeetingStates.FirstMeeting ? GetString("AmaterasMeetingFirst") : GetString("AmaterasMeetingAlive");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var need = Math.Max(0, requiredWishCount);
        var color = CanWinNow() ? RoleInfo.RoleColor : Color.gray;
        return Utils.ColorString(color, $"({fulfilledWishCount}/{need})");
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost || OptionAddWin.GetBool() || !CanWinNow()) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Amateras, Player.PlayerId, false, CustomRoles.Amateras))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (!OptionAddWin.GetBool() || !CanWinNow()) return false;
        winnerRole = CustomRoles.Amateras;
        return true;
    }

    void SelectWishTarget(PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!CanSelectWishTarget(target))
        {
            Utils.SendMessage(GetString("AmaterasTargetInvalid"), Player.PlayerId);
            return;
        }

        if (!OptionAllowSamePlayer.GetBool() && fulfilledTargetIds.Contains(target.PlayerId))
        {
            Utils.SendMessage(GetString("AmaterasTargetAlreadyFulfilled"), Player.PlayerId);
            return;
        }

        wishTargetId = target.PlayerId;
        currentWish = WishType.None;
        SendRPC();

        Utils.SendMessage(GetString("AmaterasWishMenu"), target.PlayerId);
        Utils.SendMessage(string.Format(GetString("AmaterasTargetSelected"), UtilsName.GetPlayerColor(target, true)), Player.PlayerId);
    }

    static bool CanSelectWishTarget(PlayerControl target)
        => target != null && target.IsAlive() && !target.Is(CustomRoles.GM);

    public static void HandleWishCommand(PlayerControl sender, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var role = Instances.FirstOrDefault(x => x.wishTargetId == sender.PlayerId);
        if (role == null) return;

        if (role.currentWish != WishType.None)
        {
            Utils.SendMessage(GetString("AmaterasWishAlreadyChosen"), sender.PlayerId);
            return;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out var wishNo) || wishNo < 1 || wishNo > 10)
        {
            Utils.SendMessage(GetString("AmaterasWishCommandHelp"), sender.PlayerId);
            return;
        }

        role.ChooseWish(sender, (WishType)wishNo);
    }

    void ChooseWish(PlayerControl sender, WishType wish)
    {
        currentWish = wish;
        ResetWishTasks();
        SendRPC();

        Utils.SendMessage(GetString("AmaterasWishChosen"), sender.PlayerId);
        Utils.SendMessage(string.Format(GetString("AmaterasWishChosenForAmateras"), UtilsName.GetPlayerColor(sender, true)), Player.PlayerId);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void ResetWishTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Player.Data.RpcSetTasks(Array.Empty<byte>());
        MyTaskState.CompletedTasksCount = 0;
        Player.SyncSettings();
    }

    void TryGrantPendingWish()
    {
        if (wishTargetId == byte.MaxValue || currentWish == WishType.None) return;

        var target = GetPlayerById(wishTargetId);
        if (!CanSelectWishTarget(target))
        {
            CancelCurrentWish(resetTasks: true, decreaseRequiredCount: true);
            return;
        }

        if (!IsTaskFinished) return;

        GrantCurrentWish(target);
    }

    void GrantCurrentWish(PlayerControl target)
    {
        switch (currentWish)
        {
            case WishType.GiveBuff:
                GrantRandomBuff(target);
                break;
            case WishType.RemoveDebuff:
                RemoveRandomDebuff(target);
                break;
            case WishType.RevealImpostorCount:
                AddGrantedEffect(target.PlayerId, GrantedWishEffect.RevealImpostorCount);
                break;
            case WishType.RevealMadmateCount:
                AddGrantedEffect(target.PlayerId, GrantedWishEffect.RevealMadmateCount);
                break;
            case WishType.RevealCrewCount:
                AddGrantedEffect(target.PlayerId, GrantedWishEffect.RevealCrewCount);
                break;
            case WishType.RevealNeutralCount:
                AddGrantedEffect(target.PlayerId, GrantedWishEffect.RevealNeutralCount);
                break;
            case WishType.OneTimeGuard:
                target.GetPlayerState().HaveGuard[1] += 1;
                Utils.SendMessage(GetString("AmaterasOneTimeGuardGranted"), target.PlayerId);
                break;
            case WishType.RevealRandomRole:
                SendRandomRoleInfo(target);
                break;
            case WishType.RevealRandomFaction:
                SendRandomFactionInfo(target);
                break;
            case WishType.DeathArrow:
                AddGrantedEffect(target.PlayerId, GrantedWishEffect.DeathArrow);
                Utils.SendMessage(GetString("AmaterasDeathArrowGranted"), target.PlayerId);
                break;
        }

        Utils.SendMessage(GetString("AmaterasWishGranted"), target.PlayerId);
        fulfilledWishCount++;
        fulfilledTargetIds.Add(target.PlayerId);
        wishTargetId = byte.MaxValue;
        currentWish = WishType.None;
        SendRPC();

        CheckWinner(GameOverReason.ImpostorsByKill);
    }

    void CancelCurrentWish(bool resetTasks, bool decreaseRequiredCount)
    {
        var oldTargetId = wishTargetId;
        wishTargetId = byte.MaxValue;
        currentWish = WishType.None;

        if (decreaseRequiredCount && OptionDecreaseNeedOnRequesterDeath.GetBool())
            requiredWishCount = Math.Max(0, requiredWishCount - 1);

        if (resetTasks)
            ResetWishTasks();

        if (oldTargetId != byte.MaxValue)
            Utils.SendMessage(GetString("AmaterasWishCancelled"), Player.PlayerId);

        SendRPC();
        CheckWinner(GameOverReason.ImpostorsByKill);
    }

    void GrantRandomBuff(PlayerControl target)
    {
        var addon = GetAssignableAddons(target)
            .Where(role => role.IsBuffAddon() && !target.GetCustomSubRoles().Contains(role) && role is not CustomRoles.Speeding and not CustomRoles.Opener)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault(CustomRoles.NotAssigned);

        if (addon == CustomRoles.NotAssigned)
        {
            Utils.SendMessage(GetString("AmaterasNoBuffAvailable"), target.PlayerId);
            return;
        }

        if (addon == CustomRoles.Guarding)
            target.GetPlayerState().HaveGuard[1] += Guarding.HaveGuard;

        target.RpcReplaceSubRole(addon);
        Utils.SendMessage(string.Format(GetString("AmaterasBuffGranted"), GetRoleName(addon)), target.PlayerId);
    }

    static IEnumerable<CustomRoles> GetAssignableAddons(PlayerControl target)
    {
        var roleType = target.GetCustomRole().GetCustomRoleTypes();

        foreach (var data in AddOnsAssignData.AllData.Values)
        {
            var option = roleType switch
            {
                CustomRoleTypes.Crewmate => data.CrewmateMaximum,
                CustomRoleTypes.Impostor => data.ImpostorMaximum,
                CustomRoleTypes.Madmate => data.MadmateMaximum,
                CustomRoleTypes.Neutral => data.NeutralMaximum,
                _ => null
            };

            if (option != null && option.GetInt() > 0)
                yield return data.Role;
        }
    }

    void RemoveRandomDebuff(PlayerControl target)
    {
        var addon = target.GetCustomSubRoles()
            .Where(role => role.IsDebuffAddon())
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault(CustomRoles.NotAssigned);

        if (addon == CustomRoles.NotAssigned)
        {
            Utils.SendMessage(GetString("AmaterasNoDebuffFound"), target.PlayerId);
            return;
        }

        target.RpcReplaceSubRole(addon, remove: true);
        Utils.SendMessage(string.Format(GetString("AmaterasDebuffRemoved"), GetRoleName(addon)), target.PlayerId);
    }

    void SendRandomRoleInfo(PlayerControl target)
    {
        var reveal = PlayerCatch.AllPlayerControls
            .Where(pc => pc.PlayerId != target.PlayerId && !pc.Is(CustomRoles.GM))
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault();

        reveal ??= Player;
        Utils.SendMessage(
            string.Format(GetString("AmaterasRandomRoleResult"), UtilsName.GetPlayerColor(reveal, true), GetRoleName(reveal.GetCustomRole())),
            target.PlayerId
        );
    }

    void SendRandomFactionInfo(PlayerControl target)
    {
        var reveal = PlayerCatch.AllPlayerControls
            .Where(pc => pc.PlayerId != target.PlayerId && !pc.Is(CustomRoles.GM))
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefault();

        reveal ??= Player;
        Utils.SendMessage(
            string.Format(GetString("AmaterasRandomFactionResult"), UtilsName.GetPlayerColor(reveal, true), GetTeamName(reveal.GetCustomRole().GetCustomRoleTypes())),
            target.PlayerId
        );
    }

    static string GetTeamName(CustomRoleTypes roleType) => roleType switch
    {
        CustomRoleTypes.Impostor => GetString("Impostor"),
        CustomRoleTypes.Madmate => GetString("Madmate"),
        CustomRoleTypes.Neutral => GetString("Neutral"),
        _ => GetString("TeamCrewmate")
    };

    void SendGrantedMeetingInfo()
    {
        foreach (var (playerId, effects) in grantedEffects.ToArray())
        {
            var lines = new List<string>();
            if (effects.HasFlag(GrantedWishEffect.RevealImpostorCount))
                lines.Add(string.Format(GetString("AmaterasRemainingTeamCount"), GetString("Impostor"), CountAliveByType(CustomRoleTypes.Impostor)));
            if (effects.HasFlag(GrantedWishEffect.RevealMadmateCount))
                lines.Add(string.Format(GetString("AmaterasRemainingTeamCount"), GetString("Madmate"), CountAliveByType(CustomRoleTypes.Madmate)));
            if (effects.HasFlag(GrantedWishEffect.RevealCrewCount))
                lines.Add(string.Format(GetString("AmaterasRemainingTeamCount"), GetString("TeamCrewmate"), CountAliveByType(CustomRoleTypes.Crewmate)));
            if (effects.HasFlag(GrantedWishEffect.RevealNeutralCount))
                lines.Add(string.Format(GetString("AmaterasRemainingTeamCount"), GetString("Neutral"), CountAliveByType(CustomRoleTypes.Neutral)));

            if (lines.Count > 0)
                Utils.SendMessage(string.Join("\n", lines), playerId);
        }
    }

    static int CountAliveByType(CustomRoleTypes roleType)
        => PlayerCatch.AllAlivePlayerControls.Count(pc => pc.GetCustomRole().GetCustomRoleTypes() == roleType);

    void AddGrantedEffect(byte playerId, GrantedWishEffect effect)
    {
        grantedEffects.TryGetValue(playerId, out var current);
        grantedEffects[playerId] = current | effect;
    }

    bool HasGrantedEffect(byte playerId, GrantedWishEffect effect)
        => grantedEffects.TryGetValue(playerId, out var current) && current.HasFlag(effect);

    void AddDeathArrow(PlayerControl deadPlayer)
    {
        var position = deadPlayer.transform.position;
        deathArrowPositions[deadPlayer.PlayerId] = position;

        foreach (var seer in PlayerCatch.AllAlivePlayerControls)
            GetArrow.Add(seer.PlayerId, position);

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    void ClearDeathArrows()
    {
        if (deathArrowPositions.Count == 0) return;

        foreach (var position in deathArrowPositions.Values)
        {
            foreach (var seer in PlayerCatch.AllPlayerControls)
                GetArrow.Remove(seer.PlayerId, position);
        }

        deathArrowPositions.Clear();
    }

    bool CanWinNow()
        => fulfilledWishCount >= Math.Max(0, requiredWishCount)
            && (!OptionMustBeAliveToWin.GetBool() || Player.IsAlive());

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPCType.SyncState);
        sender.Writer.Write(wishTargetId);
        sender.Writer.WritePacked((int)currentWish);
        sender.Writer.WritePacked(fulfilledWishCount);
        sender.Writer.WritePacked(requiredWishCount);

        sender.Writer.WritePacked(fulfilledTargetIds.Count);
        foreach (var playerId in fulfilledTargetIds)
            sender.Writer.Write(playerId);

        sender.Writer.WritePacked(grantedEffects.Count);
        foreach (var (playerId, effects) in grantedEffects)
        {
            sender.Writer.Write(playerId);
            sender.Writer.WritePacked((int)effects);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if ((RPCType)reader.ReadPackedInt32() != RPCType.SyncState) return;

        wishTargetId = reader.ReadByte();
        currentWish = (WishType)reader.ReadPackedInt32();
        fulfilledWishCount = reader.ReadPackedInt32();
        requiredWishCount = reader.ReadPackedInt32();

        fulfilledTargetIds.Clear();
        var fulfilledCount = reader.ReadPackedInt32();
        for (var i = 0; i < fulfilledCount; i++)
            fulfilledTargetIds.Add(reader.ReadByte());

        grantedEffects.Clear();
        var effectCount = reader.ReadPackedInt32();
        for (var i = 0; i < effectCount; i++)
        {
            var playerId = reader.ReadByte();
            var effects = (GrantedWishEffect)reader.ReadPackedInt32();
            grantedEffects[playerId] = effects;
        }
    }
}
