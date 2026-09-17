using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.Vanilla;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Hunter : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hunter),
            player => new Hunter(player),
            CustomRoles.Hunter,
            () => RequiresTasks ? RoleTypes.Crewmate : RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            39000,
            SetupOptionItem,
            "hun",
            "#f8cd46",
            (2, 3),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            Desc: () =>
            {
                string baseDesc = StartInTaskMode.GetBool() ? GetString("HunterDescTaskMode") : GetString("HunterDescNotask");

                string mark = OptionCanSeeTeam.GetBool() ? GetString("HunterMark") : "";
                mark = $"<b>{mark}</b>";

                return baseDesc + mark;
            },
            from: From.TownOfHost_Y
        );

    public Hunter(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => RequiresTasks? HasTask.True : HasTask.False
    )
    {
        ShotLimit = ShotLimitOpt.GetInt();
        CurrentKillCooldown = KillCooldown.GetFloat();
        Taskmode = RequiresTasks;
        nowcool = CurrentKillCooldown;
        LastCooltime = 0;
        HasMark = false;
    }

    public static OptionItem KillCooldown;
    public static OptionItem ShotLimitOpt;
    public static OptionItem StartInTaskMode;
    private static bool RequiresTasks => StartInTaskMode?.OptionMeGetBool() ?? true;
    public static OptionItem CanKillAllAlive;

    public static OptionItem OptionCanSeeTeam;
    public static OptionItem OptionMadIsImp;
    enum OptionName
    {
        SheriffShotLimit,
        SheriffStartInTaskMode,
        SheriffCanKillAllAlive,
        HunterKnowTargetIsImpostor,
        HunterKnowTargetMadIsImpostor
    }

    public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = new();
    public static Dictionary<ISchrodingerCatOwner.TeamType, OptionItem> SchrodingerCatKillTargetOptions = new();

    public int ShotLimit = 0;
    public float CurrentKillCooldown = 30;
    public bool Taskmode;
    float nowcool;
    int LastCooltime;
    bool diedTaskModeApplied;
    bool HasMark;
    string Mark = "";

    private bool EffectiveRequiresTasks => RequiresTasks;

    public static readonly string[] KillOption =
    {
        "SheriffCanKillAll", "SheriffCanKillSeparately"
    };

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Crew;

    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 990f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 15);
        ShotLimitOpt = IntegerOptionItem.Create(RoleInfo, 25, OptionName.SheriffShotLimit, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        StartInTaskMode = BooleanOptionItem.Create(RoleInfo, 30, OptionName.SheriffStartInTaskMode, true, false);
        OverrideTasksData.Create(RoleInfo, 35, parent: StartInTaskMode);
        CanKillAllAlive = BooleanOptionItem.Create(RoleInfo, 40, OptionName.SheriffCanKillAllAlive, true, false);
        OptionCanSeeTeam = BooleanOptionItem.Create(RoleInfo, 45, OptionName.HunterKnowTargetIsImpostor, true, false);
        OptionMadIsImp = BooleanOptionItem.Create(RoleInfo, 50, OptionName.HunterKnowTargetMadIsImpostor, true, false, OptionCanSeeTeam);
    }


    public override void Add()
    {

        var effectiveRequiresTasks = RequiresTasks;

        ShotLimit = ShotLimitOpt.GetInt();
        CurrentKillCooldown = KillCooldown.GetFloat();
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
        => EffectiveRequiresTasks
        && Player.IsAlive()
        && ShotLimit > 0;

    bool CanUseSheriffMode()
        => Player.IsAlive()
        && (GetCanKillAllAlive() || GameStates.AlreadyDied)
        && ShotLimit > 0;

    private bool GetCanKillAllAlive()
        => CanKillAllAlive.GetBool();

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
            if (EffectiveRequiresTasks && LastCooltime > 0)
            {
                info.DoKill = false;
                return;
            }

            (var killer, var target) = info.AttemptTuple;

            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 残り{ShotLimit}発", "Hunter");
            if (ShotLimit <= 0)
            {
                info.DoKill = false;
                return;
            }
            ShotLimit--;
            SendRPC();
            if (!OptionCanSeeTeam.GetBool())
            {
                return;
            }
            HasMark = true;
            if (target.Is(CustomRoleTypes.Impostor))
            {
                Mark = "◎";
            }
            if (target.Is(CustomRoleTypes.Neutral))
            {
                Mark = "▽";
            }
            if (target.Is(CustomRoleTypes.Madmate))
            {
                Mark = "";

                if (OptionMadIsImp.GetBool())
                {
                    Mark = "◎";
                }
            }
            if (target.Is(CustomRoleTypes.Crewmate))
            {
                Mark = "";
            }
            _ = new LateTask(() => HasMark = false, 5f, "Hunter_Mark");
        }
        return;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (seen == seer && OptionCanSeeTeam.GetBool() && HasMark)
        {
            return Utils.ColorString(RoleInfo.RoleColor, $"{Mark}");
        }
        return string.Empty;
    }

    public override RoleTypes? AfterMeetingRole => EffectiveRequiresTasks ? null : RoleTypes.Impostor;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return; HasMark = false;

        if (!EffectiveRequiresTasks) return;
        _ = new LateTask(() => nowcool = CurrentKillCooldown, Main.LagTime, "Reset-Hunter");
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(CanUseSheriffMode() ? Color.yellow : Color.gray, $"({ShotLimit})");
        if (!GameStates.CalledMeeting && !gamelog)
            progress += Utils.ColorString(Color.yellow, Taskmode
                ? $" [タスク]<color=#ffffff>({LastCooltime})</color>"
                : $"  [ハンター]<color=#ffffff>({LastCooltime})</color>");
        return progress;
    }

    public override bool CanTask()
    {
        if (!RequiresTasks) return false;
        if (!Player.IsAlive()) return true;
        return Taskmode;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;
        if (!player.IsAlive())
        {
            if (!diedTaskModeApplied && !Taskmode)
            {
                ForceTaskModeOnDeath();
            }
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
    }

    private void ForceTaskModeOnDeath()
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
    }

    private bool ModeSwitching(bool? taskMode = null)
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
                    SetRoleForSheriffClient(pc, Taskmode ? RoleTypes.Crewmate : RoleTypes.Impostor, clientId);
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

    public bool OverrideKillButton(out string text)
    {
        text = "Sheriff_Kill";
        return true;
    }
}