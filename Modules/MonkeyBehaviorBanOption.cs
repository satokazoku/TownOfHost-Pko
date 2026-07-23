//めんどくさかったから。ありがとうちゃっぴー
using System.Collections.Generic;
using TownOfHost.Roles.Core;

namespace TownOfHost;

public static class MonkeyBehaviorBanOption
{
    private const string OptionKey = "DisplayMonkeyBehaviorBan";
    private const string OptionDisplayName = "猿行為を禁止するか表示する";
    private const string BanNotice = "\n この部屋でこの役職での猿行為は禁止です。";

    private static readonly Dictionary<CustomRoles, int> OptionIds = new()
    {
        [CustomRoles.Fortuner] = 1_700_001,
        [CustomRoles.NiceAddoer] = 1_700_002,
        [CustomRoles.NiceEraser] = 1_700_003,
        [CustomRoles.NiceGuesser] = 1_700_004,
        [CustomRoles.Sheriff] = 1_700_005,
        [CustomRoles.SheriffHadouHo] = 1_700_006,
        [CustomRoles.MeetingSheriff] = 1_700_007,
        [CustomRoles.FortuneTeller] = 1_700_008,
        [CustomRoles.PonkotuTeller] = 1_700_009,
        [CustomRoles.AmateurTeller] = 1_700_010,
        [CustomRoles.SuspiciousTeller] = 1_700_011,
        [CustomRoles.ShrineMaiden] = 1_700_012,
        [CustomRoles.Inspector] = 1_700_013,
        [CustomRoles.Medium] = 1_700_014,
        [CustomRoles.Police] = 1_700_015,
        [CustomRoles.Jailer] = 1_700_016,
        [CustomRoles.Dictator] = 1_700_017,
        [CustomRoles.Nimrod] = 1_700_018,
        [CustomRoles.WhiteHacker] = 1_700_019,
        [CustomRoles.Balancer] = 1_700_020,
        [CustomRoles.Observer] = 1_700_021,
        [CustomRoles.Santa] = 1_700_022,
    };

    private static readonly Dictionary<CustomRoles, OptionItem> Options = new();

    public static void Create(SimpleRoleInfo roleInfo)
    {
        if (!OptionIds.TryGetValue(roleInfo.RoleName, out var optionId)) return;
        if (Options.ContainsKey(roleInfo.RoleName)) return;

        Options[roleInfo.RoleName] = BooleanOptionItem.Create(optionId, OptionKey, false, roleInfo.Tab, false)
            .SetParent(roleInfo.RoleOption)
            .SetParentRole(roleInfo.RoleName)
            .SetOptionName(() => OptionDisplayName);
    }

    public static string ApplyNotice(CustomRoles role, string description)
    {
        if (!Options.TryGetValue(role, out var option) || !option.GetBool()) return description;
        if (description.EndsWith(BanNotice)) return description;

        return description + BanNotice;
    }
}
