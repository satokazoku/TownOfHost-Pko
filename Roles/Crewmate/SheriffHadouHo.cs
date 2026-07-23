using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class SheriffHadouHo : RoleBase, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SheriffHadouHo),
            player => new SheriffHadouHo(player),
            CustomRoles.SheriffHadouHo,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            260200,
            SetupOptionItem,
            "shh",
            "#f8cd46",
            (2, 0),
            true,
            countType: CountTypes.Crew
        );

    public SheriffHadouHo(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        Cooldown = OptionCooldown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        ShotLimit = OptionShotLimit.GetInt();
        SelfDestructOnMiss = OptionSelfDestructOnMiss.GetBool();
        BeamColorModeValue = OptionBeamColorMode.GetValue();
        IsCharging = false;
        chargeTimer = 0f;
        PlayerSpeed = 0f;
        ShowBeamMark = false;
        HasHit = false;
        IsFiring = false;
        _prevCharging = false;
        _prevBeamMark = false;
        BeamFacingLeft = false;
        PlayerColor = 0;
        beamMode = false;
        nowcool = Cooldown;
        LastCooltime = (int)Cooldown;
    }

    static OptionItem OptionCooldown;
    static float Cooldown;
    static OptionItem OptionChargeTime;
    static float ChargeTime;
    static OptionItem OptionShotLimit;
    static OptionItem OptionSelfDestructOnMiss;
    static bool SelfDestructOnMiss;
    static OptionItem OptionBeamColorMode;
    static int BeamColorModeValue;
    static OptionItem OptionBeamUnlockTask;
    static int BeamUnlockTaskCount;

    int ShotLimit;
    public bool IsCharging;
    float chargeTimer;
    float PlayerSpeed;
    public bool ShowBeamMark;
    bool HasHit;
    bool IsFiring;
    bool spawnCooldownStarted;
    bool _prevCharging;
    bool _prevBeamMark;
    bool BeamFacingLeft;
    int PlayerColor;
    bool beamMode;
    float nowcool;
    int LastCooltime;

    bool IsBeamUnlocked =>
        BeamUnlockTaskCount <= 0 ||
        (MyTaskState != null && MyTaskState.CompletedTasksCount >= BeamUnlockTaskCount);

    enum BeamColorMode { Cyan, Yellow }

    enum OptionName
    {
        SheriffHadouHoChargeTime,
        SheriffHadouHoShotLimit,
        SheriffHadouHoBeamColorMode,
        SheriffHadouHoBeamUnlockTask,
        SheriffHadouHoSelfDestruct,
    }

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown,
            new(0f, 60f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.SheriffHadouHoChargeTime,
            new(0.5f, 10f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptionShotLimit = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SheriffHadouHoShotLimit,
            new(1, 15, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionSelfDestructOnMiss = BooleanOptionItem.Create(RoleInfo, 13,
            OptionName.SheriffHadouHoSelfDestruct, true, false);
        OptionBeamColorMode = StringOptionItem.Create(RoleInfo, 14,
            OptionName.SheriffHadouHoBeamColorMode,
            new string[] { "Cyan", "Yellow" }, 1, false);
        OptionBeamUnlockTask = IntegerOptionItem.Create(RoleInfo, 15,
            OptionName.SheriffHadouHoBeamUnlockTask,
            new(0, 10, 1), 0, false).SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        PlayerColor = Player.Data.DefaultOutfit.ColorId;
        BeamColorModeValue = OptionBeamColorMode.GetValue();
        BeamUnlockTaskCount = OptionBeamUnlockTask.GetInt();
        SelfDestructOnMiss = OptionSelfDestructOnMiss.GetBool();
        spawnCooldownStarted = false;
        beamMode = false;
        nowcool = Cooldown;
        LastCooltime = (int)Cooldown;
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        float cd = Mathf.Max(nowcool, 0.1f);
        if (!beamMode)
        {
            AURoleOptions.EngineerCooldown = cd;
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
        else
        {
            AURoleOptions.PhantomCooldown = cd;
        }
    }

    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => false;
    public override bool CanClickUseVentButton => !beamMode;

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (IsCharging || ShowBeamMark) return false;
        return !beamMode;
    }

    bool IUsePhantomButton.IsPhantomRole => beamMode && !IsFiring;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override bool CanUseAbilityButton()
        => beamMode && !IsFiring && ShotLimit > 0 && nowcool <= 0f && IsBeamUnlocked;

    public override RoleTypes? AfterMeetingRole
        => (beamMode && ShotLimit > 0 && IsBeamUnlocked) ? RoleTypes.Phantom : RoleTypes.Engineer;

    void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        if (ShotLimit <= 0) return;
        if (IsFiring || IsCharging || ShowBeamMark) return;
        if (!beamMode && !IsBeamUnlocked) return;

        beamMode = !beamMode;
        ApplyModeDesync(beamMode);
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void ApplyModeDesync(bool toBeamMode)
    {
        if (!Player.IsAlive()) return;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toBeamMode ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toBeamMode ? RoleTypes.Phantom : RoleTypes.Engineer,
                    Player.GetClientId());
        }
        _ = new LateTask(() =>
        {
            if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "SHH.ModeDesync", true);
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (IsFiring || ShowBeamMark || !Player.IsAlive() || IsCharging || !beamMode) return;
        if (ShotLimit <= 0) return;
        if (nowcool > 0f) return;
        if (!IsBeamUnlocked) return;

        IsFiring = true;
        IsCharging = true;
        chargeTimer = 0f;
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        Utils.AllPlayerKillFlash();
        Player.SyncSettings();
        _prevCharging = true;
        _prevBeamMark = false;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
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

    void ResetBeamState()
    {
        IsCharging = false;
        ShowBeamMark = false;
        IsFiring = false;
        HasHit = false;
        chargeTimer = 0f;
        _prevCharging = false;
        _prevBeamMark = false;
        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();
        SetRoleTextHeight(false);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!spawnCooldownStarted && Player.IsAlive()
            && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        if (MeetingHud.Instance != null)
        {
            if (IsCharging || ShowBeamMark || IsFiring)
            { ResetBeamState(); UtilsNotifyRoles.NotifyRoles(); SendRpc(); }
            return;
        }

        if (!Player.IsAlive() && (IsCharging || ShowBeamMark))
        {
            ResetBeamState();
            Player.RpcSetColor((byte)PlayerColor);
            UtilsNotifyRoles.NotifyRoles(); SendRpc();
            return;
        }

        if (!IsFiring && Player.IsAlive() && GameStates.IsInTask)
        {
            if (nowcool > 0) nowcool -= Time.fixedDeltaTime;
            else nowcool = 0;
            var now = (int)nowcool;
            if (now != LastCooltime)
            {
                LastCooltime = now;
                Player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
                }, 0.1f, "SHH.CDSync", true);
                if (player != PlayerControl.LocalPlayer)
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
            }
        }

        bool changed = (IsCharging != _prevCharging) || (ShowBeamMark != _prevBeamMark);
        if (changed)
        {
            _prevCharging = IsCharging;
            _prevBeamMark = ShowBeamMark;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        }

        if (ShowBeamMark && Player.IsAlive()) ApplyBeamHit();
        if (IsCharging)
        {
            chargeTimer += Time.fixedDeltaTime;
            if (chargeTimer >= ChargeTime) FireBeam();
        }
    }

    void FireBeam()
    {
        if (!Player.IsAlive()) return;

        BeamFacingLeft = Player.cosmetics.FlipX;
        SendBeamDirection(BeamFacingLeft);
        Utils.AllPlayerKillFlash();

        IsCharging = false;
        chargeTimer = 0f;
        HasHit = false;
        ShowBeamMark = true;
        SetRoleTextHeight(true);
        _prevCharging = false;
        _prevBeamMark = true;
        ShotLimit--;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
        ApplyBeamHit();

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive())
            {
                ShowBeamMark = false; _prevBeamMark = false;
                SetRoleTextHeight(false); IsFiring = false;
                Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
                Player.MarkDirtySettings();
                Player.RpcSetColor((byte)PlayerColor);
                UtilsNotifyRoles.NotifyRoles(); SendRpc(); return;
            }

            ShowBeamMark = false; _prevBeamMark = false;
            SetRoleTextHeight(false);
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
            SendRpc();

            if (!HasHit && SelfDestructOnMiss)
            {
                Player.RpcSetColor((byte)PlayerColor);
                Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
                Player.MarkDirtySettings();
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayerV2(Player);
                IsFiring = false;
                UtilsGameLog.AddGameLog("SheriffHadouHo",
                    $"{UtilsName.GetPlayerColor(Player)} はビームで誰も倒せず自爆した");
                return;
            }

            Player.RpcSetColor((byte)PlayerColor);
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            Player.MarkDirtySettings();
            nowcool = Cooldown;
            LastCooltime = (int)Cooldown;
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) { IsFiring = false; return; }
                Player.MarkDirtySettings();
                Player.RpcResetAbilityCooldown(Sync: true);
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                _ = new LateTask(() => { IsFiring = false; }, 0.3f, "SHHResetFiring", true);
            }, 0.2f, "SHHResetCooldown", true);
        }, 3f, "SHHBeamEnd", true);
    }

    void ApplyBeamHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        bool facingLeft = BeamFacingLeft;
        var myPos = Player.GetTruePosition();
        Vector2 dir = facingLeft ? Vector2.left : Vector2.right;

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId) continue;
            var toTarget = target.GetTruePosition() - myPos;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            var proj = dir * dot;
            var perp = toTarget - proj;
            if (perp.magnitude > 1.3f) continue;

            CustomRoleManager.OnCheckMurder(Player, target, target, target,
                true, deathReason: CustomDeathReason.Hit);
            HasHit = true;
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ResetBeamState();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    public override void OnStartMeeting()
    {
        ResetBeamState();
        if (beamMode) { beamMode = false; SendRpc(); }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        nowcool = Cooldown;
        LastCooltime = (int)Cooldown;
        ApplyModeDesync(beamMode);
    }

    public override bool GetTemporaryName(ref string name, ref bool NoMarker,
        bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;

        string myColor = "#" + ColorUtility.ToHtmlStringRGB(
            Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        if (IsCharging && seen.PlayerId == Player.PlayerId)
        {
            bool fl = seer.PlayerId == Player.PlayerId
                ? Player.cosmetics.FlipX : BeamFacingLeft;
            string bigStar = $"<size=800%><color={myColor}>★</color></size>";
            string blank = "　　　";
            name = "<line-height=1200%>\n"
                 + (fl ? bigStar + blank : blank + bigStar)
                 + "</line-height>";
            NoMarker = true; return true;
        }

        if (seen == seer && Is(seer) && !seer.IsModClient()
            && (IsCharging || ShowBeamMark))
        {
            if (ShowBeamMark && seen.PlayerId == Player.PlayerId)
            {
                BuildBeamName(ref name, myColor, false);
                NoMarker = true; return true;
            }
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
        string beam = BuildBeamBlock();
        string blank = "<size=1200%>　</size>";
        string sB = fl ? star + blank : blank + star;
        string longBeam = fl ? beam + beam + sB : sB + beam + beam;
        string hugeBlank = "<alpha=#00>　　　　　　　　　　</alpha>";
        string ls = wider ? "<line-height=5300%>\n" : "<line-height=4300%>\n";
        string ss = "<size=5000%>", se = "</size></line-height>";

        name = fl
            ? ls + $"{ss}{longBeam}{se}{ss}{hugeBlank}{se}"
            : ls + $"{ss}{hugeBlank}{se}{ss}{longBeam}{se}";
    }

    string BuildBeamBlock()
    {
        if ((BeamColorMode)BeamColorModeValue == BeamColorMode.Yellow)
            return "<color=#f8cd46>━━━━━━━</color>";
        return "<color=#00CFFF>━━━━━━━</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";

        if (ShotLimit <= 0)
            return $"{size}<color=#888888>弾切れ</color>";

        if (!IsBeamUnlocked)
        {
            int done = MyTaskState?.CompletedTasksCount ?? 0;
            int remaining = BeamUnlockTaskCount - done;
            return $"{size}<color=#888888>波動砲ロック中 (あと {remaining} タスク)</color>";
        }

        if (!beamMode)
        {
            string cdText = LastCooltime > 0
                ? $" (CD: {LastCooltime}s)"
                : " <color=#00ff88>(準備完了)</color>";
            return $"{size}<color=#f8cd46>ペット → 波動砲モードへ{cdText}</color>";
        }

        if (IsCharging)
            return $"{size}<color=#ff0000>チャージ中... {(ChargeTime - chargeTimer):F1}s</color>";

        string ready = nowcool <= 0f
            ? " <color=#00ff88>(発射可能)</color>"
            : $" (CD: {LastCooltime}s)";
        return $"{size}<color=#ff0000>ファントムボタン → チャージ発射{ready}</color>";
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || isForMeeting || !Player.IsAlive()) return "";
        if (IsCharging && seer.PlayerId != Player.PlayerId)
            return $"\n<color=#ff0000>チャージ中... {(int)(ChargeTime - chargeTimer)}s</color>";
        if (ShowBeamMark && seer.PlayerId != Player.PlayerId)
            return "\n<color=#ff0000>ビーム中</color>";
        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (!IsBeamUnlocked)
        {
            int done = MyTaskState?.CompletedTasksCount ?? 0;
            return $"<color=#888888>[{done}/{BeamUnlockTaskCount}]</color>";
        }
        string shots = Utils.ColorString(ShotLimit > 0 ? Color.yellow : Color.gray, $"({ShotLimit})");
        string mode = beamMode
            ? "<color=#ff4444>[波動砲]</color>"
            : "<color=#f8cd46>[タスク]</color>";
        string cd = $"<color=#ffffff>({LastCooltime})</color>";
        return $"{shots}{mode}{cd}";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)0);
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(ShowBeamMark);
        sender.Writer.Write(ShotLimit);
        sender.Writer.Write(beamMode);
    }

    void SendBeamDirection(bool left)
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
        sender.Writer.Write(left);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte tag = reader.ReadByte();
        if (tag == 1) { BeamFacingLeft = reader.ReadBoolean(); return; }
        IsCharging = reader.ReadBoolean();
        ShowBeamMark = reader.ReadBoolean();
        ShotLimit = reader.ReadInt32();
        beamMode = reader.ReadBoolean();
    }
    public override string GetAbilityButtonText() => "発射";

    public override bool OverrideAbilityButton(out string text)
    {
        text = "HadouHo_Ability";
        return true;
    }
}