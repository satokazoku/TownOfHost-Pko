using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Absorb
    {
        private static readonly int Id = 24900;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Absorb);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Г");
        public static List<byte> playerIdList = new();
        private static OptionItem OptionAddGuard;
        private static OptionItem OptionDisableDay;
        private static OptionItem OptionDisablePlayerCount;
        public static int HaveGuard, DisableDay, DisablePlayerCount;
        public static Dictionary<byte, int> AbsorbGuard;
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Absorb);
            AddOnsAssignData.Create(Id + 10, CustomRoles.Absorb, true, true, true, true);
            ObjectOptionitem.Create(Id + 53, "AddonOption", true, "", TabGroup.Addons).SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Absorb);
            OptionAddGuard = IntegerOptionItem.Create(Id + 50, "AddGuardCount", new(1, 10, 1), 1, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Absorb);
            OptionDisableDay = IntegerOptionItem.Create(Id + 51, "AbsorbDisableDay", new(0, 30, 1), 3, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Absorb).SetZeroNotation(OptionZeroNotation.Off);
            OptionDisablePlayerCount = IntegerOptionItem.Create(Id + 52, "AbsorbDisablePlayerCount", new(0, 15, 1), 6, TabGroup.Addons, false).SetSubRoleOptionItem(CustomRoles.Absorb).SetZeroNotation(OptionZeroNotation.Off);
        }
        public static void Init()
        {
            playerIdList = new();
            AbsorbGuard = new();
            HaveGuard = OptionAddGuard.GetInt();
            DisableDay = OptionDisableDay.GetInt();
            DisablePlayerCount = OptionDisablePlayerCount.GetInt();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            AbsorbGuard.TryAdd(playerId, HaveGuard);
        }

        public static bool IsAchive()
        {
            // offではなく、無効化される人数より生存人数がおおい
            if (0 < DisablePlayerCount && DisablePlayerCount < PlayerCatch.AllAlivePlayersCount) return true;
            // offではなく、無効化される日数に達してない
            if (0 < DisableDay && UtilsGameLog.day < DisableDay) return true;

            return false;
        }
    }
}