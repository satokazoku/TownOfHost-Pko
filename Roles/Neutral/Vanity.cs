using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static TownOfHost.Roles.Core.Interfaces.ISchrodingerCatOwner;


namespace TownOfHost.Roles.Neutral;
public sealed class Vanity : RoleBase, IKiller, ISchrodingerCatOwner, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Vanity),
            player => new Vanity(player),
            CustomRoles.Vanity,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            56800,
            SetupOptionItem,
            "van",
            "#aa8d22",
            (5, 9),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.NebulaontheShip,
            Desc: () => string.Format(GetString("VanityDesc"), OptionSoloKiller.GetBool() ? GetString("VanityDescKiller") : GetString("VanityDescAdd"))
        );
    public Vanity(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        KilledCrewmate = false;
        Realized = false;
        ShotLimit = Sheriff.ShotLimitOpt.GetInt();
        CurrentKillCooldown = Sheriff.KillCooldown.GetFloat();
        Taskmode = RequiresTasks;
        nowcool = CurrentKillCooldown;
        LastCooltime = 0;
        CompletedTaskCount = 0;
    }
    public static OptionItem OptionSoloKiller;
    static OptionItem CanWinIsDead;
    static OptionItem OptionCanAwarenessKillCrew;
    static OptionItem OptionCanAwarenessTasks;
    static OptionItem OptionCanAwarenessTaskCount;
    public bool KilledCrewmate = false;
    public bool Realized = false;

    void RealizedTask()
    {
        if (Realized)
        {
            return;
        }
        if (MyTaskState.HasCompletedEnoughCountOfTasks(OptionCanAwarenessTaskCount.GetInt()) && CompletedTaskCount >= OptionCanAwarenessTaskCount.GetInt())
        {
            Realized = true;
        }
    }
    public float CurrentKillCooldown = 30;
    public bool Taskmode;
    float nowcool;
    public int LastCooltime;
    bool diedTaskModeApplied;
    private bool EffectiveRequiresTasks => RequiresTasks;
    private static bool RequiresTasks => Sheriff.StartInTaskMode?.OptionMeGetBool() ?? true;

    public int ShotLimit;
    int CompletedTaskCount;
    public override bool OnCompleteTask(uint taskid)
    {
        CompletedTaskCount++;
        return true;
    }
    enum OptionName
    {
        VanitySoloKiller,
        VanityCanWinIsDead,
        VanityCanAwarenessKillCrew,
        VanityCanAwarenessTasks,
        VanityCanAwarenessCount
    }
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);

        CanWinIsDead = BooleanOptionItem.Create(RoleInfo, 10, OptionName.VanityCanWinIsDead, true, false);
        OptionSoloKiller = BooleanOptionItem.Create(RoleInfo, 11, OptionName.VanitySoloKiller, false, false);
        OptionCanAwarenessKillCrew = BooleanOptionItem.Create(RoleInfo, 12, OptionName.VanityCanAwarenessKillCrew, true, false);
        OptionCanAwarenessTasks = BooleanOptionItem.Create(RoleInfo, 13, OptionName.VanityCanAwarenessTasks, true, false);
        OptionCanAwarenessTaskCount = IntegerOptionItem.Create(RoleInfo, 25, OptionName.VanityCanAwarenessCount, new(1, 255, 1), 8, false, OptionCanAwarenessTasks);
    }

    public TeamType SchrodingerCatChangeTo => Realized ? TeamType.Vanity : TeamType.Crew;
    public override void Add()
    {
        var effectiveRequiresTasks = RequiresTasks;

        ShotLimit = Sheriff.ShotLimitOpt.GetInt();
        CurrentKillCooldown = Sheriff.KillCooldown.GetFloat();
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

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public float CalculateKillCooldown() => CurrentKillCooldown;

    public bool CanUseKillButton()
        => CanUseSheriffMode()
        && !Taskmode;

    bool CanChangeMode()
        => EffectiveRequiresTasks
        && Player.IsAlive()
        && ShotLimit > 0;

    public bool CanUseSheriffMode()
        => Player.IsAlive()
        && (GetCanKillAllAlive() || GameStates.AlreadyDied)
        && ShotLimit > 0;

    private bool GetCanKillAllAlive()
        => Sheriff.CanKillAllAlive.GetBool();

    public override CustomRoles Misidentify() => !Realized ? CustomRoles.Sheriff : CustomRoles.Vanity;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!OptionSoloKiller.GetBool() && ShotLimit <= 0)
        {
            info.DoKill = false;
            return;
        }
        var target = info.AttemptTarget;
        if (ShotLimit > 0)
        {
            --ShotLimit;
        }
        if (target.Is(CustomRoles.Vanity))
        {
            return;
        }
        if (target.Is(CustomRoleTypes.Crewmate))
        {
            KilledCrewmate = true;
            if (OptionCanAwarenessKillCrew.GetBool())
            {
                Realized = true;
            }
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (Player.IsAlive())
        {
            ModeSwitching(EffectiveRequiresTasks);
            SendRPC();
        }
    }

    public override bool CanTask()
    {
        if (!RequiresTasks) return false;
        if (!Player.IsAlive()) return true;
        return Taskmode;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Realized)
        {
            RealizedTask();
        }

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
        if (!EffectiveRequiresTasks || Realized) taskMode = false;
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
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
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

    public override RoleTypes? AfterMeetingRole => EffectiveRequiresTasks ? RoleTypes.Crewmate : RoleTypes.Impostor;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        if (!EffectiveRequiresTasks) return;
        _ = new LateTask(() => nowcool = CurrentKillCooldown, Main.LagTime, "Reset-Vanity");
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {       
        if (CustomWinnerHolder.WinnerTeam is CustomWinner.Vanity && CustomWinnerHolder.WinnerIds.Contains(Player.PlayerId)) return false;

        if (OptionSoloKiller.GetBool() && KilledCrewmate)
        {
            return false;
        }
        if (KilledCrewmate && CanWinIsDead.GetBool())
        {
            return CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate;
        }
        if (KilledCrewmate && !CanWinIsDead.GetBool() && Player.IsAlive())
        {
            return CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate;
        }
        if (CanWinIsDead.GetBool() && !KilledCrewmate)
        {
            return CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate;
        }
        if (!CanWinIsDead.GetBool() && !KilledCrewmate && Player.IsAlive())
        {
            return CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate;
        }
        return false;
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (Realized && OptionSoloKiller.GetBool())
        {
            return "";
        }
        if (Realized && !OptionSoloKiller.GetBool())
        {
            return Utils.ColorString(CanUseSheriffMode() ? Color.yellow : Color.gray, $"({ShotLimit})");
        }
        var progress = Utils.ColorString(CanUseSheriffMode() ? Color.yellow : Color.gray, $"({ShotLimit})");
        if (!GameStates.CalledMeeting && !gamelog)
            progress += Utils.ColorString(Color.yellow, Taskmode
                ? $" [タスク]<color=#ffffff>({LastCooltime})</color>"
                : $"  [シェリフ]<color=#ffffff>({LastCooltime})</color>");
        return progress;
    }

    public bool OverrideKillButton(out string text)
    {
        if (Realized)
        {
            text = "DoubleKiller_Ability";
            return false;
        }

        text = "Sheriff_Kill";
        return true;
    }
}
