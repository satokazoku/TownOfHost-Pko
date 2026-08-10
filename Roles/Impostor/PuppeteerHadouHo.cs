using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;

namespace TownOfHost.Roles.Impostor;

public sealed class PuppeteerHadouHo : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(PuppeteerHadouHo),
            player => new PuppeteerHadouHo(player),
            CustomRoles.PuppeteerHadouHo,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            8400,
            SetUpOptionItem,
            "phh",
            OptionSort: (3, 15),
            from: From.TownOfHost_Pko
        );

    public PuppeteerHadouHo(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptKillCooldown.GetFloat();
        PhantomCooldown = OptPhantomCooldown.GetFloat();
        puppetId = byte.MaxValue;
        isCharging = false;
        isFiring = false;
        chargeTimer = 0f;
        beamTimer = 0f;
    }

    static OptionItem OptKillCooldown;
    static float KillCooldown;
    static OptionItem OptPhantomCooldown;
    static float PhantomCooldown;
    static OptionItem OptDelay;
    static OptionItem OptSelfDestructOnMiss;
    static OptionItem OptSelfDestructTarget;
    static OptionItem OptSuperEnabled;
    static OptionItem OptSuperChance;

    const float NormalBeamWidth = 1.3f;
    const float SuperBeamWidth = 2.6f;
    const float BeamDuration = 3f; // 本家HadouHoのShowBeamMark継続時間と同じ
    static readonly Vector2 AirshipLiftPosition = new(7.76f, 8.56f); // 昇降機の座標(CharismaStar.csより)

    byte puppetId;
    bool isCharging;
    bool isFiring;
    float chargeTimer;
    float beamTimer;
    bool hasHitAnyone;
    bool beamFacingLeft;
    float currentHitWidth;

    internal static readonly Dictionary<byte, PuppeteerHadouHo> ActivePuppets = new();

    [Attributes.GameModuleInitializer]
    public static void ResetActivePuppets() => ActivePuppets.Clear();

    public override void OnDestroy()
    {
        if (puppetId != byte.MaxValue) ActivePuppets.Remove(puppetId);
    }

    enum OptionName
    {
        PuppeteerHadouHoDelay,
        PuppeteerHadouHoSelfDestruct,
        PuppeteerHadouHoEnableSuper,
        PuppeteerHadouHoSuperChance,
    }

    enum SelfDestructTarget { Puppeteer, Target }

    static void SetUpOptionItem()
    {
        OptKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0.5f, 60f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptPhantomCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, new(0.5f, 60f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptDelay = FloatOptionItem.Create(RoleInfo, 12, OptionName.PuppeteerHadouHoDelay, new(0.5f, 30f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptSelfDestructOnMiss = BooleanOptionItem.Create(RoleInfo, 13, OptionName.PuppeteerHadouHoSelfDestruct, false, false);
        OptSelfDestructTarget = StringOptionItem.Create(RoleInfo, 14, "PuppeteerHadouHoSelfDestructMode", EnumHelper.GetAllNames<SelfDestructTarget>(), 0, false, OptSelfDestructOnMiss);
        OptSuperEnabled = BooleanOptionItem.Create(RoleInfo, 15, OptionName.PuppeteerHadouHoEnableSuper, false, false);
        OptSuperChance = IntegerOptionItem.Create(RoleInfo, 16, OptionName.PuppeteerHadouHoSuperChance, new(1, 100, 1), 10, false, OptSuperEnabled)
            .SetValueFormat(OptionFormat.Percent);
    }

    float IKiller.CalculateKillCooldown() => KillCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = PhantomCooldown;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!Player.IsAlive() || puppetId != byte.MaxValue) return;

        var nearest = FindNearestValidTarget();
        if (nearest == null) return;

        puppetId = nearest.PlayerId;
        isCharging = true;
        isFiring = false;
        chargeTimer = 0f;
        ActivePuppets[puppetId] = this;

        SendRpc();
    }

    PlayerControl FindNearestValidTarget()
    {
        PlayerControl nearest = null;
        float minDist = float.MaxValue;
        var myPos = Player.GetTruePosition();

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
            if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;

            float dist = Vector2.Distance(myPos, target.GetTruePosition());
            if (dist < minDist)
            {
                minDist = dist;
                nearest = target;
            }
        }
        return nearest;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (puppetId == byte.MaxValue) return;

        if (!Player.IsAlive() || GameStates.CalledMeeting)
        {
            EndSequence();
            return;
        }

        var puppet = GetPlayerById(puppetId);
        if (puppet == null || !puppet.IsAlive())
        {
            EndSequence();
            return;
        }

        if (isCharging)
        {
            chargeTimer += Time.fixedDeltaTime;
            if (chargeTimer >= OptDelay.GetFloat())
                StartFiring(puppet);
            return;
        }

        if (isFiring)
        {
            // 本家HadouHoのShowBeamMark中と同じく、ビームが出てる間は毎tick当たり判定をやり続ける。
            ApplyBeamHit(puppet);

            beamTimer += Time.fixedDeltaTime;
            if (beamTimer >= BeamDuration)
                ResolveBeamEnd(puppet);
        }
    }

    void StartFiring(PlayerControl puppet)
    {
        bool invalidState =
            puppet.inVent
            || puppet.walkingToVent
            || puppet.onLadder
            || puppet.inMovingPlat
            || puppet.MyPhysics.Animations.IsPlayingEnterVentAnimation()
            || puppet.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
            || ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && Vector2.Distance(puppet.GetTruePosition(), AirshipLiftPosition) <= 1.9f)
            || PuppeteerHadouHoZiplineTracker.IsOnZipline(puppet.PlayerId);

        if (invalidState)
        {
            EndSequence();
            return;
        }

        isCharging = false;
        isFiring = true;
        beamTimer = 0f;
        hasHitAnyone = false;
        beamFacingLeft = puppet.cosmetics.FlipX;
        currentHitWidth = (OptSuperEnabled.GetBool() && RollChance(OptSuperChance.GetInt())) ? SuperBeamWidth : NormalBeamWidth;

        SendRpc();
        ApplyBeamHit(puppet); // 発射した瞬間にも1回判定(本家のFireBeam直後のApplyBeamHitと同じ)
    }

    void ApplyBeamHit(PlayerControl puppet)
    {
        if (!AmongUsClient.Instance.AmHost || !puppet.IsAlive()) return;

        var origin = puppet.GetTruePosition();
        Vector2 dir = beamFacingLeft ? Vector2.left : Vector2.right;

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == puppet.PlayerId) continue;
            if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;

            var toTarget = target.GetTruePosition() - origin;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            var proj = dir * dot;
            var perp = toTarget - proj;
            if (perp.magnitude > currentHitWidth) continue;

            // appearanceKiller=puppet: 見た目上は打たされたプレイヤーが撃った扱い。攻撃実績/勝利判定はPuppeteer(attemptKiller)側につく。
            CustomRoleManager.OnCheckMurder(Player, target, puppet, target, true, deathReason: CustomDeathReason.Evaporation);
            hasHitAnyone = true;
        }
    }

    void ResolveBeamEnd(PlayerControl puppet)
    {
        SetPuppetRoleTextHeight(puppet, false);

        if (!hasHitAnyone && OptSelfDestructOnMiss.GetBool())
        {
            var deadTarget = OptSelfDestructTarget.GetValue() == (int)SelfDestructTarget.Puppeteer ? Player : puppet;
            PlayerState.GetByPlayerId(deadTarget.PlayerId).DeathReason = CustomDeathReason.Suicide;
            deadTarget.RpcMurderPlayerV2(deadTarget);
        }

        EndSequence();
    }

    void EndSequence()
    {
        if (puppetId != byte.MaxValue) ActivePuppets.Remove(puppetId);
        puppetId = byte.MaxValue;
        isCharging = false;
        isFiring = false;
        chargeTimer = 0f;
        beamTimer = 0f;
        SendRpc();
    }

    internal bool TryBuildPuppetName(PlayerControl seen, ref string name, ref bool noMarker)
    {
        if (seen.PlayerId != puppetId) return false;
        if (!Player.IsAlive()) return false;

        string myColor = "#" + ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        if (isCharging)
        {
            bool facingLeft = seen.cosmetics.FlipX;
            string bigStar = $"<size=800%><color={myColor}>★</color></size>";
            string blank = "　　　";
            name = "<line-height=1200%>\n" + (facingLeft ? bigStar + blank : blank + bigStar) + "</line-height>";
            noMarker = true;
            return true;
        }

        if (isFiring)
        {
            SetPuppetRoleTextHeight(seen, true);
            bool fl = beamFacingLeft;
            string star = $"<voffset=0.35em><size=800%><color={myColor}>★</color></size></voffset>";
            string beam = "<#00CFFF>━━━━━━━</color>";
            string blank = "<size=1200%>　</size>";
            string sB = fl ? star + blank : blank + star;
            string lB = fl ? beam + beam + sB : sB + beam + beam;
            string hugeBlank = "<alpha=#00>　　　　　　　　　　</alpha>";
            string ss = "<size=5000%>", se = "</size></line-height>";
            name = fl
                ? "<line-height=4300%>\n" + $"{ss}{lB}{se}{ss}{hugeBlank}{se}"
                : "<line-height=4300%>\n" + $"{ss}{hugeBlank}{se}{ss}{lB}{se}";
            noMarker = true;
            return true;
        }

        return false;
    }

    static void SetPuppetRoleTextHeight(PlayerControl puppet, bool beaming)
    {
        var t = puppet.cosmetics.nameText.transform.Find("RoleText");
        if (t == null) return;
        var rt = t.GetComponent<TMPro.TextMeshPro>();
        if (rt == null) return;
        if (beaming) { rt.text = "<alpha=#00>　</alpha>"; t.SetLocalY(0.35f); }
        else { rt.enabled = true; t.SetLocalY(0.35f); }
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(puppetId);
        sender.Writer.Write(isCharging);
        sender.Writer.Write(isFiring);
        sender.Writer.Write(chargeTimer);
        sender.Writer.Write(beamTimer);
        sender.Writer.Write(beamFacingLeft);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        puppetId = reader.ReadByte();
        isCharging = reader.ReadBoolean();
        isFiring = reader.ReadBoolean();
        chargeTimer = reader.ReadSingle();
        beamTimer = reader.ReadSingle();
        beamFacingLeft = reader.ReadBoolean();
    }

    public override void OnStartMeeting() => EndSequence();

    static bool RollChance(int chance)
    {
        chance = Mathf.Clamp(chance, 0, 100);
        return chance > 0 && IRandom.Instance.Next(1, 101) <= chance;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seen.PlayerId != seer.PlayerId) return "";
        string sz = isForHud ? "" : "<size=60%>";

        if (seer.PlayerId == Player.PlayerId && puppetId != byte.MaxValue)
        {
            var puppet = GetPlayerById(puppetId);
            string name = puppet?.Data?.PlayerName ?? "???";
            if (isCharging) return $"{sz}<color=#ff0000>{name} が波動砲をチャージ中... {(OptDelay.GetFloat() - chargeTimer):F1}s</color>";
            if (isFiring) return $"{sz}<color=#ff0000>{name} が波動砲を発射中...</color>";
        }

        if (puppetId != byte.MaxValue && seer.PlayerId == puppetId && (isCharging || isFiring))
            return $"{sz}<color=#ff0000>何かに操られている… 抵抗できない！</color>";

        return "";
    }

    public override string GetAbilityButtonText() => "操作開始";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "PuppeteerHadouHo_Ability";
        return true;
    }
}

