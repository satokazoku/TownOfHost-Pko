/*
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Vanilla;

public sealed class Phantom : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Phantom),
            player => new Phantom(player),
            RoleTypes.Phantom,
            SetUpCustomOption
            , from: From.AmongUs
        );
    public Phantom(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem PhantomCooldown;
    public static OptionItem PhantomDuration;
    public static void SetUpCustomOption()
    {
        PhantomCooldown = FloatOptionItem.Create(RoleInfo, 43, "PhantomCooldown", new(0f, 180f, 2.5f), 15f, false)
        .SetValueFormat(OptionFormat.Seconds);
        PhantomDuration = FloatOptionItem.Create(RoleInfo, 44, "PhantomDuration", new(0f, 180f, 2.5f), 5f, false)
                .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = PhantomCooldown.GetFloat();
        AURoleOptions.PhantomDuration = PhantomDuration.GetFloat();
    }
}
*/