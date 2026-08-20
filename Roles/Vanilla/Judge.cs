using System.Collections.Generic;
using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;

namespace TownOfHost.Roles.Vanilla;

public sealed class Judge : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Judge),
            player => new Judge(player),
            RoleTypes.Judge,
            SetUpCustomOption,
            "#a1472c"
            , from: From.AmongUs
        );
    public Judge(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        taskrequirement = OptionTaskRequirement.GetFloat();
        IsUsed = false;
        Isfall = false;
    }
    bool IsUsed;
    bool Isfall;
    static float taskrequirement;
    public static OptionItem OptionTaskRequirement;
    private static OptionItem OptionCanKillMadMate;
    private static OptionItem OptionCanKillNeutrals;
    private static OptionItem OptionCanKillLovers;
    public static void SetUpCustomOption()
    {
        OptionTaskRequirement = FloatOptionItem.Create(RoleInfo, 25110, StringNames.JudgeTaskRequirement, new(0, 100, 2), 2, false);
        OptionCanKillMadMate = BooleanOptionItem.Create(RoleInfo, 25112, "MeetingSheriffCanKillMadMate", true, false);
        OptionCanKillNeutrals = BooleanOptionItem.Create(RoleInfo, 25113, "MeetingSheriffCanKillNeutrals", true, false);
        OptionCanKillLovers = BooleanOptionItem.Create(RoleInfo, 25114, "SheriffCanKillLovers", true, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.JudgeTaskRequirementPercentage = taskrequirement;
    }
    public override bool CallJudgeVote(PlayerControl voter, PlayerControl votefor, ref byte ExilePlayerid)
    {
        ExilePlayerid = byte.MaxValue;
        if (IsUsed) return false;
        if (SelfVoteManager.Canuseability() is false) return false;
        if (votefor.IsAlive() is false) return false;

        IsUsed = true;
        var AlienTairo = false;
        var targetroleclass = votefor.GetRoleClass();
        if ((targetroleclass as Alien)?.CheckSheriffKill(votefor) == true) AlienTairo = true;
        if ((targetroleclass as JackalAlien)?.CheckSheriffKill(votefor) == true) AlienTairo = true;
        if ((targetroleclass as AlienHijack)?.CheckSheriffKill(votefor) == true) AlienTairo = true;

        if (AntiBlackout.OverrideExiledPlayer())
        {
            Utils.AllPlayerKillFlash();
        }

        if ((CanBeKilledBy(votefor.GetCustomRole()) && !AlienTairo) || (votefor.IsLovers() && OptionCanKillLovers.GetBool()) || (votefor.Is(CustomRoles.Amanojaku) && OptionCanKillNeutrals.GetBool()))
        {//成功
            ExilePlayerid = votefor.PlayerId;
            votefor.SetRealKiller(voter);
        }
        else
        {
            Isfall = true;
            ExilePlayerid = voter.PlayerId;
        }
        return true;
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled == null) return;
        if (exiled.PlayerId == Player.PlayerId && Isfall)
        {
            MyState.DeathReason = CustomDeathReason.Misfire;
        }
    }
    bool CanBeKilledBy(CustomRoles role)
    {
        if (role == CustomRoles.SKMadmate) return OptionCanKillMadMate.GetBool();
        if (role == CustomRoles.Jackaldoll) return OptionCanKillNeutrals.GetBool();

        return role.GetCustomRoleTypes() switch
        {
            CustomRoleTypes.Impostor => role is not CustomRoles.Tairou,
            CustomRoleTypes.Madmate => OptionCanKillMadMate.GetBool(),
            CustomRoleTypes.Neutral => OptionCanKillNeutrals.GetBool(),
            CustomRoleTypes.Crewmate => role is CustomRoles.WolfBoy,
            _ => false
        };
    }
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        return base.VotingResults(ref Exiled, ref IsTie, vote, mostVotedPlayers, ClearAndExile);
    }
}
