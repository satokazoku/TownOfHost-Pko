/*
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.Crewmate;

namespace TownOfHost.Roles.Impostor;

public sealed class TimeSleeper : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(TimeSleeper),
            player => new TimeSleeper(player),
            CustomRoles.TimeSleeper,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            7700,
            SetUpOptionItem,
            "ts",
            OptionSort: (6, 14),
            from: From.TownOfHost_Pko
        );

    public TimeSleeper(PlayerControl player)
        : base(RoleInfo, player)
    {
        RecordDuration = OptionRecordDuration.GetFloat();
        RewindSpeed = OptionRewindSpeed.GetFloat();
        PhantomCooldown = OptionPhantomCooldown.GetFloat();

        isRecording = false;
        isRewinding = false;
        recordTimer = 0f;
        rewindTimer = 0f;
        rewindIndex = 0;
        positionHistory = new();
        rewindSkipPlayers = new();
        movingPlatformUsedDuringRec = new();
    }

    static OptionItem OptionPhantomCooldown;
    static float PhantomCooldown;
    static OptionItem OptionRecordDuration;
    static float RecordDuration;
    static OptionItem OptionRewindSpeed;
    static float RewindSpeed;

    bool isRecording;
    bool isRewinding;
    float recordTimer;
    float rewindTimer;
    int rewindIndex;

    Dictionary<byte, List<Vector2>> positionHistory;
    HashSet<byte> rewindSkipPlayers;
    HashSet<byte> movingPlatformUsedDuringRec;

    enum OptionName
    {
        TimeSleeperRecordDuration,
        TimeSleeperRewindSpeed,
    }

    static void SetUpOptionItem()
    {
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRecordDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.TimeSleeperRecordDuration, new(1f, 20f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRewindSpeed = FloatOptionItem.Create(RoleInfo, 12, OptionName.TimeSleeperRewindSpeed, new(0.5f, 5f, 0.5f), 1f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public float CalculateKillCooldown() => 30f;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = PhantomCooldown;
    }

    bool IUsePhantomButton.IsPhantomRole => true;

    static bool IsUsingMovingPlatform(PlayerControl pc)
    {
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (pc.onLadder) return true;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship
            && Vector2.Distance(pc.GetTruePosition(), new Vector2(7.76f, 8.56f)) <= 1.9f) return true;
        if (pc.inMovingPlat) return true;
        return false;
    }

    static bool IsBeamingOrCharging(PlayerControl pc)
    {
        if (pc.GetRoleClass() is HadouHo hh)
            return hh.IsCharging || hh.ShowBeamMark;
        if (pc.GetRoleClass() is JackalHadouHo jhh)
            return jhh.IsCharging || jhh.IsSuperCharging || jhh.ShowBeamMark;
        if (pc.GetRoleClass() is SheriffHadouHo shh)
            return shh.IsCharging || shh.ShowBeamMark;
        return false;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (isRecording || isRewinding) return;
        if (!Player.IsAlive()) return;

        isRecording = true;
        recordTimer = 0f;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();
        movingPlatformUsedDuringRec.Clear();

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (IsUsingMovingPlatform(pc))
            {
                rewindSkipPlayers.Add(pc.PlayerId);
                continue;
            }
            positionHistory[pc.PlayerId] = new List<Vector2>();
        }

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
        SendRpc();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (isRecording)
        {
            recordTimer += Time.fixedDeltaTime;

            if (recordTimer % 0.1f < Time.fixedDeltaTime)
            {
                foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                {
                    if (IsUsingMovingPlatform(pc))
                    {
                        movingPlatformUsedDuringRec.Add(pc.PlayerId);
                        continue;
                    }

                    if (rewindSkipPlayers.Contains(pc.PlayerId)) continue;

                    if (!positionHistory.ContainsKey(pc.PlayerId))
                        positionHistory[pc.PlayerId] = new List<Vector2>();

                    var pos = pc.transform.position;
                    positionHistory[pc.PlayerId].Add(pos);
                }
            }

            if (recordTimer >= RecordDuration)
            {
                isRecording = false;
                isRewinding = true;
                rewindTimer = 0f;
                rewindIndex = positionHistory.ContainsKey(Player.PlayerId)
                    ? positionHistory[Player.PlayerId].Count - 1
                    : 0;
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                SendRpc();
            }
            return;
        }

        if (isRewinding)
        {
            rewindTimer += Time.fixedDeltaTime * RewindSpeed;

            if (rewindTimer >= 0.1f)
            {
                rewindTimer = 0f;

                if (rewindIndex < 0)
                {
                    isRewinding = false;
                    positionHistory.Clear();
                    rewindSkipPlayers.Clear();
                    movingPlatformUsedDuringRec.Clear();
                    Player.RpcResetAbilityCooldown();
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                    SendRpc();
                    return;
                }

                foreach (var kvp in positionHistory)
                {
                    var pc = PlayerCatch.GetPlayerById(kvp.Key);
                    if (pc == null || !pc.IsAlive()) continue;
                    if (rewindIndex >= kvp.Value.Count) continue;

                    if (rewindSkipPlayers.Contains(pc.PlayerId)) continue;
                    if (movingPlatformUsedDuringRec.Contains(pc.PlayerId)) continue;
                    if (IsUsingMovingPlatform(pc)) continue;
                    if (pc.inVent) continue;
                    if (IsBeamingOrCharging(pc)) continue;

                    var targetPos = kvp.Value[rewindIndex];
                    pc.RpcSnapToForced(targetPos, SendOption.None);
                }

                rewindIndex--;
            }
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        isRecording = false;
        isRewinding = false;
        recordTimer = 0f;
        rewindTimer = 0f;
        rewindIndex = 0;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();
        movingPlatformUsedDuringRec.Clear();
        SendRpc();
    }

    public override void OnStartMeeting()
    {
        isRecording = false;
        isRewinding = false;
        recordTimer = 0f;
        rewindTimer = 0f;
        rewindIndex = 0;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();
        movingPlatformUsedDuringRec.Clear();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = PhantomCooldown;
        Player.RpcResetAbilityCooldown();
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(isRecording);
        sender.Writer.Write(isRewinding);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isRecording = reader.ReadBoolean();
        isRewinding = reader.ReadBoolean();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        if (isRecording)
        {
            var remaining = RecordDuration - recordTimer;
            return $"{(isForHud ? "" : "<size=60%>")}<color=#a0c4ff>記録中... {remaining:F1}s</color>";
        }
        if (isRewinding)
            return $"{(isForHud ? "" : "<size=60%>")}<color=#a0c4ff>巻き戻し中...</color>";
        return $"{(isForHud ? "" : "<size=60%>")}<color=#a0c4ff>ファントムボタン → タイムスリープ</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (isRecording) return $"<color=#a0c4ff>●REC</color>";
        if (isRewinding) return $"<color=#a0c4ff>◀◀</color>";
        return "";
    }

    public override string GetAbilityButtonText() => "タイムスリープ";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "TimeSleeper_Ability";
        return true;
    }
}
*/
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class TimeSleeper : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(TimeSleeper),
            player => new TimeSleeper(player),
            CustomRoles.TimeSleeper,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            7700,
            SetUpOptionItem,
            "ts",
            OptionSort: (6, 14),
            from: From.TownOfHost_Pko
        );

    public TimeSleeper(PlayerControl player)
        : base(RoleInfo, player)
    {
        PhantomCooldown = OptionPhantomCooldown.GetFloat();
        RecordDuration = OptionRecordDuration.GetFloat();

        isRecording = false;
        recordTimer = 0f;
        positionHistory = new();
        rewindSkipPlayers = new();
        VentedPlayers = new();
        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
        Sabotage = null;
    }

    static OptionItem OptionPhantomCooldown;
    static float PhantomCooldown;
    static OptionItem OptionRecordDuration;
    static float RecordDuration;

    bool isRecording;
    float recordTimer;

    HashSet<byte> rewindSkipPlayers;
    Dictionary<byte, Vector2> positionHistory;
    Dictionary<byte, int> VentedPlayers;

    SystemTypes? Sabotage;
    enum OptionName
    {
        TimeSleeperRecordDuration,
        TimeSleeperRewindSpeed,
    }

    static void SetUpOptionItem()
    {
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRecordDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.TimeSleeperRecordDuration, new(1f, 20f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = PhantomCooldown;
    }

    bool IUsePhantomButton.IsPhantomRole => true;

    static bool IsUsingMovingPlatform(PlayerControl pc)
    {
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (pc.onLadder) return true;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship
            && Vector2.Distance(pc.GetTruePosition(), new Vector2(7.76f, 8.56f)) <= 1.9f) return true;
        if (pc.inMovingPlat) return true;
        return false;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (isRecording) return;
        if (!Player.IsAlive()) return;

        isRecording = true;
        recordTimer = 0f;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {                   
            if (IsUsingMovingPlatform(pc))
            {
                rewindSkipPlayers.Add(pc.PlayerId);
                continue;
            }
            positionHistory[pc.PlayerId] = pc.transform.position;
        }
        AURoleOptions.PhantomCooldown = RecordDuration;
        Player.RpcResetAbilityCooldown();

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
        SendRpc();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (isRecording)
        {
            recordTimer += Time.fixedDeltaTime;
            Sabotage = GetActiveSabotage();
            if (recordTimer >= RecordDuration)
            {
                isRecording = false;
                Rewinding();
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                SendRpc();
            }
            return;
        }
    }
    private readonly SystemTypes[] SabotageTypes = new[]
    {
        SystemTypes.Reactor,
        SystemTypes.Laboratory,
        SystemTypes.Electrical,
        SystemTypes.LifeSupp,
        SystemTypes.Comms,
        SystemTypes.HeliSabotage,
        SystemTypes.MushroomMixupSabotage,
    };

    public SystemTypes? GetActiveSabotage()
    {
        foreach (var type in SabotageTypes)
        {
            if (Utils.IsActive(type))
                return type;
        }
        return Sabotage;
    }
    void Rewinding()
    {
        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            if (p.inVent)
            {
                Player.MyPhysics?.RpcBootFromVent(VentedPlayers[p.PlayerId]);
            }
            p.RpcSnapToForced(positionHistory[p.PlayerId]);
        }
        AURoleOptions.PhantomCooldown = PhantomCooldown;
        Player.RpcResetAbilityCooldown();
        if (Sabotage is not null)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                Sabotage = null;
                return;
            }
            MessageWriter SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, Player.GetClientId());
            SabotageFixWriter.Write((byte)Sabotage);
            SabotageFixWriter.WriteNetObject(Player);
            AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);

            foreach (var target in PlayerCatch.AllPlayerControls)
            {
                if (target == Player || target.Data.Disconnected) continue;
                SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, target.GetClientId());
                SabotageFixWriter.Write((byte)Sabotage);
                SabotageFixWriter.WriteNetObject(target);
                AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);
            }
        }
        Sabotage = null;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        isRecording = false;
        recordTimer = 0f;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();
        VentedPlayers.Clear();
        Sabotage = null;
        SendRpc();
    }
    public static bool OnEnterVentOthers(PlayerPhysics physics, int ventId)
    {
        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            if (p.GetRoleClass() is TimeSleeper ts)
            {
                ts.VentedPlayers[physics.myPlayer.PlayerId] = ventId;
            }
        }
        return true;
    }

    public override void OnStartMeeting()
    {
        isRecording = false;
        recordTimer = 0f;
        positionHistory.Clear();
        rewindSkipPlayers.Clear();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = PhantomCooldown;
        Player.RpcResetAbilityCooldown();
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(isRecording);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isRecording = reader.ReadBoolean();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        if (isRecording)
        {
            var remaining = RecordDuration - recordTimer;
            return $"{(isForHud ? "" : "<size=60%>")}<color=#a0c4ff>記録中... {remaining:F1}s</color>";
        }
        return $"{(isForHud ? "" : "<size=60%>")}<color=#a0c4ff>ファントムボタン → タイムスリープ</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (isRecording) return $"<color=#a0c4ff>●REC</color>";
        return "";
    }

    public override string GetAbilityButtonText() => "タイムリープ";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "TimeSleeper_Ability";
        return true;
    }
}
