using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using TownOfHost.Patches;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Sheriff : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Sheriff),
            player => new Sheriff(player),
            CustomRoles.Sheriff,
            () => RequiresTasks ? RoleTypes.Crewmate : RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            35100,
            SetupOptionItem,
            "sh",
            "#f8cd46",
            (2, 0),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.SheriffMod
        );

    public Sheriff(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => RequiresTasks ? HasTask.True : HasTask.False
    )
    {
        Flug3 = 0;
        ShotLimit = ShotLimitOpt.GetInt();
        CurrentKillCooldown = KillCooldown.GetFloat();
        Taskmode = RequiresTasks;
        nowcool = CurrentKillCooldown;
        LastCooltime = 0;
    }

    public static OptionItem KillCooldown;
    private static OptionItem MisfireKillsTarget;
    private static OptionItem CanKillMadmate;
    public static OptionItem ShotLimitOpt;
    public static OptionItem StartInTaskMode;
    private static bool RequiresTasks => StartInTaskMode?.OptionMeGetBool() ?? true;
    public static OptionItem CanKillAllAlive;
    public static OptionItem CanKillNeutrals;
    public static OptionItem CanKillLovers;


    enum OptionName
    {
        SheriffMisfireKillsTarget,
        SheriffShotLimit,
        SheriffStartInTaskMode,
        SheriffCanKillAllAlive,
        SheriffCanKillNeutrals,
        SheriffCanKill,
        SheriffCanKillLovers,
    }

    public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = new();
    public static Dictionary<ISchrodingerCatOwner.TeamType, OptionItem> SchrodingerCatKillTargetOptions = new();

    public int ShotLimit = 0;
    public float CurrentKillCooldown = 30;
    public bool Taskmode;
    float nowcool;
    int LastCooltime;
    int Flug3;

    /// <summary>村長に任命されたプレイヤーID一覧（タスクなし扱い）</summary>
    public static HashSet<byte> AppointedPlayerIds = new();

    public static readonly string[] KillOption =
    {
        "SheriffCanKillAll", "SheriffCanKillSeparately"
    };

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Crew;

    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 990f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 8);
        MisfireKillsTarget = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SheriffMisfireKillsTarget, false, false);
        ShotLimitOpt = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SheriffShotLimit, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        StartInTaskMode = BooleanOptionItem.Create(RoleInfo, 17, OptionName.SheriffStartInTaskMode, true, false);
        OverrideTasksData.Create(RoleInfo, 22, parent: StartInTaskMode);
        CanKillAllAlive = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SheriffCanKillAllAlive, true, false);
        CanKillMadmate = SetUpKillTargetOption(CustomRoles.Madmate, 13);
        CanKillNeutrals = StringOptionItem.Create(RoleInfo, 14, OptionName.SheriffCanKillNeutrals, KillOption, 0, false);
        SetUpNeutralOptions(30);
        CanKillLovers = BooleanOptionItem.Create(RoleInfo, 16, OptionName.SheriffCanKillLovers, true, false);
    }

    public static void SetUpNeutralOptions(int idOffset)
    {
        foreach (var neutral in CustomRolesHelper.AllStandardRoles.Where(x => x.IsNeutral()).ToArray())
        {
            if (Event.CheckRole(neutral) is false) continue;
            if (neutral is CustomRoles.SchrodingerCat) continue;
            SetUpKillTargetOption(neutral, idOffset, true, CanKillNeutrals);
            idOffset++;
        }
        foreach (var catType in EnumHelper.GetAllValues<ISchrodingerCatOwner.TeamType>())
        {
            if ((byte)catType < 50) continue;
            SetUpSchrodingerCatKillTargetOption(catType, idOffset, true, CanKillNeutrals);
            idOffset++;
        }
    }

    public static OptionItem SetUpKillTargetOption(CustomRoles role, int idOffset, bool defaultValue = true, OptionItem parent = null)
    {
        var id = RoleInfo.ConfigId + idOffset;
        if (parent == null) parent = RoleInfo.RoleOption;
        var roleName = UtilsRoleText.GetRoleName(role);
        Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(UtilsRoleText.GetRoleColor(role), roleName) } };
        var roleoptionitem = BooleanOptionItem.Create(id, OptionName.SheriffCanKill + "%role%", defaultValue, RoleInfo.Tab, false).SetParent(parent).SetParentRole(CustomRoles.Sheriff);
        KillTargetOptions[role] = roleoptionitem;
        KillTargetOptions[role].ReplacementDictionary = replacementDic;
        return roleoptionitem;
    }

    public static void SetUpSchrodingerCatKillTargetOption(ISchrodingerCatOwner.TeamType catType, int idOffset, bool defaultValue = true, OptionItem parent = null)
    {
        var id = RoleInfo.ConfigId + idOffset;
        parent ??= RoleInfo.RoleOption;
        var inTeam = GetString("In%team%", new Dictionary<string, string>() { ["%team%"] = GetRoleString(catType.ToString()) });
        var catInTeam = Utils.ColorString(SchrodingerCat.GetCatColor(catType), UtilsRoleText.GetRoleName(CustomRoles.SchrodingerCat) + inTeam);
        Dictionary<string, string> replacementDic = new() { ["%role%"] = catInTeam };
        SchrodingerCatKillTargetOptions[catType] = BooleanOptionItem.Create(id, OptionName.SheriffCanKill + "%role%", defaultValue, RoleInfo.Tab, false).SetParent(parent).SetParentRole(CustomRoles.Sheriff);
        SchrodingerCatKillTargetOptions[catType].ReplacementDictionary = replacementDic;
    }

    public override void Add()
    {
        AppointedPlayerIds.Clear(); // ゲーム開始時にリセット
        ShotLimit = ShotLimitOpt.GetInt();
        CurrentKillCooldown = KillCooldown.GetFloat();
        Taskmode = RequiresTasks;
        Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : 残り{ShotLimit}発", "Sheriff");
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
        if (!RequiresTasks)
        {
            nowcool = 0f;
            LastCooltime = 0;
            ModeSwitching(false);
            SendRPC();
        }
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    private void OnPetUsed()
    {
        if (!RequiresTasks) return;
        if (!CanChangeMode()) return;
        ModeSwitching();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ShotLimit);
        sender.Writer.Write(Taskmode);
        sender.Writer.Write(nowcool);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        ShotLimit = reader.ReadInt32();
        Taskmode = reader.ReadBoolean();
        nowcool = reader.ReadSingle();
    }

    public float CalculateKillCooldown() => CanUseKillButton() ? CurrentKillCooldown : 0f;

    public bool CanUseKillButton()
        => CanUseSheriffMode()
        && !Taskmode;

    bool CanChangeMode()
        => RequiresTasks
        && Player.IsAlive()
        && ShotLimit > 0;

    bool CanUseSheriffMode()
        => Player.IsAlive()
        && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
        && ShotLimit > 0;

    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (Is(info.AttemptKiller) && !info.IsSuicide)
        {
            if (RequiresTasks && LastCooltime > 0)
            {
                info.DoKill = false;
                return;
            }

            (var killer, var target) = info.AttemptTuple;

            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 残り{ShotLimit}発", "Sheriff");
            if (ShotLimit <= 0)
            {
                info.DoKill = false;
                return;
            }
            ShotLimit--;
            SendRPC();

            var AlienTairo = false;
            var targetroleclass = target.GetRoleClass();
            if ((targetroleclass as Alien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            if ((targetroleclass as JackalAlien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            if ((targetroleclass as AlienHijack)?.CheckSheriffKill(target) == true) AlienTairo = true;

            if (!CanBeKilledBy(target) || AlienTairo)
            {
                PlayerState.GetByPlayerId(killer.PlayerId).DeathReason =
                    target.Is(CustomRoles.Tairou) && Tairou.TairoDeathReason ? CustomDeathReason.Counter :
                    target.Is(CustomRoles.Alien) && Alien.TairoDeathReason ? CustomDeathReason.Counter :
                    (target.Is(CustomRoles.JackalAlien) && JackalAlien.TairoDeathReason ? CustomDeathReason.Counter :
                    (target.Is(CustomRoles.AlienHijack) && Alien.TairoDeathReason ? CustomDeathReason.Counter : CustomDeathReason.Misfire));

                killer.RpcMurderPlayer(killer);
                Flug3 = Utils.IsActive(Main.SabotageType) && Main.SabotageType.IsCriticalSabotage() ? 1 : 0;
                UtilsGameLog.AddGameLog("Sheriff", string.Format(GetString("SheriffMissLog"), UtilsName.GetPlayerColor(target.PlayerId)));
                if (!MisfireKillsTarget.GetBool())
                {
                    info.DoKill = false;
                    return;
                }
            }

            nowcool = CurrentKillCooldown;
            ModeSwitching(RequiresTasks);
            SendRPC();
            killer.ResetKillCooldown();
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, SheriffAchievement.achievements[0]);
            Achievements.RpcCompleteAchievement(Player.PlayerId, 1, SheriffAchievement.achievements[1]);
        }
        return;
    }

    public override void AfterSabotage(SystemTypes systemType) => Flug3 = 0;

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (target is null) return;
        if (target.PlayerId == Player.Data.PlayerId && Flug3 == 1)
        {
            if (Utils.IsActive(Main.SabotageType) && Main.SabotageType.IsCriticalSabotage())
            {
                var systems = ShipStatus.Instance.Systems;
                LifeSuppSystemType LifeSupp;
                if (systems.ContainsKey(SystemTypes.LifeSupp) &&
                    (LifeSupp = systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>()) != null &&
                    LifeSupp.Countdown <= 15f)
                {
                    Achievements.RpcCompleteAchievement(Player.PlayerId, 0, SheriffAchievement.achievements[2]);
                }
                ISystemType sys = null;
                if (systems.ContainsKey(SystemTypes.Reactor)) sys = systems[SystemTypes.Reactor];
                else if (systems.ContainsKey(SystemTypes.Laboratory)) sys = systems[SystemTypes.Laboratory];
                else if (systems.ContainsKey(SystemTypes.HeliSabotage)) sys = systems[SystemTypes.HeliSabotage];
                ICriticalSabotage critical;
                if (sys != null &&
                (critical = sys.TryCast<ICriticalSabotage>()) != null &&
                critical.Countdown <= 15f)
                {
                    Achievements.RpcCompleteAchievement(Player.PlayerId, 0, SheriffAchievement.achievements[2]);
                }
            }
        }

        if (Player.IsAlive())
        {
            ModeSwitching(RequiresTasks);
            SendRPC();
        }
        Player.RpcResetAbilityCooldown(Sync: true);
    }
    public override RoleTypes? AfterMeetingRole => RequiresTasks ? null : RoleTypes.Impostor;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        if (!RequiresTasks) return;
        _ = new LateTask(() => nowcool = CurrentKillCooldown, Main.LagTime, "Reset-Sheriff");
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(CanUseSheriffMode() ? Color.yellow : Color.gray, $"({ShotLimit})");
        if (!GameStates.CalledMeeting && !gamelog)
            progress += Utils.ColorString(Color.yellow, Taskmode
                ? $" [Task]<color=#ffffff>({LastCooltime})</color>"
                : $"  [Sheriff]<color=#ffffff>({LastCooltime})</color>");
        return progress;
    }

    public override bool CanTask()
    {
        if (!RequiresTasks) return false;
        if (!Player.IsAlive()) return true;
        // 村長に任命されたシェリフはタスクなし
        if (AppointedPlayerIds.Contains(Player.PlayerId)) return false;
        return Taskmode;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;
        if (!player.IsAlive()) return;
        if (!RequiresTasks) return;

        if (nowcool > 0)
            nowcool -= Time.fixedDeltaTime;
        else
            nowcool = 0;

        var now = (int)nowcool;
        if (now != LastCooltime)
        {
            if (now <= 0) player.SetKillCooldown(0.5f);
            LastCooltime = now;
            if (player != PlayerControl.LocalPlayer)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }
    }

    private bool ModeSwitching(bool? taskMode = null)
    {
        if (!RequiresTasks) taskMode = false;
        Taskmode = taskMode ?? !Taskmode;

        var clientId = Player.GetClientId();
        if (Player.IsAlive() && clientId != -1)
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                var role = pc.GetCustomRole();
                if (role.IsImpostor())
                    SetRoleForSheriffClient(pc, Taskmode ? role.GetRoleTypes() : RoleTypes.Scientist, clientId);
                if (Is(pc))
                    SetRoleForSheriffClient(pc, Taskmode ? RoleTypes.Crewmate : RoleTypes.Impostor, clientId);
            }
        }

        if (!Taskmode)
        {
            var cooldown = RequiresTasks ? Mathf.Max(LastCooltime, 0.1f) : CurrentKillCooldown;
            Player.SetKillCooldown(cooldown, delay: true);
        }
        UpdateLocalHud();
        return Taskmode;
    }

    private void SetRoleForSheriffClient(PlayerControl target, RoleTypes role, int clientId)
    {
        if (target == PlayerControl.LocalPlayer && Is(PlayerControl.LocalPlayer))
        {
            RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, role);
            return;
        }

        target.RpcSetRoleDesync(role, clientId);
    }

    private void UpdateLocalHud()
    {
        if (!Is(PlayerControl.LocalPlayer) || !HudManager.InstanceExists) return;

        var hud = HudManager.Instance;
        hud.SetHudActive(true);
        hud.KillButton.ToggleVisible(Player.CanUseKillButton());
        hud.ImpostorVentButton.ToggleVisible(Player.CanUseImpostorVentButton());
        hud.SabotageButton.ToggleVisible(Player.CanUseSabotageButton());
        CustomButtonHud.BottonHud();
    }

    public static bool CanBeKilledBy(PlayerControl player)
    {
        var cRole = player.GetCustomRole();

        if (player.GetRoleClass() is SchrodingerCat schrodingerCat)
        {
            if (schrodingerCat.Team == ISchrodingerCatOwner.TeamType.None)
            {
                Logger.Warn($"シェリフ({player.GetRealName()})にキルされたシュレディンガーの猫のロールが変化していません", nameof(Sheriff));
                return false;
            }
            else
            {
                if (player.IsLovers() && CanKillLovers.GetBool()) return true;
            }
            return schrodingerCat.Team switch
            {
                ISchrodingerCatOwner.TeamType.Mad => KillTargetOptions.TryGetValue(CustomRoles.Madmate, out var option) && option.GetBool(),
                ISchrodingerCatOwner.TeamType.Crew => false,
                _ => CanKillNeutrals.GetValue() == 0 || (SchrodingerCatKillTargetOptions.TryGetValue(schrodingerCat.Team, out var option) && option.GetBool()),
            };
        }

        if (player.IsLovers() && CanKillLovers.GetBool()) return true;

        if (cRole == CustomRoles.Jackaldoll) return CanKillNeutrals.GetValue() == 0 || (!KillTargetOptions.TryGetValue(CustomRoles.Jackal, out var option) && option.GetBool()) || (!KillTargetOptions.TryGetValue(CustomRoles.JackalMafia, out var op) && op.GetBool());
        if (cRole == CustomRoles.SKMadmate) return KillTargetOptions.TryGetValue(CustomRoles.Madmate, out var option) && option.GetBool();
        if (player.Is(CustomRoles.Amanojaku)) return CanKillNeutrals.GetValue() == 0;

        return cRole.GetCustomRoleTypes() switch
        {
            CustomRoleTypes.Impostor => cRole is not CustomRoles.Tairou,
            CustomRoleTypes.Madmate => KillTargetOptions.TryGetValue(CustomRoles.Madmate, out var option) && option.GetBool(),
            CustomRoleTypes.Neutral => CanKillNeutrals.GetValue() == 0 || (!KillTargetOptions.TryGetValue(cRole, out var option) && option.GetBool()),
            CustomRoleTypes.Crewmate => cRole is CustomRoles.WolfBoy,
            _ => false,
        };
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Sheriff_Kill";
        return true;
    }
}

class SheriffAchievement
{
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(Sheriff.RoleInfo, 0, 1, 0, 0);
        var l2 = new Achievement(Sheriff.RoleInfo, 1, 10, 0, 1);
        var sp3 = new Achievement(Sheriff.RoleInfo, 2, 1, 0, 3, true);
        achievements.Add(0, n1);
        achievements.Add(1, l2);
        achievements.Add(2, sp3);
    }
}
