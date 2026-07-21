/*using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;
using System.Collections.Generic;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Impostor;

public sealed class Swooper : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Swooper),
            player => new Swooper(player),
            CustomRoles.Swooper,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            326500,
            SetupOptionItem,
            "sw",
            OptionSort: (3, 10),
            from: From.TownOfHost_Enhanced
        );

    public Swooper(PlayerControl player) : base(RoleInfo, player)
    {
        cooldownTimer = 0f;
        durationTimer = 0f;
        isInvisible = false;
        lastVentId = -1;
        lastCoolDisplay = -1;
        lastDurDisplay = -1;
    }

    static OptionItem OptionCooldown;
    static OptionItem OptionDuration;
    static OptionItem OptionVentNormallyOnCooldown;

    static float Cooldown;
    static float Duration;
    static bool VentNormallyOnCooldown;

    enum OptionName
    {
        SwooperCooldown,
        SwooperDuration,
        SwooperVentNormallyOnCooldown,
    }

    float cooldownTimer;
    float durationTimer;
    bool isInvisible;
    int lastVentId;
    int lastCoolDisplay;
    int lastDurDisplay;

    public static readonly HashSet<byte> InvisibleSwooperIds = new();

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SwooperCooldown,
            new(1f, 180f, 1f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.SwooperDuration,
            new(1f, 60f, 1f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionVentNormallyOnCooldown = BooleanOptionItem.Create(RoleInfo, 12,
            OptionName.SwooperVentNormallyOnCooldown, true, false);
    }

    public float CalculateKillCooldown() => 30f;
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    bool CanGoInvis => cooldownTimer <= 0f && !isInvisible;

    public override void Add()
    {
        Cooldown = OptionCooldown.GetFloat();
        Duration = OptionDuration.GetFloat();
        VentNormallyOnCooldown = OptionVentNormallyOnCooldown.GetBool();
        cooldownTimer = Cooldown;
        durationTimer = 0f;
        isInvisible = false;
        lastVentId = -1;
        lastCoolDisplay = -1;
        lastDurDisplay = -1;
        InvisibleSwooperIds.Remove(Player.PlayerId);
    }

    public override void OnDestroy()
    {
        InvisibleSwooperIds.Remove(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = isInvisible
            ? Mathf.Max(durationTimer, 0.1f)
            : Mathf.Max(cooldownTimer, 0.1f);
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
    }

    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.UseOneclickButton => true;

    public override bool CanClickUseVentButton => true;

    private static void RpcBootFromVentDesync(PlayerPhysics physics, int ventId, PlayerControl target)
    {
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            physics.NetId,
            (byte)RpcCalls.BootFromVent,
            SendOption.Reliable,
            target.GetClientId()
        );
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (isInvisible)
        {
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                Player.MyPhysics?.RpcBootFromVent(lastVentId >= 0 ? lastVentId : ventId);
                ExitInvisible();
            }, 0.1f, "Swooper.ExitInvis", true);
            return true;
        }

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive() || isInvisible) return;

            if (!CanGoInvis)
            {
                if (!VentNormallyOnCooldown)
                {
                    physics.RpcBootFromVent(ventId);
                    int cdSec = Mathf.CeilToInt(cooldownTimer);
                    SendMessage(
                        string.Format(GetString("SwooperInvisInCooldown"), cdSec),
                        Player.PlayerId);
                }
            }
            else
            {
                lastVentId = ventId;
                RpcBootFromVentDesync(physics, ventId, Player);
                EnterInvisible();
            }
        }, 0.8f, "Swooper.VentCheck", true);

        return true;
    }

    private void EnterInvisible()
    {
        if (!Player.IsAlive()) return;

        isInvisible = true;
        durationTimer = Duration;
        cooldownTimer = 0f;

        InvisibleSwooperIds.Add(Player.PlayerId);
        SendRpc();

        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(log: false, Sync: true);

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        SendMessage(GetString("SwooperInvisState"), Player.PlayerId);
    }

    private void ExitInvisible()
    {
        if (!isInvisible) return;

        isInvisible = false;
        durationTimer = 0f;
        cooldownTimer = Cooldown;
        lastVentId = -1;

        InvisibleSwooperIds.Remove(Player.PlayerId);
        SendRpc();

        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(log: false, Sync: true);

        UtilsNotifyRoles.NotifyRoles();
        SendMessage(GetString("SwooperInvisStateOut"), Player.PlayerId);
    }

    private static void SnapToPosition(PlayerControl player, Vector2 position)
    {
        player.NetTransform.SnapTo(position);
        ushort sid = (ushort)(player.NetTransform.lastSequenceId + 2U);
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            player.NetTransform.NetId, (byte)RpcCalls.SnapTo, Hazel.SendOption.Reliable);
        NetHelpers.WriteVector2(position, writer);
        writer.Write(sid);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        if (!isInvisible) return;

        info.DoKill = false;
        (var killer, var target) = info.AttemptTuple;

        Vector2 targetPos = target.transform.position;

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        killer.RpcProtectedMurderPlayer(target);
        target.SetRealKiller(killer);
        target.RpcMurderPlayer(target);

        SnapToPosition(killer, targetPos);

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (!Player.IsAlive()) return;

        bool needSync = false;

        if (!isInvisible && cooldownTimer > 0f)
        {
            cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.fixedDeltaTime);
            int now = Mathf.CeilToInt(cooldownTimer);
            if (now != lastCoolDisplay)
            {
                lastCoolDisplay = now;
                if (cooldownTimer <= 0f)
                    SendMessage(GetString("SwooperCanVent"), Player.PlayerId);
                needSync = true;
            }
        }

        if (isInvisible && durationTimer > 0f)
        {
            durationTimer = Mathf.Max(0f, durationTimer - Time.fixedDeltaTime);
            int now = Mathf.CeilToInt(durationTimer);
            if (now != lastDurDisplay)
            {
                lastDurDisplay = now;
                needSync = true;
            }
            if (durationTimer <= 0f)
            {
                if (lastVentId >= 0)
                    Player.MyPhysics?.RpcBootFromVent(lastVentId);
                ExitInvisible();
                return;
            }
        }

        if (needSync)
        {
            SendRpc();
            Player.MarkDirtySettings();
            if (player != PlayerControl.LocalPlayer)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (isInvisible)
        {
            if (lastVentId >= 0)
                Player.MyPhysics?.RpcBootFromVent(lastVentId);
            ExitInvisible();
        }
        cooldownTimer = 0f;
        SendRpc();
    }

    public override void OnStartMeeting()
    {
        InvisibleSwooperIds.Remove(Player.PlayerId);
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (isInvisible) ExitInvisible();
        lastVentId = -1;
        cooldownTimer = Cooldown;
        lastCoolDisplay = -1;
        lastDurDisplay = -1;
        SendRpc();
        Player.RpcResetAbilityCooldown(log: false, Sync: true);
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(isInvisible);
        sender.Writer.Write(cooldownTimer);
        sender.Writer.Write(durationTimer);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isInvisible = reader.ReadBoolean();
        cooldownTimer = reader.ReadSingle();
        durationTimer = reader.ReadSingle();

        if (!Player.AmOwner)
        {
            if (isInvisible)
                InvisibleSwooperIds.Add(Player.PlayerId);
            else
            {
                InvisibleSwooperIds.Remove(Player.PlayerId);
                Player.Visible = true;
            }
        }
        else
        {
            if (isInvisible) InvisibleSwooperIds.Add(Player.PlayerId);
            else InvisibleSwooperIds.Remove(Player.PlayerId);
        }
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        if (isInvisible || cooldownTimer > 0f) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;
        return $"{size}<color={color}>ベントに入ると透明化！</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (isInvisible)
            return $"<color={RoleInfo.RoleColorCode}>(透明: {Mathf.CeilToInt(durationTimer)}s)</color>";
        if (cooldownTimer > 0f)
            return $"<color=#888888>(CD: {Mathf.CeilToInt(cooldownTimer)}s)</color>";
        return $"<color={RoleInfo.RoleColorCode}>(透明OK)</color>";
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class SwooperInvisibilityPatch
{
    static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        if (!Swooper.InvisibleSwooperIds.Contains(__instance.PlayerId)) return;
        if (__instance.AmOwner) return;
        if (__instance.Visible) __instance.Visible = false;
    }
}*/