using AmongUs.GameOptions;
using System.Linq;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

public sealed class Workaholic : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Workaholic),
            player => new Workaholic(player),
            CustomRoles.Workaholic,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            55400,
            SetupOptionItem,
            "wh",
            "#008b8b",
            (5, 3),
            from: From.TownOfHost_Y,
            introSound: () => ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.FixWiring).FirstOrDefault().MinigamePrefab.OpenSound
        );
    public Workaholic(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        ventCooldown = OptionVentCooldown.GetFloat();
        CanWinAtDeath = OptionWinatDeath.GetBool();
    }
    private static OptionItem OptionCanVent;
    private static OptionItem OptionVentCooldown;
    private static OptionItem OptionWinatDeath;
    enum OptionName { WorkaholicCanWinAtDeath }
    private static bool CanWinAtDeath;
    private static float ventCooldown;
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 10, defo: 1);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.Cooldown, new(0f, 180f, 2.5f), 0f, false, OptionCanVent)
                .SetValueFormat(OptionFormat.Seconds);
        OptionWinatDeath = BooleanOptionItem.Create(RoleInfo, 13, OptionName.WorkaholicCanWinAtDeath, false, false);

        OverrideTasksData.Create(RoleInfo, 20);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = ventCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0;
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished && (CanWinAtDeath || Player.IsAlive()))
        {
            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Workaholic, Player.PlayerId, true))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            }
        }
        return true;
    }
    public override void OverrideProgressTextAsSeen(PlayerControl seer, ref bool enabled, ref string text)
    {
        seer ??= Player; //自視点/GMは変更なし
        if (!Player.IsAlive() && !CanWinAtDeath)
        {
            text = "";
        }

        if (Is(seer) || seer.Is(CustomRoles.GM)) return;
        text = $"(?/{MyTaskState.AllTasksCount})";
    }
}
