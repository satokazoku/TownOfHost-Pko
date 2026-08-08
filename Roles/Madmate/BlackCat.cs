using AmongUs.GameOptions;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

public sealed class BlackCat : RoleBase, INekomata
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BlackCat),
            player => new BlackCat(player),
            CustomRoles.BlackCat,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            22500,
            SetupOptionItems,
            "bc",
            OptionSort: (2, 3),
            from: From.SuperNewRoles
        );

    public BlackCat(PlayerControl player)
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

    /// <summary>インポスターを道連れ候補に含む</summary>
    private static BooleanOptionItem optionImpostorsGetRevenged;

    /// <summary>マッドメイトを道連れ候補に含む</summary>
    private static BooleanOptionItem optionMadmatesGetRevenged;

    /// <summary>ニュートラルを道連れ候補に含む</summary>
    private static BooleanOptionItem optionNeutralsGetRevenged;

    private static void SetupOptionItems()
    {
        optionImpostorsGetRevenged =
            BooleanOptionItem.Create(RoleInfo, 10,
                OptionName.BlackCatImpostorsGetRevenged,
                false, false);

        optionMadmatesGetRevenged =
            BooleanOptionItem.Create(RoleInfo, 20,
                OptionName.BlackCatMadmatesGetRevenged,
                false, false);

        optionNeutralsGetRevenged =
            BooleanOptionItem.Create(RoleInfo, 30,
                OptionName.BlackCatNeutralsGetRevenged,
                false, false);
    }

    private enum OptionName
    {
        BlackCatImpostorsGetRevenged,
        BlackCatMadmatesGetRevenged,
        BlackCatNeutralsGetRevenged,
<<<<<<< HEAD
=======
        BlackCatRevengeOnExile
>>>>>>> b638eaf3b010c59dd1af8695e2fc9375e6b4f5b3
    }

    #endregion

    private static bool impostorsGetRevenged;
    private static bool madmatesGetRevenged;
    private static bool neutralsGetRevenged;

    /// <summary>
    /// 追放された時のみ道連れを発動
    /// </summary>
    public bool DoRevenge(CustomDeathReason deathReason)
        => deathReason == CustomDeathReason.Vote;

    /// <summary>
    /// 道連れ候補判定
    /// </summary>
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