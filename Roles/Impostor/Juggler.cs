using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Juggler : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Juggler),
        player => new Juggler(player),
        CustomRoles.Juggler,
        () => RoleTypes.Phantom,
        CustomRoleTypes.Impostor,
        8900,
        SetupOptionItem,
        "jug",
        OptionSort: (6, 0),
        from: From.TheOtherRoles,
        assignInfo: new RoleAssignInfo(CustomRoles.Juggler, CustomRoleTypes.Impostor)
        {
            IsInitiallyAssignableCallBack = () => Main.NormalOptions.MapId is not 5
        }
    );

    private static OptionItem OptionKillCoolDown;
    private static OptionItem OptionCooldown;
    private static OptionItem OptionAblitytime;

    public static bool NowUse { get; set; }

    private float _limit;
    HashSet<byte> ShapeShiftedPlayer = new();
    HashSet<byte> JPlayer = new();

    private enum OptionName
    {
        GhostNoiseSenderTime 
    }

    public Juggler(PlayerControl player) : base(RoleInfo, player)
    {
        NowUse = false;
        _limit = -50;
        ShapeShiftedPlayer.Clear();
        JPlayer.Clear();
    }

    private static void SetupOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionAblitytime = FloatOptionItem.Create(RoleInfo, 12, OptionName.GhostNoiseSenderTime, new(0f, 100f, 0.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = NowUse ? (OptionAblitytime.GetFloat()) : OptionCooldown.GetFloat();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !NowUse || GameStates.CalledMeeting || _limit <= -50) return;

        _limit -= Time.fixedDeltaTime;
        if (_limit <= 0)
        {
            _limit = -100;
            NowUse = false;
            ResetCamouflage(false);

            _ = new LateTask(() =>
            {
                if (GameStates.CalledMeeting) return;

                ResetCamouflage(true);
                UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            }, 0.4f, "", true);
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        NowUse = false;
        _limit = -50;
        ShapeShiftedPlayer.Clear();
        JPlayer.Clear();
    }

    public override bool NotifyRolesCheckOtherName => true;

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (NowUse) return;
        ResetCooldown = true;

        Camouflage();

        _ = new LateTask(() =>
        {
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        }, 0.2f, "", true);
    }
    void Camouflage()
    {
        foreach (var pl in PlayerCatch.AllAlivePlayerControls)
        {
            if (Main.ShapeshiftTarget.TryGetValue(pl.PlayerId, out byte targetId) && targetId != pl.PlayerId)
            {
                ShapeShiftedPlayer.Add(pl.PlayerId);
            }
            PlayerControl target = PlayerCatch.AllAlivePlayerControls
                .Where(p => p != pl)
                .Where(p => !JPlayer.Contains(p.PlayerId)) 
                .OrderBy(_ => UnityEngine.Random.value) // ランダムにシャッフル
                .FirstOrDefault(); // 先頭の1人を取得（いなければnull）

            JPlayer.Add(target.PlayerId);
            if (target != null && !ShapeShiftedPlayer.Contains(pl.PlayerId))
            {
                pl.RpcShapeshift(target, false);
                var sender = CustomRpcSender.Create("CamouflagerShape");
                sender.AutoStartRpc(pl.NetId, RpcCalls.Shapeshift)
                    .Write(target)
                    .Write(false)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }
        _limit = OptionAblitytime.GetFloat();
        NowUse = true;
    }
    void ResetCamouflage(bool reset)
    {
        foreach (var pl in PlayerCatch.AllAlivePlayerControls)
        {
            if (!ShapeShiftedPlayer.Contains(pl.PlayerId))
            {
                pl.RpcShapeshift(pl, false);
                var sender = CustomRpcSender.Create("CamouflagerShape");
                sender.AutoStartRpc(pl.NetId, RpcCalls.Shapeshift)
                    .Write(pl)
                    .Write(false)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }
        if (reset)
        {
            ShapeShiftedPlayer.Clear();
            JPlayer.Clear();
            Player.RpcResetAbilityCooldown(log: false, Sync: true);
        }
    }

    public float CalculateKillCooldown() => OptionKillCoolDown.GetFloat();

    public override bool OverrideAbilityButton(out string text)
    {
        text = "Camouflager_Ability";
        return true;
    }

    public override string GetAbilityButtonText() => GetString("JugglerText");

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        if (isForHud) return GetString("PhantomButtonLowertext");
        return $"<size=50%>{GetString("PhantomButtonLowertext")}</size>";
    }

    bool IUsePhantomButton.IsresetAfterKill => false;
}
