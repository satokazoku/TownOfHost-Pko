using static TownOfHost.Utils;

namespace TownOfHost
{
    public static class UtilsName
    {
        public static string GetPlayerColor(this PlayerControl player, bool bold = false)
        {
            if (player == null) return "";
            var name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var N) ? N : player.Data.PlayerName;
            return ColorString(Main.PlayerColors[player.PlayerId], $"{name}");
        }

        public static string GetPlayerColor(this byte player, bool bold = false)
        {
            var pc = PlayerCatch.GetPlayerById(player);
            if (pc == null) return "";
            var name = Main.AllPlayerNames.TryGetValue(player, out var N) ? N : pc.Data.PlayerName;
            return ColorString(Main.PlayerColors[player], $"{name}");
        }

        public static string GetPlayerColor(this NetworkedPlayerInfo player, bool bold = false)
        {
            if (player == null) return "";
            var name = player.PlayerName;
            return ColorString(Main.PlayerColors[player.PlayerId], $"{name}");
        }

        public static bool SetNameCheck(this PlayerControl player, string name, PlayerControl seer = null, bool force = false)
        {
            if (seer == null) seer = player;

            if (Main.LastNotifyNames is null)
                Main.LastNotifyNames = new();

            if (!Main.LastNotifyNames.ContainsKey((player.PlayerId, seer.PlayerId)))
                Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = "nulldao";

            if (!force && Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] == name)
            {
                return false;
            }
            {
                Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
                if (!GameStates.IsLobby) HudManagerPatch.LastSetNameDesyncCount++;
            }

            return true;
        }

        public static string GetNameWithRole(this PlayerControl player)
        {
            return $"{player?.Data?.GetLogPlayerName()}" + (GameStates.IsInGame ? $"({player?.GetAllRoleName()})" : "");
        }

        public static string GetRealName(this PlayerControl player, bool isMeeting = false)
        {
            string baseName = "";

            if (Main.ShapeshiftTarget.TryGetValue(player.PlayerId, out var targetid) && targetid != player.PlayerId && !isMeeting)
            {
                if (Camouflage.PlayerSkins.TryGetValue(targetid, out var outfit))
                {
                    baseName = outfit.PlayerName;
                }
            }
            else if (GameStates.InGame && Camouflage.PlayerSkins.TryGetValue(player?.PlayerId ?? byte.MaxValue, out var skin))
            {
                baseName = skin.PlayerName;
            }
            else
            {
                baseName = isMeeting ? player?.Data?.PlayerName : player?.name;
            }

            // ★ 会議中(isMeeting)は絶対にジャンボのサイズを適用しない！
            if (GameStates.IsInGame && !isMeeting && !GameStates.IsMeeting && player != null)
            {
                string jumboPrefix = TownOfHost.Roles.AddOns.Common.Jumbo.GetNameSizePrefix(player.PlayerId);
                if (!string.IsNullOrEmpty(jumboPrefix))
                {
                    baseName = jumboPrefix + baseName + "</size>";
                }
            }

            return baseName;
        }
    }
}