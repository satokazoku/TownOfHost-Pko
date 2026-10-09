using AmongUs.GameOptions;
using Hazel;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Mare : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Mare),
            player => new Mare(player),
            CustomRoles.Mare,
            () => OptionOneClick.GetBool() ? RoleTypes.Phantom : RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            5200,
            SetupCustomOption,
            "ma",
            OptionSort: (4, 5),
            assignInfo: new(CustomRoles.Mare, CustomRoleTypes.Impostor)
            {
                IsInitiallyAssignableCallBack = () => ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Electrical, out var systemType) && systemType.TryCast<SwitchSystem>(out _),  // 停電が存在する
            },
            from: From.TownOfHost,
            Desc: () =>
            {
                if (!OptionOneClick.GetBool())
                {
                    return string.Format(GetString("MareDesc"), OptionKillCooldownInLightsOut.GetFloat(), OptionSpeedInLightsOut.GetFloat(), OptionCanSeeNameColor.GetBool() ? GetString("MareDescNameColor") : ""
            , OptionAllCanKill.GetBool() ? GetString("MareDescCanKill") : GetString("MareDescNonKill"));
                }
                else
                {
                    return string.Format(GetString("MareDescOc"), OptionKillCooldownInLightsOut.GetFloat(), OptionSpeedInLightsOut.GetFloat(), OptionCanSeeNameColor.GetBool() ? GetString("MareDescNameColor") : ""
, OptionAllCanKill.GetBool() ? GetString("MareDescCanKill") : GetString("MareDescNonKill"));
                }
            }
        );
    public Mare(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldownInLightsOut = OptionKillCooldownInLightsOut.GetFloat();
        SpeedInLightsOut = OptionSpeedInLightsOut.GetFloat();
        CanSeeNameColor = OptionCanSeeNameColor.GetBool();
        AllCanKill = OptionAllCanKill.GetBool();
        NomalKillCooldown = NowKillCoolDown = OptionKillCooldown.GetFloat();
        NoDelay = OptionNoDelay.GetBool();
        CoolDown = OptionCoolDown.GetFloat();
        DeactiveCoolDown = OptionDeactiveCoolDown.GetFloat();
        Duration = OptionDuration.GetFloat();
        OnlyOneClick = OptionOneClick.GetBool();
        Durationtimer = Duration;
        Used = false;

        IsActivateKill = false;
        IsAccelerated = false;

        flugl1 = false;
        flugn1 = 0;
    }

    private static OptionItem OptionKillCooldownInLightsOut;
    private static OptionItem OptionSpeedInLightsOut;
    private static OptionItem OptionCanSeeNameColor; static bool CanSeeNameColor;
    private static OptionItem OptionAllCanKill; static bool AllCanKill;
    private static OptionItem OptionKillCooldown; static float NomalKillCooldown;
    private static OptionItem OptionDarkKilldis;
    private static OptionItem OptionNoDelay; static bool NoDelay;
    private static OptionItem OptionOneClick;
    private static OptionItem OptionCoolDown; static float CoolDown;
    private static OptionItem OptionDeactiveCoolDown; static float DeactiveCoolDown;
    private static OptionItem OptionDuration; static float Duration;
    private static OptionItem OptionOnlyOneClick; static bool OnlyOneClick;
    enum OptionName
    {
        MareAddSpeedInLightsOut,
        MareKillCooldownInLightsOut,
        MareCanSeeNameColor,
        MareAllCanKill,
        MareDarkKilldistance,
        MareNoDelay,
        MareOneClick,
        MareDeactiveCoolDown,
        ReverserDuration,
        MareOnlyOneclick
    }
    private float KillCooldownInLightsOut;
    private float SpeedInLightsOut;
    private static bool IsActivateKill;
    private bool IsAccelerated;  //加速済みかフラグ
    float NowKillCoolDown;
    bool Used;
    float Durationtimer;
    public bool IsPhantomRole => OptionOneClick.GetBool();
    public bool IsresetAfterKill => false;
    public static void SetupCustomOption()
    {
        OptionSpeedInLightsOut = FloatOptionItem.Create(RoleInfo, 10, OptionName.MareAddSpeedInLightsOut, new(0.0f, 5.0f, 0.2f), 1.4f, false);
        OptionKillCooldownInLightsOut = FloatOptionItem.Create(RoleInfo, 11, OptionName.MareKillCooldownInLightsOut, new(0f, 180f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanSeeNameColor = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MareCanSeeNameColor, true, false);
        OptionAllCanKill = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MareAllCanKill, true, false);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 14, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 40f, false, OptionAllCanKill)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDarkKilldis = StringOptionItem.Create(RoleInfo, 15, OptionName.MareDarkKilldistance, EnumHelper.GetAllNames<OverrideKilldistance.KillDistance>(), 0, false);
        OptionNoDelay = BooleanOptionItem.Create(RoleInfo, 16, OptionName.MareNoDelay, true, false);

        OptionOneClick = BooleanOptionItem.Create(RoleInfo, 17, OptionName.MareOneClick, true, false);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 18, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false, OptionOneClick)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDeactiveCoolDown = FloatOptionItem.Create(RoleInfo, 19, OptionName.MareDeactiveCoolDown, OptionBaseCoolTime, 10f, false, OptionOneClick)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 20, OptionName.ReverserDuration, OptionBaseCoolTime, 0f, false, OptionOneClick)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionOnlyOneClick = BooleanOptionItem.Create(RoleInfo, 21, OptionName.MareOnlyOneclick, true, false, OptionOneClick);
    }
    public bool CanUseKillButton() => IsActivateKill || AllCanKill;
    public float CalculateKillCooldown() => IsActivateKill ? KillCooldownInLightsOut : NomalKillCooldown;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (IsActivateKill && !IsAccelerated)
        { //停電中で加速済みでない場合
            IsAccelerated = true;
            Main.AllPlayerSpeed[Player.PlayerId] += SpeedInLightsOut;//Mareの速度を加算
        }
        else if (!IsActivateKill && IsAccelerated)
        { //停電中ではなく加速済みになっている場合
            IsAccelerated = false;
            Main.AllPlayerSpeed[Player.PlayerId] -= SpeedInLightsOut;//Mareの速度を減算
        }
        if (IsActivateKill)
        {
            AURoleOptions.KillDistance = OptionDarkKilldis.GetInt();
        }
        else if (!IsActivateKill)
        {
            AURoleOptions.KillDistance = Main.NormalOptions.KillDistance;
        }
        AURoleOptions.PhantomCooldown = Used ? DeactiveCoolDown : CoolDown;
    }
    private void ActivateKill(bool activate, bool OneClick = false)
    {
        IsActivateKill = activate;
        if (OneClick)
        {
            Used = activate;
            Durationtimer = Duration;
        }
        if (AmongUsClient.Instance.AmHost)
        {
            SendRPC();
            _ = new LateTask(() => Player.SetKillCooldown(IsActivateKill ? -1 : (1 < NowKillCoolDown ? NowKillCoolDown : 0.5f), delay: true), 0.2f, "MareKillCool");
            UtilsNotifyRoles.NotifyRoles();
        }
    }
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = true;
        if (Utils.IsActive(SystemTypes.Electrical) && IsActivateKill && !OnlyOneClick) return;
        ActivateKill(!Used, true);
        AURoleOptions.PhantomCooldown = Used ? DeactiveCoolDown : CoolDown;
    }
    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsActivateKill);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        IsActivateKill = reader.ReadBoolean();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsInTask)
        {
            if (IsActivateKill)
            {
                if (!Utils.IsActive(SystemTypes.Electrical) && !Used)
                {
                    //停電解除されたらキルモード解除
                    ActivateKill(false);
                }
            }
            else if (0 < NowKillCoolDown)//停電中は通常キルクールを進めない
            {
                NowKillCoolDown -= Time.fixedDeltaTime;
            }
            if (!OptionOneClick.GetBool()) return;
            if (!Utils.IsActive(SystemTypes.Electrical) && Used && Duration != 0f)
            {
                Durationtimer -= Time.fixedDeltaTime;
            }
            else if (OnlyOneClick && Used && Duration != 0f)
            {
                Durationtimer -= Time.fixedDeltaTime;
            }
            if (Durationtimer <= 0f && Duration != 0f)
            {
                ActivateKill(false, true);
            }
        }
    }
    public override bool OnSabotage(PlayerControl player, SystemTypes systemType)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player) || OnlyOneClick) return true;

        if (systemType == SystemTypes.Electrical && !NoDelay)
        {
            flugl1 = false;
            _ = new LateTask(() =>
            {
                //まだ停電が直っていなければキル可能モードに
                if (Utils.IsActive(SystemTypes.Electrical))
                {
                    ActivateKill(true);
                }
            }, CanSeeNameColor ? 0.5f : 4.0f, "Mare Activate Kill");
        }
        else if (systemType == SystemTypes.Electrical)
        {
            flugl1 = false;
            ActivateKill(true);
        }
        return true;
    }
    public static bool KnowTargetRoleColor(PlayerControl target, bool isMeeting)
    {
        if (!CanSeeNameColor || isMeeting || target.GetRoleClass() is not Mare mare) return false;

        var Use = IsActivateKill || mare.Used;
        return Use;
    }

    void IKiller.OnMurderPlayerAsKiller(MurderInfo info)
    {
        NowKillCoolDown = NomalKillCooldown;
        if (Utils.IsActive(SystemTypes.Electrical))
            flugn1++;
        if (flugn1 is 2) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
    }
    public override void AfterSabotage(SystemTypes systemType)
    {
        if (systemType == SystemTypes.Electrical && Main.SabotageActivetimer < 3 && flugn1 is 0)
            flugl1 = true;
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (flugl1 && Player.PlayerId == (exiled?.PlayerId ?? byte.MaxValue))
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
        flugl1 = false;
    }
    public override void AfterMeetingTasks()
    {
        NowKillCoolDown = NomalKillCooldown;
        flugn1 = 0;
    }
    int flugn1;
    bool flugl1;
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
    }
}