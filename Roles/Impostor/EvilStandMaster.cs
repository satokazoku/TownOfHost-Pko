using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilStandMaster : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilStandMaster),
            player => new EvilStandMaster(player),
            CustomRoles.EvilStandMaster,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            327000,
            SetupOptionItem,
            "esm",
            OptionSort: (3, 11),
            from: From.TownOfHost_Pko
        );

    public EvilStandMaster(PlayerControl player) : base(RoleInfo, player) { }

    static OptionItem OptionWarpCooldown;
    static OptionItem OptionWarpStayDuration;
    static OptionItem OptionReduceTeammateKillCD;
    static OptionItem OptionTeammateKillCDReduce;
    static OptionItem OptionReduceOwnKillCD;
    static OptionItem OptionOwnKillCDReduce;

    static float WarpCooldown;
    static float WarpStayDuration;
    static bool ReduceTeammateKillCD;
    static float TeammateKillCDReduce;
    static bool ReduceOwnKillCD;
    static float OwnKillCDReduce;

    enum OptionName
    {
        EvilStandMasterWarpCooldown,
        EvilStandMasterWarpStayDuration,
        EvilStandMasterReduceTeammateKillCD,
        EvilStandMasterTeammateKillCDReduce,
        EvilStandMasterReduceOwnKillCD,
        EvilStandMasterOwnKillCDReduce,
    }

    static void SetupOptionItem()
    {
        OptionWarpCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.EvilStandMasterWarpCooldown,
            new(1f, 180f, 1f), 30f, false).SetValueFormat(OptionFormat.Seconds);

        OptionWarpStayDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.EvilStandMasterWarpStayDuration,
            new(0f, 30f, 0.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);

        OptionReduceTeammateKillCD = BooleanOptionItem.Create(RoleInfo, 12,
            OptionName.EvilStandMasterReduceTeammateKillCD, true, false);
        OptionTeammateKillCDReduce = FloatOptionItem.Create(RoleInfo, 13,
            OptionName.EvilStandMasterTeammateKillCDReduce,
            new(0f, 60f, 0.5f), 10f, false, OptionReduceTeammateKillCD).SetValueFormat(OptionFormat.Seconds);

        OptionReduceOwnKillCD = BooleanOptionItem.Create(RoleInfo, 14,
            OptionName.EvilStandMasterReduceOwnKillCD, true, false);
        OptionOwnKillCDReduce = FloatOptionItem.Create(RoleInfo, 15,
            OptionName.EvilStandMasterOwnKillCDReduce,
            new(0f, 60f, 0.5f), 5f, false, OptionReduceOwnKillCD).SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add()
    {
        WarpCooldown = OptionWarpCooldown.GetFloat();
        WarpStayDuration = OptionWarpStayDuration.GetFloat();
        ReduceTeammateKillCD = OptionReduceTeammateKillCD.GetBool();
        TeammateKillCDReduce = OptionTeammateKillCDReduce.GetFloat();
        ReduceOwnKillCD = OptionReduceOwnKillCD.GetBool();
        OwnKillCDReduce = OptionOwnKillCDReduce.GetFloat();
    }

    public float CalculateKillCooldown() => 30f;
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = WarpCooldown;
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (!Player.IsAlive()) return;

        var candidates = GetWarpCandidates();

        if (candidates.Count == 0)
        {
            if (ReduceOwnKillCD && OwnKillCDReduce > 0f)
            {
                float newCd = Mathf.Max(0.1f, Player.killTimer - OwnKillCDReduce);
                Player.SetKillCooldown(newCd);
                Logger.Info($"[EvilStandMaster] ワープ不可→自分のキルCD {OwnKillCDReduce}秒短縮", "EvilStandMaster");
            }
            Utils.SendMessage(GetString("EvilStandMasterNoTarget"), Player.PlayerId);
            return;
        }

        var target = candidates[IRandom.Instance.Next(candidates.Count)];
        var pos = Player.GetTruePosition();

        target.RpcSnapToForced(pos);
        Logger.Info($"[EvilStandMaster] {target.Data?.GetLogPlayerName()} を {pos} にワープ", "EvilStandMaster");

        if (WarpStayDuration > 0f)
        {
            var origSpeed = Main.AllPlayerSpeed.TryGetValue(target.PlayerId, out var s)
                ? s : Main.NormalOptions.PlayerSpeedMod;

            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.MarkDirtySettings();

            _ = new LateTask(() =>
            {
                if (!target.IsAlive()) return;
                Main.AllPlayerSpeed[target.PlayerId] = origSpeed;
                target.MarkDirtySettings();
            }, WarpStayDuration, $"EvilStandMaster.Unfreeze.{target.PlayerId}", true);
        }

        if (ReduceTeammateKillCD && TeammateKillCDReduce > 0f)
        {
            float newCd = Mathf.Max(0.1f, target.killTimer - TeammateKillCDReduce);
            target.SetKillCooldown(newCd);
            Logger.Info($"[EvilStandMaster] {target.Data?.GetLogPlayerName()} のキルCD {TeammateKillCDReduce}秒短縮", "EvilStandMaster");
        }

        Utils.SendMessage(
            string.Format(GetString("EvilStandMasterWarped"), target.Data?.PlayerName ?? "???"),
            Player.PlayerId);
        UtilsNotifyRoles.NotifyRoles();
    }

    private List<PlayerControl> GetWarpCandidates()
    {
        return AllAlivePlayerControls
            .Where(pc =>
                pc.PlayerId != Player.PlayerId &&
                pc.GetCustomRole().IsImpostor() &&
                !pc.inVent &&
                !pc.inMovingPlat &&
                !pc.walkingToVent &&
                !pc.onLadder &&
                !pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() &&
                !pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
            .ToList();
    }

    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.UseOneclickButton => true;

    public override bool OverrideAbilityButton(out string text)
    {
        text = "EvilStandMaster_Warp";
        return true;
    }

    public override string GetAbilityButtonText() => GetString("EvilStandMasterButtonText");

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        var count = GetWarpCandidates().Count;
        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        return count > 0
            ? $"{size}<color={color}>ワープ対象: {count}人</color>"
            : $"{size}<color=#888888>ワープ対象なし（キルCD短縮待機中）</color>";
    }
}