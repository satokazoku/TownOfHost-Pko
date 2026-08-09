using AmongUs.GameOptions;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceNekomata : RoleBase, INekomata
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceNekomata),
            player => new NiceNekomata(player),
            CustomRoles.NiceNekomata,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            38000,
            SetupOptionItems,
            "nn",
            "#FFA55A",
            (1, 7),
            from: From.SuperNewRoles
        );

    public NiceNekomata(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
    {
        impostorsGetRevenged = optionImpostorsGetRevenged.GetBool();
        madmatesGetRevenged = optionMadmatesGetRevenged.GetBool();
        neutralsGetRevenged = optionNeutralsGetRevenged.GetBool();
    }

    #region Custom Options

    private static BooleanOptionItem optionImpostorsGetRevenged;
    private static BooleanOptionItem optionMadmatesGetRevenged;
    private static BooleanOptionItem optionNeutralsGetRevenged;

    private enum OptionName
    {
        NiceNekomataImpostorsGetRevenged,
        NiceNekomataMadmatesGetRevenged,
        NiceNekomataNeutralsGetRevenged,
    }

    private static void SetupOptionItems()
    {
        optionImpostorsGetRevenged =
            BooleanOptionItem.Create(
                RoleInfo,
                10,
                OptionName.NiceNekomataImpostorsGetRevenged,
                false,
                false
            );

        optionMadmatesGetRevenged =
            BooleanOptionItem.Create(
                RoleInfo,
                20,
                OptionName.NiceNekomataMadmatesGetRevenged,
                false,
                false
            );

        optionNeutralsGetRevenged =
            BooleanOptionItem.Create(
                RoleInfo,
                30,
                OptionName.NiceNekomataNeutralsGetRevenged,
                false,
                false
            );
    }

    #endregion

    private static bool impostorsGetRevenged;
    private static bool madmatesGetRevenged;
    private static bool neutralsGetRevenged;

    public bool DoRevenge(CustomDeathReason deathReason)
    {
        return deathReason == CustomDeathReason.Vote;
    }

    public bool IsCandidate(PlayerControl player)
    {
        return player.GetCustomRole().GetCustomRoleTypes() switch
        {
            CustomRoleTypes.Impostor => impostorsGetRevenged,
            CustomRoleTypes.Madmate => madmatesGetRevenged,
            CustomRoleTypes.Neutral => neutralsGetRevenged,
            _ => true,
        };
    }
}