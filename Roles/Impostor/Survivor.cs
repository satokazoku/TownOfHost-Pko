using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Survivor : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Survivor),
                player => new Survivor(player),
                CustomRoles.Survivor,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Impostor,
                9900,
                SetUpOptionItem,
                "sur",
                OptionSort: (7, 0),
                from: From.SuperNewRoles
            );
        public Survivor(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            KillCooldown = OptionKillCooldown.GetFloat();
        }
        private static OptionItem OptionKillCooldown;
 
        private static float KillCooldown;

        public bool CanBeLastImpostor { get; } = false;

        private static void SetUpOptionItem()
        {
            OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 20f, false)
                .SetValueFormat(OptionFormat.Seconds);
        }
        public float CalculateKillCooldown() => KillCooldown;
        public override void CheckWinner(GameOverReason reason)
        {
            bool IsJacw = Player.Is(CustomRoles.JackalWolf);
            if (!Player.IsAlive())
            {
                if (Player.IsWinner(IsJacw ? CustomWinner.Jackal : CustomWinner.Impostor))
                {
                    CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
                    CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
                }
            }
        }
    }
}