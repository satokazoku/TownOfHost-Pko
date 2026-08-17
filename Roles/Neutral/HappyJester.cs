using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;
public sealed class HappyJester : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(HappyJester),
            player => new HappyJester(player),
            CustomRoles.HappyJester,
            () => CanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            552700,
            SetupOptionItem,
            "hj",
            "#ffb6c1",
            (4, 0),
            true,
            assignInfo: new RoleAssignInfo(CustomRoles.HappyJester, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(0, 15, 1)
            },
            from: From.TownOfHost_Pko
        );

    public HappyJester(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        requireTask = OptionRequireTask.GetBool();
    }

    static OptionItem CanUseVent;
    static OptionItem CanVentMove;
    static OptionItem OptionRequireTask;
    static bool requireTask;

    enum Option
    {
        MadmateCanMovedByVent,
        HappyJesterRequireTask,
    }

    private static void SetupOptionItem()
    {
        CanUseVent = BooleanOptionItem.Create(RoleInfo, 6, GeneralOption.CanVent, false, false);
        CanVentMove = BooleanOptionItem.Create(RoleInfo, 7, Option.MadmateCanMovedByVent, false, false, CanUseVent);
        OptionRequireTask = BooleanOptionItem.Create(RoleInfo, 10, Option.HappyJesterRequireTask, false, false);
        OverrideTasksData.Create(RoleInfo, 11);
    }

    public bool CanUseImpostorVentButton() => false;
    public override bool CanClickUseVentButton => CanUseVent.GetBool();
    public override bool CanUseAbilityButton() => false;
    public bool CanUseSabotageButton() => false;
    public override bool OnInvokeSabotage(SystemTypes systemType) => false;
    public bool CanKill { get; private set; } = false;
    public bool CanUseKillButton() => false;
    float IKiller.CalculateKillCooldown() => 0f;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
        opt.SetVision(false);
    }

    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVentMove.GetBool();

    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished) Player.MarkDirtySettings();
        return true;
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost || Player.PlayerId != exiled.PlayerId) return;

        if (requireTask && !IsTaskFinished) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jester, Player.PlayerId))
        {
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);

            foreach (var pc in PlayerCatch.AllPlayerControls)
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }

        DecidedWinner = true;
    }
}