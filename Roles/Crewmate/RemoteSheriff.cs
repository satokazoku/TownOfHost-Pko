/*using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Epic.OnlineServices.Presence;
using HarmonyLib;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;
using static Sentry.MeasurementUnit;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class RemoteSheriff : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(RemoteSheriff),
            player => new RemoteSheriff(player),
            CustomRoles.RemoteSheriff,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Crewmate,
            40000,
            SetupOptionItem,
            "sh",
            "#f8cd46",
            (2, 0),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.SuperNewRoles
        );

    public RemoteSheriff(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => (RequiresTasks && !AppointedPlayerIds.Contains(player.PlayerId)) ? HasTask.True : HasTask.False
    )
    {
        Cooldown = KillCooldown.GetFloat();
        Flug3 = 0;
        var isAppointed = AppointedPlayerIds.Contains(player.PlayerId);
        ShotLimit = isAppointed ? VillageChief.SheriffShotLimit.GetInt() : ShotLimitOpt.GetInt();
        CurrentKillCooldown = isAppointed ? VillageChief.SheriffKillCooldown.GetFloat() : KillCooldown.GetFloat();
        Taskmode = RequiresTasks && !isAppointed;
        nowcool = CurrentKillCooldown;
        LastCooltime = 0;
        TeleportandKill = new();
        CheckVentD.Clear();
        LadderPatch.Ladder.Clear();
        isAnimation = false;
        Duration = OptionDuration.GetFloat();
        Maximum = ShotLimitOpt.GetInt();
    }
    static float Duration;
    static Dictionary<byte, int> CheckVentD = new();
    List<byte> TeleportandKill;
    public static OptionItem KillCooldown;
    private static OptionItem MisfireKillsTarget;
    private static OptionItem CanKillMadmate;
    public static OptionItem ShotLimitOpt;
    public static OptionItem StartInTaskMode;

    (Vector2, Vector2, float) AnimationData;

    private static bool RequiresTasks => StartInTaskMode?.OptionMeGetBool() ?? true;
    public static OptionItem CanKillAllAlive;
    public static OptionItem CanKillNeutrals;
    public static OptionItem CanKillLovers;

    static float Cooldown;
    static float Maximum;
    int usecount;
    static bool TeleportKillerVentgaaa; //↓ターゲットが使ってると自爆する系
    static bool TeleportKillerPlatformFall;
    static bool TeleportKillerLadderFall;
    static bool TeleportKillerDokkaaaan; //ターゲットが死んでいると自爆する

    bool isAnimation;

    enum OptionName
    {
        SheriffMisfireKillsTarget,
        SheriffShotLimit,
        SheriffStartInTaskMode,
        SheriffCanKillAllAlive,
        SheriffCanKillNeutrals,
        SheriffCanKill,
        SheriffCanKillLovers,
        TeleportKillerMaximum,
        Duration,
        TeleportKillerFall,
        TeleportKillerVentgaaa,
        TeleportKillerPlatformFall,
        TeleportKillerLadderFall,
        TeleportKillerDokkaaaan,
        Maxnium = SheriffShotLimit
    }

    public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = new();
    public static Dictionary<ISchrodingerCatOwner.TeamType, OptionItem> SchrodingerCatKillTargetOptions = new();

    public int ShotLimit = 0;
    public float CurrentKillCooldown = 30;
    public bool Taskmode;
    float nowcool;
    int LastCooltime;
    int Flug3;
    bool diedTaskModeApplied;

    public static HashSet<byte> AppointedPlayerIds = new();
    private bool IsAppointedSheriff => AppointedPlayerIds.Contains(Player.PlayerId);
    private bool EffectiveRequiresTasks => RequiresTasks && !IsAppointedSheriff;

    public static readonly string[] KillOption =
    {
        "SheriffCanKillAll", "SheriffCanKillSeparately"
    };

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Crew;

    static OptionItem OptionDuration;
    static OptionItem OptionTeleportKillerFall;
    static OptionItem OptionTeleportKillerVentgaaa;
    static OptionItem OptionTeleportKillerPlatformFall;
    static OptionItem OptionTeleportKillerLadderFall;
    //static OptionItem OptionZiplineFall;
    static OptionItem OptionTeleportKillerDokkaaaan;

    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 990f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 8);
       // MisfireKillsTarget = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SheriffMisfireKillsTarget, false, false);
        ShotLimitOpt = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SheriffShotLimit, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        //StartInTaskMode = BooleanOptionItem.Create(RoleInfo, 17, OptionName.SheriffStartInTaskMode, true, false);
        //OverrideTasksData.Create(RoleInfo, 22, parent: StartInTaskMode);
        CanKillAllAlive = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SheriffCanKillAllAlive, true, false);
        CanKillMadmate = SetUpKillTargetOption(CustomRoles.Madmate, 13);
        CanKillNeutrals = StringOptionItem.Create(RoleInfo, 14, OptionName.SheriffCanKillNeutrals, KillOption, 0, false);
        SetUpNeutralOptions(30);
        CanKillLovers = BooleanOptionItem.Create(RoleInfo, 16, OptionName.SheriffCanKillLovers, true, false);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 17, OptionName.Duration, new(0f, 15, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionTeleportKillerFall = BooleanOptionItem.Create(RoleInfo, 18, OptionName.TeleportKillerFall, false, false);
        OptionTeleportKillerVentgaaa = BooleanOptionItem.Create(RoleInfo, 19, OptionName.TeleportKillerVentgaaa, false, false, OptionTeleportKillerFall);
        OptionTeleportKillerPlatformFall = BooleanOptionItem.Create(RoleInfo, 20, OptionName.TeleportKillerPlatformFall, false, false, OptionTeleportKillerFall);
        OptionTeleportKillerLadderFall = BooleanOptionItem.Create(RoleInfo, 21, OptionName.TeleportKillerLadderFall, false, false, OptionTeleportKillerFall);
        OptionTeleportKillerDokkaaaan = BooleanOptionItem.Create(RoleInfo, 22, OptionName.TeleportKillerDokkaaaan, false, false, OptionTeleportKillerFall);

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
        var isAppointedSheriff = IsAppointedSheriff; // Clear前に確定させる
        AppointedPlayerIds.Clear();
        if (isAppointedSheriff) AppointedPlayerIds.Add(Player.PlayerId);

        var effectiveRequiresTasks = RequiresTasks && !isAppointedSheriff;

        ShotLimit = isAppointedSheriff ? VillageChief.SheriffShotLimit.GetInt() : ShotLimitOpt.GetInt();
        CurrentKillCooldown = isAppointedSheriff ? VillageChief.SheriffKillCooldown.GetFloat() : KillCooldown.GetFloat();
        Taskmode = effectiveRequiresTasks;
        diedTaskModeApplied = false;
        Logger.Info($"{PlayerCatch.GetPlayerById(Player.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : 残り{ShotLimit}発", "Sheriff");
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
        if (!effectiveRequiresTasks)
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
        if (!EffectiveRequiresTasks) return;
        if (!CanChangeMode()) return;
    //    ModeSwitching();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(usecount);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        usecount = reader.ReadInt32();
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


    public override void OnShapeshift(PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        //var AlienTairo = false;
            //var targetroleclass = target.GetRoleClass();
            //if ((targetroleclass as Alien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            //if ((targetroleclass as JackalAlien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            //if ((targetroleclass as AlienHijack)?.CheckSheriffKill(target) == true) AlienTairo = true;
        if (!CanBeKilledBy(target))
        {
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason =
                target.Is(CustomRoles.Tairou) && Tairou.TairoDeathReason ? CustomDeathReason.Counter :
                target.Is(CustomRoles.Alien) && Alien.TairoDeathReason ? CustomDeathReason.Counter :
                (target.Is(CustomRoles.JackalAlien) && JackalAlien.TairoDeathReason ? CustomDeathReason.Counter :
                (target.Is(CustomRoles.AlienHijack) && Alien.TairoDeathReason ? CustomDeathReason.Counter : CustomDeathReason.Misfire));

            MyState.DeathReason = CustomDeathReason.Misfire;
            Player.RpcMurderPlayer(Player);
            Flug3 = Utils.IsActive(Main.SabotageType) && Main.SabotageType.IsCriticalSabotage() ? 1 : 0;
            UtilsGameLog.AddGameLog("Sheriff", string.Format(GetString("SheriffMissLog"), UtilsName.GetPlayerColor(target.PlayerId)));

            var misfireKillsTarget = IsAppointedSheriff ? VillageChief.SheriffMisfireKillsTarget.GetBool() : MisfireKillsTarget.GetBool();
            return;
        }
        if (!AmongUsClient.Instance.AmHost || Is(target) || (!target.IsAlive() && !TeleportKillerDokkaaaan) || (usecount >= Maximum && Maximum != 0)) return;
        usecount++;
        SendRPC();
        Logger.Info($"Player: {Player.name},Target: {target.name}, count: {usecount}", "TeleportKiller");
        _= new LateTask(() =>
        {
            if (!target.IsAlive() && TeleportKillerDokkaaaan)
            {
                Logger.Info($"ターゲットが生きてないから自爆☆ Killer:{Player.name} Target:{target.name}", "TeleportKiller");
                //MyState.DeathReason = CustomDeathReason.Bombed;
                //Player.RpcMurderPlayer(Player);
                return;
            }
            if (!TPCheck(target, true))
            {
                Logger.Info($"ターゲットはキル可能な状態ではないためキルがブロックされました Killer:{Player.name} Target:{target.name}", "TeleportKiller");
                Player.SetKillCooldown();
                if ((target.inVent || target.MyPhysics.Animations.IsPlayingEnterVentAnimation())
                        && TeleportKillerVentgaaa)
                {
                    //MyState.DeathReason = CustomDeathReason.Bombed;
                   // Player.RpcMurderPlayer(Player, true);
                    Logger.Info($"ターゲットがベントに入ってたせいでTPした時ベントに体があああ(自爆) Killer:{Player.name} Target:{target.name}", "TeleportKiller");
                    return;
                }
                if (!target.IsAlive()) return;
                Logger.Info($"キル待機中", "TeleportKiller");
                TeleportandKill.Add(target.PlayerId);
            }
            else
            {
                TeleportKill(Player, target);
            }
        }, 1.5f, "TeleportKiller-1");
    }
    public static bool TPCheck(PlayerControl target, bool KillerTP = false)
    {
        if (target.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
        {
            if (!KillerTP) return false;
            return TeleportKillerLadderFall;
        }

        if (target.inMovingPlat)
        {
            if (!KillerTP) return false;
            return TeleportKillerPlatformFall;
        }

        if (target.MyPhysics.Animations.IsPlayingEnterVentAnimation()
                || target.inVent)
        {
            if (!KillerTP) return false;
            return !TeleportKillerVentgaaa;
        }

        if (!target.IsAlive()) return false;

        return true;
    }

    public void TeleportKill(PlayerControl Player, PlayerControl target)
    {
        if (target.Is(CustomRoles.King) || target.Is(CustomRoles.Autocrat))
        {
            MyState.DeathReason = CustomDeathReason.Bombed;
            Player.RpcMurderPlayer(Player, true);
            Logger.Info($"この我を殺そうなど無謀な。ガッハッハ Killer:{Player.name} Target:{target.name}", "TeleportKiller");
            return;
        }
        var check = TPCheck(target);
        if ((target.inVent || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()) && !TeleportKillerVentgaaa)
        {
            target.MyPhysics.RpcBootFromVent(CheckVentD[target.PlayerId]);
            Logger.Info($"ベントでもキルするのだ", "TeleportKiller");
            _ = new LateTask(() => TeleportandKill.Add(target.PlayerId), 1.5f);
            check = false;
        }
        if (check)
        {

            _ = new LateTask(() =>
            {
                if (!target.inVent && !target.MyPhysics.Animations.IsPlayingEnterVentAnimation())
                {
                    if (target.GetCustomRole().IsImpostor()) return;
                    if (CustomRoleManager.OnCheckMurder(Player, target, target, target, true, false, (int)CustomDeathReason.Kill))
                    {
                        var state = PlayerState.GetByPlayerId(target.PlayerId);
                        state.SetDead();
                        target.SetRealKiller(Player);
                    }
                }
            }, 0.5f, "TeleportKiller-2");
        }

        if (target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || target.inMovingPlat)
        {
            var start = Player.transform.position;
            var goal = target.inMovingPlat ? (Vector2)Player.transform.position - new Vector2(0, 4) : new Vector2(Player.transform.position.x, LadderPatch.Ladder[target.PlayerId].y);
            var t = 0.0f;
            AnimationData = (start, goal, t);
            isAnimation = true;
        }
    }


    public float CalculateKillCooldown() => CanUseKillButton() ? CurrentKillCooldown : 0f;

    public bool CanUseKillButton()
        => CanUseSheriffMode()
        && !Taskmode;

    bool CanChangeMode()
        => EffectiveRequiresTasks
        && Player.IsAlive()
        && ShotLimit > 0;

    bool CanUseSheriffMode()
        => Player.IsAlive()
        && (GetCanKillAllAlive() || GameStates.AlreadyDied)
        && ShotLimit > 0;

    private bool GetCanKillAllAlive()
        => IsAppointedSheriff ? VillageChief.SheriffCanKillAllAlive.GetBool() : CanKillAllAlive.GetBool();

    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        AURoleOptions.ShapeshifterCooldown = Cooldown;
        //AURoleOptions.ShapeshifterLeaveSkin = LeaveSkin;
        AURoleOptions.ShapeshifterDuration = Duration;
    }

    /*public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (Is(info.AttemptKiller) && !info.IsSuicide)
        {
            if (EffectiveRequiresTasks && LastCooltime > 0)
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

                var misfireKillsTarget = IsAppointedSheriff ? VillageChief.SheriffMisfireKillsTarget.GetBool() : MisfireKillsTarget.GetBool();
                if (!misfireKillsTarget)
                {
                    info.DoKill = false;
                    return;
                }
            }

            nowcool = CurrentKillCooldown;
            ModeSwitching(EffectiveRequiresTasks);
            SendRPC();
            killer.ResetKillCooldown();
        }
        return;
    }*/

   /* public override void AfterSabotage(SystemTypes systemType) => Flug3 = 0;

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
            ModeSwitching(EffectiveRequiresTasks);
            SendRPC();
        }
        Player.RpcResetAbilityCooldown(Sync: true);
    }
    public override RoleTypes? AfterMeetingRole => EffectiveRequiresTasks ? null : RoleTypes.Impostor;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        if (!EffectiveRequiresTasks) return;
        _ = new LateTask(() => nowcool = CurrentKillCooldown, Main.LagTime, "Reset-Sheriff");
    }

   public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(CanUseSheriffMode() ? Color.yellow : Color.gray, $"({ShotLimit})");
        if (!GameStates.CalledMeeting && !gamelog)
            progress += Utils.ColorString(Color.yellow, Taskmode
                ? ""
                : "");
        return progress;
    }

    public override bool CanTask()
    {
        if (!RequiresTasks) return false;
        if (!Player.IsAlive()) return true;
        if (AppointedPlayerIds.Contains(Player.PlayerId)) return false;
        return Taskmode;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;
        if (!player.IsAlive())
        {
            if (EffectiveRequiresTasks && !Taskmode && !diedTaskModeApplied)
                //ForceTaskModeOnDeath();
            return;
        }

        if (!EffectiveRequiresTasks) return;

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
    }*/

    /*private void ForceTaskModeOnDeath()
    {
        diedTaskModeApplied = true;
        Taskmode = true;

        var clientId = Player.GetClientId();
        if (clientId != -1)
        {
            SetRoleForSheriffClient(Player, RoleTypes.Crewmate, clientId);

            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.PlayerId == Player.PlayerId) continue;
                var role = pc.GetCustomRole();
                if (role.IsImpostor())
                    SetRoleForSheriffClient(pc, role.GetRoleTypes(), clientId);
            }
        }

        SendRPC();
        Logger.Info(
            $"{Player.GetNameWithRole().RemoveHtmlTags()} は死亡によりタスクモードへ強制切替",
            "Sheriff");
    }*/

  /*  private bool ModeSwitching(bool? taskMode = null)
    {
        if (!EffectiveRequiresTasks) taskMode = false;
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
                    SetRoleForSheriffClient(pc, Taskmode ? RoleTypes.Crewmate : RoleTypes.Shapeshifter, clientId);
            }
        }

        if (!Taskmode)
        {
            var cooldown = EffectiveRequiresTasks ? Mathf.Max(LastCooltime, 0.1f) : CurrentKillCooldown;
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
    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ClimbLadder))]
    class LadderPatch
    {
        public static Dictionary<byte, Vector2> Ladder = new();
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Sheriff_Kill";
        return true;
    }
}*/