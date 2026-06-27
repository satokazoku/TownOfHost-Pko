using System;
using System.Linq;
using HarmonyLib;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Neutral;

namespace TownOfHost;

public abstract class GameEndPredicate
{
    /// <summary>ゲームの終了条件をチェックし、CustomWinnerHolderに値を格納します。</summary>
    /// <params name="reason">バニラのゲーム終了処理に使用するGameOverReason</params>
    /// <returns>ゲーム終了の条件を満たしているかどうか</returns>
    public abstract bool CheckForEndGame(out GameOverReason reason);

    /// <summary>GameData.TotalTasksとCompletedTasksをもとにタスク勝利が可能かを判定します。</summary>
    public virtual bool CheckGameEndByTask(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0 || Fox.BlockTaskWin()) return false;

        if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
        {
            reason = GameOverReason.CrewmatesByTask;
            CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Crewmate, byte.MaxValue);
            return true;
        }
        return false;
    }

    /// <summary>ShipStatus.Systems内の要素をもとにサボタージュ勝利が可能かを判定します。</summary>
    public virtual bool CheckGameEndBySabotage(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (ShipStatus.Instance.Systems == null || GameStates.IsMeeting) return false;

        // TryGetValueは使用不可
        var systems = ShipStatus.Instance.Systems;
        if (systems.ContainsKey(SystemTypes.LifeSupp)
            && systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>() is { Countdown: < 0f } lifeSupp)
        {
            SetSabotageWinner();
            Main.IsActiveSabotage = false;
            reason = GameOverReason.ImpostorsBySabotage;
            lifeSupp.Countdown = 10000f;
            return true;
        }

        ISystemType system = null;
        if (systems.ContainsKey(SystemTypes.Reactor)) system = systems[SystemTypes.Reactor];
        else if (systems.ContainsKey(SystemTypes.Laboratory)) system = systems[SystemTypes.Laboratory];
        else if (systems.ContainsKey(SystemTypes.HeliSabotage)) system = systems[SystemTypes.HeliSabotage];

        if (system?.TryCast<ICriticalSabotage>() is not { Countdown: < 0f } critical) return false;

        if (Options.CurrentGameMode is CustomGameMode.SuddenDeath or CustomGameMode.MurderMystery)
        {
            PlayerCatch.AllAlivePlayerControls.Do(player => player.RpcMurderPlayerV2(player));
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
        }
        else
        {
            SetSabotageWinner();
        }

        Main.IsActiveSabotage = false;
        reason = GameOverReason.ImpostorsBySabotage;
        critical.ClearSabotage();
        return true;
    }

    private static void SetSabotageWinner()
    {
        if (!Options.ChangeSabotageWinRole.GetBool())
        {
            SetImpostorWinner();
            return;
        }

        var saboteur = PlayerCatch.GetPlayerById(Main.LastSab);
        if (saboteur == null || !saboteur.CanUseSabotageButton())
        {
            SetImpostorWinner();
            return;
        }

        SetSaboteurFactionWinner(saboteur);
    }

    private static void SetSaboteurFactionWinner(PlayerControl saboteur)
    {
        var role = saboteur.GetCustomRole();
        var countType = saboteur.GetCountTypes();
        CustomWinner winner;
        Func<PlayerControl, bool> isFactionMember;

        if (role is CustomRoles.Egoist)
        {
            winner = CustomWinner.Egoist;
            isFactionMember = player => player.Is(CustomRoles.Egoist);
        }
        else if (role is CustomRoles.MadBetrayer)
        {
            winner = CustomWinner.MadBetrayer;
            isFactionMember = player => player.Is(CustomRoles.MadBetrayer);
        }
        else if (saboteur.Is(CustomRoleTypes.Impostor) || saboteur.Is(CustomRoleTypes.Madmate))
        {
            winner = CustomWinner.Impostor;
            isFactionMember = player =>
                player.Is(CustomRoleTypes.Impostor)
                || player.Is(CustomRoleTypes.Madmate)
                || player.Is(CustomRoles.SKMadmate);
        }
        else
        {
            winner = countType switch
            {
                CountTypes.Jackal => CustomWinner.Jackal,
                CountTypes.Remotekiller => CustomWinner.Remotekiller,
                CountTypes.GrimReaper => CustomWinner.GrimReaper,
                CountTypes.Fox => CustomWinner.Fox,
                CountTypes.MilkyWay => CustomWinner.MilkyWay,
                CountTypes.Pavlov => CustomWinner.Pavlov,
                CountTypes.Eater => CustomWinner.Eater,
                CountTypes.Monika => CustomWinner.Monika,
                CountTypes.StandMaster => CustomWinner.StandMaster,
                CountTypes.Villain => CustomWinner.Villain,
                _ => (CustomWinner)role,
            };
            isFactionMember = countType is CountTypes.OutOfGame or CountTypes.None or CountTypes.Crew
                ? player => player.GetCustomRole() == role
                : player => player.GetCountTypes() == countType;
        }

        CustomWinnerHolder.ResetAndSetWinner(winner);
        PlayerCatch.AllPlayerControls
            .Where(isFactionMember)
            .Do(player => CustomWinnerHolder.WinnerIds.Add(player.PlayerId));
    }

    private static void SetImpostorWinner()
    {
        CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Impostor, byte.MaxValue);
    }
}
