using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;
namespace TownOfHost.Roles.Impostor;

public sealed class Teleporter : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Teleporter),
            player => new Teleporter(player),
            CustomRoles.Teleporter,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            7500,
            SetupOptionItem,
            "etp",
            OptionSort: (3, 16),
            from: From.SuperNewRoles
        );

    public Teleporter(PlayerControl player)
        : base(RoleInfo, player)
    {
        AbilityCooldown = OptionAbilityCooldown.GetFloat();
        WaitingTime = OptionWaitingTime.GetFloat();

        pendingTimer = -1f;
        destPlayerId = byte.MaxValue;

        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    static OptionItem OptionAbilityCooldown;
    static float AbilityCooldown;
    static OptionItem OptionWaitingTime;
    static float WaitingTime;

    enum OptionName
    {
        TeleporterAbilityCooldown,
        TeleporterWaitingTime,
    }

    float pendingTimer;
    byte destPlayerId;

    static readonly Vector2 LIFT_POSITION = new(7.76f, 8.56f);

    static void SetupOptionItem()
    {
        OptionAbilityCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.TeleporterAbilityCooldown,
            new(5f, 120f, 2.5f), 45f, false).SetValueFormat(OptionFormat.Seconds);
        OptionWaitingTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.TeleporterWaitingTime,
            new(0f, 10f, 1f), 3f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => Main.NormalOptions.KillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void OnDestroy()
    {
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown;
    }

    static bool IsOnRestrictedMove(PlayerControl pc)
    {
        if (pc == null) return false;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (pc.onLadder) return true;
        if (pc.inMovingPlat) return true;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship &&
            Vector2.Distance(pc.GetTruePosition(), LIFT_POSITION) <= 1.9f) return true;
        return false;
    }

    static bool IsBeamingOrCharging(PlayerControl pc)
    {
        if (pc?.GetRoleClass() is HadouHo hh)
            return hh.IsCharging || hh.ShowBeamMark;
        if (pc?.GetRoleClass() is JackalHadouHo jhh)
            return jhh.IsCharging || jhh.IsSuperCharging || jhh.ShowBeamMark;
        return false;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (pendingTimer >= 0f) return;

        var candidates = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != Player.PlayerId)
            .ToArray();
        if (candidates.Length == 0) return;

        var dest = candidates[IRandom.Instance.Next(candidates.Length)];
        destPlayerId = dest.PlayerId;
        pendingTimer = WaitingTime;

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();

        UtilsGameLog.AddGameLog("Teleporter",
            $"{UtilsName.GetPlayerColor(Player)} がテレポート開始 → {UtilsName.GetPlayerColor(dest)}");

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = AbilityCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.1f, "Teleporter.ResetCD", true);

        if (WaitingTime <= 0f)
            ExecuteTeleport();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (pendingTimer < 0f) return;

        float prev = pendingTimer;
        pendingTimer -= Time.fixedDeltaTime;
        if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(pendingTimer))
            UtilsNotifyRoles.NotifyRoles();
        if (pendingTimer <= 0f)
            ExecuteTeleport();
    }

    void ExecuteTeleport()
    {
        var destPlayer = PlayerCatch.GetPlayerById(destPlayerId);
        destPlayerId = byte.MaxValue;
        pendingTimer = -1f;

        if (destPlayer == null || !destPlayer.IsAlive() || !Player.IsAlive())
        {
            SendRpc(); UtilsNotifyRoles.NotifyRoles(); return;
        }

        if (IsBeamingOrCharging(Player))
        {
            SendRpc(); UtilsNotifyRoles.NotifyRoles(); return;
        }

        if (IsOnRestrictedMove(destPlayer) || IsBeamingOrCharging(destPlayer))
        {
            SendRpc(); UtilsNotifyRoles.NotifyRoles(); return;
        }

        // ★ 修正：GetTruePosition() から ペンギンと同じく transform.position ベースの取得に変更
        var dest = (Vector2)destPlayer.transform.position;

        Player.RpcSnapToForced(dest);

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (pc.PlayerId == destPlayer.PlayerId) continue;
            if (IsOnRestrictedMove(pc)) continue;
            if (IsBeamingOrCharging(pc)) continue;
            pc.RpcSnapToForced(dest);
        }

        UtilsGameLog.AddGameLog("Teleporter",
            $"{UtilsName.GetPlayerColor(Player)} が全員を {UtilsName.GetPlayerColor(destPlayer)} の元にテレポートさせた");

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        pendingTimer = -1f;
        destPlayerId = byte.MaxValue;
        SendRpc();
    }

    public override void OnStartMeeting()
    {
        pendingTimer = -1f;
        destPlayerId = byte.MaxValue;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        Main.AllPlayerKillCooldown[Player.PlayerId] = Main.NormalOptions.KillCooldown;

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = AbilityCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.3f, "Teleporter.AfterMeeting.CD", true);
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer) return "";
        if (Is(seer)) return "";
        if (isForMeeting) return "";
        if (!Player.IsAlive()) return "";
        if (pendingTimer < 0f) return "";

        var dest = PlayerCatch.GetPlayerById(destPlayerId);
        string destName = dest != null ? UtilsName.GetPlayerColor(dest, true) : "???";
        int sec = Mathf.CeilToInt(pendingTimer);
        return $"\\n<color=#ff4500>{destName} の元に {sec}秒後テレポートします！</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (pendingTimer >= 0f)
        {
            var dest = PlayerCatch.GetPlayerById(destPlayerId);
            string destName = dest != null ? UtilsName.GetPlayerColor(dest, true) : "???";
            int sec = Mathf.CeilToInt(pendingTimer);
            return $"{size}<color={color}>{destName} の元へ {sec}秒後テレポート！</color>";
        }
        return $"{size}<color={color}>ファントム → ランダムな人の元へ全員テレポート</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (pendingTimer >= 0f)
            return $"<color=#ff4500>({Mathf.CeilToInt(pendingTimer)}s)</color>";
        return "";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(pendingTimer);
        sender.Writer.Write(destPlayerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        pendingTimer = reader.ReadSingle();
        destPlayerId = reader.ReadByte();
    }
}