using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.AddOns.Neutral;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class DollBetrayer : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(DollBetrayer),
            player => new DollBetrayer(player),
            CustomRoles.DollBetrayer,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            552400,
            SetupOptionItem,
            "dlb",
            "#00b4eb",
            (1, 5),
            true,
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
            assignInfo: new RoleAssignInfo(CustomRoles.DollBetrayer, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
                // 通常配役は，ジャッカルが存在する場合のみ排出する
                IsInitiallyAssignableCallBack = () =>
                {
                    return CustomRoles.Jackal.IsEnable()
                    || CustomRoles.JackalAlien.IsEnable()
                    || CustomRoles.JackalMafia.IsEnable()
                    || CustomRoles.JackalWolf.IsEnable()
                    || CustomRoles.JackalHadouHo.IsEnable();
                }
            }//,
            //countType: CountTypes.None
        );
    public DollBetrayer(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        KillCooldown = OptionKillCoolDown.GetFloat();
        ImpostorRevealTaskCount = OptionImpostorRevealTaskCount.GetInt();
        CanVent = OptionCanVent.GetBool();
        HasImpostorVision = OptionHasImpostorVision.GetBool();

        IsBetray = false;
        IsImpostorReveal = false;
        CanSeeRolename = true;
        CanBetray = false;
    }
    public static bool IsBetray;
    bool IsImpostorReveal;

    static OptionItem OptionKillCoolDown; static float KillCooldown;
    static OptionItem OptionImpostorRevealTaskCount; static int ImpostorRevealTaskCount;
    static OptionItem OptionCanVent; static bool CanVent;
    static OptionItem OptionHasImpostorVision; static bool HasImpostorVision;
    static OptionItem OptionCanSeeOtherMDBet;
    static OptionItem OptionCanSeeBetrayer;
    bool CanSeeRolename;
    public bool CanBetray;
    enum OptionName
    {
        DollBetrayerImpostorRevealTaskcount,
        MadBetrayerCanSeeOtherMDBetrayer,
        MadBetrayerCanSeeOtherBetrayer,
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 2);
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 20f, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionImpostorRevealTaskCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.DollBetrayerImpostorRevealTaskcount, new(0, 99, 1), 5, false)
                .SetZeroNotation(OptionZeroNotation.Off);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.ImpostorVision, true, false);
        OptionCanSeeOtherMDBet = BooleanOptionItem.Create(RoleInfo, 15, OptionName.MadBetrayerCanSeeOtherMDBetrayer, true, false);
        OptionCanSeeBetrayer = BooleanOptionItem.Create(RoleInfo, 16, OptionName.MadBetrayerCanSeeOtherBetrayer, true, false);

        OverrideTasksData.Create(RoleInfo, 20);
        RoleAddAddons.Create(RoleInfo, 40, NeutralKiller: true);
    }
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Betrayer;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
        if (IsBetray)
        {
            opt.SetVision(HasImpostorVision);
        }
        else
            opt.SetVision(false);
    }
    public static bool IsJackal() => IsBetray is false;

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (IsImpostorReveal is false) return "";
        seen ??= seer;
        var role = seen.GetCustomRole();
        if (role is CustomRoles.Jackal or CustomRoles.JackalAlien or CustomRoles.JackalHadouHo or CustomRoles.JackalMafia or CustomRoles.JackalWolf)
            return "<#00b4eb>★</color>";
        return "";
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (Is(seen) is false || isForMeeting) return "";
        if (IsTaskFinished && !IsBetray)
            return $"<{RoleInfo.RoleColorCode}>{GetString("DollBetrayerLowerText")}</color>";
        return "";
    }
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (IsBetray && !CanSeeRolename)
        {
            roleText = GetString("Betrayer");
            roleColor = new Color(139f / 255f, 37f / 255f, 81f / 255f);
        }
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false) => IsBetray ? $"<{RoleInfo.RoleColorCode}>★</color>" : "";

    public override RoleTypes? AfterMeetingRole => IsTaskFinished || IsBetray ? RoleTypes.Impostor : RoleTypes.Engineer;
    bool IKiller.CanUseImpostorVentButton() => CanVent && (CanBetray || IsBetray);
    public override bool CanUseAbilityButton() => CanVent && !CanBetray && !IsBetray;
    bool IKiller.CanUseSabotageButton() => false;
    float IKiller.CalculateKillCooldown() => KillCooldown;
    bool IKiller.CanUseKillButton() => IsTaskFinished || IsBetray;
    public override bool CanClickUseVentButton => OptionCanVent.GetBool();

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (((seen.Is(CustomRoles.MadBetrayer) && MadBetrayer.IsMadmate() is false) || (seen.Is(CustomRoles.DollBetrayer) && DollBetrayer.IsJackal() is false)) && OptionCanSeeBetrayer.GetBool())
        {
            enabled = CanBetray || IsBetray;
            roleText = GetString("Betrayer");
            roleColor = new Color(139f / 255f, 37f / 255f, 81f / 255f);
        }
        if (((seen.GetRoleClass() is MadBetrayer md && md.CanBetray) || (seen.GetRoleClass() is DollBetrayer db && db.CanBetray)) && OptionCanSeeOtherMDBet.GetBool() && IsJackal() is false)
        {
            var role = seen.GetCustomRole();

            enabled = CanBetray || IsBetray;
            roleText = GetString($"{role}");
            roleColor = UtilsRoleText.GetRoleColor(role);
        }
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (seen.PlayerId == Player.PlayerId)
        {
            enabled = true;
            if (IsBetray && CanSeeRolename)
            {
                roleText = GetString("Betrayer");
                roleColor = new Color(139f / 255f, 37f / 255f, 81f / 255f);
            }
            return;
        }
        if (seen.Is(CountTypes.Jackal) && !seen.Is(CustomRoles.Jackaldoll) && !seen.Is(CustomRoles.Tama))
            enabled = CanSeeRolename;
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (Player.IsAlive() is false) return true;
        if (IsImpostorReveal is false && MyTaskState.HasCompletedEnoughCountOfTasks(ImpostorRevealTaskCount) && ImpostorRevealTaskCount is not 0)
        {
            IsImpostorReveal = true;
            if (!IsTaskFinished)
            {
                _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(true, true, false), 0.2f, "SetImpostorKillCool", true);
            }
        }
        if (IsTaskFinished)
        {
            HasAbility = false;
            CanBetray = true;
            if (!AmongUsClient.Instance.AmHost) return true;
            Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(true, true, false), 0.2f, "SetImpostorKillCool", true);
            _ = new LateTask(() => Player.SetKillCooldown(force: true), 0.2f, "SetImpostorKillCool", true);
        }
        return true;
    }
    public override void AfterMeetingTasks()
    {
        if (IsBetray && CanSeeRolename)
        {
            CanSeeRolename = false;
        }
    }
    void IKiller.OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AppearanceTuple;
        if ((target.Is(CustomRoles.MadBetrayer) && MadBetrayer.IsMadmate() is false) || (target.Is(CustomRoles.DollBetrayer) && DollBetrayer.IsJackal() is false))
        {
            info.DoKill = false;
            return;
        }
        if (IsBetray is true)
        {
            return;
        }

        if (IsTaskFinished is false || (IsBetray is false && !target.Is(CustomRoles.Jackal) && !target.Is(CustomRoles.JackalAlien) && !target.Is(CustomRoles.JackalHadouHo) && !target.Is(CustomRoles.JackalMafia) && !target.Is(CustomRoles.JackalWolf)))
        {
            info.DoKill = false;
            return;
        }
        if ((target.Is(CustomRoles.Jackal) || target.Is(CustomRoles.JackalAlien) || target.Is(CustomRoles.JackalHadouHo) || target.Is(CustomRoles.JackalMafia) || target.Is(CustomRoles.JackalWolf)) && IsBetray is false)
        {
            info.KillPower = 3;
            IsBetray = true;
            SendRPC();
            UtilsGameLog.AddGameLog("MadBetrayer", GetString("MadBetrayerLog"));
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(true, true, false), 0.2f, "SetImpostorKillCool", true);
            _ = new LateTask(() => Player.SetKillCooldown(force: true), 0.2f, "SetImpostorKillCool", true);
        }
    }

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsBetray);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        IsBetray = reader.ReadBoolean();
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (IsBetray is false && Player.IsWinner(CustomWinner.Jackal)) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        if (IsBetray && Player.IsWinner(CustomWinner.DollBetrayer) && !CustomWinnerHolder.winners.Contains(CustomWinner.Jackal))
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        var sp1 = new Achievement(RoleInfo, 2, 1, 0, 2);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
        achievements.Add(2, sp1);
    }
}
