/*using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;
using TownOfHost.Roles.AddOns.Neutral;
using System.Linq;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class kenzoku
    {
        private static readonly int Id = 74200;
        public static List<byte> playerIdList = new();
        public static OptionItem AssingDay;
        public static OptionItem SurvivetoWin;
        public static OptionItem OptCanFixLightsOut;
        public static OptionItem OptCanFixComms;
        public static Dictionary<CustomWinner, OptionItem> OptionRole = new();

        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            if (!playerIdList.Contains(playerId))
            {
                playerIdList.Add(playerId);
            }
        }

        public static bool CheckWin(PlayerControl pc, bool IsDraculawin)
        {
            if (pc.IsLovers()) return false;

            if (playerIdList.Contains(pc.PlayerId))
            {
                //ドラキュラ以外の勝利は除外
                if (!IsDraculawin)
                {
                    CustomWinnerHolder.WinnerIds.Remove(pc.PlayerId);
                    CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
                    return false;
                }
                else
                {
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.kenzoku);
                    return true;
                }
            }
            return false;
        }
    }
}*/