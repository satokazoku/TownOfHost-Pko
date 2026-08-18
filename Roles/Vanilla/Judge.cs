using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Vanilla;

public sealed class Judge : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Judge),
            player => new Judge(player),
            RoleTypes.Judge,
            SetUpCustomOption,
            "#a1472c"
            , from: From.AmongUs
        );
    public Judge(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        taskrequirement = OptionTaskRequirement.GetFloat();
    }
    static float taskrequirement;
    private static OptionItem OptionTaskRequirement;
    public static void SetUpCustomOption()
    {
        OptionTaskRequirement = FloatOptionItem.Create(RoleInfo, 25110, StringNames.JudgeTaskRequirement, new(0, 100, 2), 2, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.JudgeTaskRequirementPercentage = taskrequirement;
    }
}
