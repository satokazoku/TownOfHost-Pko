using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class Walkure : RoleBase, ISelfVoter, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Walkure),
            player => new Walkure(player),
            CustomRoles.Walkure,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            36900,
            SetupOptionItem,
            "wk",
            "#78d7ff",
             (3, 6),
            isDesyncImpostor: true
        );

    private static OptionItem OptionMeetingRevealLimit;
    private static OptionItem OptionShowRoleToRevealed;
    private static OptionItem OptionExileVoteCount;
    private static OptionItem OptionFollowCrewmateCount;
    private static OptionItem OptionRemoveRoleCrewmateCount;
    private static OptionItem OptionNeedRevealToWin;
    private static OptionItem OptionNeedRevealCount;
    private static OptionItem OptionNotifyRevealShortage;
    private static OptionItem OptionNotifyRevealShortageDay;
    private static OptionItem OptionReflectRoleChange;
    private static OptionItem OptionReflectCrewmateRoleChanges;
    private static OptionItem OptionReflectImpostorRoleChanges;
    private static OptionItem OptionReflectJackalRoleChanges;
    private static OptionItem OptionSeeingRevealCount;
    private static OptionItem OptionWatchingRevealCount;
    private static OptionItem OptionLightingRevealCount;
    private static OptionItem OptionTiebreakerRevealCount;
    private static OptionItem OptionGuesserRevealCount;
    private static OptionItem OptionCanKillAfterReveal;
    private static OptionItem OptionKillRevealCount;
    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionCanVent;

    private readonly HashSet<byte> revealedPlayerIds = new();
    private int meetingRevealCount;
    private bool isDead;
    private bool isExiled;
    private bool deathPenaltyDone;

    private int RevealCount => revealedPlayerIds.Count;
    private bool HasKillAbility =>
        OptionCanKillAfterReveal.GetBool() &&
        RevealCount >= OptionKillRevealCount.GetFloat();

    private enum OptionName
    {
        WalkureMeetingRevealLimit,
        WalkureShowRoleToRevealed,
        WalkureExileVoteCount,
        WalkureFollowCrewmateCount,
        WalkureRemoveRoleCrewmateCount,
        WalkureNeedRevealToWin,
        WalkureNeedRevealCount,
        WalkureNotifyRevealShortage,
        WalkureNotifyRevealShortageDay,
        WalkureReflectRoleChange,
        WalkureReflectCrewmateRoleChanges,
        WalkureReflectImpostorRoleChanges,
        WalkureReflectJackalRoleChanges,
        WalkureRevealBuffSeeing,
        WalkureRevealBuffWatching,
        WalkureRevealBuffLighting,
        WalkureRevealBuffTiebreaker,
        WalkureRevealBuffGuesser,
        WalkureCanKillAfterReveal,
        WalkureKillRevealCount,
    }

    private static void SetupOptionItem()
    {
        OptionMeetingRevealLimit = IntegerOptionItem.Create(RoleInfo, 10, OptionName.WalkureMeetingRevealLimit, new(0, 15, 1), 2, false)
            .SetZeroNotation(OptionZeroNotation.Infinity)
            .SetValueFormat(OptionFormat.Times);
        OptionShowRoleToRevealed = BooleanOptionItem.Create(RoleInfo, 11, OptionName.WalkureShowRoleToRevealed, true, false);
        OptionExileVoteCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.WalkureExileVoteCount, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Votes);
        OptionFollowCrewmateCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.WalkureFollowCrewmateCount, new(1, 15, 1), 4, false)
            .SetValueFormat(OptionFormat.Players);
        OptionRemoveRoleCrewmateCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.WalkureRemoveRoleCrewmateCount, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Players);
        OptionNeedRevealToWin = BooleanOptionItem.Create(RoleInfo, 15, OptionName.WalkureNeedRevealToWin, true, false);
        OptionNeedRevealCount = IntegerOptionItem.Create(RoleInfo, 16, OptionName.WalkureNeedRevealCount, new(1, 15, 1), 3, false, OptionNeedRevealToWin)
            .SetValueFormat(OptionFormat.Times);
        OptionNotifyRevealShortage = BooleanOptionItem.Create(RoleInfo, 17, OptionName.WalkureNotifyRevealShortage, false, false);
        OptionNotifyRevealShortageDay = IntegerOptionItem.Create(RoleInfo, 18, OptionName.WalkureNotifyRevealShortageDay, new(1, 15, 1), 3, false, OptionNotifyRevealShortage)
            .SetValueFormat(OptionFormat.day);

        OptionReflectRoleChange = BooleanOptionItem.Create(RoleInfo, 19, OptionName.WalkureReflectRoleChange, true, false);
        OptionReflectCrewmateRoleChanges = BooleanOptionItem.Create(RoleInfo, 20, OptionName.WalkureReflectCrewmateRoleChanges, true, false, OptionReflectRoleChange);
        OptionReflectImpostorRoleChanges = BooleanOptionItem.Create(RoleInfo, 21, OptionName.WalkureReflectImpostorRoleChanges, true, false, OptionReflectRoleChange);
        OptionReflectJackalRoleChanges = BooleanOptionItem.Create(RoleInfo, 22, OptionName.WalkureReflectJackalRoleChanges, true, false, OptionReflectRoleChange);

        OptionSeeingRevealCount = FloatOptionItem.Create(RoleInfo, 23, OptionName.WalkureRevealBuffSeeing, new(1f, 15f, 1f), 1f, false)
            .SetValueFormat(OptionFormat.Times);
        OptionWatchingRevealCount = FloatOptionItem.Create(RoleInfo, 24, OptionName.WalkureRevealBuffWatching, new(1f, 15f, 1f), 2f, false)
            .SetValueFormat(OptionFormat.Times);
        OptionLightingRevealCount = FloatOptionItem.Create(RoleInfo, 25, OptionName.WalkureRevealBuffLighting, new(1f, 15f, 1f), 3f, false)
            .SetValueFormat(OptionFormat.Times);
        OptionTiebreakerRevealCount = FloatOptionItem.Create(RoleInfo, 26, OptionName.WalkureRevealBuffTiebreaker, new(1f, 15f, 1f), 4f, false)
            .SetValueFormat(OptionFormat.Times);
        OptionGuesserRevealCount = FloatOptionItem.Create(RoleInfo, 27, OptionName.WalkureRevealBuffGuesser, new(1f, 15f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Times);

        OptionCanKillAfterReveal = BooleanOptionItem.Create(RoleInfo, 28, OptionName.WalkureCanKillAfterReveal, true, false);
        OptionKillRevealCount = FloatOptionItem.Create(RoleInfo, 29, OptionName.WalkureKillRevealCount, new(1f, 15f, 1f), 6f, false, OptionCanKillAfterReveal)
            .SetValueFormat(OptionFormat.Times);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 30, GeneralOption.KillCooldown, new(0f, 255f, 0.5f), 40f, false, OptionCanKillAfterReveal)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 31, GeneralOption.CanVent, false, false, OptionCanKillAfterReveal);
    }
    public enum RoleChangeSource
    {
        Crewmate,
        Impostor,
        Jackal,
    }
    public Walkure(PlayerControl player)
        : base(RoleInfo, player)
    {
        meetingRevealCount = 0;
        isDead = false;
        isExiled = false;
        deathPenaltyDone = false;
    }

    public override void Add()
    {
        revealedPlayerIds.Clear();
        meetingRevealCount = 0;
        isDead = false;
        isExiled = false;
        deathPenaltyDone = false;
        SetMode(Player, false);
        SendRPC();
    }

    public override void OnDestroy()
    {
        SetMode(Player, false);
    }

    public override void OnStartMeeting()
    {
        meetingRevealCount = 0;
        SetMode(Player, false);
        SendRPC();
    }

    bool ISelfVoter.CanUseVoted() => CanUseRevealAbility();

    private bool CanUseRevealAbility()
    {
        if (!Canuseability()) return false;
        if (!Player.IsAlive()) return false;
        var limit = OptionMeetingRevealLimit.GetInt();
        return limit == 0 || meetingRevealCount < limit;
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !CanUseRevealAbility()) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            switch (status)
            {
                case VoteStatus.Self:
                    Utils.SendMessage(GetString("WalkureRevealModeOn"), Player.PlayerId);
                    SetMode(Player, true);
                    break;
                case VoteStatus.Skip:
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    SetMode(Player, false);
                    break;
                case VoteStatus.Vote:
                    TryReveal(votedForId);
                    SetMode(Player, false);
                    break;
            }
            SendRPC();
            return false;
        }

        return true;
    }

    private void TryReveal(byte targetId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var target = PlayerCatch.GetPlayerById(targetId);
        if (target == null || target.Data.Disconnected || !target.IsAlive() || target.PlayerId == Player.PlayerId)
        {
            Utils.SendMessage(GetString("WalkureRevealInvalid"), Player.PlayerId);
            return;
        }

        var limit = OptionMeetingRevealLimit.GetInt();
        if (limit != 0 && meetingRevealCount >= limit)
        {
            Utils.SendMessage(GetString("WalkureRevealLimit"), Player.PlayerId);
            return;
        }

        if (!revealedPlayerIds.Add(target.PlayerId))
        {
            Utils.SendMessage(string.Format(GetString("WalkureRevealAlready"), UtilsName.GetPlayerColor(target, true)), Player.PlayerId);
            return;
        }

        meetingRevealCount++;
        SendRevealMessage(target);
        ApplyRevealBuffs();

        var totalLeft = OptionNeedRevealToWin.GetBool()
            ? System.Math.Max(OptionNeedRevealCount.GetInt() - RevealCount, 0)
            : 0;
        Utils.SendMessage(
            string.Format(GetString("WalkureRevealComplete"), UtilsName.GetPlayerColor(target, true), RevealCount, totalLeft),
            Player.PlayerId);

        if (!Utils.RoleSendList.Contains(target.PlayerId)) Utils.RoleSendList.Add(target.PlayerId);
        if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
        Player.SyncSettings();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRPC();
    }

    private void SendRevealMessage(PlayerControl target)
    {
        var message = string.Format(
            GetString("WalkureRevealMessage"),
            UtilsName.GetPlayerColor(Player, true),
            UtilsRoleText.GetRoleColorAndtext(CustomRoles.Walkure));
        Utils.SendMessage(message, target.PlayerId, GetString("WalkureRevealTitle"));
    }

    private void ApplyRevealBuffs()
    {
        TryGrantBuff(CustomRoles.Seeing, OptionSeeingRevealCount.GetFloat());
        TryGrantBuff(CustomRoles.Watching, OptionWatchingRevealCount.GetFloat());
        TryGrantBuff(CustomRoles.Lighting, OptionLightingRevealCount.GetFloat());
        TryGrantBuff(CustomRoles.Tiebreaker, OptionTiebreakerRevealCount.GetFloat());
        TryGrantBuff(CustomRoles.Guesser, OptionGuesserRevealCount.GetFloat());

        if (HasKillAbility)
        {
            Player.RpcResetAbilityCooldown();
            Player.SetKillCooldown(OptionKillCooldown.GetFloat(), delay: true);
        }
    }

    private void TryGrantBuff(CustomRoles addon, float revealThreshold)
    {
        if (RevealCount < revealThreshold) return;
        if (Player.Is(addon)) return;

        Player.RpcSetCustomRole(addon);
        Utils.SendMessage(
            string.Format(GetString("WalkureBuffGained"), UtilsRoleText.GetRoleColorAndtext(addon)),
            Player.PlayerId);
    }

    public override string MeetingAddMessage()
    {
        if (!OptionNotifyRevealShortage.GetBool()) return "";
        if (UtilsGameLog.day < OptionNotifyRevealShortageDay.GetInt()) return "";
        var required = OptionNeedRevealCount.GetInt();
        if (RevealCount >= required) return "";
        return string.Format(GetString("WalkurePublicWarning"), RevealCount, required);
    }

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (vote.TryGetValue(Player.PlayerId, out var count) && count >= OptionExileVoteCount.GetInt())
        {
            IsTie = false;
            Exiled = Player.Data;
            isExiled = true;
            return true;
        }
        return false;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        info.GuardPower = 99;
        var (killer, target) = info.AppearanceTuple;
        killer.SetKillCooldown(target: target);
        return false;
    }

    public override bool? CheckGuess(PlayerControl killer) => false;

    public override void OnDead(PlayerControl player)
    {
        if (player.PlayerId == Player.PlayerId)
            TriggerDeathPenalty();
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player != Player) return;
        if (AmongUsClient.Instance.AmHost && isExiled && !deathPenaltyDone)
            _ = new LateTask(TriggerDeathPenalty, 20f, "WalkureExiledLeft");
        isDead = true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.ExiledAnimate) return;

        if (!isExiled)
        {
            if (isDead) return;
            if (player.Data.Disconnected && MyState.DeathReason is CustomDeathReason.Disconnected) return;
        }

        if (!player.IsAlive())
            TriggerDeathPenalty();
    }

    private void TriggerDeathPenalty()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (deathPenaltyDone) return;
        if (isDead && !isExiled) return;

        deathPenaltyDone = true;
        isDead = true;
        isExiled = false;

        var rand = IRandom.Instance;
        var crews = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null && pc != Player && pc.IsAlive() && pc.Is(CustomRoleTypes.Crewmate))
            .ToList();

        for (var i = 0; i < OptionFollowCrewmateCount.GetInt(); i++)
        {
            if (crews.Count == 0) break;
            var target = TakeRandomCrew(crews, rand);
            if (target == null) break;
            KillFollowingCrewmate(target);
        }

        for (var i = 0; i < OptionRemoveRoleCrewmateCount.GetInt(); i++)
        {
            if (crews.Count == 0) break;
            var candidates = crews.Where(pc => pc.GetCustomRole() != CustomRoles.Crewmate).ToList();
            var pool = candidates.Count > 0 ? candidates : crews;
            var target = TakeRandomCrew(pool, rand);
            if (target == null) break;
            crews.Remove(target);

            target.RpcSetCustomRole(CustomRoles.Crewmate, true, null);
            Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} lost role by Walkure death.", "Walkure");
        }

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRPC();
    }

    private static PlayerControl TakeRandomCrew(List<PlayerControl> crews, IRandom rand)
    {
        while (crews.Count > 0)
        {
            var index = rand.Next(0, crews.Count);
            var target = crews[index];
            crews.RemoveAt(index);
            if (target != null && target.IsAlive()) return target;
        }
        return null;
    }

    private void KillFollowingCrewmate(PlayerControl target)
    {
        target.SetRealKiller(Player);
        if (GameStates.CalledMeeting || MeetingHud.Instance != null)
        {
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.FollowingSuicide, target.PlayerId);
            Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} follows Walkure after meeting.", "Walkure");
            return;
        }

        CustomRoleManager.OnCheckMurder(Player, target, target, target, true, true, 999, CustomDeathReason.FollowingSuicide);
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} follows Walkure.", "Walkure");
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (!OptionShowRoleToRevealed.GetBool()) return;
        if (seer.PlayerId == Player.PlayerId) return;
        if (!revealedPlayerIds.Contains(seer.PlayerId)) return;

        enabled = true;
        roleColor = RoleInfo.RoleColor;
        roleText = GetString("Walkure");
        addon = false;
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var required = OptionNeedRevealToWin.GetBool() ? OptionNeedRevealCount.GetInt() : 0;
        var color = required == 0 || RevealCount >= required ? RoleInfo.RoleColor : Color.gray;
        return Utils.ColorString(color, $"({RevealCount})");
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!isForMeeting || seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId) return "";
        if (!CanUseRevealAbility()) return "";

        var limit = OptionMeetingRevealLimit.GetInt();
        var remaining = limit == 0 ? "inf" : (limit - meetingRevealCount).ToString();
        var text = string.Format(GetString("WalkureRevealModeOff"), remaining);
        return isForHud ? $"<color={RoleInfo.RoleColorCode}>{text}</color>" : $"<size=40%><color={RoleInfo.RoleColorCode}>{text}</color></size>";
    }

    public override RoleTypes? AfterMeetingRole => HasKillAbility ? RoleTypes.Impostor : RoleTypes.Crewmate;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    bool IKiller.CanKill => HasKillAbility;
    bool IKiller.IsKiller => HasKillAbility;
    public bool CanUseKillButton() => Player.IsAlive() && HasKillAbility;
    public float CalculateKillCooldown() => HasKillAbility ? OptionKillCooldown.GetFloat() : 0f;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => HasKillAbility && OptionCanVent.GetBool();

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller)) return;
        if (!HasKillAbility)
        {
            info.DoKill = false;
            return;
        }

        Player.ResetKillCooldown();
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("WalkureKillButtonText");
        return true;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Walkure_Kill";
        return true;
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!OptionNeedRevealToWin.GetBool()) return;
        if (!Player.IsWinner(CustomWinner.Crewmate)) return;
        if (RevealCount >= OptionNeedRevealCount.GetInt()) return;

        CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
        Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} cannot win: reveal count {RevealCount}/{OptionNeedRevealCount.GetInt()}", "Walkure");
    }

    public static bool TryRejectRoleChange(PlayerControl actor, PlayerControl target, RoleChangeSource source)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (actor == null || target == null) return false;
        if (actor.PlayerId == target.PlayerId) return false;
        if (target.GetRoleClass() is not Walkure walkure) return false;
        if (!walkure.CanReflect(source)) return false;

        walkure.ReflectRoleChange(actor, source);
        return true;
    }

    private bool CanReflect(RoleChangeSource source)
    {
        if (!OptionReflectRoleChange.GetBool()) return false;
        return source switch
        {
            RoleChangeSource.Crewmate => OptionReflectCrewmateRoleChanges.GetBool(),
            RoleChangeSource.Impostor => OptionReflectImpostorRoleChanges.GetBool(),
            RoleChangeSource.Jackal => OptionReflectJackalRoleChanges.GetBool(),
            _ => false,
        };
    }

    private void ReflectRoleChange(PlayerControl actor, RoleChangeSource source)
    {
        if (!actor.IsAlive()) return;

        var state = PlayerState.GetByPlayerId(actor.PlayerId);
        if (state != null) state.DeathReason = CustomDeathReason.Suicide;
        actor.SetRealKiller(Player);

        Utils.SendMessage(
            string.Format(GetString("WalkureReflectMessage"), UtilsName.GetPlayerColor(Player, true)),
            actor.PlayerId);

        if (MeetingHud.Instance != null)
        {
            MeetingVoteManager.ResetVoteManager(actor.PlayerId);
        }
        else
        {
            actor.RpcMurderPlayerV2(actor);
        }

        Logger.Info($"{actor.GetNameWithRole().RemoveHtmlTags()} self-destructed by Walkure reflection ({source}).", "Walkure");
    }

    private void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(meetingRevealCount);
        sender.Writer.Write(isDead);
        sender.Writer.Write(isExiled);
        sender.Writer.Write(deathPenaltyDone);
        sender.Writer.Write(revealedPlayerIds.Count);
        foreach (var id in revealedPlayerIds)
            sender.Writer.Write(id);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        meetingRevealCount = reader.ReadInt32();
        isDead = reader.ReadBoolean();
        isExiled = reader.ReadBoolean();
        deathPenaltyDone = reader.ReadBoolean();

        revealedPlayerIds.Clear();
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
            revealedPlayerIds.Add(reader.ReadByte());
    }
}
