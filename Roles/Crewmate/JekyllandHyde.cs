using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static Il2CppSystem.Threading.SemaphoreSlim;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Crewmate;


/*
ジキルとハイド
ジキルとハイドが入れ替わる。
ハイドがインポスターをキルすると、ハイドはインポスターカウントでハイド固定。
*/
public sealed class JekyllandHyde : RoleBase
{
    public static OptionItem OptionCanSeeImpostor;
    public static OptionItem OptionKillCooldown;
    public static OptionItem OptionCanVent;
    public static OptionItem OptionCanUseSabotage;
    public static OptionItem OptionKillMaxniums;
    internal static bool IsSpecialMeetingNoSwap()
    {
        if (Roles.Crewmate.Balancer.Id != byte.MaxValue
            || (Roles.Crewmate.Balancer.target1 != byte.MaxValue
                && Roles.Crewmate.Balancer.target2 != byte.MaxValue))
        {
            return true;
        }

        if (Roles.Crewmate.Nimrod.IsExecutionMeeting())
        {
            return true;
        }

        var assassinState = Roles.Impostor.Assassin.assassin?.NowState;
        if (assassinState is Roles.Impostor.Assassin.AssassinMeeting.Guessing
            or Roles.Impostor.Assassin.AssassinMeeting.Collected
            or Roles.Impostor.Assassin.AssassinMeeting.DieWait)
        {
            return true;
        }

        return false;
    }

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JekyllandHyde),
            player => new JekyllandHyde(player),
            CustomRoles.JekyllandHyde,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            34800,
            SetupOptionItem,
            "jah",
            "#8cffff",
            (4, 2),
            from: From.NebulaontheShip,
                        countType: CountTypes.Crew,
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public JekyllandHyde(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
    }

    static void SetupOptionItem()
    {
        //OptionCanSeeImpostor = BooleanOptionItem.Create(RoleInfo, 5, OptionName.JekyllanSeeImpostor, false, false);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        OptionCanUseSabotage = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanUseSabotage, false, false);
        OptionKillMaxniums = FloatOptionItem.Create(RoleInfo, 13, OptionName.HydeKillMaxniums, new(1, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        HideRoleOptions(CustomRoles.Jekyll);
    }

    enum OptionName
    {
        JekyllanSeeImpostor,
        HydeKillMaxniums,
    }

    public static bool CanSeeImpostorNameColor(CustomRoles role)
    {
        if (role is CustomRoles.Hyde)
        {
        }

        return UsesMadmateCommonSettings(role) && Options.MadCanSeeImpostor.GetBool();
    }

    public static bool UsesMadmateCommonSettings(PlayerControl player)
    {
        return player != null && UsesMadmateCommonSettings(player.GetCustomRole());
    }

    public static bool UsesMadmateCommonSettings(CustomRoles role)
    {
        return role.IsMadmate() && role is not CustomRoles.Hyde;
    }

    internal static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null &&
            Options.CustomRoleSpawnChances.TryGetValue(role, out var spawnOption))
        {
            spawnOption.SetHidden(true);
        }

        if (Options.CustomRoleCounts != null &&
            Options.CustomRoleCounts.TryGetValue(role, out var countOption))
        {
            countOption.SetHidden(true);
        }
    }
}

