using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class SilverBuzzer
    {
        private const int Id = 74100;
        private const string ColorCode = "#C0C0C0";
        private static readonly Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.SilverBuzzer);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Sb");

        public static List<byte> playerIdList = new();
        private static readonly HashSet<byte> initialHolderIds = new();
        private static readonly HashSet<CustomRoles> initialHolderRoles = new();
        private static readonly HashSet<byte> pendingGrantIds = new();

        private static bool initialAssignmentOpen;
        private static bool firstMeetingSent;
        private static OptionItem OptionNotifyAfterFirstGrant;
        private static OptionItem OptionShowInitialCount;

        public static void SetupCustomOption()
        {
            var spawn = SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.SilverBuzzer, fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));
            AddOnsAssignData.Create(Id + 10, CustomRoles.SilverBuzzer, true, true, true, true);

            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option")
                .SetSubRoleOptionItem(CustomRoles.SilverBuzzer);

            OptionNotifyAfterFirstGrant = BooleanOptionItem.Create(Id + 21, "SilverBuzzerNotifyAfterFirstGrant", true, TabGroup.Addons, false)
                .SetParent(spawn).SetParentRole(CustomRoles.SilverBuzzer);
            OptionShowInitialCount = BooleanOptionItem.Create(Id + 22, "SilverBuzzerShowInitialCount", false, TabGroup.Addons, false)
                .SetParent(spawn).SetParentRole(CustomRoles.SilverBuzzer);
        }

        public static void Init()
        {
            playerIdList = new();
            initialHolderIds.Clear();
            initialHolderRoles.Clear();
            pendingGrantIds.Clear();
            initialAssignmentOpen = true;
            firstMeetingSent = false;
        }

        public static void CloseInitialAssignment()
        {
            initialAssignmentOpen = false;
        }

        public static void Add(byte playerId)
        {
            if (!playerIdList.Contains(playerId))
            {
                playerIdList.Add(playerId);
            }
        }

        public static void OnGranted(byte playerId)
        {
            var state = PlayerState.GetByPlayerId(playerId);
            if (state == null) return;

            if (initialAssignmentOpen)
            {
                initialHolderIds.Add(playerId);
                if (state.MainRole != CustomRoles.NotAssigned)
                {
                    initialHolderRoles.Add(state.MainRole);
                }
                return;
            }

            pendingGrantIds.Add(playerId);
        }

        public static string SendMessage()
        {
            if (!firstMeetingSent && MeetingStates.FirstMeeting)
            {
                firstMeetingSent = true;
                if (!HasInitialHolder()) return "";

                return BuildMessage(includeInitialDetails: true);
            }

            if (!firstMeetingSent)
            {
                return "";
            }

            if (!OptionNotifyAfterFirstGrant.GetBool() || pendingGrantIds.Count == 0)
            {
                pendingGrantIds.Clear();
                return "";
            }

            pendingGrantIds.Clear();
            return BuildMessage(includeInitialDetails: false);
        }

        private static bool HasInitialHolder()
            => initialHolderIds.Count > 0 || GetRoleOptionHolderRoles().Any();

        private static string BuildMessage(bool includeInitialDetails)
        {
            var lines = new List<string>
            {
                $"<size=100%>{Translator.GetString("SilverBuzzerAmbush")}</size>",
                FormatSilverLine(Translator.GetString("SilverBuzzerAlarm"))
            };

            if (includeInitialDetails)
            {
                if (OptionShowInitialCount.GetBool())
                {
                    lines.Add(FormatSilverLine(string.Format(Translator.GetString("SilverBuzzerInitialCount"), CountInitialHolders())));
                }

                var roles = initialHolderRoles
                    .Concat(GetRoleOptionHolderRoles())
                    .Where(role => role != CustomRoles.NotAssigned)
                    .Distinct()
                    .OrderBy(role => role.ToString())
                    .Select(UtilsRoleText.GetRoleColorAndtext)
                    .ToArray();

                if (roles.Length > 0)
                {
                    var roleText = $"<size=65%> {string.Join(",", roles)}</size>";
                    lines.Add(FormatSilverLine(string.Format(Translator.GetString("SilverBuzzerInitialRoles"), roleText)));
                }
            }

            return string.Join("\n", lines);
        }

        private static string FormatSilverLine(string text)
        {
            return $"<size=80%><{ColorCode}>{text}</color></size>";
        }

        private static int CountInitialHolders()
        {
            return initialHolderIds
                .Concat(GetRoleOptionHolderIds())
                .Distinct()
                .Count();
        }

        private static IEnumerable<byte> GetRoleOptionHolderIds()
        {
            foreach (var (playerId, state) in PlayerState.AllPlayerStates)
            {
                var player = PlayerCatch.GetPlayerById(playerId);
                if (player == null) continue;
                if (RoleAddAddons.GetRoleAddon(state.MainRole, out var data, player, subrole: CustomRoles.SilverBuzzer)
                    && data.GiveSilverBuzzer.GetBool())
                {
                    yield return playerId;
                }
            }
        }

        private static IEnumerable<CustomRoles> GetRoleOptionHolderRoles()
        {
            foreach (var playerId in GetRoleOptionHolderIds())
            {
                var state = PlayerState.GetByPlayerId(playerId);
                if (state != null)
                {
                    yield return state.MainRole;
                }
            }
        }
    }
}
