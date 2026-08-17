using System.Linq;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

public sealed class LoversBreaker : RoleBase, ILNKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(LoversBreaker),
            player => new LoversBreaker(player),
            CustomRoles.LoversBreaker,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            52700,
            SetupOptionItem,
            "lb",
            "#ff66cc",
            (5, 4),
            true,
            from: From.SuperNewRoles
        );

    static OptionItem OptionKillCooldown;
    static OptionItem OptionRequiredLoversKills;
    static OptionItem OptionCanWinAtDeath;
    static OptionItem OptionOnlyAssignWithLoversRole;

    static float KillCooldown => OptionKillCooldown?.GetFloat() ?? 25f;
    static int RequiredLoversKills => OptionRequiredLoversKills?.GetInt() ?? 1;
    static bool CanWinAtDeath => OptionCanWinAtDeath?.GetBool() ?? false;
    public static bool IsTanabataEventActive => Event.Tanabata;

    enum OptionName
    {
        LoversBreakerRequiredLoversKills,
        LoversBreakerCanWinAtDeath,
        LoversBreakerOnlyAssignWithLoversRole
    }

    int loversKillCount;
    bool hadTargetLovers;

    public LoversBreaker(PlayerControl player)
        : base(RoleInfo, player)
    {
        loversKillCount = 0;
        hadTargetLovers = false;
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 10, defo: 1);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.KillCooldown, new(2.5f, 60f, 2.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRequiredLoversKills = IntegerOptionItem.Create(RoleInfo, 12, OptionName.LoversBreakerRequiredLoversKills, new(1, 10, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanWinAtDeath = BooleanOptionItem.Create(RoleInfo, 13, OptionName.LoversBreakerCanWinAtDeath, false, false);
        OptionOnlyAssignWithLoversRole = BooleanOptionItem.Create(RoleInfo, 14, OptionName.LoversBreakerOnlyAssignWithLoversRole, true, false);
    }

    public override void Add()
    {
        RefreshTargetLoversState();
        SendRPC();
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        RefreshTargetLoversState();
        var target = info.AttemptTarget;

        if (CanKillTarget(target)) return;

        info.DoKill = false;
        PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Misfire;
        info.AttemptKiller.RpcMurderPlayer(info.AttemptKiller);
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        if (!CountsAsLoversKill(info.AttemptTarget)) return;

        loversKillCount++;
        hadTargetLovers = true;
        SendRPC();
    }

    public override void CheckWinner(GameOverReason reason)
        => TryWinNow();

    public bool TryWinNow()
    {
        RefreshTargetLoversState();
        if (!CanWinAtDeath && !Player.IsAlive()) return false;
        if (!CanSoloWinNow()) return false;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.LoversBreaker, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            return true;
        }

        return false;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({loversKillCount}/{RequiredLoversKills})");

    bool CanSoloWinNow()
    {
        if (loversKillCount < RequiredLoversKills) return false;
        if (!hadTargetLovers) return false;
        return !PlayerCatch.AllAlivePlayerControls.Any(IsVictoryTarget);
    }

    void RefreshTargetLoversState()
    {
        if (hadTargetLovers) return;
        hadTargetLovers = PlayerCatch.AllPlayerControls.Any(IsVictoryTarget);
    }

    static bool IsBreakableLover(PlayerControl target)
        => target != null && target.IsLovers(checkonelover: false);

    static bool IsTanabataCouple(PlayerControl target)
        => IsTanabataEventActive
            && target?.GetCustomRole() is CustomRoles.Vega or CustomRoles.Altair;

    static bool IsVictoryTarget(PlayerControl target)
        => IsBreakableLover(target) || IsTanabataCouple(target);

    static bool CanKillTarget(PlayerControl target)
        => IsVictoryTarget(target)
            || target?.Is(CustomRoles.Madonna) == true
            || target?.Is(CustomRoles.Cupid) == true;

    static bool CountsAsLoversKill(PlayerControl target)
    {
        if (target == null) return false;
        if (target.Is(CustomRoles.Cupid)) return false;
        if (target.Is(CustomRoles.Madonna)) return target.Is(CustomRoles.MadonnaLovers);
        return IsVictoryTarget(target);
    }

    public static bool ShouldRemoveFromAssignment(IEnumerable<CustomRoles> assignedRoles)
    {
        if (OptionOnlyAssignWithLoversRole?.GetBool() != true) return false;

        return !assignedRoles.Any(role =>
            role is CustomRoles.Lovers
                or CustomRoles.RedLovers
                or CustomRoles.YellowLovers
                or CustomRoles.BlueLovers
                or CustomRoles.GreenLovers
                or CustomRoles.WhiteLovers
                or CustomRoles.PurpleLovers
                or CustomRoles.Madonna
                or CustomRoles.Cupid
            || (IsTanabataEventActive && (role is CustomRoles.Vega or CustomRoles.Altair)));
    }

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPCType.SyncState);
        sender.Writer.Write(loversKillCount);
        sender.Writer.Write(hadTargetLovers);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        switch ((RPCType)reader.ReadPackedInt32())
        {
            case RPCType.SyncState:
                loversKillCount = reader.ReadInt32();
                hadTargetLovers = reader.ReadBoolean();
                break;
        }
    }

    enum RPCType
    {
        SyncState
    }
}
