using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static TownOfHost.Roles.Crewmate.AllArounder;

namespace TownOfHost.Roles.Neutral;

public sealed class Frust : RoleBase, ILNKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Frust),
            player => new Frust(player),
            CustomRoles.Frust,
            () => BeforeCanVent ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            58100,
            SetupOptionItem,
            "Frt",
            "#ff8c00",
            (2, 8),
            true
        );
    public Frust(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCoolDown.GetFloat();
        BeforeCanVent = OptionBeforeCanVent.GetBool();
        AfterCanVent = OptionAfterCanVent.GetBool();
        CanSabotage = OptionCanSabotage.GetBool();
        IsKilled = false;
        IsWin = false;
        KillCount = 0;
    }
    static OptionItem OptionKillCoolDown;
    static OptionItem OptionBeforeCanVent;
    static OptionItem OptionAfterCanVent;
    static OptionItem OptionCanSabotage;
    static bool BeforeCanVent;
    static bool AfterCanVent;
    static float KillCooldown;
    static bool CanSabotage;
    public bool IsKilled;
    static bool IsWin;
    enum Op
    {
        FrustBeforeCanVent,
        FrustAfterCanVent,
        FrustCanSabotage
    }
    public override void Add()
    {
        KillCount = 0;
        IsKilled = false;
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9);
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionBeforeCanVent = BooleanOptionItem.Create(RoleInfo, 11, Op.FrustBeforeCanVent, false, false);
        OptionAfterCanVent = BooleanOptionItem.Create(RoleInfo, 12, Op.FrustAfterCanVent, true, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 13, Op.FrustCanSabotage, true, false);
    }
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Frust;
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanSabotage && IsKilled;
    public bool CanUseImpostorVentButton() => AfterCanVent && IsKilled;
    public override bool CanUseAbilityButton() => BeforeCanVent && !IsKilled;
    public bool CanKill => IsKilled;
    public int KillCount;
    void SetRoleForFrustClient(PlayerControl target, RoleTypes role, int clientId)
    {
        if (target == null || !target.IsAlive()) return;

        if (target == PlayerControl.LocalPlayer && Is(PlayerControl.LocalPlayer))
        {
            RoleManager.Instance.SetRole(target, role);
        }
        target.RpcSetRoleDesync(role, clientId);
    }
    public override bool OnCheckMurderAsTargetAfter(MurderInfo info)
    {
        if (!info.DoKill) return false;
        if (IsKilled)
        {
            if (KillCount < 1) return true;
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
            return true;
        }
        var (kille, tar) = info.AttemptTuple;
        //コネクトセーバーとかリミッター弾いちゃうので...
        info.GuardPower = 9;
        IsKilled = true;
        SendRpc();
        SetRoleForFrustClient(Player, RoleTypes.Impostor, Player.GetClientId());
        Player.SetKillCooldown(KillCooldown);
        if (info.KillPower <= 9)
        {
            Utils.AllPlayerKillFlash();
            if (CustomRoleManager.OnCheckMurder(tar, kille, tar, kille, Killpower: 10, PlayKillSound: true))
            {
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            }
        }
        return true;
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        KillCount++;
        SendRpc();
    }
    public void FrustCheckWin(ref GameOverReason reason)
    {
        if (!Player.IsAlive() || IsKilled) return;

        if (reason is GameOverReason.CrewmatesByTask && CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate)
        {
            return;
        }
        else
        {
            if (IsWin && CustomWinnerHolder.WinnerTeam is CustomWinner.Frust)
            {
                CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
                return;
            }
            else
            {
                if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Frust, Player.PlayerId))
                {
                    IsWin = true;
                    CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
                    reason = GameOverReason.ImpostorsByKill;
                    return;
                }
            }
        }
        return;
    }
    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsKilled);
        sender.Writer.Write(KillCount);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        IsKilled = reader.ReadBoolean();
        KillCount = reader.ReadInt32();
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var kl = new Achievement(RoleInfo, 0, 1, 0, 1);
        var wi = new Achievement(RoleInfo, 1, 1, 0, 2);
        var akl = new Achievement(RoleInfo, 2, 1, 0, 3);

        achievements.Add(0, kl);
        achievements.Add(1, wi);
        achievements.Add(2, akl);
    }
}