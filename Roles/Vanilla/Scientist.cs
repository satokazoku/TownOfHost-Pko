using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Vanilla;

public sealed class Scientist : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Scientist),
            player => new Scientist(player),
            RoleTypes.Scientist,
            SetUpCustomOption,
            "#4abbe8"
            , from: From.AmongUs
        );
    //バイタル画面クールダウン
    //充電持続時間
    public Scientist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem ScientistCooldown;
    public static OptionItem ScientistBatteryCharge;
    public static void SetUpCustomOption()
    {
        ScientistCooldown = FloatOptionItem.Create(RoleInfo, 253, "ScientistCooldown", new(0f, 180f, 0.5f), 15f, false)
        .SetValueFormat(OptionFormat.Seconds);
        ScientistBatteryCharge = FloatOptionItem.Create(RoleInfo, 254, "ScientistBatteryCharge", new(0f, 180f, 0.5f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ScientistCooldown = ScientistCooldown.GetFloat();
        AURoleOptions.ScientistBatteryCharge = ScientistBatteryCharge.GetFloat();
    }
}
