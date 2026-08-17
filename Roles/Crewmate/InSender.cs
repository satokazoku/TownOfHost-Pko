using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class InSender : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(InSender),
            player => new InSender(player),
            CustomRoles.InSender,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            32000,
            SetupOptionItem,
            "in",
            "#eee8aa",
            (5, 2),
            from: From.RevolutionaryHostRoles
        );
    public InSender(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Awakened = !OptAwakening.GetBool();
    }
    enum OptionName
    {
        BaitReportDelay, BaitMaxDelay
    }
    static OptionItem OptAwakening;
    static OptionItem OptAwakeningTaskcount;
    static OptionItem OptCanUseActiveComms;
    static OptionItem OptReportDelay;
    static OptionItem OptMaxDelay;
    bool Awakened;
    private static void SetupOptionItem()
    {
        OptCanUseActiveComms = BooleanOptionItem.Create(RoleInfo, 9, GeneralOption.CanUseActiveComms, true, false);
        OptReportDelay = FloatOptionItem.Create(RoleInfo, 12, OptionName.BaitReportDelay, new(0f, 180f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptMaxDelay = FloatOptionItem.Create(RoleInfo, 13, OptionName.BaitMaxDelay, new(0f, 180f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptAwakening = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.TaskAwakening, false, false);
        OptAwakeningTaskcount = IntegerOptionItem.Create(RoleInfo, 14, GeneralOption.AwakeningTaskcount, new(1, 255, 1), 5, false, OptAwakening);
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (!Awakened) return;
        var tien = 0f;
        //小数対応
        if (OptMaxDelay.GetFloat() > 0)
        {
            int ti = IRandom.Instance.Next(0, (int)OptMaxDelay.GetFloat() * 10);
            tien = ti * 0.1f;
            Logger.Info($"{tien}sの追加遅延発生!!", "InSender");
        }
        var (killer, target) = info.AttemptTuple;
        if (target.Is(CustomRoles.InSender) && !info.IsSuicide && !info.IsFakeSuicide && (OptCanUseActiveComms.GetBool() || !Utils.IsActive(SystemTypes.Comms)))
            _ = new LateTask(() => ReportDeadBodyPatch.ExReportDeadBody(target, target.Data, false), 0.15f + OptReportDelay.GetFloat() + tien, "InSender Self Report");
    }

    public override CustomRoles Misidentify() => Awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(OptAwakeningTaskcount.GetInt()))
        {
            if (Awakened == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            Awakened = true;
        }
        return true;
    }
}