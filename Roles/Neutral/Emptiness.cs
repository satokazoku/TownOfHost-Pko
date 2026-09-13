using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Emptiness : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Emptiness),
            player => new Emptiness(player),
            CustomRoles.Emptiness,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            552800,
            null,
            "emp",
            "#221d26",
            (99, 99),
            countType: CountTypes.None,
            from: From.TownOfHost_K
        );

    public Emptiness(PlayerControl player)
        : base(RoleInfo, player)
    {
    }
}