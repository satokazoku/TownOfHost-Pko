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
    public override string Description
    {
        get
        {
            // 純クルー・純インポスターはBlurbMedが未実装(簡素な文章しか返らない)ため
            // InfoLongの翻訳文字列を直接参照する
            if (vanillaRoleType is RoleTypes.Crewmate or RoleTypes.Impostor)
                return Translator.GetString($"{RoleInfo.RoleName}{SingleRoleDescription.DescriptionSuffix}");

            return DestroyableSingleton<RoleManager>.Instance.GetRole(vanillaRoleType).BlurbMed;
        }
    }
}*/
using AmongUs.GameOptions;

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
}