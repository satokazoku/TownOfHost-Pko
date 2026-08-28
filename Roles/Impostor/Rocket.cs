/*
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Epic.OnlineServices.Presence;
using Epic.OnlineServices.RTC;
using Hazel;
using Il2CppSystem.Reflection;
using MS.Internal.Xml.XPath;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor;

public sealed class Rocket : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Rocket),
            player => new Rocket(player),
            CustomRoles.Rocket,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            26350,
            SetupOptionItem,
            "rkt",
            OptionSort: (3, 14),
            from: From.SuperNewRoles
        );

    public Rocket(PlayerControl player) : base(RoleInfo, player)
    {
        InitialGrabCooldown = OptionInitialGrabCooldown.GetFloat();
        SubsequentGrabCooldown = OptionSubsequentGrabCooldown.GetFloat();
        LaunchCooldown = OptionLaunchCooldown.GetFloat();

        GrabbedPlayers = new();
        launchPending = false;
        killCDOverride = InitialGrabCooldown;

        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    static OptionItem OptionInitialGrabCooldown; static float InitialGrabCooldown;
    static OptionItem OptionSubsequentGrabCooldown; static float SubsequentGrabCooldown;
    static OptionItem OptionLaunchCooldown; static float LaunchCooldown;
    static OptionItem OptLaunchAtMeeting;

    enum OptionName
    {
        RocketInitialGrabCooldown,
        RocketSubsequentGrabCooldown,
        RocketLaunchCooldown,
        RocketLaunchAtMeeting
    }

    public readonly List<PlayerControl> GrabbedPlayers;
    public static readonly HashSet<byte> GrabbedPlayerIds = new();

    bool launchPending;
    float killCDOverride;
    int snapFrame = 0;

    static readonly Dictionary<byte, (string msg, float expireTime)> LaunchNotifications = new();

    static void SetupOptionItem()
    {
        OptionInitialGrabCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.RocketInitialGrabCooldown,
            new(2.5f, 60f, 2.5f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSubsequentGrabCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.RocketSubsequentGrabCooldown,
            new(0f, 60f, 2.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionLaunchCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.RocketLaunchCooldown,
            new(0f, 60f, 2.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        OptLaunchAtMeeting = BooleanOptionItem.Create(RoleInfo, 13, OptionName.RocketLaunchAtMeeting, true, false);
    }

    public float CalculateKillCooldown() => killCDOverride;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    public override bool CanClickUseVentButton => true;
    public override bool CanUseAbilityButton() => GrabbedPlayers.Count > 0;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    [Attributes.GameModuleInitializer]
    public static void Init() => GrabbedPlayerIds.Clear();

    public override void Add()
    {
        killCDOverride = InitialGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
    }

    public override void ApplyGameOptions(IGameOptions opt)
        => AURoleOptions.PhantomCooldown = LaunchCooldown;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (target == null) { info.DoKill = false; return; }
        if (GrabbedPlayerIds.Contains(target.PlayerId))
        {
            info.DoKill = false;
            return;
        }

        info.DoKill = false;
        if (AmongUsClient.Instance.AmHost)
        {
            GrabPlayer(target);
        }
        else
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)0);
            sender.Writer.Write(target.PlayerId);
        }
    }

    void GrabPlayer(PlayerControl target)
    {
        if (GrabbedPlayerIds.Contains(target.PlayerId))
        {
            return;
        }
        GrabbedPlayers.Add(target);
        GrabbedPlayerIds.Add(target.PlayerId);

        PlayerState.GetByPlayerId(target.PlayerId).CanMove = false;
        target.MarkDirtySettings();
         SetAppearsAsImpostorForRocket(target, true);

        killCDOverride = SubsequentGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
        var grabState = PlayerState.GetByPlayerId(Player.PlayerId);
        if (grabState != null) grabState.Is10secKillButton = false;
        Player.SetKillCooldown(killCDOverride, force: false);

        SendSyncRpc();
        UtilsNotifyRoles.NotifyRoles();
        Logger.Info($"{Player.Data.GetLogPlayerName()} が {target.Data.GetLogPlayerName()} を掴んだ", "Rocket");
    }

    void SetAppearsAsImpostorForRocket(PlayerControl target, bool asImpostor)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Player.AmOwner) return;

        var fakeRole = asImpostor ? RoleTypes.Impostor : target.Data.RoleType;
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            target.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, Player.OwnerId);
        writer.Write((ushort)fakeRole);
        writer.Write(true);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    void ReleasePlayer(PlayerControl target)
    {
        if (!GrabbedPlayers.Remove(target)) return;
        GrabbedPlayerIds.Remove(target.PlayerId);
        PlayerState.GetByPlayerId(target.PlayerId).CanMove = true;
        target.MarkDirtySettings();
            SetAppearsAsImpostorForRocket(target, false);
    }

    void ReleaseAll()
    {
        foreach (var p in GrabbedPlayers.ToArray())
        {
            PlayerState.GetByPlayerId(p.PlayerId).CanMove = true;
            p.MarkDirtySettings();
                SetAppearsAsImpostorForRocket(p, false);
        }
        GrabbedPlayers.Clear();
        GrabbedPlayerIds.Clear();
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (GrabbedPlayers.Count == 0)
        {
            AdjustKillCooldown = true;
            ResetCooldown = false;
            return;
        }

        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!Player.IsAlive()) return;

        if (AmongUsClient.Instance.AmHost)
        {
            ExecuteLaunch();
        }
        else
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)1);
        }
    }

    void ExecuteLaunch()
    {
        if (!Player.IsAlive()) return;

        LaunchAll(Player.GetTruePosition());

        killCDOverride = InitialGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
        var launchState = PlayerState.GetByPlayerId(Player.PlayerId);
        if (launchState != null) launchState.Is10secKillButton = false;
        Player.SetKillCooldown(killCDOverride, force: false);

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = LaunchCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.1f, "Rocket.ResetCD", true);

        SendSyncRpc();
    }

    void LaunchAll(Vector2 launchPos)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var targets = GrabbedPlayers.ToArray();
        ReleaseAll();
        UtilsNotifyRoles.NotifyRoles();

        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null || !target.IsAlive()) continue;

            float xOffset = (i - (targets.Length - 1) / 2f) * 0.4f;
            Vector2 spawnPos = launchPos + new Vector2(xOffset, 0f);

            PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Launch;
            target.RpcExileV3();
            PlayerState.GetByPlayerId(target.PlayerId).SetDead();

            UtilsGameLog.AddGameLog("Rocket",
                $"{UtilsName.GetPlayerColor(Player)}が{UtilsName.GetPlayerColor(target)}を打ち上げた");

            var capturedTarget = target;
            var capturedPos = spawnPos;
            int idx = i;

            NotifyLaunchToNearby(launchPos);
        }

        UtilsNotifyRoles.NotifyRoles();
    }

    void NotifyLaunchToNearby(Vector2 launchPos)
    {
        string msg = "\n<color=#ff6600>🚀 誰かが打ち上げられた！</color>";
        float expireTime = Time.realtimeSinceStartup + 2f;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            pc.KillFlash();
            LaunchNotifications[pc.PlayerId] = (msg, expireTime);
        }

        UtilsNotifyRoles.NotifyRoles();
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || isForMeeting) return "";

        if (LaunchNotifications.TryGetValue(seer.PlayerId, out var entry))
        {
            if (Time.realtimeSinceStartup < entry.expireTime)
                return entry.msg;
            LaunchNotifications.Remove(seer.PlayerId);
        }
        return "";
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (LaunchNotifications.Count > 0)
        {
            bool anyExpired = false;
            foreach (var key in LaunchNotifications.Keys.ToArray())
            {
                if (Time.realtimeSinceStartup >= LaunchNotifications[key].expireTime)
                {
                    LaunchNotifications.Remove(key);
                    anyExpired = true;
                }
            }
            if (anyExpired) UtilsNotifyRoles.NotifyRoles();
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (GrabbedPlayers.Count == 0) return;
        if (!Player.IsAlive()) { ReleaseAll(); SendSyncRpc(); return; }

        bool removed = false;
        foreach (var p in GrabbedPlayers.ToArray())
        {
            if (p == null || !p.IsAlive())
            {
                ReleasePlayer(p);
                removed = true;
            }
        }
        if (removed) SendSyncRpc();

        snapFrame++;
        if (snapFrame % 3 != 0) return;

        var myPos = Player.GetTruePosition();
        foreach (var grabbed in GrabbedPlayers.ToArray())
        {
            if (grabbed == null || !grabbed.IsAlive()) continue;
            if (grabbed.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) continue;
            var sId = grabbed.NetTransform.lastSequenceId + 5;
            ushort sid = (ushort)(grabbed.NetTransform.lastSequenceId + 2U);
            grabbed.NetTransform.SnapTo(Player.transform.position, (ushort)sId);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                grabbed.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
            NetHelpers.WriteVector2(myPos, writer);
            writer.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (OptLaunchAtMeeting.GetBool()) return;

            if (GrabbedPlayers.Count > 0) launchPending = true;

        foreach (var p in GrabbedPlayers.ToArray())
        {
            PlayerState.GetByPlayerId(p.PlayerId).CanMove = true;
            p.MarkDirtySettings();
            if (!Player.AmOwner)
                SetAppearsAsImpostorForRocket(p, false);
        }
        SendSyncRpc();
    }
    public override void OnStartMeeting()
    {
        if (OptLaunchAtMeeting.GetBool()) return;
            launchPending = false;
        foreach (var p in GrabbedPlayers.ToArray())
        {
            if (!Player.AmOwner)
                SetAppearsAsImpostorForRocket(p, false);
        }
        GrabbedPlayers.Clear();
        SendSyncRpc();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) { ReleaseAll(); SendSyncRpc(); return; }
        if (OptLaunchAtMeeting.GetBool())
        {
            if (Player.IsAlive())
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    ExecuteLaunch();
                }
                else
                {
                    using var sender = CreateSender();
                    sender.Writer.Write((byte)1);
                }
            }

        }
        GrabbedPlayers.Clear();
        GrabbedPlayerIds.Clear();

        if (launchPending && GrabbedPlayers.Count > 0)
        {
            var pos = Player.GetTruePosition();
            _ = new LateTask(() =>
            {
                LaunchAll(pos);
                killCDOverride = InitialGrabCooldown;
                Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
                var afterMeetingState = PlayerState.GetByPlayerId(Player.PlayerId);
                if (afterMeetingState != null) afterMeetingState.Is10secKillButton = false;
                Player.SetKillCooldown(killCDOverride, force: false);
                SendSyncRpc();
            }, 1.5f, "Rocket.LaunchAfterMeeting", true);
        }
        else
        {
            foreach (var p in GrabbedPlayers.ToArray())
            {
                PlayerState.GetByPlayerId(p.PlayerId).CanMove = false;
                p.MarkDirtySettings();
                if (!Player.AmOwner)
                    SetAppearsAsImpostorForRocket(p, true);
            }
            SendSyncRpc();
        }
        launchPending = false;
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = LaunchCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.3f, "Rocket.AfterMeeting.CD", true);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer)) return "";
        if (GrabbedPlayers.Contains(seen)) return "<color=#ff6600>🚀</color>";
        return "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";
        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;
        int count = GrabbedPlayers.Count;
        if (count == 0)
            return $"{size}<color={color}>キルボタン → 掴む | ファントム → 打ち上げ</color>";
        return $"{size}<color={color}>掴み中: {count}人 | ファントムボタン → 打ち上げ！</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        int count = GrabbedPlayers.Count;
        if (count == 0) return "";
        return $"<color=#ff6600>({count}人)</color>";
    }

    void SendSyncRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)2);
        sender.Writer.Write(GrabbedPlayers.Count);
        foreach (var p in GrabbedPlayers)
            sender.Writer.Write(p?.PlayerId ?? byte.MaxValue);
        sender.Writer.Write(launchPending);
        sender.Writer.Write(killCDOverride);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte rpcType = reader.ReadByte();
        if (rpcType == 0)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            byte targetId = reader.ReadByte();
            var target = PlayerCatch.GetPlayerById(targetId);
            if (target != null && !GrabbedPlayers.Contains(target))
            {
                GrabPlayer(target);
            }
        }
        else if (rpcType == 1)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            ExecuteLaunch();
        }
        else if (rpcType == 2)
        {
            int count = reader.ReadInt32();
            foreach (var old in GrabbedPlayers) GrabbedPlayerIds.Remove(old.PlayerId);
            GrabbedPlayers.Clear();

            for (int i = 0; i < count; i++)
            {
                var id = reader.ReadByte();
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc != null)
                {
                    GrabbedPlayers.Add(pc);
                    GrabbedPlayerIds.Add(id);
                }
            }
            launchPending = reader.ReadBoolean();
            killCDOverride = reader.ReadSingle();
        }
    }

    public override string GetAbilityButtonText() =>
        GrabbedPlayers.Count > 0 ? "打ち上げ" : "掴む";

    public bool OverrideKillButton(out string text)
    {
        text = "Rocket_Kill";
        return true;
    }

    public override bool OverrideAbilityButton(out string text)
    {
        text = "Rocket_Ability";
        return true;
    }
}
*/