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
            22800,
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
            player,
            () => HasTask.True
        )
    {
    }

    private static OptionItem OptionCanVent;
    public static OptionItem OptionDeathReason;
    public static OptionItem OptionTaskTrigger;

    // 自分でタスク完了数をカウントする変数
    public int CompletedTaskCount { get; private set; } = 0;

    public static bool CanPsycho => Instance != null && Instance.CompletedTaskCount >= OptionTaskTrigger.GetInt();

    public static bool CanPsychoFor(PlayerControl player)
    {
        return Instance != null && Instance.Player == player && Instance.CompletedTaskCount >= OptionTaskTrigger.GetInt();
    }

    public static Madpsycho Instance { get; private set; }

    public override void Add()
    {
        Instance = this;
        CompletedTaskCount = 0;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    enum OptionName
    {
        psychoDeathReason
    }
    private static void SetupOptionItems()
    {
        var cRolesString = deathReasons.Select(x => x.ToString()).ToArray();
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 9, GeneralOption.CanVent, false, false);
        OptionDeathReason = StringOptionItem.Create(RoleInfo, 10, OptionName.psychoDeathReason, cRolesString, 1, false);
        OptionTaskTrigger = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.TaskTrigger, new(0, 99, 1), 1, false).SetValueFormat(OptionFormat.Pieces);
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public static readonly CustomDeathReason[] deathReasons =
    {
        CustomDeathReason.Kill, CustomDeathReason.Counter
    };

    public override bool OnCompleteTask(uint taskid)
    {
        CompletedTaskCount++;
        return true;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        var killer = info.AttemptKiller;
        if (info.KillPower >= 2) return true;
        if (!CanPsycho) return true;

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