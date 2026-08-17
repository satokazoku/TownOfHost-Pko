using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Neutral;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Securer
    {
        private static readonly int Id = 73900;
        private static readonly Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Securer);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Su");
        public static List<byte> playerIdList = new();
        private static OptionItem AssignToJackalTeam;
        private static OptionItem AssignToJackal;
        private static OptionItem AssignToJackalMafia;
        private static OptionItem AssignToJackalAlien;
        private static OptionItem AssignToJackalWolf;
        private static OptionItem AssignToJackalHadouHo;

        public static void SetupCustomOption()
        {
            var assignOption = SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Securer, fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Securer, false, false, true, false);
            AssignToJackalTeam = BooleanOptionItem.Create(Id + 20, "SecurerAssignToJackalTeam", false, TabGroup.Addons, false)
                .SetParent(assignOption).SetParentRole(CustomRoles.Securer);
            AssignToJackal = BooleanOptionItem.Create(Id + 21, "SecurerAssignToJackal", false, TabGroup.Addons, false)
                .SetParent(AssignToJackalTeam).SetParentRole(CustomRoles.Securer);
            AssignToJackalMafia = BooleanOptionItem.Create(Id + 22, "SecurerAssignToJackalMafia", false, TabGroup.Addons, false)
                .SetParent(AssignToJackalTeam).SetParentRole(CustomRoles.Securer);
            AssignToJackalAlien = BooleanOptionItem.Create(Id + 25, "SecurerAssignToJackalAlien", false, TabGroup.Addons, false)
                .SetParent(AssignToJackalTeam).SetParentRole(CustomRoles.Securer);
            AssignToJackalWolf = BooleanOptionItem.Create(Id + 23, "SecurerAssignToJackalWolf", false, TabGroup.Addons, false)
                .SetParent(AssignToJackalTeam).SetParentRole(CustomRoles.Securer);
            AssignToJackalHadouHo = BooleanOptionItem.Create(Id + 24, "SecurerAssignToJackalHadouHo", false, TabGroup.Addons, false)
                .SetParent(AssignToJackalTeam).SetParentRole(CustomRoles.Securer);
        }

        public static void Init()
        {
            playerIdList = new();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool CanBeAssignedToRole(CustomRoles role)
            => role.IsImpostor() || role is CustomRoles.Jackal
                or CustomRoles.JackalMafia
                or CustomRoles.JackalAlien
                or CustomRoles.JackalHadouHo
                or CustomRoles.JackalWolf
                or CustomRoles.JackalSeer;

        public static bool CanBeAssigned(PlayerControl player)
        {
            if (player == null) return false;
            if (player.Is(CustomRoleTypes.Impostor)) return true;
            var role = player.GetCustomRole();
            if (!CanBeAssignedToRole(role)) return false;

            return CanUseSabotageByOption(role)
                || (player.GetRoleClass() as IKiller)?.CanUseSabotageButton() == true;
        }

        public static bool ShouldAssignToJackalRole(CustomRoles role)
        {
            if (AssignToJackalTeam?.GetBool() != true) return false;

            return role switch
            {
                CustomRoles.Jackal => AssignToJackal?.GetBool() == true,
                CustomRoles.JackalMafia => AssignToJackalMafia?.GetBool() == true,
                CustomRoles.JackalAlien => AssignToJackalAlien?.GetBool() == true,
                CustomRoles.JackalWolf => AssignToJackalWolf?.GetBool() == true,
                CustomRoles.JackalHadouHo => AssignToJackalHadouHo?.GetBool() == true,
                _ => false,
            };
        }

        private static bool CanUseSabotageByOption(CustomRoles role)
        {
            return role switch
            {
                CustomRoles.Jackal => Jackal.OptionCanUseSabotage?.GetBool() == true,
                CustomRoles.JackalMafia => JackalMafia.OptionCanUseSabotage?.GetBool() == true,
                CustomRoles.JackalAlien => JackalAlien.OptionCanUseSabotage?.GetBool() == true,
                CustomRoles.JackalHadouHo => JackalHadouHo.GetCanUseSabotageOption(),
                CustomRoles.JackalWolf => JackalWolf.GetCanUseSabotageOption(),
                _ => false,
            };
        }

        public static bool BlocksSabotage(PlayerControl player)
        {
            if (player == null) return false;
            if (player.Is(CustomRoles.Securer)) return true;

            return RoleAddAddons.GetRoleAddon(player.GetCustomRole(), out var data, player, subrole: CustomRoles.Securer)
                && data.GiveSecurer.GetBool()
                && CanBeAssigned(player);
        }
    }
}
