using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

public sealed class MadSheriff : RoleBase, IKiller, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadSheriff),
            player => new MadSheriff(player),
            CustomRoles.MadSheriff,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Madmate,
            21400,
            SetupOptionItem,
            "Msf",
            OptionSort: (2, 1),
            isDesyncImpostor: true,
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            from: From.TownOfHost_Y
        );

    public MadSheriff(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        CurrentSuicideMotion = (SuicideMotionOption)OptionSuicideMotion.GetValue();
        MisfireKillsTarget = OptionMisfireKillsTarget.GetBool();
        CanVent = OptionCanVent.GetBool();
        CanSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        CanSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
    }

    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionSuicideMotion;
    private static OptionItem OptionMisfireKillsTarget;
    private static OptionItem OptionCanVent;

    private static float KillCooldown;
    private static bool MisfireKillsTarget;
    private static bool CanVent;
    private static bool CanSeeKillFlash;
    private static bool CanSeeDeathReason;

    private SuicideMotionOption CurrentSuicideMotion;

    private enum SuicideMotionOption
    {
        Default,
        MotionKilled
    }

    private enum OptionName
    {
        SheriffMisfireKillsTarget,
        SillySheriffSuicideMotion
    }

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMisfireKillsTarget = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SheriffMisfireKillsTarget, false, false);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, false, false);
        OptionSuicideMotion = StringOptionItem.Create(RoleInfo, 13, OptionName.SillySheriffSuicideMotion, EnumHelper.GetAllNames<SuicideMotionOption>(), 0, false);
        RoleAddAddons.Create(RoleInfo, 20, MadMate: true);
    }

    public bool CanUseKillButton() => Player.IsAlive();
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseImpostorVentButton() => CanVent;
    public bool CanUseSabotageButton() => false;
    public bool? CheckKillFlash(MurderInfo info) => CanSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => CanSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        var (killer, target) = info.AttemptTuple;
        PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = CustomDeathReason.Misfire;

        switch (CurrentSuicideMotion)
        {
            case SuicideMotionOption.Default:
                killer.RpcMurderPlayer(killer);
                break;
            case SuicideMotionOption.MotionKilled:
                target.RpcMurderPlayer(killer);
                break;
        }

        UtilsGameLog.AddGameLog("MadSheriff", string.Format(GetString("SheriffMissLog"), UtilsName.GetPlayerColor(target.PlayerId)));

        if (!MisfireKillsTarget) info.DoKill = false;
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("DeathReason.Misfire");
        return true;
    }
}
