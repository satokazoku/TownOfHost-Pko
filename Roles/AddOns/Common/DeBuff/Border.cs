using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Border
    {
        private const int Id = 74200;
        private static readonly Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Border);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Ｂ");

        public static List<byte> playerIdList = new();
        public static Dictionary<byte, int> playerKillCount = new();
        private static OptionItem OptionMissionKillcount;
        public static OptionItem KillCooldown;
        public static void SetupCustomOption()
        {
            var spawn = SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Border);
            AddOnsAssignDataOnlyKiller.Create(Id + 10, CustomRoles.Border, true, true, true, true);

            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option")
                .SetSubRoleOptionItem(CustomRoles.Border);

            KillCooldown = FloatOptionItem.Create(Id + 21, "KillCooldown", new(0f, 180f, 0.5f), 20f, TabGroup.Addons, false).SetValueFormat(OptionFormat.Seconds).SetSubRoleOptionItem(CustomRoles.Border);
            OptionMissionKillcount = IntegerOptionItem.Create(Id + 22, "BorderKillerMissionKillcount", new(1, 14, 1), 3, TabGroup.Addons, false)
                .SetValueFormat(OptionFormat.Players).SetParent(spawn).SetParentRole(CustomRoles.Border);
        }

        public static void Init()
        {
            playerIdList = new();
            playerKillCount = new();
        }

        public static void Add(byte playerId)
        {
            if (!playerIdList.Contains(playerId))
            {
                playerIdList.Add(playerId);
            }
            if (!playerKillCount.ContainsKey(playerId))
            {
                playerKillCount[playerId] = 0;
            }
        }

        public static void CheckWin(byte playerId)
        {
            if (playerKillCount.ContainsKey(playerId))
            {
                var player = PlayerCatch.GetPlayerById(playerId);
                var st = player.GetRoleClass().MyState;
                int killCount = st.GetKillCount(false);
                if (killCount >= OptionMissionKillcount.GetFloat())
                {
                    CustomWinnerHolder.CantWinPlayerIds.Remove(playerId);
                    CustomWinnerHolder.WinnerIds.Add(playerId);
                }
                else
                {
                    CustomWinnerHolder.CantWinPlayerIds.Add(playerId);
                    CustomWinnerHolder.WinnerIds.Remove(playerId);
                }
            }
        }
    }
}
