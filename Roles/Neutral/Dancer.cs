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

    enum OptionName
    {
        DancerNumToWin,
        DancerDanceCooldown,
        DancerDanceDuration,
        DancerDanceRange,
        DancerForecastDuration,
        DancerFollowingSuicide,
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

    // 死の舞踏/死の預言
    float danceCooldownRemaining;
    int killChargesLeft;
    readonly HashSet<byte> usedCorpseOwnerIds = new();
    readonly Dictionary<byte, float> activeMarks = new(); // playerId -> 残り秒数
    readonly HashSet<byte> completedMarks = new(); // キル or 予言成就済み

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
    }

    public float CalculateKillCooldown() => 0f;
    public bool CanUseKillButton() => false; // キルボタンは使わない(ダンスで自動判定)
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => OptCanVent?.GetBool() ?? true;

    public override void Add() => ResetRuntimeState();
    public override void AfterMeetingTasks() => ResetDanceProgress();

    void ResetRuntimeState()
    {
        lastPos = null;
        recentPathLength = 0f;
        recentDisplacement = Vector2.zero;
        danceGauge = 0f;
        dancingProgress = 0f;
        notDancingProgress = 0f;
        eventInvoked = false;
        danceCooldownRemaining = 0f;
        killChargesLeft = 0;
        usedCorpseOwnerIds.Clear();
        activeMarks.Clear();
        completedMarks.Clear();
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

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive())
        {
            ResetDanceProgress();
            return;
        }
        if (!GameStates.IsInTask || GameStates.CalledMeeting || GameStates.Intro) return;

        if (danceCooldownRemaining > 0f)
            danceCooldownRemaining = Mathf.Max(0f, danceCooldownRemaining - Time.fixedDeltaTime);

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

        bool isDancing = danceGauge > 0.7f && danceCooldownRemaining <= 0f;

        if (isDancing)
        {
            if (dancingProgress < 0.1f) danceStartPos = currentPos;
            dancingProgress += Time.fixedDeltaTime;
            notDancingProgress = 0f;

            if (dancingProgress > OptDanceDuration.GetFloat() && !eventInvoked)
            {
                eventInvoked = true;
                OnDanceComplete(danceStartPos);
                ResetDanceProgress();
                danceCooldownRemaining = OptDanceCooldown.GetFloat();
            }
        }
        else
        {
            eventInvoked = false;
            notDancingProgress += Time.fixedDeltaTime;
            if (notDancingProgress > 0.5f) ResetDanceProgress();
        }
    }

    // ダンス完遂時: 範囲内の死体でチャージ回復、範囲内の生存者をキル(チャージがあれば)か予言マークする
    void OnDanceComplete(Vector2 origin)
    {
        // NoS本家のパーティクル演出はホストMODで再現できないため、守護のパリン音(RpcProtectPlayer)で代用する。
        Player.RpcProtectPlayer(Player, Player.Data.DefaultOutfit.ColorId);

        float range = OptDanceRange.GetFloat();

        foreach (var corpse in Object.FindObjectsOfType<DeadBody>())
        {
            if (Vector2.Distance(origin, corpse.transform.position) > range) continue;
            if (!usedCorpseOwnerIds.Add(corpse.ParentId)) continue;
            killChargesLeft++;
        }

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
            if (Vector2.Distance(origin, target.GetTruePosition()) > range) continue;
            if (completedMarks.Contains(target.PlayerId)) continue;

            if (killChargesLeft > 0)
            {
                killChargesLeft--;
                activeMarks.Remove(target.PlayerId);
                completedMarks.Add(target.PlayerId);
                CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, deathReason: CustomDeathReason.Kill);
            }
            else
            {
                activeMarks[target.PlayerId] = OptForecastDuration.GetFloat();
            }
        }
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
    }

    void TryCompleteMark(byte targetId)
    {
        if (!activeMarks.Remove(targetId)) return;
        completedMarks.Add(targetId);
    }

    // 自分が死亡した際、予言中のプレイヤーを道連れにする(オプション)
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
    }

    public static bool CheckWin(ref GameOverReason reason)
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetRoleClass() is not Dancer dancer) continue;
            if (!pc.IsAlive()) continue;
            if (dancer.completedMarks.Count < OptNumToWin.GetInt()) continue;

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
        string killPart = killChargesLeft > 0 ? $" 死の舞踏x{killChargesLeft}" : "";
        return $"{sz}<color={RoleInfo.RoleColorCode}>予言 {completedMarks.Count}/{OptNumToWin.GetInt()}{killPart}</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({completedMarks.Count}/{OptNumToWin.GetInt()})");

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(killChargesLeft);
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
        killChargesLeft = reader.ReadInt32();
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