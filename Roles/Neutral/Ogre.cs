using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Neutral;

public sealed class Ogre : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Ogre),
            player => new Ogre(player),
            CustomRoles.Ogre,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            55600,
            SetupOptionItem,
            "og",
            "#fe8b04",
            (4, 6),
            true,
            countType: CountTypes.Crew,
            from: From.TownOfHost_Y
        );

    public Ogre(PlayerControl player) : base(RoleInfo, player, () => HasTask.False)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        HasImpostorVision = OptionHasImpostorVision.GetBool();
        CanVentOpt = OptionCanVent.GetBool();
        KillSuccessRate = OptionKillSuccessRate.GetInt();
        KilledGuardRate = OptionKilledGuardRate.GetInt();
        nowKillRate = 100;
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionCanVent;
    static OptionItem OptionKillSuccessRate;
    static OptionItem OptionKilledGuardRate;

    static float KillCooldown;
    static bool HasImpostorVision;
    static bool CanVentOpt;
    static int KillSuccessRate;
    static int KilledGuardRate;

    int nowKillRate;

    enum OptionName
    {
        OgreKillSuccessRate,
        OgreKilledGuardRate,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(2.5f, 180f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.ImpostorVision, false, false);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, false, false);
        OptionKillSuccessRate = IntegerOptionItem.Create(RoleInfo, 13, OptionName.OgreKillSuccessRate,
            new(5, 100, 5), 20, false).SetValueFormat(OptionFormat.Percent);
        OptionKilledGuardRate = IntegerOptionItem.Create(RoleInfo, 14, OptionName.OgreKilledGuardRate,
            new(5, 100, 5), 30, false).SetValueFormat(OptionFormat.Percent);
    }

    public override void Add()
    {
        nowKillRate = 100;
        SendRpc();
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => CanVentOpt;

    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision);

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        (var killer, var target) = info.AttemptTuple;

        var chance = IRandom.Instance.Next(100);
        if (chance >= nowKillRate)
        {
            info.DoKill = false;
            killer.RpcProtectedMurderPlayer(target);
            killer.ResetKillCooldown();
            Logger.Info($"[Ogre] キル失敗 (確率:{nowKillRate}%)", "Ogre");
            return;
        }

        nowKillRate = nowKillRate * KillSuccessRate / 100;
        if (nowKillRate < 1) nowKillRate = 1;
        Logger.Info($"[Ogre] キル成功 → 次回 {nowKillRate}%", "Ogre");
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        (var killer, var target) = info.AttemptTuple;

        if (killer.GetCustomRole() == CustomRoles.Tairou) return true;

        var chance = IRandom.Instance.Next(100);
        if (chance >= KilledGuardRate) return true;

        killer.RpcProtectedMurderPlayer(target);
        target.RpcProtectedMurderPlayer(target);
        killer.ResetKillCooldown();
        target.ResetKillCooldown();

        if (!killer.GetCustomRole().IsImpostor())
        {
            var state = PlayerState.GetByPlayerId(killer.PlayerId);
            if (state != null) state.DeathReason = CustomDeathReason.Counter;
            killer.SetRealKiller(target);
            target.RpcMurderPlayer(killer);
        }

        info.DoKill = false;
        return true;
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
        => Player.IsAlive()
        && PlayerCatch.AllAlivePlayerControls.Any(pc => pc.GetCustomRole().IsImpostor());

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => ColorString(RoleInfo.RoleColor, $"[{nowKillRate}%]");

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(nowKillRate);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        nowKillRate = reader.ReadInt32();
    }

    public override void AfterMeetingTasks()
    {
    }
}