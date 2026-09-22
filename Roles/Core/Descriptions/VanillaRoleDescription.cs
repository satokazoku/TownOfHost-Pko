using AmongUs.GameOptions;
using TownOfHost.Roles.Vanilla;

namespace TownOfHost.Roles.Core.Descriptions;

/// <summary>
/// バニラ役職の説明文
/// </summary>
public class VanillaRoleDescription : RoleDescription
{
    public VanillaRoleDescription(SimpleRoleInfo roleInfo, RoleTypes vanillaRoleType) : base(roleInfo)
    {
        this.vanillaRoleType = vanillaRoleType;
    }
    private readonly RoleTypes vanillaRoleType;

    public override string Blurb => DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).Blurb;
    public override string Description
    {
        get
        {
            if (vanillaRoleType is RoleTypes.Viper)
            {
                var desc = Translator.GetString("ViperDesc");
                if (Viper.OptShowMark.GetBool())
                    desc += "\n" + Translator.GetString("ViperDescMark");
                return desc;
            }
            if (vanillaRoleType is RoleTypes.Impostor)
                return DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).Blurb;
            return DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).BlurbMed;
        }
    }
}
/*using AmongUs.GameOptions;

namespace TownOfHost.Roles.Core.Descriptions;

/// <summary>
/// バニラ役職の説明文
/// </summary>
public class VanillaRoleDescription : RoleDescription
{
    public VanillaRoleDescription(SimpleRoleInfo roleInfo, RoleTypes vanillaRoleType) : base(roleInfo)
    {
        this.vanillaRoleType = vanillaRoleType;
    }
    private readonly RoleTypes vanillaRoleType;

    public override string Blurb => DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).Blurb;
    public override string Description => //vanillaRoleType is RoleTypes.Detective or RoleTypes.Viper or RoleTypes.Judge ?
        vanillaRoleType is RoleTypes.Impostor ? DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).Blurb :
    DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).BlurbMed //:
    /*DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).BlurbLong*/;
//}