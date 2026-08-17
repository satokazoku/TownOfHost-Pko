using AmongUs.GameOptions;
using System.Linq;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceWorkaholic : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceWorkaholic),
            player => new NiceWorkaholic(player),
            CustomRoles.NiceWorkaholic,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            33700,
            SetupOptionItem,
            "nwh",
            "#008b8b",
            (1, 9),
            introSound: () => ShipStatus.Instance?.CommonTasks
                .FirstOrDefault(t => t.TaskType == TaskTypes.FixWiring)
                ?.MinigamePrefab.OpenSound
        );

    public NiceWorkaholic(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        ventCooldown = OptionVentCooldown.GetFloat();
        CanWinAtDeath = OptionWinAtDeath.GetBool();
    }

    private static OptionItem OptionCanVent;
    private static OptionItem OptionVentCooldown;
    private static OptionItem OptionWinAtDeath;

    enum OptionName { NiceWorkaholicCanWinAtDeath }

    private static bool CanWinAtDeath;
    private static float ventCooldown;

    private static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.Cooldown,
            new(0f, 180f, 2.5f), 0f, false, OptionCanVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionWinAtDeath = BooleanOptionItem.Create(RoleInfo, 13, OptionName.NiceWorkaholicCanWinAtDeath, false, false);
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = ventCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0;
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!IsTaskFinished) return true;
        if (!CanWinAtDeath && !Player.IsAlive()) return true;

        CustomWinnerHolder.WinnerTeam = CustomWinner.Crewmate;
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.Is(CustomRoleTypes.Crewmate))
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }

        UtilsGameLog.AddGameLog("NiceWorkaholic",
            $"{UtilsName.GetPlayerColor(Player)} が全タスク完了！クルー陣営勝利！");

        GameManager.Instance.enabled = false;
        _ = new LateTask(() =>
            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false),
            0.5f, "NiceWorkaholic.EndGame", true);

        return true;
    }

    public override void OverrideProgressTextAsSeen(PlayerControl seer, ref bool enabled, ref string text)
    {
        seer ??= Player;
        if (!Player.IsAlive() && !CanWinAtDeath)
            text = "";
        if (Is(seer) || seer.Is(CustomRoles.GM)) return;
        text = $"(?/{MyTaskState.AllTasksCount})";
    }
}