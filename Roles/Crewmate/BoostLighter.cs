using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class BoostLighter : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BoostLighter),
            player => new BoostLighter(player),
            CustomRoles.BoostLighter,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            38700,
            SetupOptionItem,
            "bl",
            "#ffe066",
            (5, 1),
            from: From.None
        );

    public BoostLighter(PlayerControl player)
        : base(RoleInfo, player)
    {
        BoostDuration = OptionBoostDuration.GetFloat();
        BoostCooldown = OptionBoostCooldown.GetFloat();
        BoostVision = OptionBoostVision.GetFloat();
        AffectedByBlackout = OptionAffectedByBlackout.GetBool();
        isBoostActive = false;
        boostTimer = 0f;
        cooldownTimer = OptionBoostCooldown.GetFloat();
    }

    static OptionItem OptionBoostDuration;
    static float BoostDuration;
    static OptionItem OptionBoostCooldown;
    static float BoostCooldown;
    static OptionItem OptionBoostVision;
    static float BoostVision;
    static OptionItem OptionAffectedByBlackout;
    static bool AffectedByBlackout;

    bool isBoostActive;
    float boostTimer;
    float cooldownTimer;

    enum OptionName
    {
        BoostLighterCooldown,
        BoostLighterDuration,
        BoostLighterVision,
        BoostLighterAffectedByBlackout,
    }

    static void SetupOptionItem()
    {
        OptionBoostCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.BoostLighterCooldown,
            new(2.5f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionBoostDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.BoostLighterDuration,
            new(2.5f, 20f, 2.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        OptionBoostVision = FloatOptionItem.Create(RoleInfo, 12, OptionName.BoostLighterVision,
            new(0.0f, 5.0f, 0.05f), 1.5f, false).SetValueFormat(OptionFormat.Multiplier);
        OptionAffectedByBlackout = BooleanOptionItem.Create(RoleInfo, 13, OptionName.BoostLighterAffectedByBlackout,
            true, false);
    }

    public override void Add()
    {
        PetActionManager.Register(Player.PlayerId, ActivateBoost);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = isBoostActive
            ? Mathf.Max(BoostDuration - boostTimer, 0.1f)
            : Mathf.Max(cooldownTimer, 0.1f);
        AURoleOptions.EngineerInVentMaxTime = 0f;

        if (!isBoostActive) return;

        if (!AffectedByBlackout)
        {
            opt.SetVision(true);
            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.NormalOptions.ImpostorLightMod);
            return;
        }

        bool blackoutActive = Utils.IsActive(SystemTypes.Electrical);
        if (blackoutActive) return;

        opt.SetFloat(FloatOptionNames.CrewLightMod, BoostVision);
    }

    public override bool CanClickUseVentButton => true;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public void ActivateBoost()
    {
        if (!Player.IsAlive()) return;
        if (isBoostActive) return;
        if (cooldownTimer > 0f) return;

        isBoostActive = true;
        boostTimer = 0f;

        Player.MarkDirtySettings();
        if (AmongUsClient.Instance.AmHost)
            Player.SyncSettings();

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
        Logger.Info($"{Player.Data.GetLogPlayerName()} が視界ブーストを発動", "BoostLighter");
    }

    private void DeactivateBoost()
    {
        if (!isBoostActive) return;
        isBoostActive = false;
        boostTimer = 0f;
        cooldownTimer = BoostCooldown;

        Player.MarkDirtySettings();
        if (AmongUsClient.Instance.AmHost)
            Player.SyncSettings();

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!isBoostActive && cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
            if (cooldownTimer < 0f) cooldownTimer = 0f;
        }

        if (!isBoostActive) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) { DeactivateBoost(); return; }

        boostTimer += Time.fixedDeltaTime;
        if (boostTimer >= BoostDuration)
            DeactivateBoost();
    }

    public override void OnStartMeeting()
    {
        if (isBoostActive)
            DeactivateBoost();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        cooldownTimer = BoostCooldown;
        Player.RpcResetAbilityCooldown();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (isBoostActive)
            return $"{size}<color={color}>【視界ブースト中】</color>";

        return $"{size}<color={color}>ペットなで → 視界ブースト発動</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(isBoostActive);
        sender.Writer.Write(boostTimer);
        sender.Writer.Write(cooldownTimer);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isBoostActive = reader.ReadBoolean();
        boostTimer = reader.ReadSingle();
        cooldownTimer = reader.ReadSingle();
    }
}