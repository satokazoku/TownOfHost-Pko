using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Powerful
    {
        private static readonly int Id = 72400;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Powerful);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "∠");
        public static List<byte> playerIdList = new();
        public static OptionItem OptionKillPower;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Powerful, fromtext: UtilsOption.GetFrom(From.TownOfHost_K));
            AddOnsAssignDataOnlyKiller.Create(Id + 10, CustomRoles.Powerful, true, true, true, true);
            ObjectOptionitem.Create(Id + 51, "AddonOption", true, "", TabGroup.Addons).SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Powerful);
            OptionKillPower = IntegerOptionItem.Create(Id + 50, "PowerfulKillPower", new(2, 10, 1), 2, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Powerful);
        }

        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
    }
}