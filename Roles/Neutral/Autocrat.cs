using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
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
    static OptionItem OptionDeathReason;
    int RevengeCount;
    public static readonly CustomDeathReason[] deathReasons =
    {
        CustomDeathReason.Kill,CustomDeathReason.Suicide,CustomDeathReason.Revenge,CustomDeathReason.FollowingSuicide
    };
    bool IsDead;
    bool IsExiled;

    private enum OptionName
    {
        AutocratExileVoteCount,
        AutocratRevengePlayerCount,
        AutocratRemoveBuffAddonPlayerCount,
        AutocratChangeToEmptinessPlayerCount,
        AutocratCanBeGuessed,
        AutocratDeathReason,
    }

    public Autocrat(PlayerControl player) : base(RoleInfo, player)
    {
        IsDead = false;
        IsExiled = false;
        RevengeCount = 0;
    }

    private static void SetupOptionItem()
    {
        var cRolesString = deathReasons.Select(x => x.ToString()).ToArray();

        ExileVoteCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.AutocratExileVoteCount, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Votes);
        RevengePlayerCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AutocratRevengePlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        OptionDeathReason = StringOptionItem.Create(RoleInfo, 12, OptionName.AutocratDeathReason, cRolesString, 3, false);
        RemoveBuffAddonPlayerCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.AutocratRemoveBuffAddonPlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        ChangeToEmptinessPlayerCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.AutocratChangeToEmptinessPlayerCount, new(1, 15, 1), 5, false)
            .SetValueFormat(OptionFormat.Players);
        CanBeGuessed = BooleanOptionItem.Create(RoleInfo, 15, OptionName.AutocratCanBeGuessed, true, false);
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
    public override bool? CheckGuess(PlayerControl killer)
    {
        return CanBeGuessed.GetBool();
    }
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (vote.TryGetValue(Player.PlayerId, out var count))
        {
            if (ExileVoteCount.GetInt() <= count)
            {
                IsTie = false;
                Exiled = Player.Data;
                IsExiled = true;
                return true;
            }
        }
        return false;
    }
    public override void OnLeftPlayer(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            if (player == Player)
                if (IsExiled && !IsDead)
                {
                    _ = new LateTask(() => NeutralAbooooon(), 20f, "AutocratExdie");
                }
        }
        if (player == Player) IsDead = true;
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        info.GuardPower = 9;
        var (killer, target) = info.AppearanceTuple;
        if (killer.GetRoleClass() is BountyHunter bountyHunter)
        {
            bountyHunter.OnCratKill(this);
        }
        killer.SetKillCooldown(target: target);
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.ExiledAnimate) return;
        if (!IsExiled)
        {
            if (IsDead) return;

            if (player.Data.Disconnected && MyState.DeathReason is CustomDeathReason.Disconnected) return;
        }
        if (!player.IsAlive())
        {
            NeutralAbooooon();
            IsExiled = false;
            IsDead = true;
        }
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref UnityEngine.Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (seer == Player) return;
        //マーメイドは弾く
        if (seer.Is(CustomRoleTypes.Neutral) && !seer.Is(CustomRoles.Mermaid))
        {
            enabled = true;
            roleColor = StringHelper.CodeColor("#8b0000");
            roleText = GetString("Autocrat");
            addon = false;
        }
    }
    void NeutralAbooooon()
    {
        if (IsDead && !IsExiled) return;
        if (AmongUsClient.Instance.AmHost)
        {
            var rand = IRandom.Instance;
            int Count = RevengePlayerCount.GetInt();

            List<PlayerControl> neutrals = new();

            //対象者
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                if (!pc) continue;
                if (pc == Player) continue;
                if (!pc.IsAlive() || !pc.Is(CustomRoleTypes.Neutral)) continue;
                if (!neutrals.Contains(pc)) neutrals.Add(pc);
            }

            if (!GameStates.CalledMeeting)
            {
                for (var i = 0; i < Count; i++)
                {
                    if (neutrals.Count == 0) break;
                    var pc = neutrals[rand.Next(0, neutrals.Count)];

                    if (pc == null)
                    {
                        i--;
                        continue;
                    }
                    if (!pc.IsAlive())
                    {
                        i--;
                        continue;
                    }
                    if (RevengeCount < RevengePlayerCount.GetInt())
                    {
                        CustomRoleManager.OnCheckMurder(Player, pc, pc, pc, true, true, 999, deathReason: deathReasons[OptionDeathReason.GetValue()]);
                        ++RevengeCount;
                        Logger.Info($"{pc.name}が巻き込まれちゃった！", "Autocrataboooooon");
                        neutrals.Remove(pc);
                    }
                }
            }
            else
            {
                for (var i = 0; i < Count; i++)
                {
                    if (neutrals.Count == 0) break;
                    var pc = neutrals[rand.Next(0, neutrals.Count)];

                    if (pc == null)
                    {
                        i--;
                        continue;
                    }
                    if (!pc.IsAlive())
                    {
                        i--;
                        continue;
                    }
                    if (RevengeCount < RevengePlayerCount.GetInt())
                    {
                        PlayerState state = PlayerState.GetByPlayerId(pc.PlayerId);
                        ++RevengeCount;
                        state.DeathReason = deathReasons[OptionDeathReason.GetValue()];
                        Player.RpcExileV3();
                        state.SetDead();
                        ReportDeadBodyPatch.IgnoreBodyids[Player.PlayerId] = false;

                        Logger.Info($"{pc.name}が後追いしちゃった！", "AutocratEx");
                        neutrals.Remove(pc);
                    }
                }
            }

            //役職 & 属性ぼっしゅー

            var addoncount = RemoveBuffAddonPlayerCount.GetInt();
            if (addoncount != 0)
            {
                for (var i = 0; i < addoncount; i++)
                {
                    if (neutrals.Count == 0) break;
                    var pc = neutrals[rand.Next(0, neutrals.Count)];

                    if (pc == null)
                    {
                        i--;
                        continue;
                    }
                    if (!pc.IsAlive())
                    {
                        i--;
                        continue;
                    }

                    var ps = PlayerState.GetByPlayerId(pc.PlayerId);
                    List<CustomRoles> remove = new();
                    if (pc.GetCustomSubRoles() != null)
                        foreach (var addon in pc.GetCustomSubRoles())
                            if (addon.IsBuffAddon())
                            {
                                if (!remove.Contains(addon)) remove.Add(addon);
                                Logger.Info($"{pc.name}の{addon}ぼっしゅー", "AutocratAddon");
                            }

                    if (remove == null && remove?.Count != 0)
                    {
                        foreach (var addon in remove)
                            pc.RpcReplaceSubRole(addon, true);
                    }
                }
            }

            var rolecount = ChangeToEmptinessPlayerCount.GetInt();
            if (rolecount != 0)
            {
                for (var i = 0; i < rolecount; i++)
                {
                    if (neutrals.Count == 0) break;
                    var pc = neutrals[rand.Next(0, neutrals.Count)];

                    if (pc == null)
                    {
                        i--;
                        continue;
                    }
                    if (!pc.IsAlive())
                    {
                        i--;
                        continue;
                    }

                    pc.RpcSetCustomRole(CustomRoles.Emptiness, true, null);
                    Logger.Info($"{pc.name}の役職空虚者な！！ハハハ!!", "AutocratRoles");
                }
            }
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.4f, "AutocratResetNotify");
        }
        IsDead = true;
        IsExiled = false;
    }
}
