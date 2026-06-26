using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Roles.Impostor.Driver;

namespace TownOfHost.Roles.Madmate;

public sealed class Braid : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Braid),
            player => new Braid(player),
            CustomRoles.Braid,
            () => OptionBraidCanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            76300,
            null,
            "br",
            OptionSort: (0, 1),
            tab: TabGroup.Combinations,
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            combination: CombinationRoles.DriverandBraid
        );
    public Braid(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
        BraidCanSeeDriver = OptionBraidCanSeeDriver.GetBool();
        DriverCanSeeBraid = OptionDriverCanSeeBraid.GetBool();
        TaskFin = false;
        DriverseeKillFlash = false;
        Driverseedeathreason = false;
        DriverseeVote = false;
        GivedGuard = false;
        HasGuard = true;
        KillflashtaskTrigger = OptionGiveKillFlashtaskTrigger.GetInt();
        DeathreasontaskTrigger = OptionGiveKnowDeathreasontaskTrigger.GetInt();
        GuardtaskTrigger = OptionGiveGuardtaskTrigger.GetInt();
        WatchVotetaskTrigger = OptionGiveWatchVotestaskTrigger.GetInt();
        BraidKillCooldown = OptionBraidKillCooldown.GetFloat();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public static OverrideTasksData Tasks;
    public static bool TaskFin;
    public static bool DriverseeKillFlash;
    public static bool Driverseedeathreason;
    public static bool DriverseeVote;
    public static bool GivedGuard;
    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    public static bool BraidCanSeeDriver;
    public static bool DriverCanSeeBraid;
    public static int KillflashtaskTrigger;
    public static int DeathreasontaskTrigger;
    public static int GuardtaskTrigger;
    public static int WatchVotetaskTrigger;
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
    public override bool OnCompleteTask(uint taskid)
    {
        if (KillflashtaskTrigger <= MyTaskState.CompletedTasksCount && OptionGiveKillFlash.GetBool())
        {
            DriverseeKillFlash = true;
            Logger.Info("キルフラの能力を付与。", "Braid");
        }
        if (DeathreasontaskTrigger <= MyTaskState.CompletedTasksCount && OptionGiveKnowDeathreason.GetBool())
        {
            Driverseedeathreason = true;
            Logger.Info("死因の能力を付与。", "Braid");
        }
        if (GuardtaskTrigger <= MyTaskState.CompletedTasksCount && OptionGiveGuard.GetBool())
        {
            GivedGuard = true;
            Logger.Info("ガードを付与。", "Braid");
        }
        if (WatchVotetaskTrigger <= MyTaskState.CompletedTasksCount && OptionGiveWatchVotes.GetBool())
        {
            DriverseeVote = true;
            Logger.Info("匿名投票を解除。", "Braid");
        }
        if (IsTaskFinished)
        {
            TaskFin = true;
            Logger.Info("キルクール軽減。", "Braid");
        }
        return true;
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;
        if (seer.Is(CustomRoles.Braid) && seen.Is(CustomRoles.Driver) && BraidCanSeeDriver) return Utils.ColorString(RoleInfo.RoleColor, "☆");
        else return "";
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.Is(CustomRoles.Driver) && seen.Is(CustomRoles.Braid) && DriverCanSeeBraid) return Utils.ColorString(RoleInfo.RoleColor, "☆");
        else return "";
    }
}
