using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

public sealed class Jester : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jester),
            player => new Jester(player),
            CustomRoles.Jester,
            () => CanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            52600,
            SetupOptionItem,
            "je",
            "#ec62a5",
            (4, 0),
            true,
            assignInfo: new RoleAssignInfo(CustomRoles.Jester, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(0, 15, 1)
            },
            from: From.Jester
        );

    public Jester(PlayerControl player)
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
        JesterRequireTask,
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 8, defo: 1);
        CanUseVent = BooleanOptionItem.Create(RoleInfo, 6, GeneralOption.CanVent, false, false);
        CanVentMove = BooleanOptionItem.Create(RoleInfo, 7, Option.MadmateCanMovedByVent, false, false, CanUseVent);
        // ★ タスク完了を勝利条件にするか
        OptionRequireTask = BooleanOptionItem.Create(RoleInfo, 10, Option.JesterRequireTask, false, false);
        // ★ タスク数独自設定（MadJesterと同様）
        OverrideTasksData.Create(RoleInfo, 11);
    }

    public bool CanUseImpostorVentButton() => false;
    // ★ EngineerのベントボタンをそのままCanUseVentで制御
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

    // ★ タスク完了でDirtySettings（MadJesterと同様）
    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished)
            Player.MarkDirtySettings();
        return true;
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost || Player.PlayerId != exiled.PlayerId) return;

        // ★ タスク必要オンのときはタスク未完了なら勝利しない
        if (requireTask && !IsTaskFinished) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jester, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
        Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        if (10 <= UtilsGameLog.LastLogRole.Count && PlayerCatch.AllAlivePlayersCount <= 3)
            DecidedWinner = true;
    }

    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var sp1 = new Achievement(RoleInfo, 1, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, sp1);
    }
}
