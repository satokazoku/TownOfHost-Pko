using System.Linq;
using UnityEngine;
using Hazel;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class God : RoleBase, ISystemTypeUpdateHook, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(God),
            player => new God(player),
            CustomRoles.God,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            51700,
            SetupOptionItem,
            "god",
            "#ffd700",
            (6, 1),
            false,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.SuperNewRoles
        );

    public God(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
    }

    static OptionItem Bakuro;

    public static OptionItem SeeVotesOpt;
    public static OptionItem CanSeeDeathReasonOpt;
    public static OptionItem RequireTasksToWinOpt;
    public static OptionItem TaskCountOpt;

    public static OptionItem CantFixReactorOpt;
    public static OptionItem CantFixLightsOutOpt;
    public static OptionItem CantFixHeliOpt;
    public static OptionItem CantFixCommsOpt;

    enum OptionName
    {
        GodBakuro,
        GodSeeVotes,
        GodCanSeeDeathReason,
        GodRequireTasksToWin,
        GodOverrideTaskCount,
        GodTaskCount,
        GodCantFixReactor,
        GodCantFixLightsOut,
        GodCantFixHeli,
        GodCantFixComms,
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 7, defo: 5);
        OverrideTasksData.Create(RoleInfo, 20);

        Bakuro = BooleanOptionItem.Create(RoleInfo, 18, OptionName.GodBakuro, false, false)
            .SetParentRole(CustomRoles.God); //本当にキックする処理も暴露を検知する処理もありません。プレイヤーにルールを守らすために見せかけで作りました。口外しないでいただけると助かります。

        SeeVotesOpt = BooleanOptionItem.Create(RoleInfo, 10, OptionName.GodSeeVotes, true, false)
            .SetParentRole(CustomRoles.God);

        CanSeeDeathReasonOpt = BooleanOptionItem.Create(RoleInfo, 11, OptionName.GodCanSeeDeathReason, true, false)
            .SetParentRole(CustomRoles.God);

        RequireTasksToWinOpt = BooleanOptionItem.Create(RoleInfo, 12, OptionName.GodRequireTasksToWin, false, false)
            .SetParentRole(CustomRoles.God);

        TaskCountOpt = IntegerOptionItem.Create(RoleInfo, 13, OptionName.GodTaskCount, new(0, 999, 1), 0, false)
            .SetParentRole(CustomRoles.God);

        CantFixReactorOpt = BooleanOptionItem.Create(RoleInfo, 14, OptionName.GodCantFixReactor, false, false)
            .SetParentRole(CustomRoles.God);

        CantFixLightsOutOpt = BooleanOptionItem.Create(RoleInfo, 15, OptionName.GodCantFixLightsOut, false, false)
            .SetParentRole(CustomRoles.God);

        CantFixHeliOpt = BooleanOptionItem.Create(RoleInfo, 16, OptionName.GodCantFixHeli, false, false)
            .SetParentRole(CustomRoles.God);

        CantFixCommsOpt = BooleanOptionItem.Create(RoleInfo, 17, OptionName.GodCantFixComms, false, false)
            .SetParentRole(CustomRoles.God);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (SeeVotesOpt?.GetBool() == true)
        {
            opt.SetBool(BoolOptionNames.AnonymousVotes, false);
        }
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!Player.IsAlive()) return;
        enabled = true;
        roleText = UtilsRoleText.GetTrueRoleName(seen.PlayerId, true);
        addon = true;
    }

    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
    }

    public override void CheckWinner(GameOverReason reason)
    {
        base.CheckWinner(reason);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (RequireTasksToWinOpt?.GetBool() == true)
        {
            if (!UtilsTask.HasTasks(Player.Data))
            {
                Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : タスクが割り当てられていないため勝利条件を満たさない (God)", nameof(God));
                return;
            }

            var required = TaskCountOpt?.GetInt() ?? 0;
            if (required > 0)
            {
                if (MyTaskState.CompletedTasksCount < required)
                {
                    Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : 必要タスク数({required})未達のため勝利条件を満たさない (God)", nameof(God));
                    return;
                }
            }
            else
            {
                if (!MyTaskState.IsTaskFinished)
                {
                    Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : タスク未完了のため勝利条件を満たさない (God)", nameof(God));
                    return;
                }
            }
        }

        CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.God, Player.PlayerId, AddWin: false, hantrole: CustomRoles.God);
        Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : 単独勝利宣言 (God)", nameof(God));
    }

    public bool? CheckSeeDeathReason(PlayerControl seen)
    {
        if (CanSeeDeathReasonOpt?.GetBool() == true)
        {
            return true;
        }
        return null;
    }

    public bool UpdateReactorSystem(ReactorSystemType reactorSystem, byte amount)
    {
        if (CantFixReactorOpt?.GetBool() == true && Player.IsAlive()) return false;
        return true;
    }

    public bool UpdateHeliSabotageSystem(HeliSabotageSystem heliSabotageSystem, byte amount)
    {
        if (CantFixHeliOpt?.GetBool() == true && Player.IsAlive()) return false;
        return true;
    }

    public bool UpdateLifeSuppSystem(LifeSuppSystemType lifeSuppSystem, byte amount)
    {
        return true;
    }

    public bool UpdateHudOverrideSystem(HudOverrideSystemType hudOverrideSystem, byte amount)
    {
        if (CantFixCommsOpt?.GetBool() == true && Player.IsAlive()) return false;
        return true;
    }

    public bool UpdateHqHudSystem(HqHudSystemType hqHudSystemType, byte amount)
    {
        if (CantFixCommsOpt?.GetBool() == true && Player.IsAlive()) return false;
        return true;
    }

    public bool UpdateSwitchSystem(SwitchSystem switchSystem, byte amount)
    {
        if (CantFixLightsOutOpt?.GetBool() == true && Player.IsAlive()) return false;
        return true;
    }

    public bool UpdateDoorsSystem(DoorsSystemType doorsSystem, byte amount)
    {
        return true;
    }
}