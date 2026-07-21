using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceTeleporter : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceTeleporter),
            player => new NiceTeleporter(player),
            CustomRoles.NiceTeleporter,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            33600,
            SetupOptionItem,
            "ntp",
            "#4169e1",
            (1, 9),
            from: From.SuperNewRoles
        );

    public NiceTeleporter(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        Cooldown = OptionCooldown.GetFloat();
        WaitingTime = OptionWaitingTime.GetFloat();

        cooldownLeft = Cooldown;
        pendingTimer = -1f;
        destPlayerId = byte.MaxValue;
        wasOnCooldown = cooldownLeft > 0f;

        PetActionManager.Register(Player.PlayerId, OnPet);
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    static OptionItem OptionCooldown;
    static float Cooldown;
    static OptionItem OptionWaitingTime;
    static float WaitingTime;

    enum OptionName
    {
        NiceTeleporterCooldown,
        NiceTeleporterWaitingTime,
    }

    float cooldownLeft;
    float pendingTimer;
    byte destPlayerId;
    bool wasOnCooldown;

    static readonly Vector2 LIFT_POSITION = new(7.76f, 8.56f);

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.NiceTeleporterCooldown,
            new(5f, 120f, 2.5f), 45f, false).SetValueFormat(OptionFormat.Seconds);
        OptionWaitingTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.NiceTeleporterWaitingTime,
            new(0f, 10f, 1f), 3f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public override void OnSpawn(bool initialState = false)
    {
        Cooldown = OptionCooldown.GetFloat();
        cooldownLeft = Cooldown;
        pendingTimer = -1f;
        destPlayerId = byte.MaxValue;
        wasOnCooldown = cooldownLeft > 0f;
        SendRpc();
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = cooldownLeft > 0.5f ? cooldownLeft : 0.5f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    static bool IsOnRestrictedMove(PlayerControl pc)
    {
        if (pc == null) return false;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (pc.onLadder) return true;
        if (pc.inMovingPlat) return true;
        if (pc.inVent) return true;
        if (pc.walkingToVent) return true;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return true;

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

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (cooldownLeft > 0f) return;
        if (pendingTimer >= 0f) return;

        var candidates = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != Player.PlayerId)
            .ToArray();
        if (candidates.Length == 0) return;

        var dest = candidates[IRandom.Instance.Next(candidates.Length)];
        destPlayerId = dest.PlayerId;
        pendingTimer = WaitingTime;
        cooldownLeft = Cooldown;
        wasOnCooldown = true;

        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(Sync: true);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();

        UtilsGameLog.AddGameLog("NiceTeleporter",
            $"{UtilsName.GetPlayerColor(Player)} がテレポート開始 → {UtilsName.GetPlayerColor(dest)}");

        if (WaitingTime <= 0f)
            ExecuteTeleport();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;

        if (cooldownLeft > 0f)
        {
            wasOnCooldown = true;
            float prev = cooldownLeft;
            cooldownLeft -= Time.fixedDeltaTime;
            if (cooldownLeft < 0f) cooldownLeft = 0f;

            if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(cooldownLeft))
            {
                Player.MarkDirtySettings();
                SendRpc();
            }
        }
        else if (wasOnCooldown)
        {
            wasOnCooldown = false;
            cooldownLeft = 0f;
            SendRpc();

            _ = new LateTask(() =>
            {
                if (!Player.IsAlive() || pendingTimer >= 0f) return;
                Player.MarkDirtySettings();
                Player.RpcResetAbilityCooldown(Sync: true);
            }, Main.LagTime + 0.1f, "NiceTeleporter.CDExpire", true);
        }

        if (pendingTimer >= 0f)
        {
            float prev = pendingTimer;
            pendingTimer -= Time.fixedDeltaTime;
            if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(pendingTimer))
                UtilsNotifyRoles.NotifyRoles();
            if (pendingTimer <= 0f)
                ExecuteTeleport();
        }
    }

    void ExecuteTeleport()
    {
        var destPlayer = PlayerCatch.GetPlayerById(destPlayerId);
        destPlayerId = byte.MaxValue;
        pendingTimer = -1f;

        if (destPlayer == null || !destPlayer.IsAlive() || !Player.IsAlive())
        { SendRpc(); UtilsNotifyRoles.NotifyRoles(); return; }

        if (IsOnRestrictedMove(Player) || IsBeamingOrCharging(Player))
        { SendRpc(); UtilsNotifyRoles.NotifyRoles(); return; }

        if (IsOnRestrictedMove(destPlayer) || IsBeamingOrCharging(destPlayer))
        { SendRpc(); UtilsNotifyRoles.NotifyRoles(); return; }

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

        UtilsGameLog.AddGameLog("NiceTeleporter",
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
        cooldownLeft = Cooldown;
        wasOnCooldown = true;
        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(Sync: true);
        SendRpc();
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
        return $"\n<color=#4169e1>{destName} の元に {sec}秒後テレポートします！</color>";
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
        if (cooldownLeft > 0f)
            return $"{size}<color=#888888>クールダウン中</color>";
        return $"{size}<color={color}>ペットを撫でる → ランダムな人の元へ全員テレポート</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (pendingTimer >= 0f)
            return $"<color=#4169e1>({Mathf.CeilToInt(pendingTimer)}s)</color>";
        return "";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(cooldownLeft);
        sender.Writer.Write(pendingTimer);
        sender.Writer.Write(destPlayerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        cooldownLeft = reader.ReadSingle();
        pendingTimer = reader.ReadSingle();
        destPlayerId = reader.ReadByte();
    }
}