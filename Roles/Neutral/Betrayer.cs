using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static TownOfHost.Roles.Crewmate.AllArounder;

namespace TownOfHost.Roles.Neutral;

public sealed class Betrayer : RoleBase, ILNKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Betrayer),
            player => new Betrayer(player),
            CustomRoles.Betrayer,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            57700,
            SetupOptionItem,
            "Bet",
            "#8b2551",
            (2, 0),
            true,
            from: From.TownOfHost_K,
            countType: CountTypes.Betrayer
        );
    public Betrayer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Mbets.Clear();
        KillCooldown = MadBetrayer.OptionKillCoolDown.GetFloat();
        CanVent = MadBetrayer.OptionCanVent.GetBool();
    }
    static bool CanVent;
    static float KillCooldown;
    private static void SetupOptionItem()
    {
        HideRoleOptions(CustomRoles.Betrayer);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(MadBetrayer.HasImpostorVision);
    }
    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null && Options.CustomRoleSpawnChances.TryGetValue(role, out var sp))
            sp.SetHidden(true);
        if (Options.CustomRoleCounts != null && Options.CustomRoleCounts.TryGetValue(role, out var cp))
            cp.SetHidden(true);
    }
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Betrayer;
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => MadBetrayer.OptionCanUseSabotage.GetBool();
    public bool CanUseImpostorVentButton() => CanVent;
    bool IsMbet() => Mbets.Contains(Player.PlayerId);
    public static List<byte> Mbets = new();
    public override void CheckWinner(GameOverReason reason)
    {
        if (IsMbet() && Player.IsWinner(CustomWinner.Betrayer) && !CustomWinnerHolder.winners.Contains(CustomWinner.Impostor))
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        if (!IsMbet() && Player.IsWinner(CustomWinner.Betrayer) && !CustomWinnerHolder.winners.Contains(CustomWinner.Jackal))
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);

        Mbets.Clear();
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (((seen.GetRoleClass() is MadBetrayer md && md.CanBetray) || (seen.GetRoleClass() is DollBetrayer db && db.CanBetray)) && MadBetrayer.OptionCanSeeOtherMDBet.GetBool())
        {
            var role = seen.GetCustomRole();

            enabled = true;
            roleText = GetString($"{role}");
            roleColor = UtilsRoleText.GetRoleColor(role);
        }
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var sp1 = new Achievement(RoleInfo, 0, 1, 0, 2);
        achievements.Add(0, sp1);
    }
}