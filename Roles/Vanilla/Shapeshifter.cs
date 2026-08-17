using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Vanilla;

public sealed class Shapeshifter : RoleBase, IImpostor, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Shapeshifter),
            player => new Shapeshifter(player),
            RoleTypes.Shapeshifter,
            SetUpCustomOption
            , from: From.AmongUs
        );
    //変身持続時間
    //変身クールダウン
    //変身の証拠を残す
    public Shapeshifter(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem ShapeshifterCooldown;
    public static OptionItem ShapeshifterDuration; public static void SetUpCustomOption()
    {
        ShapeshifterCooldown = FloatOptionItem.Create(RoleInfo, 33, "ShapeshifterCooldown", new(0f, 180f, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshifterDuration = FloatOptionItem.Create(RoleInfo, 34, "ShapeshifterDuration", new(0f, 180f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshifterCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshifterDuration.GetFloat();
    }
}
