/*
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class Slugger : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Slugger),
            player => new Slugger(player),
            CustomRoles.Slugger,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            76350,
            SetUpOptionItem,
            "slg",
            OptionSort: (3, 13),
            from: From.SuperNewRoles
        );

    public Slugger(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        Cooldown = OptionCooldown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        SwingTime = OptionSwingTime.GetFloat();
        KillRange = OptionKillRange.GetFloat();
        MultiKill = OptionMultiKill.GetBool();
        FlyDistance = OptionFlyDistance.GetFloat();
        IsCharging = false;
        IsSwinging = false;
        chargeTimer = 0f;
        swingTimer = 0f;
        IsFiring = false;
        SwingFacingLeft = false;
    }

    public bool IsCharging;
    public bool IsSwinging;
    public bool SwingFacingLeft;
    private float chargeTimer;
    private float swingTimer;
    private bool IsFiring;
    private float PlayerSpeed;
    private bool spawnCooldownStarted = false;

    private static OptionItem OptionKillCooldown;
    private static float KillCooldown;
    private static OptionItem OptionCooldown;
    private static float Cooldown;
    private static OptionItem OptionChargeTime;
    private static float ChargeTime;
    private static OptionItem OptionSwingTime;
    private static float SwingTime;
    private static OptionItem OptionKillRange;
    private static float KillRange;
    private static OptionItem OptionMultiKill;
    private static bool MultiKill;
    private static OptionItem OptionFlyDistance;
    private static float FlyDistance;

    private enum OptionName
    {
        SluggerChargeTime, SluggerSwingTime, SluggerKillRange,
        SluggerMultiKill, SluggerFlyDistance,
    }

    private static void SetUpOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown,
            OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.SluggerChargeTime,
            new(0.5f, 5f, 0.5f), 1.5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSwingTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.SluggerSwingTime,
            new(0.1f, 2f, 0.1f), 0.5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionKillRange = FloatOptionItem.Create(RoleInfo, 14, OptionName.SluggerKillRange,
            new(0.5f, 5f, 0.25f), 2f, false);
        OptionMultiKill = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SluggerMultiKill,
            false, false);
        OptionFlyDistance = FloatOptionItem.Create(RoleInfo, 16, OptionName.SluggerFlyDistance,
            new(1f, 20f, 1f), 10f, false);
    }

    public float CalculateKillCooldown() => KillCooldown;

    public override void Add()
    {
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        spawnCooldownStarted = false;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Cooldown;
    }

    public override bool CanUseAbilityButton() => true;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (IsFiring || !Player.IsAlive()) return;

        IsFiring = true;
        IsCharging = true;
        chargeTimer = 0f;
        Main.AllPlayerKillCooldown[Player.PlayerId] = 60f;
        Player.SetKillCooldown(60f);
        _ = new LateTask(() => Player.SyncSettings(), 0.1f, "SluggerKillTimer", true);
        Player.SyncSettings();
        Utils.AllPlayerKillFlash();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!spawnCooldownStarted && Player.IsAlive()
            && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        if (MeetingHud.Instance != null)
        {
            if (IsCharging || IsSwinging) ResetState();
            return;
        }
        if (!Player.IsAlive() && (IsCharging || IsSwinging)) { ResetState(); return; }
        if (!AmongUsClient.Instance.AmHost) return;

        if (IsCharging)
        {
            chargeTimer += Time.fixedDeltaTime;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            if (chargeTimer >= ChargeTime)
            {
                IsCharging = false;
                IsSwinging = true;
                swingTimer = 0f;
                SwingFacingLeft = Player.cosmetics.FlipX;
                Utils.AllPlayerKillFlash();
                UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
                SendRpc();
            }
        }

        if (IsSwinging)
        {
            swingTimer += Time.fixedDeltaTime;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            if (swingTimer >= SwingTime) { ApplySwingHit(); ResetState(); }
        }
    }

    private void ApplySwingHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        var myPos = (Vector2)Player.GetTruePosition();
        Vector2 swingDir = SwingFacingLeft ? Vector2.left : Vector2.right;
        Vector2 perpendicular = new Vector2(-swingDir.y, swingDir.x);

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
            var toTarget = (Vector2)target.GetTruePosition() - myPos;
            float forwardDist = Vector2.Dot(toTarget, swingDir);
            float sideDist = Mathf.Abs(Vector2.Dot(toTarget, perpendicular));
            if (forwardDist < 0 || forwardDist > KillRange) continue;
            if (sideDist > 1.0f) continue;

            var flyPos = CalcFlyPosition(target.GetTruePosition(), swingDir);
            target.NetTransform.SnapTo(flyPos);

            var t = target;
            _ = new LateTask(() =>
            {
                if (PlayerState.GetByPlayerId(t.PlayerId).IsDead) return;
                PlayerState.GetByPlayerId(t.PlayerId).DeathReason = CustomDeathReason.Hit;
                t.RpcExileV3();
                PlayerState.GetByPlayerId(t.PlayerId).SetDead();
                t.SetRealKiller(Player, true);
                UtilsGameLog.AddGameLog("Slugger",
                    $"<color=#ff6600>【スラッガー】</color> {UtilsName.GetPlayerColor(Player, true)} ═> {UtilsName.GetPlayerColor(t, true)}");
            }, 0.25f, "SluggerKill_" + target.PlayerId, true);

            if (!MultiKill) break;
        }
    }

    private Vector2 CalcFlyPosition(Vector2 startPos, Vector2 dir)
    {
        var hit = Physics2D.Raycast(startPos + dir * 0.3f, dir, FlyDistance, Constants.ShipOnlyMask);
        return hit.collider != null ? hit.point - dir * 0.3f : startPos + dir * FlyDistance;
    }

    private void ResetState()
    {
        IsCharging = false;
        IsSwinging = false;
        IsFiring = false;
        chargeTimer = 0f;
        swingTimer = 0f;
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            Main.AllPlayerKillCooldown[Player.PlayerId] = KillCooldown;
            Player.SetKillCooldown(KillCooldown);
            AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.2f, "SluggerReset", true);
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (IsCharging || IsSwinging) ResetState();
    }
    public override void OnStartMeeting() { if (IsCharging || IsSwinging) ResetState(); }
    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = Cooldown;
        Player.RpcResetAbilityCooldown();
    }

    // ══════════════════════════════════════════════════════════════
    // バット表示（ピボット固定版）
    //
    // 原理:
    //   <rotate> はキャラクター中心を軸に回転する。
    //   <voffset> で中心を「バットの半分の長さ」だけ
    //   プレイヤーから離した位置に動かすことで、
    //   回転させたときに柄の端がプレイヤー付近に固定されて見える。
    //
    //   voffset = -BASE + HALF * sin(angleRad)
    //     BASE: 名前ベースラインからプレイヤーへの基準下降量
    //     HALF: バットの半分の長さ（emで近似）
    //     angle が 90° (上向き) のとき sin=1 → 中心が上 → 先端が下でプレイヤー付近
    //     angle が -90° (下向き) のとき sin=-1 → 中心が下 → 先端が上でプレイヤー付近
    //
    //   ★ BASE と HALF は見た目に合わせて要調整。
    // ══════════════════════════════════════════════════════════════

    // 調整用定数（実機で見ながらチューニングしてください）
    const float BAT_BASE = 2.2f; // 名前ベースラインからの基準下降 (em)
    const float BAT_HALF = 2.6f; // バット半長 (em, size=600% の ー に近似)

    public override bool GetTemporaryName(ref string name, ref bool NoMarker,
        bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;
        if (seen.PlayerId != Player.PlayerId) return false;
        if (!IsCharging && !IsSwinging) return false;

        bool facingLeft = seer.PlayerId == Player.PlayerId
            ? Player.cosmetics.FlipX
            : SwingFacingLeft;

        float readyAngle = facingLeft ? 75f : 105f;

        if (IsCharging)
        {
            float voff = CalcVoffset(readyAngle);
            name = $"<voffset={voff:F2}em><size=600%>" +
                   $"<rotate={(int)readyAngle}><color=#ff6600>ー</color></rotate>" +
                   $"</size></voffset>";
            NoMarker = true;
            return true;
        }

        if (IsSwinging)
        {
            float progress = Mathf.Clamp01(swingTimer / SwingTime);
            float endAngle = facingLeft ? 255f : -75f;
            float curAngle = Mathf.Lerp(readyAngle, endAngle, progress);
            float voff = CalcVoffset(curAngle);

            // ★ 残像（少し前の角度にもう一本薄く表示）
            string trail = "";
            if (progress > 0.15f)
            {
                float trailAngle = Mathf.Lerp(readyAngle, endAngle,
                    Mathf.Clamp01(progress - 0.2f));
                float trailVoff = CalcVoffset(trailAngle);
                trail = $"<voffset={trailVoff:F2}em><size=500%>" +
                        $"<rotate={(int)trailAngle}><color=#ff440066><b>ー</b></color></rotate>" +
                        $"</size></voffset>";
            }

            name = trail +
                   $"<voffset={voff:F2}em><size=800%>" +
                   $"<rotate={(int)curAngle}><color=#ff2200><b>ー</b></color></rotate>" +
                   $"</size></voffset>";
            NoMarker = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// バットの角度から voffset を計算する。
    /// voffset = -BASE + HALF * sin(angle)
    /// これにより "ー" キャラクターの中心が
    /// プレイヤー基点から半長分だけ離れた位置に来る。
    /// </summary>
    static float CalcVoffset(float angleDeg)
        => -BAT_BASE + BAT_HALF * Mathf.Sin(angleDeg * Mathf.Deg2Rad);

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";
        if (!IsCharging && !IsSwinging)
            return $"{size}<color=#ff6600>ファントムボタン → バットチャージ開始</color>";
        if (IsCharging)
        {
            float rem = Mathf.Max(0f, ChargeTime - chargeTimer);
            return $"{size}<color=#ff6600>チャージ中... {rem:F1}s</color>";
        }
        return $"{size}<color=#ff2200><b>振り抜き！！</b></color>";
    }

    public override string GetAbilityButtonText() => GetString("SluggerAbilityText");

    public void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(IsSwinging);
        sender.Writer.Write(SwingFacingLeft);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        IsCharging = reader.ReadBoolean();
        IsSwinging = reader.ReadBoolean();
        SwingFacingLeft = reader.ReadBoolean();
    }
}
*/