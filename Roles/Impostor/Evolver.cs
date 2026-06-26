using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using Hazel;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class Evolver : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Evolver),
            player => new Evolver(player),
            CustomRoles.Evolver,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            4700,
            SetupOptionItem,
            "ev",
            OptionSort: (4, 5),
            from: From.ExtremeRoles,
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter)
        );

    public Evolver(PlayerControl player)
    : base(RoleInfo, player)
    {
        HasOtherVision = OptionHasOtherVision.GetBool();
        Vision = OptionVision.GetFloat();
        ReceiveVisionEffect = OptionReceiveVisionEffect.GetBool();
        HasOtherKillCooldown = OptionHasOtherKillCooldown.GetBool();
        DefaultKillCooldown = OptionKillCooldown.GetFloat();
        PlayAnimation = OptionPlayAnimation.GetBool();
        RemoveBodyAfterEvolve = OptionRemoveBody.GetBool();
        EatRange = OptionEatRange.GetFloat();
        ReduceRate = OptionReduceRate.GetFloat();
        ReduceRateMultiplier = OptionReduceRateMultiplier.GetFloat();
        EvolveCooldown = OptionEvolveCooldown.GetFloat();
        EatTime = OptionEatTime.GetFloat();
        MaxEvolveCount = OptionMaxEvolveCount.GetInt();

        CurrentKillCooldown = HasOtherKillCooldown ? DefaultKillCooldown : Options.DefaultKillCooldown;
        InitialKillCooldown = CurrentKillCooldown;
        EvolveCount = 0;
        evolveCooldownTimer = 0f;
        pendingEvolve = null;
        EatenBodies.Clear();

        if (!RemoveBodyAfterEvolve)
            CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    static OptionItem OptionHasOtherVision, OptionVision, OptionReceiveVisionEffect;
    static OptionItem OptionHasOtherKillCooldown, OptionKillCooldown;
    static OptionItem OptionPlayAnimation, OptionRemoveBody, OptionEatRange;
    static OptionItem OptionReduceRate, OptionReduceRateMultiplier;
    static OptionItem OptionEvolveCooldown, OptionEatTime, OptionMaxEvolveCount;
    static OptionItem OptionHasOtherKillRange, OptionKillRange;

    static bool HasOtherVision, ReceiveVisionEffect, HasOtherKillCooldown;
    static bool PlayAnimation, RemoveBodyAfterEvolve;
    static float Vision, DefaultKillCooldown, EatRange;
    static float ReduceRate, ReduceRateMultiplier, EvolveCooldown, EatTime;
    static int MaxEvolveCount;

    enum OptionName
    {
        EvolverHasOtherVision, EvolverVision, EvolverReceiveVisionEffect,
        EvolverHasOtherKillCooldown, EvolverHasOtherKillRange, EvolverKillRange,
        EvolverPlayAnimation, EvolverRemoveBody, EvolverEatRange,
        EvolverReduceRate, EvolverReduceRateMultiplier,
        EvolverEvolveCooldown, EvolverEatTime, EvolverMaxEvolveCount,
    }

    private static void SetupOptionItem()
    {
        OptionHasOtherVision = BooleanOptionItem.Create(RoleInfo, 10, OptionName.EvolverHasOtherVision, false, false);
        OptionVision = FloatOptionItem.Create(RoleInfo, 11, OptionName.EvolverVision, new(0f, 5f, 0.05f), 1.25f, false, OptionHasOtherVision)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionReceiveVisionEffect = BooleanOptionItem.Create(RoleInfo, 12, OptionName.EvolverReceiveVisionEffect, true, false, OptionHasOtherVision);

        OptionHasOtherKillCooldown = BooleanOptionItem.Create(RoleInfo, 13, OptionName.EvolverHasOtherKillCooldown, true, false);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 14, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false, OptionHasOtherKillCooldown)
            .SetValueFormat(OptionFormat.Seconds);

        OptionHasOtherKillRange = BooleanOptionItem.Create(RoleInfo, 15, OptionName.EvolverHasOtherKillRange, false, false);
        OptionKillRange = StringOptionItem.Create(RoleInfo, 16, OptionName.EvolverKillRange,
            new[] { "Short", "Middle", "Long" }, 1, false, OptionHasOtherKillRange);

        OptionPlayAnimation = BooleanOptionItem.Create(RoleInfo, 17, OptionName.EvolverPlayAnimation, true, false);
        OptionRemoveBody = BooleanOptionItem.Create(RoleInfo, 18, OptionName.EvolverRemoveBody, false, false);
        OptionEatRange = FloatOptionItem.Create(RoleInfo, 19, OptionName.EvolverEatRange, new(0.5f, 5f, 0.25f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);

        OptionReduceRate = FloatOptionItem.Create(RoleInfo, 20, OptionName.EvolverReduceRate, new(0f, 90f, 1f), 10f, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionReduceRateMultiplier = FloatOptionItem.Create(RoleInfo, 21, OptionName.EvolverReduceRateMultiplier, new(1f, 3f, 0.1f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionEvolveCooldown = FloatOptionItem.Create(RoleInfo, 22, OptionName.EvolverEvolveCooldown, new(0f, 120f, 1f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionEatTime = FloatOptionItem.Create(RoleInfo, 23, OptionName.EvolverEatTime, new(0.5f, 30f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMaxEvolveCount = IntegerOptionItem.Create(RoleInfo, 24, OptionName.EvolverMaxEvolveCount, new(0, 15, 1), 5, false)
            .SetZeroNotation(OptionZeroNotation.Infinity);
    }

    float CurrentKillCooldown;
    float InitialKillCooldown;
    int EvolveCount;
    float evolveCooldownTimer;
    float lastAppliedKillCooldown = -1f;

    public float CalculateKillCooldown() => Mathf.Max(CurrentKillCooldown, 0.01f);
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanKill => Player.IsAlive();
    public bool IsKiller => true;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void OnSpawn(bool initialState = false)
    {
        if (initialState)
        {
            EvolveCount = 0;
            // ★ ゲーム開始時は EvolveCooldown でタイマーを初期化（即捕食防止）
            evolveCooldownTimer = EvolveCooldown;
            pendingEvolve = null;
            EatenBodies.Clear();
            CurrentKillCooldown = HasOtherKillCooldown ? DefaultKillCooldown : Options.DefaultKillCooldown;
            InitialKillCooldown = CurrentKillCooldown;

            (this as IUsePhantomButton).Init(Player);
            IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
            Player.RpcResetAbilityCooldown(Sync: true);

            Player.SetKillCooldown(CurrentKillCooldown, force: true, delay: true);
            Player.SyncSettings();
        }
        else
        {
            SyncPhantomCooldown();
        }
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        float phantomCd;
        if (pendingEvolve != null)
            // 捕食中 → 捕食完了までの残り時間
            phantomCd = Mathf.Max(0.1f, pendingEvolve.Required - pendingEvolve.Elapsed);
        else if (evolveCooldownTimer > 0f)
            // 進化クール中 → 残りクール
            phantomCd = evolveCooldownTimer;
        else
            // ★ 即使用可能状態でも 0 にはしない。EvolveCooldown を返してボタンに表示。
            //    （OnSpawn・AfterMeetingTasks でタイマーをリセットするため
            //      通常は evolveCooldownTimer > 0 のルートを通る）
            phantomCd = EvolveCooldown > 0f ? EvolveCooldown : 0.1f;
        AURoleOptions.PhantomCooldown = phantomCd;

        if (HasOtherVision)
        {
            var v = Mathf.Clamp(Vision, 0f, 5f);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, v);
            opt.SetFloat(FloatOptionNames.CrewLightMod, v);
            if (ReceiveVisionEffect && Utils.IsActive(SystemTypes.Electrical))
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, v / 5f);
        }
        if (OptionHasOtherKillRange != null && OptionHasOtherKillRange.GetBool())
            opt.SetInt(Int32OptionNames.KillDistance, Mathf.Clamp(OptionKillRange.GetValue(), 0, 2));
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            ResetCooldown = false;
            return;
        }

        if (pendingEvolve != null || !CanEvolveNow())
        {
            ResetCooldown = false;
            return;
        }
        var body = GetNearestEatableBody();
        if (body == null)
        {
            ResetCooldown = false;
            return;
        }

        BeginEvolve(body.ParentId, body.TruePosition);
        Player.SyncSettings();
        ResetCooldown = true;
    }

    sealed class PendingEvolveInfo
    {
        public byte BodyId;
        public Vector2 BodyPos;
        public float Elapsed;
        public float Required;
        public PendingEvolveInfo(byte id, Vector2 pos, float req)
        { BodyId = id; BodyPos = pos; Required = req; Elapsed = 0f; }
    }
    PendingEvolveInfo pendingEvolve;

    static readonly HashSet<byte> EatenBodies = new();

    [Attributes.GameModuleInitializer]
    public static void Init() => EatenBodies.Clear();

    bool CanEvolveNow()
        => Player.IsAlive()
        && pendingEvolve == null
        && evolveCooldownTimer <= 0f
        && (MaxEvolveCount <= 0 || EvolveCount < MaxEvolveCount);

    DeadBodyInfo GetNearestEatableBody()
    {
        DeadBodyInfo nearest = null;
        var myPos = Player.GetTruePosition();
        foreach (var db in Object.FindObjectsOfType<DeadBody>())
        {
            var id = db.ParentId;
            if (EatenBodies.Contains(id)) continue;
            var pos = (Vector2)db.TruePosition;
            var dist = Vector2.Distance(myPos, pos);
            if (dist > EatRange) continue;
            if (nearest == null || dist < nearest.Distance)
                nearest = new DeadBodyInfo(id, pos, dist);
        }
        return nearest;
    }

    sealed class DeadBodyInfo
    {
        public byte ParentId; public Vector2 TruePosition; public float Distance;
        public DeadBodyInfo(byte id, Vector2 pos, float d) { ParentId = id; TruePosition = pos; Distance = d; }
    }

    void BeginEvolve(byte bodyId, Vector2 bodyPos)
    {
        pendingEvolve = new PendingEvolveInfo(bodyId, bodyPos, Mathf.Max(0.5f, EatTime));
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    bool IsWithinBodyRange()
        => pendingEvolve != null
        && Vector2.Distance(Player.GetTruePosition(), pendingEvolve.BodyPos) <= EatRange;

    bool BodyStillExists(byte bodyId)
        => Object.FindObjectsOfType<DeadBody>().Any(b => b.ParentId == bodyId)
        && !EatenBodies.Contains(bodyId);

    void CancelEvolve(bool syncButton = true)
    {
        if (pendingEvolve == null) return;
        pendingEvolve = null;
        if (syncButton)
            SyncPhantomCooldown();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void ApplyKillCooldown(bool delay)
    {
        if (!Player.IsAlive()) return;
        if (Mathf.Approximately(lastAppliedKillCooldown, CurrentKillCooldown)) return;
        lastAppliedKillCooldown = CurrentKillCooldown;
        Player.SetKillCooldown(CurrentKillCooldown, force: true, delay: delay);
        Player.SyncSettings();
    }

    void SyncPhantomCooldown()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        Player.RpcResetAbilityCooldown(Sync: true);
    }

    void CompleteEvolve()
    {
        var bodyId = pendingEvolve.BodyId;
        pendingEvolve = null;

        float thisRate = ReduceRate * Mathf.Pow(ReduceRateMultiplier, EvolveCount);
        thisRate = Mathf.Clamp(thisRate, 0f, 100f);

        float before = CurrentKillCooldown;
        CurrentKillCooldown = Mathf.Clamp(
            CurrentKillCooldown * (1f - thisRate / 100f),
            0.1f, InitialKillCooldown);
        EvolveCount++;
        evolveCooldownTimer = EvolveCooldown;

        Logger.Info($"進化{EvolveCount}回目: KillCool {before:0.0} → {CurrentKillCooldown:0.0} (減少率 {thisRate:0.0}%)", "Evolver");
        UtilsGameLog.AddGameLog("Evolver", string.Format(GetString("EvolverEvolveLog"),
            UtilsName.GetPlayerColor(Player), EvolveCount, CurrentKillCooldown.ToString("0.0")));

        if (PlayAnimation) PlayEvolveAnimation();

        EatenBodies.Add(bodyId);

        // キルクールタイマーはリセットしない（次のキル後から短縮後CDが適用）
        SyncPhantomCooldown();

        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void PlayEvolveAnimation()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var dummy = PlayerCatch.AllPlayerControls
            .Where(pc => pc != null && pc.PlayerId != Player.PlayerId)
            .FirstOrDefault() ?? Player;

        Player.RpcShapeshift(dummy, true);
        Player.RpcShapeshift(Player, true);

        var sender = CustomRpcSender.Create("EvolverEvolveShape");
        sender.AutoStartRpc(Player.NetId, RpcCalls.Shapeshift).Write(dummy).Write(true).EndRpc();
        sender.AutoStartRpc(Player.NetId, RpcCalls.Shapeshift).Write(Player).Write(true).EndRpc();
        sender.EndMessage();
        sender.SendMessage();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (evolveCooldownTimer > 0f)
            evolveCooldownTimer = Mathf.Max(0f, evolveCooldownTimer - Time.fixedDeltaTime);

        if (pendingEvolve == null) return;

        if (!Player.IsAlive()) { CancelEvolve(); return; }
        if (!BodyStillExists(pendingEvolve.BodyId)) { CancelEvolve(); return; }
        if (!IsWithinBodyRange()) { CancelEvolve(); return; }

        pendingEvolve.Elapsed += Time.fixedDeltaTime;
        if (pendingEvolve.Elapsed >= pendingEvolve.Required)
            CompleteEvolve();
    }

    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (target == null) return false;
        if (EatenBodies.Contains(target.PlayerId))
        {
            reason = DontReportreson.Eat;
            return true;
        }
        return false;
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
        => CancelEvolve();

    public override void OnStartMeeting()
        => CancelEvolve();

    public override void AfterMeetingTasks()
    {
        pendingEvolve = null;
        EatenBodies.Clear();
        if (!Player.IsAlive()) return;

        lastAppliedKillCooldown = -1f;
        ApplyKillCooldown(delay: true);

        // ★ 会議後は常に EvolveCooldown でリセット（キルクールと同じ挙動）
        //    タイマー残量にかかわらず上書きし、即捕食できないようにする
        evolveCooldownTimer = EvolveCooldown;
        SyncPhantomCooldown();
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!isForMeeting) return "";
        if (EatenBodies.Contains(seen.PlayerId))
            return $"<color=#6f4204>×</color>";
        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var max = MaxEvolveCount > 0 ? MaxEvolveCount.ToString() : "∞";
        return $" <color={RoleInfo.RoleColorCode}>({EvolveCount}/{max}) {CurrentKillCooldown:0.0}s</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || !Is(seer)) return "";
        if (pendingEvolve != null && !isForMeeting)
        {
            var remain = Mathf.Max(0f, pendingEvolve.Required - pendingEvolve.Elapsed);
            return $"<size=80%><color={RoleInfo.RoleColorCode}>{string.Format(GetString("EvolverEating"), remain.ToString("0.0"))}</color></size>";
        }
        return "";
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(CurrentKillCooldown);
        sender.Writer.Write(EvolveCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        CurrentKillCooldown = reader.ReadSingle();
        EvolveCount = reader.ReadInt32();
    }
}