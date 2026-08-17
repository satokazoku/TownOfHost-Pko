using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Autocrat : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Autocrat),
            player => new Autocrat(player),
            CustomRoles.Autocrat,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            55900,
            SetupOptionItem,
            "aut",
            "#8b0000",
            (7, 8),
            from: From.TownOfHost_Pko
        );

    private static OptionItem ExileVoteCount;
    private static OptionItem RevengePlayerCount;
    private static OptionItem RemoveBuffAddonPlayerCount;
    private static OptionItem ChangeToEmptinessPlayerCount;
    public static OptionItem CanBeGuessed;

    private bool deathEffectTriggered;
    private bool wasExiled;

    private enum OptionName
    {
        AutocratExileVoteCount,
        AutocratRevengePlayerCount,
        AutocratRemoveBuffAddonPlayerCount,
        AutocratChangeToEmptinessPlayerCount,
        AutocratCanBeGuessed,
    }

    public Autocrat(PlayerControl player) : base(RoleInfo, player)
    {
        deathEffectTriggered = false;
        wasExiled = false;
    }

    private static void SetupOptionItem()
    {
        ExileVoteCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.AutocratExileVoteCount, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Votes);
        RevengePlayerCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AutocratRevengePlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        RemoveBuffAddonPlayerCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.AutocratRemoveBuffAddonPlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        ChangeToEmptinessPlayerCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.AutocratChangeToEmptinessPlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        CanBeGuessed = BooleanOptionItem.Create(RoleInfo, 14, OptionName.AutocratCanBeGuessed, true, false);
    }

    public override bool? CheckGuess(PlayerControl killer) => CanBeGuessed.GetBool();

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!vote.TryGetValue(Player.PlayerId, out var count) || count < ExileVoteCount.GetInt())
            return false;

        IsTie = false;
        Exiled = Player.Data;
        wasExiled = true;
        return true;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        info.GuardPower = 9;
        info.AppearanceKiller.SetKillCooldown(target: info.AppearanceTarget);
        return false;
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player == Player)
            deathEffectTriggered = true;
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled?.PlayerId == Player.PlayerId)
            ApplyDeathPenalty(true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || deathEffectTriggered || player == null) return;
        if (player.Data?.Disconnected == true && MyState.DeathReason == CustomDeathReason.Disconnected) return;
        if (GameStates.ExiledAnimate) return;

        if (!player.IsAlive())
            ApplyDeathPenalty(wasExiled || GameStates.CalledMeeting || MeetingHud.Instance != null);
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (seer == Player || !seer.Is(CustomRoleTypes.Neutral)) return;

        enabled = true;
        roleColor = RoleInfo.RoleColor;
        roleText = GetString(nameof(CustomRoles.Autocrat));
        addon = false;
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        var anotherNeutralWon = CustomWinnerHolder.NeutralWinnerIds.Any(id => id != Player.PlayerId)
            || CustomWinnerHolder.WinnerIds.Any(id =>
                id != Player.PlayerId
                && PlayerCatch.GetPlayerById(id)?.GetCustomRole().IsNeutral() == true)
            || CustomWinnerHolder.WinnerRoles.Any(role => role != CustomRoles.Autocrat && role.IsNeutral());

        if (!anotherNeutralWon) return false;
        winnerRole = CustomRoles.Autocrat;
        return true;
    }

    private void ApplyDeathPenalty(bool afterMeeting)
    {
        if (deathEffectTriggered || !AmongUsClient.Instance.AmHost) return;
        deathEffectTriggered = true;
        wasExiled = false;

        var neutralPlayers = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null
                && pc != Player
                && pc.IsAlive()
                && pc.Is(CustomRoleTypes.Neutral))
            .ToList();

        var revengeCandidates = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null
                && pc != Player
                && pc.IsAlive()
                && (pc.Is(CustomRoleTypes.Neutral) || pc.Is(CustomRoles.Emptiness)))
            .ToList();

        var revengeTargets = PickRandom(revengeCandidates, RevengePlayerCount.GetInt());
        var revengeIds = revengeTargets.Select(pc => pc.PlayerId).ToHashSet();

        if (afterMeeting && revengeTargets.Count > 0)
        {
            foreach (var target in revengeTargets)
                target.SetRealKiller(Player);
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Revenge, revengeTargets.Select(pc => pc.PlayerId).ToArray());
        }
        else
        {
            foreach (var target in revengeTargets)
            {
                CustomRoleManager.OnCheckMurder(
                    Player,
                    target,
                    Player,
                    target,
                    force: true,
                    DontRoleAbility: true,
                    Killpower: 999,
                    deathReason: CustomDeathReason.Revenge);
            }
        }

        var survivingNeutrals = neutralPlayers
            .Where(pc => !revengeIds.Contains(pc.PlayerId) && pc.IsAlive())
            .ToList();

        foreach (var target in PickRandom(
            survivingNeutrals.Where(pc => pc.GetCustomSubRoles().Any(addon => addon.IsBuffAddon())).ToList(),
            RemoveBuffAddonPlayerCount.GetInt()))
        {
            foreach (var addon in target.GetCustomSubRoles().Where(addon => addon.IsBuffAddon()).ToArray())
                target.RpcReplaceSubRole(addon, remove: true);
        }

        foreach (var target in PickRandom(
            survivingNeutrals.Where(pc => pc.GetCustomRole() is not CustomRoles.Emptiness and not CustomRoles.Autocrat).ToList(),
            ChangeToEmptinessPlayerCount.GetInt()))
        {
            target.RpcSetCustomRole(CustomRoles.Emptiness, true, null);
        }

        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.4f, "AutocratDeathPenalty");
    }

    private static List<PlayerControl> PickRandom(List<PlayerControl> source, int count)
    {
        var pool = source.ToList();
        var result = new List<PlayerControl>();
        var random = IRandom.Instance;

        while (result.Count < count && pool.Count > 0)
        {
            var index = random.Next(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
}
