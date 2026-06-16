using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Sealer
    {
        private static readonly int Id = 74000;
        private static readonly Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Sealer);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Se");
        public static List<byte> playerIdList = new();

        private static OptionItem AssignToImpostorTeam;
        private static OptionItem ImpostorAssignCount;
        private static AssignOptionItem ImpostorAssignTarget;
        private static OptionItem AssignToMadmate;
        private static OptionItem MadmateAssignCount;
        private static AssignOptionItem MadmateAssignTarget;
        private static OptionItem AssignToCrewmateTeam;
        private static OptionItem CrewmateAssignCount;
        private static AssignOptionItem CrewmateAssignTarget;
        private static OptionItem AssignToVentMaster;
        private static OptionItem AssignToStaff;
        private static OptionItem AssignToAndroid;
        private static OptionItem AssignToComebacker;
        private static OptionItem AssignToNiceWorkaholic;
        private static OptionItem AssignToEngineer;
        private static OptionItem AssignToNeutralTeam;
        private static OptionItem NeutralAssignCount;
        private static AssignOptionItem NeutralAssignTarget;

        private static readonly CustomRoles[] CrewmateVentRoles =
        {
            CustomRoles.VentMaster,
            CustomRoles.Staff,
            CustomRoles.Android,
            CustomRoles.Comebacker,
            CustomRoles.NiceWorkaholic,
            CustomRoles.Engineer,
        };

        private static readonly CustomRoles[] NeverAssignableRoles =
        {
            CustomRoles.Mole,
            CustomRoles.Mayor,
            CustomRoles.ToiletFan,
            CustomRoles.UltraStar,
            CustomRoles.VillageChief,
            CustomRoles.Shyboy,
            CustomRoles.Suicider,
        };

        public static void SetupCustomOption()
        {
            var assignOption = SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Sealer, fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Sealer, false, false, false, false);
            ObjectOptionitem.Create(Id + 49, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Sealer);

            var deniedVentRoles = GetDeniedRoles(CanBeAssignedToRole);
            var deniedCrewmateRoles = GetDeniedRoles(role => CrewmateVentRoles.Contains(role));

            AssignToImpostorTeam = BooleanOptionItem.Create(Id + 20, "SealerAssignToImpostorTeam", false, TabGroup.Addons, false)
                .SetParent(assignOption).SetParentRole(CustomRoles.Sealer);
            ImpostorAssignCount = IntegerOptionItem.Create(Id + 21, "SealerAssignCount", new(1, 3, 1), 1, TabGroup.Addons, false)
                .SetParent(AssignToImpostorTeam).SetParentRole(CustomRoles.Sealer).SetValueFormat(OptionFormat.Players);
            ImpostorAssignTarget = (AssignOptionItem)AssignOptionItem.Create(Id + 22, "FixedRole", 0, TabGroup.Addons, false, imp: true, notassing: deniedVentRoles)
                .SetParent(AssignToImpostorTeam).SetParentRole(CustomRoles.Sealer);

            AssignToMadmate = BooleanOptionItem.Create(Id + 30, "SealerAssignToMadmate", false, TabGroup.Addons, false)
                .SetParent(assignOption).SetParentRole(CustomRoles.Sealer);
            MadmateAssignCount = IntegerOptionItem.Create(Id + 31, "SealerAssignCount", new(1, 15, 1), 1, TabGroup.Addons, false)
                .SetParent(AssignToMadmate).SetParentRole(CustomRoles.Sealer).SetValueFormat(OptionFormat.Players);
            MadmateAssignTarget = (AssignOptionItem)AssignOptionItem.Create(Id + 32, "FixedRole", 0, TabGroup.Addons, false, mad: true, notassing: deniedVentRoles)
                .SetParent(AssignToMadmate).SetParentRole(CustomRoles.Sealer);

            AssignToCrewmateTeam = BooleanOptionItem.Create(Id + 40, "SealerAssignToCrewmateTeam", false, TabGroup.Addons, false)
                .SetParent(assignOption).SetParentRole(CustomRoles.Sealer);
            CrewmateAssignCount = IntegerOptionItem.Create(Id + 41, "SealerAssignCount", new(1, 15, 1), 1, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer).SetValueFormat(OptionFormat.Players);
            CrewmateAssignTarget = (AssignOptionItem)AssignOptionItem.Create(Id + 42, "FixedRole", 0, TabGroup.Addons, false, crew: true, notassing: deniedCrewmateRoles)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToVentMaster = BooleanOptionItem.Create(Id + 43, "SealerAssignToVentMaster", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToStaff = BooleanOptionItem.Create(Id + 44, "SealerAssignToStaff", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToAndroid = BooleanOptionItem.Create(Id + 45, "SealerAssignToAndroid", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToComebacker = BooleanOptionItem.Create(Id + 46, "SealerAssignToComebacker", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToNiceWorkaholic = BooleanOptionItem.Create(Id + 47, "SealerAssignToNiceWorkaholic", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);
            AssignToEngineer = BooleanOptionItem.Create(Id + 48, "SealerAssignToEngineer", true, TabGroup.Addons, false)
                .SetParent(AssignToCrewmateTeam).SetParentRole(CustomRoles.Sealer);

            AssignToNeutralTeam = BooleanOptionItem.Create(Id + 50, "SealerAssignToNeutralTeam", false, TabGroup.Addons, false)
                .SetParent(assignOption).SetParentRole(CustomRoles.Sealer);
            NeutralAssignCount = IntegerOptionItem.Create(Id + 51, "SealerAssignCount", new(1, 15, 1), 1, TabGroup.Addons, false)
                .SetParent(AssignToNeutralTeam).SetParentRole(CustomRoles.Sealer).SetValueFormat(OptionFormat.Players);
            NeutralAssignTarget = (AssignOptionItem)AssignOptionItem.Create(Id + 52, "FixedRole", 0, TabGroup.Addons, false, neu: true, notassing: deniedVentRoles)
                .SetParent(AssignToNeutralTeam).SetParentRole(CustomRoles.Sealer);
        }

        public static void Init()
        {
            playerIdList = new();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static List<PlayerControl> AssignTargetList()
        {
            var candidates = new List<PlayerControl>();

            if (AssignToImpostorTeam?.GetBool() == true)
            {
                AddRandomTargets(candidates, GetTeamPool(CustomRoleTypes.Impostor, ImpostorAssignTarget), ImpostorAssignCount.GetInt());
            }

            if (AssignToMadmate?.GetBool() == true)
            {
                AddRandomTargets(candidates, GetTeamPool(CustomRoleTypes.Madmate, MadmateAssignTarget), MadmateAssignCount.GetInt());
            }

            if (AssignToCrewmateTeam?.GetBool() == true)
            {
                AddRandomTargets(candidates, GetCrewmatePool(), CrewmateAssignCount.GetInt());
            }

            if (AssignToNeutralTeam?.GetBool() == true)
            {
                AddRandomTargets(candidates, GetTeamPool(CustomRoleTypes.Neutral, NeutralAssignTarget), NeutralAssignCount.GetInt());
            }

            var rnd = IRandom.Instance;
            while (candidates.Count > CustomRoles.Sealer.GetRealCount())
            {
                candidates.RemoveAt(rnd.Next(candidates.Count));
            }

            return candidates;
        }

        public static bool CanBeAssigned(PlayerControl player)
        {
            if (player == null) return false;
            var role = player.GetCustomRole();
            if (!CanBeAssignedToRole(role)) return false;

            var roleClass = player.GetRoleClass();
            if (roleClass?.CanClickUseVentButton == false) return false;

            var baseRole = player.Data?.Role?.Role ?? role.GetRoleTypes();
            if (baseRole != RoleTypes.Engineer && roleClass is IKiller killer && !killer.CanUseImpostorVentButton()) return false;

            return true;
        }

        public static bool CanBeAssignedToRole(CustomRoles role)
        {
            if (NeverAssignableRoles.Contains(role)) return false;
            if (CrewmateVentRoles.Contains(role)) return role.GetRoleTypes() == RoleTypes.Engineer;
            if (role.IsCrewmate()) return false;

            var baseRole = role.GetRoleTypes();
            if (role.IsImpostor()) return IsVentBaseRole(baseRole);
            if (role.IsMadmate()) return baseRole == RoleTypes.Engineer;
            if (role.IsNeutral()) return baseRole == RoleTypes.Engineer || IsVentBaseRole(baseRole);

            return false;
        }

        public static bool BlocksVent(PlayerControl player)
        {
            if (player == null) return false;
            if (player.Is(CustomRoles.Sealer)) return true;

            return RoleAddAddons.GetRoleAddon(player.GetCustomRole(), out var data, player, subrole: CustomRoles.Sealer)
                && data.GiveSealer.GetBool()
                && CanBeAssigned(player);
        }

        private static List<PlayerControl> GetTeamPool(CustomRoleTypes roleType, AssignOptionItem assignTarget)
        {
            var selectedRoles = assignTarget?.RoleValues[AssignOptionItem.Getpresetid()] ?? new();
            return PlayerCatch.AllPlayerControls
                .Where(pc => CanBeAssigned(pc))
                .Where(pc => assignTarget?.GetBool() == true ? selectedRoles.Contains(pc.GetCustomRole()) : pc.Is(roleType))
                .ToList();
        }

        private static List<PlayerControl> GetCrewmatePool()
        {
            var selectedRoles = CrewmateVentRoles.Where(IsCrewmateRoleOptionEnabled).ToList();
            if (CrewmateAssignTarget?.GetBool() == true)
            {
                selectedRoles = selectedRoles
                    .Where(role => CrewmateAssignTarget.RoleValues[AssignOptionItem.Getpresetid()].Contains(role))
                    .ToList();
            }

            return PlayerCatch.AllPlayerControls
                .Where(pc => selectedRoles.Contains(pc.GetCustomRole()))
                .Where(CanBeAssigned)
                .ToList();
        }

        private static void AddRandomTargets(List<PlayerControl> candidates, IEnumerable<PlayerControl> pool, int count)
        {
            var rnd = IRandom.Instance;
            var targets = pool.Where(pc => !candidates.Contains(pc)).ToList();
            for (var i = 0; i < count; i++)
            {
                if (targets.Count == 0) break;
                var target = targets[rnd.Next(targets.Count)];
                candidates.Add(target);
                targets.Remove(target);
            }
        }

        private static bool IsCrewmateRoleOptionEnabled(CustomRoles role)
        {
            return role switch
            {
                CustomRoles.VentMaster => AssignToVentMaster?.GetBool() == true,
                CustomRoles.Staff => AssignToStaff?.GetBool() == true,
                CustomRoles.Android => AssignToAndroid?.GetBool() == true,
                CustomRoles.Comebacker => AssignToComebacker?.GetBool() == true,
                CustomRoles.NiceWorkaholic => AssignToNiceWorkaholic?.GetBool() == true,
                CustomRoles.Engineer => AssignToEngineer?.GetBool() == true,
                _ => false,
            };
        }

        private static bool IsVentBaseRole(RoleTypes role)
        {
            return role is RoleTypes.Impostor
                or RoleTypes.Shapeshifter
                or RoleTypes.Phantom
                or RoleTypes.Viper;
        }

        private static CustomRoles[] GetDeniedRoles(Func<CustomRoles, bool> canAssign)
            => CustomRolesHelper.AllRoles.Where(role => !canAssign(role)).ToArray();
    }
}
