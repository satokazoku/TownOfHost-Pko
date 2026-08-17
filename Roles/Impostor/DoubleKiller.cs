using AmongUs.GameOptions;
using Epic.OnlineServices.Presence;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor;

public sealed class DoubleKiller : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(DoubleKiller),
            player => new DoubleKiller(player),
            CustomRoles.DoubleKiller,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            3400,
            SetUpOptionItem,
            "dk",
            OptionSort: (3, 15),
            from: From.SuperNewRoles
        );

    public DoubleKiller(PlayerControl player)
        : base(RoleInfo, player)
    {
        PhantomCooldown = OptionPhantomCooldown.GetFloat();
        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
        CanSabotage = OptionCanSabotage.GetBool();
        usedPhantomCount = 0;
        CanSubkill = true;
    }

    static OptionItem OptionPhantomCooldown;
    static float PhantomCooldown;
    static OptionItem OptionKillCooldown;
    static float KillCooldown;
    static OptionItem OptionCanVent;
    static bool CanVent;
    static OptionItem OptionCanSabotage;
    static bool CanSabotage;
    static OptionItem OptionPhantomUsageCount;
    int usedPhantomCount;

    enum OptionName
    {
        DoubleKillerPhantomCooldown,
        DoubleKillerKillCooldown,
        DoubleKillerCanVent,
        DoubleKillerCanSabotage,
        DoubleKillerPhantomUsageCount,
    }

    static void SetUpOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.DoubleKillerKillCooldown, new(0.5f, 60f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.DoubleKillerPhantomCooldown, new(0.5f, 60f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, OptionName.DoubleKillerCanVent, true, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 13, OptionName.DoubleKillerCanSabotage, true, false);
        OptionPhantomUsageCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.DoubleKillerPhantomUsageCount, new(1, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanSabotage;
    public bool CanUseImpostorVentButton() => CanVent;

    public bool CanSubkill;

    public override bool CanClickUseVentButton => CanVent;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => CanVent;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVent;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (usedPhantomCount < OptionPhantomUsageCount.GetInt())
            AURoleOptions.PhantomCooldown = PhantomCooldown;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!CanSubkill) //サブキルが押せないバグがあったのでこれで様子見かな。
        {
            if (PhantomCooldown < 1f) //キルク1未満でも一秒待たない。
            {
                _ = new LateTask(() =>
                {
                    if (!CanSubkill)
                    {
                        CanSubkill = true;
                    }
                }, PhantomCooldown, "", true);
            }
            else
            {
                _ = new LateTask(() =>
                {
                    if (!CanSubkill)
                    {
                        CanSubkill = true;
                    }
                }, 1f, "", true);
            }
        }
    }

    bool IUsePhantomButton.IsPhantomRole => usedPhantomCount < OptionPhantomUsageCount.GetInt();
    bool IUsePhantomButton.IsresetAfterKill => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        var target = Player.GetKillTarget(true);
        var targetrole = target.GetCustomRole();

        if (usedPhantomCount >= OptionPhantomUsageCount.GetInt() || !Player.IsAlive() || targetrole.IsImpostor() || target == null || !CanSubkill)
        {
            return;
        }
        else if (target.Is(CustomRoles.Madpsycho))
        {
            if (Madpsycho.CanPsycho)
            {
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                target.RpcMurderPlayer(Player);
                return;
            }
        }
        else
        {
            if (Player.IsAlive()) RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);

            Player.RpcResetAbilityCooldown(Sync: true);
            float savedKillTimer = Player.killTimer;
            Vector2 targetPos = target.transform.position;
            CanSubkill = false; // Murderが実行されないうちにサブキル不可にする。
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, true, 1, CustomDeathReason.Kill);
            SnapToPosition(targetPos);
        }
        if (PhantomCooldown < 1f) //キルク1未満でも一秒待たない。
        {
            _ = new LateTask(() =>
            {
                CanSubkill = true;
            }, PhantomCooldown, "", true);
        }
        else
        {
            _ = new LateTask(() =>
            {
                CanSubkill = true;
            }, 1f, "", true);
        }
    }

    private void SnapToPosition(Vector2 position)
    {
        Player.NetTransform.SnapTo(position);

        ushort sid = (ushort)(Player.NetTransform.lastSequenceId + 2U);
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            Player.NetTransform.NetId, (byte)RpcCalls.SnapTo, Hazel.SendOption.Reliable);
        NetHelpers.WriteVector2(position, writer);
        writer.Write(sid);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int remaining = Mathf.Max(0, OptionPhantomUsageCount.GetInt() - usedPhantomCount);
        return remaining <= 0 ? "" : $"<#ff0000>({remaining})</color>";
    }

    public override bool OverrideAbilityButton(out string text)
    {
        text = "DoubleKiller_Ability";
        return true;
    }
}