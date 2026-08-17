<<<<<<< HEAD
=======
using System.Linq;
>>>>>>> dcd67e7ec6cde3d9d9f89ff2ca548e7182003ccf
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

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
            OptionSort: (3, 14),
            from: From.TownOfHost_Pko
        );

    public PuppeteerHadouHo(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCoolDown.GetFloat();
        Cooldown = OptionCoolDown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        BeamTime = OptionBeamTime.GetFloat();
        SelfDestructOnMiss = OptionSelfDestructOnMiss.GetBool();
        IsCharging = false;
        chargeTimer = 0f;
        PlayerSpeed = 0f;
        ShowBeamMark = false;
        HasHit = false;
        IsDead = false;
        IsFiring = false;
        _prevCharging = false;
        _prevBeamMark = false;
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    public bool IsCharging;
    float chargeTimer;
    float PlayerSpeed;
    public bool ShowBeamMark;
    bool HasHit;
    bool BeamFacingLeft;
    bool IsDead;
    bool IsFiring = false;
    bool spawnCooldownStarted = false;
    bool _prevCharging;
    bool _prevBeamMark;

    static OptionItem OptionCoolDown;
    static float Cooldown;
    public static float CooldownValue => Cooldown;
    static OptionItem OptionKillCoolDown;
    static float KillCooldown;
    static OptionItem OptionChargeTime;
    static float ChargeTime;
    static OptionItem OptionBeamTime;
    static float BeamTime;
    static OptionItem OptionSelfDestructOnMiss;
    static bool SelfDestructOnMiss;

    enum OptionName { PuppeteerHadouHoChargeTime, PuppeteerHadouHoSelfDestruct, PuppeteerHadouHoBeamTime }

    static void SetUpOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.PuppeteerHadouHoChargeTime, new(0.5f, 10f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptionBeamTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.PuppeteerHadouHoBeamTime, new(0.5f, 10f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSelfDestructOnMiss = BooleanOptionItem.Create(RoleInfo, 14, OptionName.PuppeteerHadouHoSelfDestruct, false, false);
    }

    public override void Add()
    {
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        spawnCooldownStarted = false;
    }

    public override void OnDestroy()
    {
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);

        if (IsCharging || ShowBeamMark || IsFiring)
        {
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            Player.MarkDirtySettings();
            if (AmongUsClient.Instance.AmHost)
            {
                Player.SyncSettings();
            }
            IsCharging = false;
            ShowBeamMark = false;
            IsFiring = false;
            SetRoleTextHeight(false);
        }
    }

    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = Cooldown;
    public float CalculateKillCooldown() => KillCooldown;

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (IsCharging || ShowBeamMark) return false;
        return true;
    }
    bool IUsePhantomButton.IsPhantomRole => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (IsFiring || ShowBeamMark || !Player.IsAlive() || IsCharging) return;

        IsFiring = true;
        IsCharging = true;
        chargeTimer = 0f;
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        Utils.AllPlayerKillFlash();
        Main.AllPlayerKillCooldown[Player.PlayerId] = 60f;
        Player.SetKillCooldown(60f);
        _ = new LateTask(() => { Player.SyncSettings(); }, 0.1f, "PuppeteerHadouHoKillTimer", true);
        Player.SyncSettings();
        Main.AllPlayerKillCooldown[Player.PlayerId] = 60f;
        Player.SyncSettings();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        _prevCharging = true;
        _prevBeamMark = false;
        SendRpc();
    }

    void SetRoleTextHeight(bool beaming)
    {
        var t = Player.cosmetics.nameText.transform.Find("RoleText");
        if (t == null) return;
        var rt = t.GetComponent<TMPro.TextMeshPro>();
        if (rt == null) return;
        if (beaming) { rt.text = "<alpha=#00>　</alpha>"; t.SetLocalY(0.35f); }
        else { rt.enabled = true; t.SetLocalY(0.35f); }
    }

    private void ResetState()
    {
        IsCharging = false;
        ShowBeamMark = false;
        IsFiring = false;
        chargeTimer = 0f;
        HasHit = false;
        _prevCharging = false;
        _prevBeamMark = false;
        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();
        SetRoleTextHeight(false);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!spawnCooldownStarted && Player.IsAlive() && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        if (MeetingHud.Instance != null)
        {
            if (IsCharging || ShowBeamMark || IsFiring)
            {
                ResetState();
                UtilsNotifyRoles.NotifyRoles();
                SendRpc();
            }
            return;
        }

        if (!Player.IsAlive() && (IsCharging || ShowBeamMark))
        {
            ResetState();
            UtilsNotifyRoles.NotifyRoles();
            SendRpc();
            return;
        }

        bool changed = (IsCharging != _prevCharging) || (ShowBeamMark != _prevBeamMark);
        if (changed)
        {
            _prevCharging = IsCharging;
            _prevBeamMark = ShowBeamMark;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        }

        if (ShowBeamMark && Player.IsAlive()) ApplyBeamHit();
        if (IsCharging) { chargeTimer += Time.fixedDeltaTime; if (chargeTimer >= ChargeTime) FireBeam(); }
    }

    void FireBeam()
    {
        if (IsDead || !Player.IsAlive()) return;
        BeamFacingLeft = Player.cosmetics.FlipX;
        SendBeamDirection(BeamFacingLeft);
        Utils.AllPlayerKillFlash();
        IsCharging = false; chargeTimer = 0f; HasHit = false; ShowBeamMark = true;
        SetRoleTextHeight(true);
        _prevCharging = false;
        _prevBeamMark = true;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
        ApplyBeamHit();

        _ = new LateTask(() =>
        {
            if (IsDead || !Player.IsAlive())
            {
                ShowBeamMark = false; _prevBeamMark = false;
                SetRoleTextHeight(false); IsFiring = false;
                Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
                Player.MarkDirtySettings();
                UtilsNotifyRoles.NotifyRoles(); SendRpc(); return;
            }
            ShowBeamMark = false; _prevBeamMark = false;
            SetRoleTextHeight(false);
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true); SendRpc();

            if (!HasHit && SelfDestructOnMiss)
            {
                Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed; Player.MarkDirtySettings();
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayerV2(Player); IsFiring = false; return;
            }
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed; Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) { IsFiring = false; return; }
                Main.AllPlayerKillCooldown[Player.PlayerId] = KillCooldown;
                Player.SetKillCooldown(KillCooldown);
                Player.RpcResetAbilityCooldown(Sync: true);
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                _ = new LateTask(() => { IsFiring = false; }, 0.3f, "PuppeteerHadouHoResetFiring", true);
            }, 0.2f, "PuppeteerHadouHoResetKillCool", true);
        }, BeamTime);
    }

    void ApplyBeamHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        bool facingLeft = BeamFacingLeft;
        var myPos = Player.GetTruePosition();
        Vector2 dir = facingLeft ? Vector2.left : Vector2.right;
