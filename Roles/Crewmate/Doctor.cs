using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

public sealed class Doctor : RoleBase, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Doctor),
            player => new Doctor(player),
            CustomRoles.Doctor,
            () => RoleTypes.Scientist,
            CustomRoleTypes.Crewmate,
            31200,
            SetupOptionItem,
            "doc",
            "#80ffdd",
            (6, 1),
            from: From.NebulaontheShip
        );
    public Doctor(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        TaskCompletedBatteryCharge = OptionTaskCompletedBatteryCharge.GetFloat();
        CanseeComms = OptionCanSeeComms.GetBool();
    }
    private static OptionItem OptionTaskCompletedBatteryCharge;
    private static OptionItem OptionCanSeeComms;
    enum OptionName
    {
        DoctorTaskCompletedBatteryCharge
    }
    private static float TaskCompletedBatteryCharge;
    private static bool CanseeComms;
    private static void SetupOptionItem()
    {
        OptionTaskCompletedBatteryCharge = FloatOptionItem.Create(RoleInfo, 10, OptionName.DoctorTaskCompletedBatteryCharge, new(0f, 10f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanSeeComms = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanUseActiveComms, false, false);
    }
    public override bool NotifyRolesCheckOtherName => true;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ScientistCooldown = 0.1f;
        AURoleOptions.ScientistBatteryCharge = TaskCompletedBatteryCharge;
    }
    public bool? CheckSeeDeathReason(PlayerControl seen)//IDeathReasonSeeable
    {
        return !Utils.IsActive(SystemTypes.Comms) || CanseeComms;
    }
}