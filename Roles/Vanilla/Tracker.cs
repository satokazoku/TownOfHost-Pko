using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Vanilla;

public sealed class Tracker : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Tracker),
            player => new Tracker(player),
            RoleTypes.Tracker,
           SetUpCustomOption,
           "#5f7c96"
            , from: From.AmongUs
        );
    public Tracker(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem TrackerCooldown;
    public static OptionItem TrackerDelay;
    public static OptionItem TrackerDuration;
    public static void SetUpCustomOption()
    {
        TrackerCooldown = FloatOptionItem.Create(RoleInfo, 303, "TrackerCooldown", new(0f, 180f, 0.5f), 15f, false)
                .SetValueFormat(OptionFormat.Seconds);
        TrackerDelay = FloatOptionItem.Create(RoleInfo, 304, "TrackerDelay", new(0f, 180f, 0.5f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds);
        TrackerDuration = FloatOptionItem.Create(RoleInfo, 305, "TrackerDuration", new(0f, 180f, 1f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.TrackerCooldown = TrackerCooldown.GetFloat();
        AURoleOptions.TrackerDelay = TrackerDelay.GetFloat();
        AURoleOptions.TrackerDuration = TrackerDuration.GetFloat();
    }
}