using AmongUs.GameOptions;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class ToiletFan : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(ToiletFan),
            player => new ToiletFan(player),
            CustomRoles.ToiletFan,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            12500,
            SetupOptionItem,
            "to",
            "#735134",
            (9, 3),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            assignInfo: new RoleAssignInfo(CustomRoles.ToiletFan, CustomRoleTypes.Crewmate)
            {
                IsInitiallyAssignableCallBack = () => Main.NormalOptions.MapId is 4
            },
            from: From.SuperNewRoles
        );
    public ToiletFan(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Cooldown = OptionCooldown.GetFloat();
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Cooldown;
        AURoleOptions.EngineerInVentMaxTime = 1;
    }
    public override void Add()
    {
        CoolDownTimer = OptionCooldown.GetFloat();
        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
    static OptionItem OptionCooldown;
    private static float Cooldown;
    public override bool CanClickUseVentButton => false;
    float CoolDownTimer;
    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(1f, 30f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    void OnPet()
    {
        if (CoolDownTimer > 0.1f) return;
        Open();
        CoolDownTimer = Cooldown;
        Player.RpcResetAbilityCooldown();
    }
    public void Open()
    {
        if (Main.NormalOptions.MapId is not 4) return;
        Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 79);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 80);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 81);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 82);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        CoolDownTimer -= Time.fixedDeltaTime;
    }
    public override string GetAbilityButtonText() => GetString("ToiletFanAbility");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "ToiletFan_Ability";
        return true;
    }
    public override void OnSpawn(bool initialState)
    {
        CoolDownTimer = Cooldown;
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        achievements.Add(0, n1);
    }
}