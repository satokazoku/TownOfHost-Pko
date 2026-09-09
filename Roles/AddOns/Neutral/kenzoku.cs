using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Options;
namespace TownOfHost.Roles.AddOns.Neutral
{
    public static class Kenzoku
    {
        private static readonly int Id = 71300;
        public static byte currentId = byte.MaxValue;

        public static bool CheckWin(PlayerControl pc, GameOverReason reason, CustomWinner winner)
        {
            if (!pc.Is(CustomRoles.Kenzoku))
            {
                if (winner is not CustomWinner.Dracula)
                {
                    CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
                    CustomWinnerHolder.WinnerIds.Remove(pc.PlayerId);
                    CustomWinnerHolder.AdditionalWinnerRoles.Remove(CustomRoles.Kenzoku);
                    return false;
                }
            }
            if (winner is not CustomWinner.Dracula)
            {
                CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
                CustomWinnerHolder.WinnerIds.Remove(pc.PlayerId);
                CustomWinnerHolder.AdditionalWinnerRoles.Remove(CustomRoles.Kenzoku);
                return false;
            }
            else if (winner is CustomWinner.Dracula)
            {
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Kenzoku);
                CustomWinnerHolder.CantWinPlayerIds.Remove(pc.PlayerId);
                return true;
            }
            CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
            CustomWinnerHolder.WinnerIds.Remove(pc.PlayerId);
            CustomWinnerHolder.AdditionalWinnerRoles.Remove(CustomRoles.Kenzoku);
            return false;
        }
    }
}