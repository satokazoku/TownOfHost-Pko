using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Patches;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;
using TownOfHost.Roles.Madmate;
namespace TownOfHost.Roles.Neutral;

public sealed class JackalHadouHo : RoleBase, ILNKiller, IUsePhantomButton, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalHadouHo),
            player => new JackalHadouHo(player),
            CustomRoles.JackalHadouHo,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            552300,
            SetUpOptionItem,
            "jhh",
            "#00b4eb",
            (1, 4),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalHadouHo, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
            },
            from: From.SuperNewRoles
        );

    public JackalHadouHo(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        Cooldown = OptionCoolDown.GetFloat();
        ChargeTime = OptionChargeTime.GetFloat();
        SuperChargeTime = OptionSuperChargeTime.GetFloat();
        BeamTime = OptionBeamTime.GetFloat();
        SuperBeamTime = OptionSuperBeamTime.GetFloat();
        SelfDestructOnMiss = OptionSelfDestructOnMiss.GetBool();
        KillJackal = OptionKillJackal.GetBool();
        CanVent = OptionCanVent.GetBool();
        CanSabotage = OptionCanSabotage.GetBool();
        HasImpostorVision = OptionHasImpostorVision.GetBool();
        CanSideKick = OptionCanMakeSidekick.GetBool();

        IsCharging = false;
        IsSuperCharging = false;
        chargeTimer = 0f;
        superChargeTimer = 0f;
        PlayerSpeed = 0f;
        ShowBeamMark = false;
        HasHit = false;
        IsDead = false;
        IsFiring = false;
        IsLoaded = false;
        IsSuperBeam = false;
        _prevCharging = false;
        _prevSuperCharging = false;
        _prevBeamMark = false;

        skMode = false;
        nowcool = KillCooldown;
        LastCooltime = (int)KillCooldown;
        Charging = false;
        skCandidateId = byte.MaxValue;
        skNearTimer = 0f;
        skCooldownTimer = 0f;
        skSpawnWaitTimer = -1f;

        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    public bool IsCharging;
    public bool IsSuperCharging;
    public static bool Charging;
    float chargeTimer;
    float superChargeTimer;
    float PlayerSpeed;
    public bool ShowBeamMark;
    bool HasHit;
    bool BeamFacingLeft;
    bool IsDead;
    bool IsFiring = false;
    public bool CanSideKick;
    public bool IsLoaded;
    public bool IsSuperBeam;
    public static bool NextNoSideKick = false;
    bool _prevCharging;
    bool _prevSuperCharging;
    bool _prevBeamMark;

    bool skMode;
    float nowcool;
    int LastCooltime;

    byte skCandidateId;
    float skNearTimer;
    float skCooldownTimer;
    float skSpawnWaitTimer;
    bool SkCanApproach => skSpawnWaitTimer >= 3f;

    static OptionItem OptionKillCooldown;
    static float KillCooldown;
    static OptionItem OptionCoolDown;
    static float Cooldown;
    static OptionItem OptionChargeTime;
    static float ChargeTime;
    static OptionItem OptionSuperChargeTime;
    static float SuperChargeTime;
    static OptionItem OptionBeamTime;
    static float BeamTime;
    static OptionItem OptionSuperBeamTime;
    static float SuperBeamTime;
    static OptionItem OptionSelfDestructOnMiss;
    static bool SelfDestructOnMiss;
    static OptionItem OptionKillJackal;
    static bool KillJackal;
    static OptionItem OptionCanVent;
    static bool CanVent;
    static OptionItem OptionCanSabotage;
    static bool CanSabotage;
    static OptionItem OptionHasImpostorVision;
    static bool HasImpostorVision;
    static OptionItem OptionCanMakeSidekick;
    static OptionItem OptionSidekickCooldown;
    static float SidekickCooldown;

    static OptionItem OptionTamaLoadCooldown;
    static OptionItem OptionTamaCanLoad;
    static OptionItem OptionTamaCanVent;
    static OptionItem OptionTamaVentCooldown;
    static OptionItem OptionTamaVentMaxTime;
    static OptionItem OptionTamaCanVentMove;
    static OptionItem OptionTamaCountAsJackalKiller;
    static OptionItem OptionImpostorCanSidekick;
    static OptionItem OptionSidekickCanSeeOldImpostorTeammates;
    static OptionItem OptionImpostorCanSeeNameColor;
    static OptionItem OptionSidekickPromotion;
    static OptionItem OptionTamaCountAsKillerOnlyWhenPromotionEnabled;

    enum OptionName
    {
        JackalHadouHoChargeTime,
        JackalHadouHoSuperChargeTime,
        JackalHadouHoBeamTime,
        JackalHadouHoSuperBeamTime,
        JackalHadouHoSelfDestruct,
        JackalHadouHoKillJackal,
        JackalHadouHoSidekickCooldown,
        JackalHadouHoImpostorCanSidekick,
        JackalHadouHoSidekickCanSeeOldImpostorTeammates,
        JackalHadouHoImpostorCanSeeNameColor,
        JackalHadouHoSidekickPromotion,
        TamaOption,
        TamaCanLoad,
        TamaLoadCooldown,
        TamaCanVentMove,
        TamaCountAsKillerOnlyWhenPromotionEnabled,
    }

    public static float GetTamaLoadCooldown() => OptionTamaLoadCooldown?.GetFloat() ?? 10f;
    public static bool GetTamaCanLoad() => OptionTamaCanLoad?.GetBool() ?? true;
    public static bool GetTamaCanVent() => OptionTamaCanVent?.GetBool() ?? false;
    public static float GetTamaVentCooldown() => OptionTamaVentCooldown?.GetFloat() ?? 0f;
    public static float GetTamaVentMaxTime() => OptionTamaVentMaxTime?.GetFloat() ?? 0f;
    public static bool GetTamaCanVentMove() => OptionTamaCanVentMove?.GetBool() ?? false;
    public static bool GetCanUseSabotageOption() => OptionCanSabotage?.GetBool() ?? false;
    public static bool GetTamaCountAsJackalKiller() => OptionTamaCountAsJackalKiller?.GetBool() ?? false;
    public static bool GetTamaCountAsKillerOnlyWhenPromotionEnabled() => OptionTamaCountAsKillerOnlyWhenPromotionEnabled?.GetBool() ?? false;
    public static bool GetSidekickPromotion() => OptionSidekickPromotion?.GetBool() ?? false;

    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null && Options.CustomRoleSpawnChances.TryGetValue(role, out var sp))
            sp.SetHidden(true);
        if (Options.CustomRoleCounts != null && Options.CustomRoleCounts.TryGetValue(role, out var cp))
            cp.SetHidden(true);
    }

    static void SetUpOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.JackalHadouHoChargeTime, new(0.5f, 10f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSuperChargeTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.JackalHadouHoSuperChargeTime, new(0.5f, 15f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBeamTime = FloatOptionItem.Create(RoleInfo, 14, OptionName.JackalHadouHoBeamTime, new(0.5f, 10f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSuperBeamTime = FloatOptionItem.Create(RoleInfo, 15, OptionName.JackalHadouHoSuperBeamTime, new(0.5f, 15f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSelfDestructOnMiss = BooleanOptionItem.Create(RoleInfo, 16, OptionName.JackalHadouHoSelfDestruct, false, false);
        OptionKillJackal = BooleanOptionItem.Create(RoleInfo, 17, OptionName.JackalHadouHoKillJackal, false, false);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 18, GeneralOption.CanVent, true, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 19, GeneralOption.CanUseSabotage, false, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 20, GeneralOption.ImpostorVision, true, false);
        OptionCanMakeSidekick = BooleanOptionItem.Create(RoleInfo, 21, GeneralOption.CanCreateSideKick, true, false);
        OptionSidekickCooldown = FloatOptionItem.Create(RoleInfo, 22, OptionName.JackalHadouHoSidekickCooldown, new(0f, 180f, 0.5f), 30f, false, OptionCanMakeSidekick)
            .SetValueFormat(OptionFormat.Seconds);
        OptionImpostorCanSidekick = BooleanOptionItem.Create(RoleInfo, 23, OptionName.JackalHadouHoImpostorCanSidekick, false, false, OptionCanMakeSidekick);
        OptionSidekickCanSeeOldImpostorTeammates = BooleanOptionItem.Create(RoleInfo, 24, OptionName.JackalHadouHoSidekickCanSeeOldImpostorTeammates, false, false, OptionImpostorCanSidekick);
        OptionImpostorCanSeeNameColor = BooleanOptionItem.Create(RoleInfo, 34, OptionName.JackalHadouHoImpostorCanSeeNameColor, false, false, OptionImpostorCanSidekick);
        OptionSidekickPromotion = BooleanOptionItem.Create(RoleInfo, 35, OptionName.JackalHadouHoSidekickPromotion, false, false, OptionCanMakeSidekick);

        ObjectOptionitem.Create(RoleInfo, 25, OptionName.TamaOption, true, "")
            .SetOptionName(() => "TAMA OPTION");
        OptionTamaCanLoad = BooleanOptionItem.Create(RoleInfo, 26, "TamaCanLoad", true, false);
        OptionTamaLoadCooldown = FloatOptionItem.Create(RoleInfo, 27, "TamaLoadCooldown", new(0f, 60f, 0.5f), 10f, false, OptionTamaCanLoad)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTamaCountAsJackalKiller = BooleanOptionItem.Create(RoleInfo, 50, "TamaCountAsJackalKiller", false, false);
        OptionTamaCountAsKillerOnlyWhenPromotionEnabled = BooleanOptionItem.Create(RoleInfo, 51, OptionName.TamaCountAsKillerOnlyWhenPromotionEnabled, false, false, OptionTamaCountAsJackalKiller);
        OptionTamaCanVent = BooleanOptionItem.Create(RoleInfo, 28, GeneralOption.CanVent, false, false);
        OptionTamaVentCooldown = FloatOptionItem.Create(RoleInfo, 29, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 0f, false, OptionTamaCanVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTamaVentMaxTime = FloatOptionItem.Create(RoleInfo, 30, GeneralOption.EngineerInVentCooldown, new(0f, 180f, 0.5f), 0f, false, OptionTamaCanVent)
            .SetZeroNotation(OptionZeroNotation.Infinity)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTamaCanVentMove = BooleanOptionItem.Create(RoleInfo, 31, "MadmateCanMovedByVent", false, false, OptionTamaCanVent);

        ObjectOptionitem.Create(RoleInfo, 40, "AddonOption", true, null).SetOptionName(() => "Sidekick Setting");
        RoleAddAddons.Create(RoleInfo, 32, NeutralKiller: true);

        HideRoleOptions(CustomRoles.Tama);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanSabotage;
    public bool CanUseImpostorVentButton() => CanVent;
    public override bool CanClickUseVentButton => CanVent;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (IsCharging || IsSuperCharging || ShowBeamMark) return false;
        return CanVent;
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVent;

    public override void Add()
    {
        PlayerSpeed = Main.AllPlayerSpeed[Player.PlayerId];
        CanSideKick = NextNoSideKick ? false : OptionCanMakeSidekick.GetBool();
        NextNoSideKick = false;
        SidekickCooldown = OptionSidekickCooldown.GetFloat();
        skMode = false;
        nowcool = KillCooldown;
        LastCooltime = (int)KillCooldown;

        skCandidateId = byte.MaxValue;
        skNearTimer = 0f;
        skCooldownTimer = 0f;
        skSpawnWaitTimer = -1f;

        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);

        if (IsCharging || IsSuperCharging || ShowBeamMark || IsFiring)
        {
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            Player.MarkDirtySettings();
            if (AmongUsClient.Instance.AmHost)
            {
                Player.SyncSettings();
            }
            IsCharging = false;
            Charging = false;
            IsSuperCharging = false;
            ShowBeamMark = false;
            IsFiring = false;
            SetRoleTextHeight(false);
        }
    }

    private void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        if (!CanSideKick) return;

        skMode = !skMode;

        if (!IsFiring)
            Player.SetKillCooldown(Mathf.Max(LastCooltime, 0.1f), delay: true);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(HasImpostorVision);
        AURoleOptions.PhantomCooldown = Cooldown;
    }

    public override bool CanUseAbilityButton() => !IsSuperCharging && !IsSuperBeam;

    public bool CanUseEmergencyButton() => !IsSuperCharging && !IsSuperBeam;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => true;

    bool ISelfVoter.CanUseVoted() => CanSideKick && Player.IsAlive();

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (Madmate.MadAvenger.Skill) return true;
        if (Impostor.Assassin.NowUse) return true;
        if (!Is(voter) || !CanSideKick || !Player.IsAlive()) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
            {
                skCandidateId = byte.MaxValue;
                Utils.SendMessage(
                    "<color=#00b4eb>【SK候補選択モード】</color>\n" +
                    "誰かに投票 → 次ターン1.5秒近づいてSK\n" +
                    "スキップ → キャンセル",
                    Player.PlayerId);
                SetMode(Player, true);
                return false;
            }
            if (status is VoteStatus.Skip)
            {
                skCandidateId = byte.MaxValue;
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                SetMode(Player, false);
                return false;
            }
            if (status is VoteStatus.Vote)
            {
                var target = GetPlayerById(votedForId);
                if (target == null || !target.IsAlive() || votedForId == Player.PlayerId)
                {
                    Utils.SendMessage("<color=#00b4eb>その相手はSKにできません。</color>", Player.PlayerId);
                    SetMode(Player, false);
                    return false;
                }
                var targetRole = target.GetCustomRole();
                if (targetRole is CustomRoles.King or CustomRoles.Autocrat or CustomRoles.Jackal or CustomRoles.JackalAlien
                    or CustomRoles.Jackaldoll or CustomRoles.JackalMafia or CustomRoles.JackalHadouHo
                    or CustomRoles.Merlin)
                {
                    Utils.SendMessage("<color=#00b4eb>その役職はSKにできません。</color>", Player.PlayerId);
                    SetMode(Player, false);
                    return false;
                }
                skCandidateId = votedForId;
                Utils.SendMessage(
                    $"<color=#00b4eb>【SK候補設定】</color>\n" +
                    $"{UtilsName.GetPlayerColor(target, true)} を候補に設定しました。\n" +
                    $"次ターン、1.5秒近づいてSK実行！",
                    Player.PlayerId);
                SetMode(Player, false);
                return false;
            }
        }
        return true;
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return;

        if (skMode && CanSideKick)
        {
            info.DoKill = false;
            (_, var target) = info.AttemptTuple;
            if (target.Is(CustomRoles.Madpsycho))
            {
                if (Madpsycho.CanPsycho)
                {
                    PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                    target.RpcMurderPlayer(Player);
                    return;
                }
            }
            DoSideKick(target);

            skMode = false;
            nowcool = SidekickCooldown;
            LastCooltime = (int)nowcool;
            Player.SetKillCooldown(nowcool);
            SendRpc();
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
            return;
        }

        nowcool = KillCooldown;
        LastCooltime = (int)nowcool;
        AfterKillPhantomReset();
    }

    public void AfterKillPhantomReset()
    {
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            if (PlayerControl.LocalPlayer != null && Is(PlayerControl.LocalPlayer))
                AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.2f, "JHHPhantomResetOnKill", true);
    }

    public void SetLoaded(bool loaded)
    {
        IsLoaded = loaded;
        SendRpc();
        if (loaded)
        {
            Utils.SendMessage(GetString("JackalHadouHoLoaded"), Player.PlayerId);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (IsFiring || ShowBeamMark || !Player.IsAlive() || IsCharging || IsSuperCharging) return;

        IsFiring = true;
        Charging = true;
        if (IsLoaded)
        {
            IsSuperCharging = true;
            superChargeTimer = 0f;
            Utils.AllPlayerKillFlash();
            StartSuperChargeFlashLoop();
        }
        else
        {
            IsCharging = true;
            chargeTimer = 0f;
            Utils.AllPlayerKillFlash();
        }

        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        Main.AllPlayerKillCooldown[Player.PlayerId] = 60f;
        Player.SetKillCooldown(60f);
        Player.SyncSettings();
        _ = new LateTask(() => { Player.SyncSettings(); }, 0.1f, "JackalHadouHoKillTimer", true);
        _prevCharging = IsCharging;
        _prevSuperCharging = IsSuperCharging;
        _prevBeamMark = ShowBeamMark;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
    }

    void StartSuperChargeFlashLoop()
    {
        const float flashInterval = 0.5f;
        int count = (int)(SuperChargeTime / flashInterval);
        for (int i = 1; i <= count; i++)
        {
            float t = i * flashInterval;
            _ = new LateTask(() =>
            {
                if (IsDead || !Player.IsAlive() || !IsSuperCharging) return;
                if (GameStates.IsMeeting || GameStates.CalledMeeting) return;
                Utils.AllPlayerKillFlash();
            }, t, null, null);
        }
    }

    void SetRoleTextHeight(bool beaming)
    {
        var rt = Player.cosmetics.nameText.transform.Find("RoleText");
        if (rt == null) return;
        var tmp = rt.GetComponent<TMPro.TextMeshPro>();
        if (tmp == null) return;
        if (beaming) { tmp.text = "<alpha=#00>　</alpha>"; rt.SetLocalY(0.35f); }
        else { tmp.enabled = true; rt.SetLocalY(0.35f); }
    }

    private void ResetAllState()
    {
        IsCharging = false;
        IsSuperCharging = false;
        ShowBeamMark = false;
        Charging = false;
        chargeTimer = 0f;
        superChargeTimer = 0f;
        HasHit = false;
        IsFiring = false;
        IsSuperBeam = false;
        _prevCharging = false;
        _prevSuperCharging = false;
        _prevBeamMark = false;
        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();
        Player.SyncSettings();
        SetRoleTextHeight(false);
        if (AmongUsClient.Instance.AmHost && ShipStatus.Instance != null)
        {
            try { ShipStatus.Instance.RpcUpdateSystem(Utils.GetCriticalSabotageSystemType(), 16); } catch { }
        }
        Utils.NowKillFlash = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (player.AmOwner && Is(player))
            AURoleOptions.PhantomCooldown = Cooldown;

        if (GameStates.IsInTask && Player.IsAlive() && CanSideKick)
        {
            if (skSpawnWaitTimer >= 0f && skSpawnWaitTimer < 3f)
                skSpawnWaitTimer += Time.fixedDeltaTime;
            if (skCooldownTimer > 0f)
                skCooldownTimer -= Time.fixedDeltaTime;
        }

        if (IsCharging) chargeTimer += Time.fixedDeltaTime;
        if (IsSuperCharging) superChargeTimer += Time.fixedDeltaTime;

        if (!AmongUsClient.Instance.AmHost) return;

        if (GameStates.IsInTask && Player.IsAlive() && CanSideKick && skCandidateId != byte.MaxValue)
        {
            if (SkCanApproach && skCooldownTimer <= 0f)
            {
                var skTarget = GetPlayerById(skCandidateId);
                if (skTarget == null || !skTarget.IsAlive())
                {
                    skCandidateId = byte.MaxValue;
                    skNearTimer = 0f;
                    SendRpc();
                }
                else
                {
                    float dist = Vector2.Distance(Player.GetTruePosition(), skTarget.GetTruePosition());
                    if (dist <= 1.5f)
                    {
                        skNearTimer += Time.fixedDeltaTime;
                        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
                        if (skNearTimer >= 1.5f)
                        {
                            DoSideKick(skTarget);
                            skCandidateId = byte.MaxValue;
                            skNearTimer = 0f;
                        }
                    }
                    else
                    {
                        skNearTimer = 0f;
                    }
                }
            }
        }

        if (!IsFiring && !IsCharging && !IsSuperCharging && !ShowBeamMark && Player.IsAlive())
        {
            if (nowcool > 0) nowcool -= Time.fixedDeltaTime;
            else nowcool = 0;

            var now = (int)nowcool;
            if (now != LastCooltime)
            {
                if (now <= 0) Player.SetKillCooldown(0.5f);
                LastCooltime = now;
                if (player != PlayerControl.LocalPlayer)
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
            }
        }

        if (MeetingHud.Instance != null)
        {
            if (IsCharging || IsSuperCharging || ShowBeamMark || IsFiring)
            {
                ResetAllState();
                UtilsNotifyRoles.NotifyRoles();
                SendRpc();
            }
            return;
        }

        if (!Player.IsAlive() && (IsCharging || IsSuperCharging || ShowBeamMark))
        {
            ResetAllState();
            UtilsNotifyRoles.NotifyRoles();
            SendRpc();
            return;
        }

        bool changed = (IsCharging != _prevCharging) || (IsSuperCharging != _prevSuperCharging) || (ShowBeamMark != _prevBeamMark);
        if (changed)
        {
            _prevCharging = IsCharging;
            _prevSuperCharging = IsSuperCharging;
            _prevBeamMark = ShowBeamMark;
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        }

        if (ShowBeamMark && Player.IsAlive()) ApplyBeamHit();
        if (IsCharging && chargeTimer >= ChargeTime) FireBeam();
        if (IsSuperCharging && superChargeTimer >= SuperChargeTime) FireSuperBeam();
    }

    void FireBeam()
    {
        if (IsDead || !Player.IsAlive()) return;
        BeamFacingLeft = Player.cosmetics.FlipX;
        SendBeamDirection(BeamFacingLeft);
        Utils.AllPlayerKillFlash();
        IsCharging = false; chargeTimer = 0f;
        HasHit = false; ShowBeamMark = true; IsSuperBeam = false;
        SetRoleTextHeight(true);
        _prevCharging = false; _prevBeamMark = true;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
        ApplyBeamHit();
        _ = new LateTask(() => FinishBeam(), BeamTime, "JHHBeamEnd", true);
    }

    void FireSuperBeam()
    {
        if (IsDead || !Player.IsAlive()) return;
        BeamFacingLeft = Player.cosmetics.FlipX;
        SendBeamDirection(BeamFacingLeft);
        Utils.AllPlayerKillFlash();
        IsSuperCharging = false; superChargeTimer = 0f;
        HasHit = false; ShowBeamMark = true; IsLoaded = false; IsSuperBeam = true;

        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetRoleClass() is Tama tama && tama.OwnerId == Player.PlayerId && pc.IsAlive())
            {
                PlayerState.GetByPlayerId(pc.PlayerId).DeathReason = CustomDeathReason.Sacrifice;
                pc.RpcExileV3();
                PlayerState.GetByPlayerId(pc.PlayerId).SetDead();
                UtilsGameLog.AddGameLog("JackalHadouHo", $"<color=#ff0000>【超波動砲】</color> 弾({UtilsName.GetPlayerColor(pc, true)})を消費しました");
                break;
            }
        }

        SetRoleTextHeight(true);
        _prevSuperCharging = false; _prevBeamMark = true;
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();
        ApplySuperBeamHit();
        _ = new LateTask(() => FinishBeam(), SuperBeamTime, "JHHSuperBeamEnd", true);
    }

    void FinishBeam()
    {
        if (IsDead || !Player.IsAlive())
        {
            ShowBeamMark = false; _prevBeamMark = false; IsSuperBeam = false;
            SetRoleTextHeight(false); IsFiring = false;
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            UtilsNotifyRoles.NotifyRoles(); SendRpc(); return;
        }

        ShowBeamMark = false; _prevBeamMark = false; IsSuperBeam = false;
        SetRoleTextHeight(false);
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        SendRpc();

        if (!HasHit && SelfDestructOnMiss)
        {
            Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
            Player.MarkDirtySettings();
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
            Player.RpcMurderPlayerV2(Player);
            IsFiring = false; return;
        }

        Main.AllPlayerSpeed[Player.PlayerId] = PlayerSpeed;
        Player.MarkDirtySettings();

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) { IsFiring = false; return; }

            nowcool = KillCooldown;
            LastCooltime = (int)nowcool;
            Main.AllPlayerKillCooldown[Player.PlayerId] = nowcool;
            Player.SetKillCooldown(nowcool);

            if (PlayerControl.LocalPlayer != null && Is(PlayerControl.LocalPlayer))
                AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            _ = new LateTask(() => { IsFiring = false; }, 0.3f, "JHHResetFiring", true);
        }, 0.2f, "JHHResetKillCool", true);
    }

    void ApplyBeamHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        var myPos = Player.GetTruePosition();
        Vector2 dir = BeamFacingLeft ? Vector2.left : Vector2.right;
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (!Player.IsAlive()) break;
            if (target.PlayerId == Player.PlayerId) continue;
            if (!target.IsAlive()) continue;            if (!KillJackal && target.GetCustomRole() is CustomRoles.Jackal or CustomRoles.JackalMafia
                or CustomRoles.JackalAlien or CustomRoles.Jackaldoll or CustomRoles.JackalHadouHo
                or CustomRoles.Tama && !SuddenDeathMode.NowSuddenDeathMode) continue;
            var toTarget = (Vector2)target.GetTruePosition() - myPos;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            if ((toTarget - dir * dot).magnitude > 1.3f) continue;
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, deathReason: CustomDeathReason.Evaporation);
            HasHit = true;
            UtilsGameLog.AddGameLog("JackalHadouHo", $"<color=#00b4eb>【波動砲】</color> {UtilsName.GetPlayerColor(Player, true)} ═> {UtilsName.GetPlayerColor(target, true)}");
        }
    }

    void ApplySuperBeamHit()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        var myPos = Player.GetTruePosition();
        Vector2 dir = BeamFacingLeft ? Vector2.left : Vector2.right;
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (!Player.IsAlive()) break;
            if (target.PlayerId == Player.PlayerId) continue;
            if (!target.IsAlive()) continue;
            if (!KillJackal && target.GetCustomRole() is CustomRoles.Jackal or CustomRoles.JackalMafia
                or CustomRoles.JackalAlien or CustomRoles.Jackaldoll or CustomRoles.JackalHadouHo
                or CustomRoles.Tama && !SuddenDeathMode.NowSuddenDeathMode) continue;
            var toTarget = (Vector2)target.GetTruePosition() - myPos;
            float dot = Vector2.Dot(toTarget, dir);
            if (dot <= 0) continue;
            if ((toTarget - dir * dot).magnitude > 4.0f) continue;
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, deathReason: CustomDeathReason.Evaporation);
            HasHit = true;
            UtilsGameLog.AddGameLog("JackalHadouHo", $"<color=#ff0000>【超波動砲】</color> {UtilsName.GetPlayerColor(Player, true)} ═> {UtilsName.GetPlayerColor(target, true)}");
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ResetAllState(); UtilsNotifyRoles.NotifyRoles(ForceLoop: true); SendRpc();
    }

    public override void OnStartMeeting()
    {
        ResetAllState();
        if (skMode) { skMode = false; SendRpc(); }
        skCandidateId = byte.MaxValue;
        skNearTimer = 0f;
        skSpawnWaitTimer = -1f;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (PlayerControl.LocalPlayer != null && Is(PlayerControl.LocalPlayer))
        {
            AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown();
        }

        nowcool = KillCooldown;
        LastCooltime = (int)nowcool;
        Main.AllPlayerKillCooldown[Player.PlayerId] = nowcool;
        Player.SetKillCooldown(nowcool);

        skSpawnWaitTimer = 0f;
        skCooldownTimer = SidekickCooldown;
        skNearTimer = 0f;
    }

    private void DoSideKick(PlayerControl target)
    {
        CanSideKick = false;
        var targetRole = target.GetCustomRole();
        if (targetRole is CustomRoles.King or CustomRoles.Autocrat or CustomRoles.Jackal or CustomRoles.JackalAlien
            or CustomRoles.Jackaldoll or CustomRoles.JackalMafia or CustomRoles.JackalHadouHo
            or CustomRoles.Merlin
            || ((targetRole.IsImpostor() || targetRole is CustomRoles.Egoist) && !OptionImpostorCanSidekick.GetBool()))
        {
            Utils.SendMessage("<color=#00b4eb>この役職はSKにできません。</color>", Player.PlayerId);
            SendRpc(); return;
        }

        Player.RpcProtectedMurderPlayer(target);
        target.RpcProtectedMurderPlayer(Player);
        target.RpcProtectedMurderPlayer(target);
        if (target.Is(CustomRoles.Madpsycho))
        {
            if (Madpsycho.CanPsycho)
            {
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                target.RpcMurderPlayer(Player);
                return;
            }
        }
        target.RpcSetCustomRole(CustomRoles.Tama, log: null);
        if (target.GetRoleClass() is Tama tama) tama.SetOwner(Player.PlayerId);
        if (!Utils.RoleSendList.Contains(target.PlayerId)) Utils.RoleSendList.Add(target.PlayerId);

        if (OptionSidekickCanSeeOldImpostorTeammates.GetBool())
        {
            var state = PlayerState.GetByPlayerId(target.PlayerId);
            foreach (var imp in PlayerCatch.AllPlayerFirstTypes.Where(x => x.Value is CustomRoleTypes.Impostor))
            {
                if (state.TargetColorData.ContainsKey(imp.Key)) NameColorManager.Remove(target.PlayerId, imp.Key);
                NameColorManager.Add(target.PlayerId, imp.Key, "ffffff");
            }
        }
        if (OptionImpostorCanSeeNameColor.GetBool())
        {
            foreach (var imp in PlayerCatch.AllPlayerFirstTypes.Where(x => x.Value is CustomRoleTypes.Impostor))
            {
                var impostorstate = PlayerState.GetByPlayerId(imp.Key);
                if (impostorstate.TargetColorData.ContainsKey(target.PlayerId)) NameColorManager.Remove(imp.Key, target.PlayerId);
                NameColorManager.Add(imp.Key, target.PlayerId, "ffffff");
            }
        }

        UtilsGameLog.AddGameLog("JackalHadouHoSideKick",
            string.Format(GetString("log.Sidekick"),
            UtilsName.GetPlayerColor(target, true) + $"({UtilsRoleText.GetTrueRoleName(target.PlayerId)})",
            UtilsName.GetPlayerColor(Player, true)));

        UtilsOption.MarkEveryoneDirtySettings();

        if (PlayerControl.LocalPlayer != null && Is(PlayerControl.LocalPlayer))
        {
            AURoleOptions.PhantomCooldown = Cooldown;
            Player.RpcResetAbilityCooldown();
        }

        SendRpc(); UtilsNotifyRoles.NotifyRoles();
    }

    public void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsCharging);
        sender.Writer.Write(IsSuperCharging);
        sender.Writer.Write(ShowBeamMark);
        sender.Writer.Write(CanSideKick);
        sender.Writer.Write(IsLoaded);
        sender.Writer.Write(IsSuperBeam);
        sender.Writer.Write(skMode);
        sender.Writer.Write(skCandidateId);
    }

    void SendBeamDirection(bool left)
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
        sender.Writer.Write(left);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if (reader.Length - reader.Position == 2)
        {
            reader.ReadByte(); BeamFacingLeft = reader.ReadBoolean(); return;
        }
        IsCharging = reader.ReadBoolean();
        IsSuperCharging = reader.ReadBoolean();
        ShowBeamMark = reader.ReadBoolean();
        CanSideKick = reader.ReadBoolean();
        IsLoaded = reader.ReadBoolean();
        IsSuperBeam = reader.ReadBoolean();
        skMode = reader.ReadBoolean();
        if (reader.BytesRemaining > 0)
            skCandidateId = reader.ReadByte();
    }

    public override bool GetTemporaryName(ref string name, ref bool NoMarker, bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Player.IsAlive() || isForMeeting) return false;
        string myColor = "#" + ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        if ((IsCharging || IsSuperCharging) && seen.PlayerId == Player.PlayerId)
        {
            bool fl = seer.PlayerId == Player.PlayerId ? Player.cosmetics.FlipX : BeamFacingLeft;
            string bigStar = $"<size=800%><color={myColor}>★</color></size>";
            string blank = "　　　";
            name = "<line-height=1200%>\n" + (fl ? bigStar + blank : blank + bigStar) + "</line-height>";
            NoMarker = true; return true;
        }

        if (seen == seer && Is(seer) && !seer.IsModClient() && (IsCharging || IsSuperCharging || ShowBeamMark))
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

        string beam = IsSuperBeam ? "<#f00>━━━━━</color>" : "<#00CFFF>━━━━━━━</color>";
        string finalBeam = IsSuperBeam ? beam : beam + beam;

        string blank = IsSuperBeam ? "<size=1800%>　</size>" : "<size=1200%>　</size>";
        string sB = fl ? star + blank : blank + star;
        string lB = fl ? finalBeam + sB : sB + finalBeam;

        string hugeBlank = "<alpha=#00>" + new string('　', IsSuperBeam ? 1 : 10) + "</alpha>";

        string ls;
        if (IsSuperBeam)
            ls = wider ? "<line-height=15000%>\n" : "<line-height=11000%>\n";
        else
            ls = wider ? "<line-height=5300%>\n" : "<line-height=4300%>\n";

        string ss = IsSuperBeam ? "<size=15000%>" : "<size=5000%>";
        string se = "</size></line-height>";

        name = fl
            ? ls + $"{ss}{lB}{se}{ss}{hugeBlank}{se}"
            : ls + $"{ss}{hugeBlank}{se}{ss}{lB}{se}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";

        if (skMode && CanSideKick)
            return $"{size}<color=#00b4eb>【SKモード】SKしたいプレイヤーにキル！ペット→キルモードへ</color>";

        if (CanSideKick && skCandidateId != byte.MaxValue && !IsCharging && !IsSuperCharging && !ShowBeamMark)
        {
            var skTarget = GetPlayerById(skCandidateId);
            string tName = skTarget != null ? skTarget.Data.PlayerName : "???";
            if (!SkCanApproach)
                return $"{size}<color=#00b4eb>待機中... | 候補: {tName}</color>";
            if (skCooldownTimer > 0f)
                return $"{size}<color=#00b4eb>SK CD: {Mathf.CeilToInt(skCooldownTimer)}s | 候補: {tName}</color>";
            float progress = Mathf.Min(skNearTimer, 1.5f);
            return $"{size}<color=#00b4eb>{tName}に近づき中 {progress:F1}/1.5s</color>";
        }

        if (CanSideKick && !IsCharging && !IsSuperCharging && !ShowBeamMark)
            return $"{size}<color=#ff0000>ファントム→ビーム / ペット→SKモード / 会議自投票→SK候補指定</color>";

        if (IsLoaded) return $"{size}<color=#00b4eb>【装填済】ファントムボタン → 超波動砲チャージ！</color>";
        if (!IsCharging && !IsSuperCharging) return $"{size}<color=#ff0000>ファントムボタン → チャージ発射</color>";
        if (IsSuperCharging)
        {
            var r = Mathf.Max(0f, SuperChargeTime - superChargeTimer);
            return $"{(isForHud ? "" : "<size=100%>")}<color=#ff0000>超チャージ中... {r:F1}s</color>";
        }
        var rem = Mathf.Max(0f, ChargeTime - chargeTimer);
        return $"{(isForHud ? "" : "<size=100%>")}<color=#ff0000>チャージ中... {rem:F1}s</color>";
    }

    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || isForMeeting || !Player.IsAlive()) return "";
        if (IsSuperCharging && seer.PlayerId != Player.PlayerId)
            return $"\n<size=100%><color=#ff0000>超チャージ中... {(int)Mathf.Max(0f, SuperChargeTime - superChargeTimer)}s</color></size>";
        if (IsCharging && seer.PlayerId != Player.PlayerId)
            return $"\n<size=100%><color=#ff0000>チャージ中... {(int)Mathf.Max(0f, ChargeTime - chargeTimer)}s</color></size>";
        if (ShowBeamMark && seer.PlayerId != Player.PlayerId)
            return "\n<size=100%><color=#ff0000>ビーム中</color></size>";
        return "";
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (!CanSideKick && !skMode) return "";
        return skMode
            ? " <color=#ff6600>[SK]</color>"
            : " <color=#00b4eb>[キル]</color>";
    }

    public override string GetAbilityButtonText() => IsLoaded ? "超発射" : "発射";
    public override bool OverrideAbilityButton(out string text)
    {
        text = IsLoaded ? "SuperHadouHo_Ability" : "HadouHo_Ability";
        return true;
    }

    public bool OverrideKillButtonText(out string text)
    {
        if (skMode && CanSideKick) { text = "弾SK"; return true; }
        text = ""; return false;
    }

    public bool OverrideKillButton(out string text)
    {
        if (skMode && CanSideKick) { text = "JackalHadouHo_Kill"; return true; }
        text = ""; return false;
    }
}

