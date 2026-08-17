using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Madmate;

public sealed class Madpsycho : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Madpsycho),
            player => new Madpsycho(player),
            CustomRoles.Madpsycho,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            22700,
            SetupOptionItems,
            "mps",
            OptionSort: (2, 3),
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
                assignInfo: new RoleAssignInfo(CustomRoles.Madpsycho, CustomRoleTypes.Madmate)
                {
                    AssignCountRule = new(1, 1, 1)
                }
        );

    public Madpsycho(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
    {
        CanPsycho = false;
    }
    private static OptionItem OptionCanVent;
    public static OptionItem OptionDeathReason;
    public static bool CanPsycho;
    public static OptionItem OptionTaskTrigger;
    private static void SetupOptionItems()
    {
        var cRolesString = deathReasons.Select(x => x.ToString()).ToArray();
        OptionDeathReason = StringOptionItem.Create(RoleInfo, 10, OptionName.psychoDeathReason, cRolesString, 1, false);
        OptionTaskTrigger = IntegerOptionItem.Create(RoleInfo, 12, GeneralOption.TaskTrigger, new(0, 99, 1), 1, false).SetValueFormat(OptionFormat.Pieces);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        OverrideTasksData.Create(RoleInfo, 20);
    }
    public static readonly CustomDeathReason[] deathReasons =
{
        CustomDeathReason.Kill,CustomDeathReason.Counter    };
    private enum OptionName
    {
        psychoDeathReason
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(OptionTaskTrigger.GetInt()))
        {
            CanPsycho = true;
        }
        return true;
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        var killer = info.AttemptKiller;
        if (info.KillPower >= 2) return true;
        if (killer.Is(CustomRoles.SheriffHadouHo) && SheriffHadouHo.Charging)
        {
            return true;
        }
        if (killer.Is(CustomRoles.HadouHo) && HadouHo.Charging)
        {
            return true;
        }
        if (killer.Is(CustomRoles.JackalHadouHo))
        {
            if (JackalHadouHo.Charging)
            {
                return true;
            }
        }
        PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = deathReasons[OptionDeathReason.GetValue()];
        info.KillPower = 10;
        Player.RpcMurderPlayer(killer);
        return false;
    }
}