// CheckUseZiplineにはvanilla側でジップライン搭乗中かを直接判定するプロパティが無いため、
// 使用開始時刻から移動所要時間(既存のジップライン死亡演出と同じ 5s/8s)だけ「搭乗中」とみなす簡易トラッカー。
// 他のZiplineパッチ(戻り値false等)には影響しない、記録専用のPrefix。
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
static class PuppeteerHadouHoZiplineTracker
{
    static readonly Dictionary<byte, float> RidingUntil = new();

    [Attributes.GameModuleInitializer]
    public static void Reset() => RidingUntil.Clear();

    public static void Prefix(PlayerControl __instance, [HarmonyArgument(2)] bool fromTop)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        RidingUntil[__instance.PlayerId] = Time.time + (fromTop ? 5f : 8f);
    }

    public static bool IsOnZipline(byte playerId) =>
        RidingUntil.TryGetValue(playerId, out var until) && Time.time < until;
}

// GetTemporaryNameは「見られている側(seen)自身のロールクラス」にディスパッチされるため、
// PuppeteerHadouHo側にオーバーライドを書いてもパペット(打たされたプレイヤー)の名前表示には反映されない。
// RoleBase.GetTemporaryName自体にPrefixで割り込み、現在操作中のパペットならPuppeteerHadouHo側の見た目を割り込ませる。
// 注意: パペットが元々GetTemporaryNameをオーバーライドしている役職(HadouHo系・Jumper・Slugger等)の場合は
// そちらの仮想メソッドが直接呼ばれるため、このPrefixは効かない(未対応の既知の穴)。
[HarmonyPatch(typeof(RoleBase), nameof(RoleBase.GetTemporaryName))]
static class PuppeteerHadouHoNameOverridePatch
{
    public static bool Prefix(ref string name, ref bool NoMarker, bool isForMeeting, PlayerControl seer, PlayerControl seen, ref bool __result)
    {
        seen ??= seer;
        if (isForMeeting) return true;
        if (!PuppeteerHadouHo.ActivePuppets.TryGetValue(seen.PlayerId, out var controller)) return true;

        if (controller.TryBuildPuppetName(seen, ref name, ref NoMarker))
        {
            __result = true;
            return false;
        }
        return true;
    }
}