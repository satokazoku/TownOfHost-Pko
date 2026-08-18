/*
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;

namespace TownOfHost.Roles.Neutral;

public sealed class Dancer : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Dancer),
            player => new Dancer(player),
            CustomRoles.Dancer,
            () => OptCanVent.GetBool() ? RoleTypes.Impostor : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            63200,
            SetupOptionItem,
            "dc",
            "#f39800",
            (6, 10),
            from: From.NebulaontheShip,
            isDesyncImpostor: true
        );

    public Dancer(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        ResetRuntimeState();
    }

    static OptionItem OptNumToWin;
    static OptionItem OptDanceCooldown;
    static OptionItem OptDanceDuration;
    static OptionItem OptDanceRange;
    static OptionItem OptForecastDuration;
    static OptionItem OptFollowingSuicide;
    static OptionItem OptCanVent;
    static OptionItem OptLastDanceMode;
    static OptionItem OptDeathNotification;

    enum OptionName
    {
        DancerNumToWin,
        DancerDanceCooldown,
        DancerDanceDuration,
        DancerDanceRange,
        DancerForecastDuration,
        DancerFollowingSuicide,
        DancerLastDanceMode,
        DancerDeathNotification,
    }

    // ダンス(ゆらぎ)検出用
    Vector2? lastPos;
    float recentPathLength;
    Vector2 recentDisplacement;
    float danceGauge;
    float dancingProgress;
    float notDancingProgress;
    bool eventInvoked;
    Vector2 danceStartPos;

    // 死のダンス管理: 死体のそばで踊ると"次の1回"だけ死のダンスになるトグル方式(本家準拠)
    bool nextIsDeathDance;
    readonly HashSet<byte> usedCorpseOwnerIds = new();

    // 死の預言
    readonly Dictionary<byte, float> activeMarks = new(); // playerId -> 残り秒数
    readonly HashSet<byte> completedMarks = new(); // キル or 予言成就済み

    // ラストダンスモード
    bool winReady;

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 20);

        OptNumToWin = IntegerOptionItem.Create(RoleInfo, 10, OptionName.DancerNumToWin, new(1, 10, 1), 4, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptDanceCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.DancerDanceCooldown, new(2.5f, 60f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptDanceDuration = FloatOptionItem.Create(RoleInfo, 12, OptionName.DancerDanceDuration, new(1f, 20f, 1f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptDanceRange = FloatOptionItem.Create(RoleInfo, 13, OptionName.DancerDanceRange, new(1f, 10f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Times);
        OptForecastDuration = FloatOptionItem.Create(RoleInfo, 14, OptionName.DancerForecastDuration, new(20f, 300f, 10f), 90f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptFollowingSuicide = BooleanOptionItem.Create(RoleInfo, 15, OptionName.DancerFollowingSuicide, true, false);
        OptCanVent = BooleanOptionItem.Create(RoleInfo, 16, GeneralOption.CanVent, true, false);
        OptLastDanceMode = BooleanOptionItem.Create(RoleInfo, 17, OptionName.DancerLastDanceMode, false, false);
        OptDeathNotification = BooleanOptionItem.Create(RoleInfo, 18, OptionName.DancerDeathNotification, true, false);
    }

    public float CalculateKillCooldown() => 0f;
    public bool CanUseKillButton() => false; // キルボタンは使わない(ダンスで自動判定)
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => OptCanVent?.GetBool() ?? true;

    public override void Add()
    {
        ResetRuntimeState();
        ApplyDeathNotificationDesync();
    }

    public override void AfterMeetingTasks()
    {
        ResetDanceProgress();
        ApplyDeathNotificationDesync();
    }

    void ResetRuntimeState()
    {
        lastPos = null;
        recentPathLength = 0f;
        recentDisplacement = Vector2.zero;
        danceGauge = 0f;
        dancingProgress = 0f;
        notDancingProgress = 0f;
        eventInvoked = false;
        nextIsDeathDance = false;
        usedCorpseOwnerIds.Clear();
        activeMarks.Clear();
        completedMarks.Clear();
        winReady = false;
    }

    void ResetDanceProgress()
    {
        lastPos = null;
        recentPathLength = 0f;
        recentDisplacement = Vector2.zero;
        danceGauge = 0f;
        dancingProgress = 0f;
        notDancingProgress = 0f;
        eventInvoked = false;
    }

    // サイキックと同じ要領: 死体が出た時にバニラのノイズメーカー通知が鳴るよう、
    // 自分視点で全プレイヤーをノイズメーカーとしてデシンクしておく(自作の矢印システムは不要)。
    void ApplyDeathNotificationDesync()
    {
        if (!OptDeathNotification.GetBool()) return;
        if (!Player.IsAlive()) return;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (Player == PlayerControl.LocalPlayer)
                pc.StartCoroutine(pc.CoSetRole(RoleTypes.Noisemaker, true));
            else
                pc.RpcSetRoleDesync(RoleTypes.Noisemaker, Player.GetClientId());
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive())
        {
            ResetDanceProgress();
            return;
        }
        if (!GameStates.IsInTask || GameStates.CalledMeeting || GameStates.Intro) return;

        UpdateMarkTimers();
        UpdateDanceDetection();
    }

    // 本家のdistance/displacementの指数減衰トラッキングをそのまま移植(定数も同じ)。
    // 「移動量はあるが正味の変位が小さい」＝足踏み/ゆらぎをダンスとして検出する。
    void UpdateDanceDetection()
    {
        var currentPos = Player.GetTruePosition();
        if (lastPos.HasValue)
        {
            recentPathLength = recentPathLength * 0.89f + Vector2.Distance(currentPos, lastPos.Value);
            recentDisplacement = recentDisplacement * 0.89f + (currentPos - lastPos.Value);
        }
        lastPos = currentPos;

        bool wiggling = recentPathLength > 0.3f && recentDisplacement.magnitude < 0.18f;
        danceGauge = wiggling
            ? Mathf.Min(danceGauge + Time.fixedDeltaTime * 4.2f, 1f)
            : Mathf.Max(danceGauge - Time.fixedDeltaTime * 2.7f, 0f);

        bool isDancing = danceGauge > 0.7f;

        if (isDancing)
        {
            if (dancingProgress < 0.1f) danceStartPos = currentPos;
            dancingProgress += Time.fixedDeltaTime;
            notDancingProgress = 0f;

            if (dancingProgress > OptDanceDuration.GetFloat() && !eventInvoked)
            {
                eventInvoked = true;
                OnDanceComplete(danceStartPos);
            }
        }
        else
        {
            eventInvoked = false;
            notDancingProgress += Time.fixedDeltaTime;
            if (notDancingProgress > 0.5f) ResetDanceProgress();
        }
    }

    // ダンス完遂時。死体のそばで踊ったかで「次の」ダンスが死のダンスになるかを決める(トグル/フラグ方式、蓄積しない)。
    void OnDanceComplete(Vector2 origin)
    {
        // NoS本家のパーティクル演出はホストMODで再現できないため、守護のパリン音(RpcProtectPlayer)で代用する。
        Player.RpcProtectPlayer(Player, Player.Data.DefaultOutfit.ColorId);

        bool isDeathDance = nextIsDeathDance;
        float range = OptDanceRange.GetFloat();

        bool foundFreshCorpse = false;
        foreach (var corpse in Object.FindObjectsOfType<DeadBody>())
        {
            if (Vector2.Distance(origin, corpse.transform.position) > range) continue;
            if (!usedCorpseOwnerIds.Add(corpse.ParentId)) continue;
            foundFreshCorpse = true;
        }
        // 死のダンスを終えると通常に戻るが、その場に未使用の死体があれば次も即死のダンスになる。
        nextIsDeathDance = foundFreshCorpse;

        var nearbyTargets = PlayerCatch.AllAlivePlayerControls
            .Where(p => p.PlayerId != Player.PlayerId)
            .Where(p => Vector2.Distance(origin, p.GetTruePosition()) <= range)
            .Where(p => !completedMarks.Contains(p.PlayerId))
            .ToList();

        if (isDeathDance)
        {
            foreach (var target in nearbyTargets)
            {
                activeMarks.Remove(target.PlayerId);
                completedMarks.Add(target.PlayerId);
                CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, deathReason: CustomDeathReason.Kill);
            }
        }
        else
        {
            foreach (var target in nearbyTargets)
                activeMarks[target.PlayerId] = OptForecastDuration.GetFloat();
        }

        if (!winReady && completedMarks.Count >= OptNumToWin.GetInt())
        {
            if (!OptLastDanceMode.GetBool() || nearbyTargets.Count > 0)
                winReady = true;
        }

        ResetDanceProgress();
        SendRPC();
    }

    void UpdateMarkTimers()
    {
        if (activeMarks.Count == 0) return;

        List<byte> expired = null;
        foreach (var id in activeMarks.Keys.ToArray())
        {
            activeMarks[id] -= Time.fixedDeltaTime;
            if (activeMarks[id] > 0f) continue;
            (expired ??= new()).Add(id);
        }
        if (expired == null) return;
        foreach (var id in expired) activeMarks.Remove(id);
    }

    // 予言中のプレイヤーが(キル以外の理由も含め)死亡したら成就とみなす
    public static void OnMurderPlayerOthers(MurderInfo info)
    {
        var target = info.AttemptTarget;
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetRoleClass() is not Dancer dancer) continue;
            dancer.TryCompleteMark(target.PlayerId);
        }
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        TryCompleteMark(exiled.PlayerId);

        // 自分自身が追放された場合も道連れ判定にかける
        if (exiled.PlayerId == Player.PlayerId) TryFollowingSuicide();
    }

    void TryCompleteMark(byte targetId)
    {
        if (!activeMarks.Remove(targetId)) return;
        completedMarks.Add(targetId);
        SendRPC();
    }

    // 自分が(会議以外で)死亡した際、予言中のプレイヤーを道連れにする(オプション)
    public override void OnMurderPlayerAsTarget(MurderInfo info) => TryFollowingSuicide();

    void TryFollowingSuicide()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!OptFollowingSuicide.GetBool()) return;

        foreach (var id in activeMarks.Keys.ToArray())
        {
            var target = GetPlayerById(id);
            if (target == null || !target.IsAlive()) continue;
            PlayerState.GetByPlayerId(id).DeathReason = CustomDeathReason.Suicide;
            target.RpcMurderPlayerV2(target);
        }
        activeMarks.Clear();
        SendRPC();
    }

    public static bool CheckWin(ref GameOverReason reason)
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetRoleClass() is not Dancer dancer) continue;
            if (!pc.IsAlive()) continue;
            if (!dancer.winReady) continue;

            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Dancer, pc.PlayerId, AddWin: false))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                foreach (var claimedId in dancer.completedMarks)
                    CustomWinnerHolder.WinnerIds.Add(claimedId);
                reason = GameOverReason.ImpostorsByKill;
                return true;
            }
        }
        return false;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer)) return "";
        if (completedMarks.Contains(seen.PlayerId)) return Utils.ColorString(RoleInfo.RoleColor, "◆");
        if (activeMarks.ContainsKey(seen.PlayerId)) return Utils.ColorString(RoleInfo.RoleColor, "◇");
        return "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        string sz = isForHud ? "" : "<size=60%>";
        string deathDancePart = nextIsDeathDance ? " <color=#00CFFF>次は死のダンス</color>" : "";
        string readyPart = winReady && OptLastDanceMode.GetBool() ? " <color=#ffff00>ラストダンス待機中</color>" : "";
        return $"{sz}<color={RoleInfo.RoleColorCode}>予言 {completedMarks.Count}/{OptNumToWin.GetInt()}{deathDancePart}{readyPart}</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({completedMarks.Count}/{OptNumToWin.GetInt()})");

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(nextIsDeathDance);
        sender.Writer.Write(winReady);
        sender.Writer.Write((byte)completedMarks.Count);
        foreach (var id in completedMarks) sender.Writer.Write(id);
        sender.Writer.Write((byte)activeMarks.Count);
        foreach (var kvp in activeMarks)
        {
            sender.Writer.Write(kvp.Key);
            sender.Writer.Write(kvp.Value);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        nextIsDeathDance = reader.ReadBoolean();
        winReady = reader.ReadBoolean();

        completedMarks.Clear();
        var completedCount = reader.ReadByte();
        for (int i = 0; i < completedCount; i++) completedMarks.Add(reader.ReadByte());

        activeMarks.Clear();
        var activeCount = reader.ReadByte();
        for (int i = 0; i < activeCount; i++)
        {
            var id = reader.ReadByte();
            var remain = reader.ReadSingle();
            activeMarks[id] = remain;
        }
    }
}
*/