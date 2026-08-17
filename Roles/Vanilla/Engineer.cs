using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Vanilla;

public sealed class Engineer : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Engineer),
            player => new Engineer(player),
            RoleTypes.Engineer,
            SetUpCustomOption,
            "#35375e"
            , from: From.AmongUs
        );
    public Engineer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem EngineerCooldown;
    public static OptionItem EngineerInVentMaxTime;
    public static void SetUpCustomOption()
    {
        EngineerCooldown = FloatOptionItem.Create(RoleInfo, 203, StringNames.EngineerCooldown, new(0f, 180f, 0.5f), 15f, false)
        .SetValueFormat(OptionFormat.Seconds);
        EngineerInVentMaxTime = FloatOptionItem.Create(RoleInfo, 204, StringNames.EngineerInVentCooldown, new(0f, 180f, 0.5f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = EngineerCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = EngineerInVentMaxTime.GetFloat();
    }
}