public sealed class Jekyll : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jekyll),
            player => new Jekyll(player),
            CustomRoles.Jekyll,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            34900,
            SetupOptionItem,
            "jkl",
            "#8cffff",
            (8, 1),
            from: From.NebulaontheShip,
            countType: CountTypes.Crew,
            assignInfo: new RoleAssignInfo(CustomRoles.Jekyll, CustomRoleTypes.Crewmate)
            {
                IsInitiallyAssignableCallBack = () => false,
                AssignCountRule = new(0, 0, 1)
            }
        );

    public Jekyll(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {

    }
    bool skipSwapForThisMeeting;
 //   bool CanChangeHyde;

    static void SetupOptionItem()
    {
        JekyllandHyde.HideRoleOptions(CustomRoles.Jekyll);
    }

    public override void OnStartMeeting()
    {
        skipSwapForThisMeeting = JekyllandHyde.IsSpecialMeetingNoSwap();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (skipSwapForThisMeeting)
        {
            skipSwapForThisMeeting = false;
            return;
        }
        skipSwapForThisMeeting = false;
        Player.RpcSetCustomRole(CustomRoles.Hyde, log: null);
    }
    /*public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished && Player.IsAlive())
        {
            CanChangeHyde = true;
        }
        return true;
    }*/
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
       // AURoleOptions.EngineerCooldown = 0;
       // AURoleOptions.EngineerInVentMaxTime = 0.5f;
    }
    //public override bool CanVentMoving(PlayerPhysics physics, int ventId) => false;
   /*public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (CanChangeHyde)
        {
            Player.RpcSetCustomRole(CustomRoles.Hyde, log: null);
        }
        return false;
    }*/
}

public sealed class Hyde : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hyde),
            player => new Hyde(player),
            CustomRoles.Hyde,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Madmate,
            70660,
            SetupOptionItem,
            "hy",
            "#ff1919",
            (8, 2),
            from: From.NebulaontheShip,
            countType: CountTypes.Crew,
            assignInfo: new RoleAssignInfo(CustomRoles.Hyde, CustomRoleTypes.Madmate)
            {
                IsInitiallyAssignableCallBack = () => false,
                AssignCountRule = new(0, 0, 1)
            },
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public Hyde(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        KillCooldown = JekyllandHyde.OptionKillCooldown.GetFloat();
        CanVent = JekyllandHyde.OptionCanVent.GetBool();
        CanUseSabotage = JekyllandHyde.OptionCanUseSabotage.GetBool();
        KillCount = (int)JekyllandHyde.OptionKillMaxniums.GetFloat();
        ImpostorKilled = false;
    }
    bool skipSwapForThisMeeting;

    bool ImpostorKilled;
    private int KillCount;

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Mad;

    private static float KillCooldown;
    public static bool CanVent;
    public static bool CanUseSabotage;

    enum OptionName
    {
        HydeKillMaxniums
    }

    static void SetupOptionItem()
    {

    }
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanUseSabotage;
    public bool CanUseImpostorVentButton() => CanVent;
    public override void OnStartMeeting()
    {
        skipSwapForThisMeeting = JekyllandHyde.IsSpecialMeetingNoSwap();
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => true;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (KillCount <= 0)
        {
            info.DoKill = false;
        }
        else
        {
            var (killer, target) = info.AttemptTuple;
            --KillCount;
            info.DoKill = true;
            if (target.GetCustomRole().IsImpostor())
            {
                Player.RpcSetCustomRole(CustomRoles.HydeImp, log: null);
                ImpostorKilled = true;
            }
        }
    }
    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (skipSwapForThisMeeting)
        {
            skipSwapForThisMeeting = false;
            return;
        }
        skipSwapForThisMeeting = false;
        if (!ImpostorKilled)
        {
            Player.RpcSetCustomRole(CustomRoles.Jekyll, log: null);
        }
        else
        {
            Player.RpcSetCustomRole(CustomRoles.HydeImp, log: null);
        }
    }
}
public sealed class HydeImp : RoleBase, IImpostor, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(HydeImp),
            player => new HydeImp(player),
            CustomRoles.HydeImp,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            70670,
            SetupOptionItem,
            "hy",
            "#ff1919",
            (8, 2),
            from: From.TOR_GM_Haoming_Edition,
            countType: CountTypes.Impostor,
            assignInfo: new RoleAssignInfo(CustomRoles.HydeImp, CustomRoleTypes.Impostor)
            {
                IsInitiallyAssignableCallBack = () => false,
                AssignCountRule = new(0, 0, 1)
            },
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public HydeImp(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        KillCooldown = JekyllandHyde.OptionKillCooldown.GetFloat();
    }

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Mad;

    private static float KillCooldown;

    static void SetupOptionItem()
    {

    }
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => true;
}
