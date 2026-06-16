using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Villain : RoleBase, ILNKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Villain),
            player => new Villain(player),
            CustomRoles.Villain,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            55800,
            SetupOptionItem,
            "vln",
            "#8B0000",
            (6, 5),
            true,
            countType: CountTypes.Crew,
            assignInfo: new RoleAssignInfo(CustomRoles.Villain, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            from: From.TownOfHost_Pko
        );

    public Villain(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        isVillain = false;
        disguiseRole = CustomRoles.Crewmate;
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionImpostorVision;
    static OptionItem OptionSelfAware;
    static OptionItem OptionSeeImpostors;
    static OptionItem OptionTaskWin;

    public bool isVillain;
    public CustomRoles disguiseRole;

    static readonly CustomRoles[] DisguiseCandidates =
    {
        CustomRoles.Bait,
        CustomRoles.Lighter,
        CustomRoles.Doctor,
        CustomRoles.Crewmate,
        CustomRoles.Crewmate,
        CustomRoles.Crewmate,
    };

    enum OptionName
    {
        VillainKillCooldown,
        VillainCanVent,
        VillainImpostorVision,
        VillainSelfAware,
        VillainSeeImpostors,
        VillainTaskWin,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.VillainKillCooldown,
            new(2.5f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, OptionName.VillainCanVent, false, false);
        OptionImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, OptionName.VillainImpostorVision, false, false);
        OptionSelfAware = BooleanOptionItem.Create(RoleInfo, 13, OptionName.VillainSelfAware, true, false);
        OptionSeeImpostors = BooleanOptionItem.Create(RoleInfo, 14, OptionName.VillainSeeImpostors, false, false, OptionSelfAware);
        OptionTaskWin = BooleanOptionItem.Create(RoleInfo, 15, OptionName.VillainTaskWin, false, false);
    }

    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public bool CanUseKillButton() => isVillain && Player.IsAlive();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => isVillain && OptionCanVent.GetBool();

    public override bool CanClickUseVentButton => isVillain && OptionCanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => isVillain && OptionCanVent.GetBool();

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!isVillain) info.DoKill = false;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (isVillain && OptionImpostorVision.GetBool())
            opt.SetVision(true);
    }

    public override void Add()
    {
        isVillain = false;

        if (AmongUsClient.Instance.AmHost)
        {
            disguiseRole = DisguiseCandidates[IRandom.Instance.Next(DisguiseCandidates.Length)];
            SendRpc();
        }

        CustomRoleManager.MarkOthers.Add(GetMarkOthersHandler);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthersHandler);
    }

    private string GetMarkOthersHandler(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Player.IsAlive()) return "";
        if (seer.PlayerId != Player.PlayerId) return "";
        if (isVillain) return "";
        if (!OptionSelfAware.GetBool()) return "";
        if (!OptionSeeImpostors.GetBool()) return "";

        if (seen.Is(CustomRoleTypes.Impostor))
            return Utils.ColorString(Palette.ImpostorRed, "★");
        return "";
    }

    public override RoleTypes? AfterMeetingRole =>
        isVillain ? RoleTypes.Shapeshifter :
        (OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate);

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.3f, "Villain.AfterMeeting.CD", true);
    }

    bool AllKillersDead() =>
        !PlayerCatch.AllAlivePlayerControls.Any(pc =>
            pc.PlayerId != Player.PlayerId &&
            (pc.Is(CustomRoleTypes.Impostor) || pc.IsNeutralKiller()));

    public void TransformToVillain()
    {
        if (isVillain) return;
        isVillain = true;

        Player.RpcSetRoleDesync(RoleTypes.Shapeshifter, Player.GetClientId());
        foreach (var pc in PlayerCatch.AllAlivePlayerControls.Where(p => p.PlayerId != Player.PlayerId))
            pc.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());

        Player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            if (Player.IsAlive())
                Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "Villain.Transform.CD", true);

        Utils.SendMessage(
            "<color=#8B0000><size=120%>【ヴィランに変化！】</size>\nキル陣営が全滅しました。本来の姿を現し、生き残れ！</color>",
            Player.PlayerId);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
        UtilsGameLog.AddGameLog("Villain",
            $"{UtilsName.GetPlayerColor(Player)} がヴィランに変化した");
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (isVillain || !Player.IsAlive()) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;

        if (AllKillersDead())
            TransformToVillain();
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        if (isVillain) return;

        if (OptionTaskWin.GetBool() && MyTaskState.IsTaskFinished)
        {
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
                CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }
    }

    public override void OverrideDisplayRoleNameAsSeen(
        PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!isVillain)
        {
            if (seer.PlayerId == Player.PlayerId && OptionSelfAware.GetBool())
                return;

            enabled = true;
            roleColor = UtilsRoleText.GetRoleColor(disguiseRole);
            roleText = UtilsRoleText.GetRoleName(disguiseRole);
            addon = false;
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";

        if (isVillain)
            return $"<color={RoleInfo.RoleColorCode}>[V]</color>";

        if (OptionSelfAware.GetBool())
        {
            string dColor = $"#{ColorUtility.ToHtmlStringRGB(UtilsRoleText.GetRoleColor(disguiseRole))}";
            return $"<color={dColor}>[潜伏中]</color>";
        }
        return "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (OptionSelfAware.GetBool())
            return $"{size}<color={color}>キル陣営全滅でヴィランに変化</color>";
        return "";
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(isVillain);
        sender.Writer.Write((int)disguiseRole);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        isVillain = reader.ReadBoolean();
        disguiseRole = (CustomRoles)reader.ReadInt32();
    }
}