<<<<<<< HEAD
        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
=======
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (!Player.IsAlive()) break;
            if (target.PlayerId == Player.PlayerId) continue;
            if (!target.IsAlive()) continue;
>>>>>>> dcd67e7ec6cde3d9d9f89ff2ca548e7182003ccf
            var toTarget = target.GetTruePosition() - myPos;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            var proj = dir * dot;
            var perp = toTarget - proj;
            if (perp.magnitude > 1.3f) continue;
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, deathReason: CustomDeathReason.Evaporation);
            HasHit = true;
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ResetState();
        Player.SyncSettings();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true); SendRpc();
    }

    public override void OnStartMeeting()
    {
        ResetState();
        Player.SyncSettings();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        AURoleOptions.PhantomCooldown = Cooldown;
        Player.RpcResetAbilityCooldown();
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(ShowBeamMark);
    }

    void SendBeamDirection(bool left)
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
        sender.Writer.Write(left);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if (reader.Length - reader.Position == 2) { reader.ReadByte(); BeamFacingLeft = reader.ReadBoolean(); return; }
        bool oldCharging = IsCharging;
        bool oldBeamMark = ShowBeamMark;
        IsCharging = reader.ReadBoolean();
        ShowBeamMark = reader.ReadBoolean();
        if (oldCharging != IsCharging || oldBeamMark != ShowBeamMark)
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    public override bool GetTemporaryName(ref string name, ref bool NoMarker, bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;
        string myColor = "#" + ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        if (IsCharging && seen.PlayerId == Player.PlayerId)
        {
            bool fl = seer.PlayerId == Player.PlayerId ? Player.cosmetics.FlipX : BeamFacingLeft;
            string bigStar = $"<size=800%><color={myColor}>★</color></size>";
            string blank = "　　　";
            name = "<line-height=1200%>\n" + (fl ? bigStar + blank : blank + bigStar) + "</line-height>";
            NoMarker = true; return true;
        }

        if (seen == seer && Is(seer) && !seer.IsModClient() && (IsCharging || ShowBeamMark))
        {
            if (ShowBeamMark && seen.PlayerId == Player.PlayerId) { BuildBeamName(ref name, myColor, false); NoMarker = true; return true; }
            return false;
        }

        if (ShowBeamMark && seen.PlayerId == Player.PlayerId)
        {
            SetRoleTextHeight(true);
            BuildBeamName(ref name, myColor, true);
            NoMarker = true; return true;
        }
        return false;
    }

    void BuildBeamName(ref string name, string myColor, bool wider)
    {
        SetRoleTextHeight(true);
        bool fl = BeamFacingLeft;
        string star = $"<voffset=0.35em><size=800%><color={myColor}>★</color></size></voffset>";
        string beam = "<#00CFFF>━━━━━━━</color>";
        string blank = "<size=1200%>　</size>";
        string sB = fl ? star + blank : blank + star;
        string lB = fl ? beam + beam + sB : sB + beam + beam;
        string hugeBlank = "<alpha=#00>　　　　　　　　　　</alpha>";
        string ls = wider ? "<line-height=5300%>\n" : "<line-height=4300%>\n";
        string ss = "<size=5000%>", se = "</size></line-height>";
        name = fl
            ? ls + $"{ss}{lB}{se}{ss}{hugeBlank}{se}"
            : ls + $"{ss}{hugeBlank}{se}{ss}{lB}{se}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        if (!IsCharging) return $"{(isForHud ? "" : "<size=100%>")}<color=#ff0000>ファントムボタン → チャージ発射</color>";
        return $"{(isForHud ? "" : "<size=100%>")}<color=#ff0000>チャージ中... {(ChargeTime - chargeTimer):F1}s</color>";
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || isForMeeting || !Player.IsAlive()) return "";
        if (IsCharging && seer.PlayerId != Player.PlayerId) return $"\n<size=100%><color=#ff0000>チャージ中... {(int)(ChargeTime - chargeTimer)}s</color></size>";
        if (ShowBeamMark && seer.PlayerId != Player.PlayerId) return "\n<size=100%><color=#ff0000>ビーム中</color></size>";
        return "";
    }

    public override string GetAbilityButtonText() => "発射";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "PuppeteerHadouHo_Ability";
        return true;
    }
}