public sealed class Tama : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Tama),
            player => new Tama(player),
            CustomRoles.Tama,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            554900,
            SetupOptionItem,
            "tm",
            "#00b4eb",
            (1, 5),
            from: From.SuperNewRoles,
            isDesyncImpostor: true,
            countType: CountTypes.Crew
        );

    private static void SetupOptionItem()
    {
        JackalHadouHo.HideRoleOptions(CustomRoles.Tama);
        RoleAddAddons.Create(RoleInfo, 10, NeutralKiller: true);
    }

    public Tama(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.False)
    {
        OwnerId = byte.MaxValue;
        hasLoaded = false;
        isLoading = false;
        ApplyJackalKillerCount();
    }

    public byte OwnerId;
    public bool hasLoaded;
    bool isLoading;
    int snapFrame = 0;

    public void SetOwner(byte ownerId)
    {
        OwnerId = ownerId;
        SendRPC();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(true);
        if (!JackalHadouHo.GetTamaCanLoad())
            Player.RpcSetRoleDesync(RoleTypes.Engineer, Player.GetClientId());
        AURoleOptions.EngineerCooldown = JackalHadouHo.GetTamaVentCooldown();
        AURoleOptions.EngineerInVentMaxTime = JackalHadouHo.GetTamaVentMaxTime();
    }

    public float CalculateKillCooldown() => JackalHadouHo.GetTamaLoadCooldown();

    public bool CanUseKillButton()
    {
        if (!JackalHadouHo.GetTamaCanLoad()) return false;
        return Player.IsAlive() && !hasLoaded && !isLoading && IsOwnerAlive();
    }
    private void ApplyJackalKillerCount()
    {
        var shouldCount = JackalHadouHo.GetTamaCountAsJackalKiller()
            && (!JackalHadouHo.GetTamaCountAsKillerOnlyWhenPromotionEnabled() || JackalHadouHo.GetSidekickPromotion());
        MyState.SetCountType(shouldCount ? CountTypes.Jackal : CountTypes.Crew);
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => JackalHadouHo.GetTamaCanVent();
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => JackalHadouHo.GetTamaCanVentMove();

    private bool IsOwnerAlive()
    {
        if (OwnerId == byte.MaxValue) return false;
        var owner = GetPlayerById(OwnerId);
        return owner != null && owner.IsAlive();
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;
        if (!JackalHadouHo.GetTamaCanLoad()) return;

        var (killer, target) = info.AttemptTuple;
        if (hasLoaded || isLoading) return;
        if (target.PlayerId != OwnerId) return;

        isLoading = true;
        hasLoaded = true;

        var owner = GetPlayerById(OwnerId);
        if (owner?.GetRoleClass() is JackalHadouHo jhh)
            jhh.SetLoaded(true);

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        Utils.SendMessage(GetString("TamaLoaded"), Player.PlayerId);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!JackalHadouHo.GetTamaCanLoad()) return "<color=#5e5e5e>【装填不可】</color>";
        if (hasLoaded) return $"<color=#00b4eb>【装填済】</color>";
        return $"<color=#5e5e5e>【未装填】</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        if (!JackalHadouHo.GetTamaCanLoad())
            return $"{(isForHud ? "" : "<size=60%>")}<color=#5e5e5e>装填機能は無効化されています</color>";
        if (hasLoaded)
            return $"{(isForHud ? "" : "<size=60%>")}<color=#00b4eb>装填済み！波動砲ジャッカルが超波動砲を撃てる</color>";
        if (!IsOwnerAlive())
            return $"{(isForHud ? "" : "<size=60%>")}<color=#5e5e5e>波動砲ジャッカルが死亡しています</color>";
        return $"{(isForHud ? "" : "<size=60%>")}<color=#00b4eb>波動砲ジャッカルにキルボタンで装填</color>";
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        ApplyJackalKillerCount();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (OwnerId == byte.MaxValue) return;

        var owner = GetPlayerById(OwnerId);

        if (!player.IsAlive() && hasLoaded)
        {
            hasLoaded = false;
            isLoading = false;
            if (owner?.GetRoleClass() is JackalHadouHo jhh)
                jhh.SetLoaded(false);
            SendRPC();
            return;
        }

        if (player.IsAlive() && (owner == null || !owner.IsAlive() || owner.GetCustomRole() != CustomRoles.JackalHadouHo))
        {
            if (!JackalHadouHo.GetSidekickPromotion())
            {
                OwnerId = byte.MaxValue;
                SendRPC();
                return;
            }

            OwnerId = byte.MaxValue;
            MyState.SetCountType(CountTypes.Jackal);
            if (!Utils.RoleSendList.Contains(Player.PlayerId))
                Utils.RoleSendList.Add(Player.PlayerId);
            JackalHadouHo.NextNoSideKick = true;
            Player.RpcSetCustomRole(CustomRoles.JackalHadouHo, true);
            SendRPC();
            UtilsNotifyRoles.NotifyRoles();
            return;
        }

        if (!hasLoaded) return;
        if (owner == null || !owner.IsAlive() || !player.IsAlive()) return;

        snapFrame++;
        if (snapFrame % 3 == 0)
        {
            var targetPos = owner.transform.position;
            player.NetTransform.SnapTo(targetPos);

            ushort sid = (ushort)(player.NetTransform.lastSequenceId + 2U);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                player.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
            NetHelpers.WriteVector2(targetPos, writer);
            writer.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (hasLoaded || isLoading)
        {
            hasLoaded = false;
            isLoading = false;
            var owner = GetPlayerById(OwnerId);
            if (owner?.GetRoleClass() is JackalHadouHo jhh)
                jhh.SetLoaded(false);
            SendRPC();
            UtilsNotifyRoles.NotifyRoles();
        }
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (seen.Is(CountTypes.Jackal)) enabled = true;
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (seen.Is(CountTypes.Jackal)) enabled = true;
    }

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(OwnerId);
        sender.Writer.Write(hasLoaded);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        OwnerId = reader.ReadByte();
        hasLoaded = reader.ReadBoolean();
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = "装填";
        return true;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Tama_Kill";
        return true;
    }
}