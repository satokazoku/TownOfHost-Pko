using System.Linq;
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

    static float KillCooldown => OptionKillCooldown?.GetFloat() ?? 25f;
    static int RequiredLoversKills => OptionRequiredLoversKills?.GetInt() ?? 1;
    static bool CanWinAtDeath => OptionCanWinAtDeath?.GetBool() ?? false;

    enum OptionName
    {
        LoversBreakerRequiredLoversKills,
        LoversBreakerCanWinAtDeath
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

        if (IsBreakableLover(target)) return;

        info.DoKill = false;
        PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Misfire;
        info.AttemptKiller.RpcMurderPlayer(info.AttemptKiller);
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        if (!IsBreakableLover(info.AttemptTarget)) return;

        loversKillCount++;
        hadTargetLovers = true;
        SendRPC();
    }

    public override void CheckWinner(GameOverReason reason)
    {
        RefreshTargetLoversState();
        if (!CanWinAtDeath && !Player.IsAlive()) return;
        if (!CanSoloWinNow()) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.LoversBreaker, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({loversKillCount}/{RequiredLoversKills})");

    bool CanSoloWinNow()
    {
        if (loversKillCount < RequiredLoversKills) return false;
        if (!hadTargetLovers) return false;
        return !PlayerCatch.AllAlivePlayerControls.Any(IsBreakableLover);
    }

    void RefreshTargetLoversState()
    {
        if (hadTargetLovers) return;
        hadTargetLovers = PlayerCatch.AllPlayerControls.Any(IsBreakableLover);
    }

    static bool IsBreakableLover(PlayerControl target)
        => target != null && target.IsLovers(checkonelover: false);

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
