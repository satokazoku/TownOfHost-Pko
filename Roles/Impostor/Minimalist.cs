using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Minimalist : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Minimalist),
            player => new Minimalist(player),
            CustomRoles.Minimalist,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            126600,
            SetupOptionItem,
            "mml",
            OptionSort: (2, 11),
            from: From.SuperNewRoles
        );

    public Minimalist(PlayerControl player) : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
    }

    static OptionItem OptionKillCooldown;
    static float KillCooldown;

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 60f, 0.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;
}