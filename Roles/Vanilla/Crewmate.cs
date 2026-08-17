using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Vanilla;

public sealed class Crewmate : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Crewmate),
            player => new Crewmate(player),
            RoleTypes.Crewmate,
            null,
            "#8cffff",
            assignInfo: new RoleAssignInfo(CustomRoles.Crewmate, CustomRoleTypes.Crewmate)
            {
                IsInitiallyAssignableCallBack =
                    () => false
            }, from: From.AmongUs
        );
    public Crewmate(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
}