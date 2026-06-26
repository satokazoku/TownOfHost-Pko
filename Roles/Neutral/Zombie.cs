using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;

namespace TownOfHost.Roles.Neutral;

public sealed class Zombie : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Zombie),
            player => new Zombie(player),
            CustomRoles.Zombie,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Neutral,
            55500,
            SetupOptionItem,
            "zb",
            "#6f8f4d",
            (5, 5),
            countType: CountTypes.Crew
        );

    static OptionItem OptionCanVent;
    static readonly HashSet<byte> PendingInfectionTargets = new();

    [GameModuleInitializer]
    public static void ResetRuntimeData()
    {
        PendingInfectionTargets.Clear();
    }

    public Zombie(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 8, defo: 15);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 9, GeneralOption.CanVent, true, false);
    }

    public override bool CanClickUseVentButton => OptionCanVent?.GetBool() ?? true;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => OptionCanVent?.GetBool() ?? true;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => OptionCanVent?.GetBool() ?? true;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (OptionCanVent?.GetBool() != true)
            AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost || info == null) return;
        if (info.IsSuicide || info.IsAccident) return;

        var killer = info.AttemptKiller;
        if (killer == null || killer.PlayerId == Player.PlayerId) return;
        QueueOrApplyInfection(killer);
    }

    public override void AfterMeetingTasks()
    {
        TryProcessPendingInfections();
    }

    static void QueueOrApplyInfection(PlayerControl killer)
    {
        if (killer == null || killer.Data == null || killer.Data.Disconnected) return;

        if (GameStates.CalledMeeting)
        {
            PendingInfectionTargets.Add(killer.PlayerId);
            return;
        }

        Infect(killer);
    }

    static void TryProcessPendingInfections()
    {
        if (!AmongUsClient.Instance.AmHost || PendingInfectionTargets.Count == 0) return;

        foreach (var playerId in PendingInfectionTargets.ToArray())
        {
            var killer = GetPlayerById(playerId);
            if (killer == null || killer.Data == null || killer.Data.Disconnected) continue;
            Infect(killer);
        }

        PendingInfectionTargets.Clear();
    }

    static void Infect(PlayerControl killer)
    {
        if (killer.GetCustomRole() is CustomRoles.GM or CustomRoles.Zombie) return;

        if (!Utils.RoleSendList.Contains(killer.PlayerId))
            Utils.RoleSendList.Add(killer.PlayerId);

        killer.RpcSetCustomRole(CustomRoles.Zombie, log: null);
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} -> Zombie", nameof(Zombie));
    }

    static bool HasAliveKillerSide()
    {
        foreach (var pc in AllAlivePlayerControls)
        {
            if (pc == null || pc.Data?.Disconnected == true) continue;
            if (pc.GetRoleClass() is not IKiller killer || !killer.IsKiller) continue;

            var roleType = pc.GetCustomRole().GetCustomRoleTypes();
            if (roleType is CustomRoleTypes.Impostor or CustomRoleTypes.Madmate or CustomRoleTypes.Neutral)
                return true;
        }

        return false;
    }

    public static bool TryTakeOverCrewWin(ref GameOverReason reason)
    {
        TryProcessPendingInfections();

        if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Crewmate) return false;
        if (HasAliveKillerSide()) return false;

        var aliveZombies = AllAlivePlayerControls
            .Where(pc => pc != null && pc.Is(CustomRoles.Zombie) && pc.Data?.Disconnected != true)
            .ToArray();
        if (aliveZombies.Length == 0) return false;

        var zombies = AllPlayerControls
            .Where(pc => pc != null && pc.Is(CustomRoles.Zombie) && pc.Data?.Disconnected != true)
            .ToArray();
        if (zombies.Length == 0) return false;

        if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Zombie, aliveZombies[0].PlayerId, true))
            return false;

        CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Zombie);
        foreach (var zombie in zombies)
        {
            CustomWinnerHolder.WinnerIds.Add(zombie.PlayerId);
            CustomWinnerHolder.NeutralWinnerIds.Add(zombie.PlayerId);
            CustomWinnerHolder.CantWinPlayerIds.Remove(zombie.PlayerId);
        }

        reason = GameOverReason.ImpostorsByKill;
        return true;
    }
}
