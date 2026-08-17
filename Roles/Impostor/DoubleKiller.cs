using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

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
        phantomNowCool = 0f;
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
    float phantomNowCool;

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
        OptionPhantomUsageCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.DoubleKillerPhantomUsageCount, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanSabotage;
    public bool CanUseImpostorVentButton() => CanVent;

    public override bool CanClickUseVentButton => CanVent;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => CanVent;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVent;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (usedPhantomCount < OptionPhantomUsageCount.GetInt())
            AURoleOptions.PhantomCooldown = Mathf.Max(phantomNowCool, 0.1f);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (phantomNowCool > 0f)
        {
            phantomNowCool -= Time.fixedDeltaTime;
            if (phantomNowCool < 0f) phantomNowCool = 0f;
        }
    }

    bool IUsePhantomButton.IsPhantomRole => usedPhantomCount < OptionPhantomUsageCount.GetInt();
    bool IUsePhantomButton.IsresetAfterKill => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (usedPhantomCount >= OptionPhantomUsageCount.GetInt() || !Player.IsAlive()) return;

        PlayerControl nearest = null;
        float minDist = Main.NormalOptions.KillDistance switch
        {
            0 => 1f,
            1 => 1.8f,
            _ => 2.5f
        };

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
            if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;

            float dist = Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition());
            if (dist < minDist)
            {
                minDist = dist;
                nearest = target;
            }
        }

        if (nearest == null) return;

        usedPhantomCount++;
        float savedKillTimer = Player.killTimer;
        Vector2 targetPos = nearest.transform.position;
        CustomRoleManager.OnCheckMurder(Player, nearest, nearest, nearest, true, true, 1, CustomDeathReason.Kill);

        SnapToPosition(targetPos);

        phantomNowCool = PhantomCooldown;

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            RestoreKillCooldown(savedKillTimer);
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.2f, "DoubleKillerRestoreCD", true);
    }

    private void RestoreKillCooldown(float cooldown)
    {
        cooldown = Mathf.Max(cooldown, 0f);
        Player.SetKillCooldown(cooldown, delay: true);
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