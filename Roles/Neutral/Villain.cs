/*
using System;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;
using static TownOfHost.PlayerCatch;

namespace TownOfHost.Roles.Neutral;

public sealed class Villain : RoleBase, ILNKiller, ISelfVoter
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
            countType: CountTypes.Villain,
            assignInfo: new RoleAssignInfo(CustomRoles.Villain, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            from: From.TownOfHost_Pko
        );

    private static readonly CustomRoles[] ForbiddenDisguiseRoles =
    [
        CustomRoles.Villain,
        CustomRoles.MagicalGirl,
    ];

    private const int StateRpcTag = 0x564C4E52;

    public Villain(PlayerControl player)
        : base(RoleInfo, player)
    {
        hasTasks = GetCurrentHasTasks;
        SelfAware = OptSelfAware.GetBool();
        KnowImpostors = OptKnowImpostors.GetBool();
        CanVentAfterAwaken = OptCanVentAfterAwaken.GetBool();
        ImpostorVisionAfterAwaken = OptImpostorVisionAfterAwaken.GetBool();
        Realized = false;
        disguiseRole = CustomRoles.Crewmate;
        addRole = null;
    }

    static OptionItem OptSelfAware;
    static OptionItem OptKnowImpostors;
    static OptionItem OptCanVentAfterAwaken;
    static OptionItem OptImpostorVisionAfterAwaken;

    bool SelfAware;
    bool KnowImpostors;
    bool CanVentAfterAwaken;
    bool ImpostorVisionAfterAwaken;

    public bool Realized;

    CustomRoles disguiseRole;
    private RoleBase addRole;

    enum OptionName
    {
        VillainSelfAware,
        VillainKnowImpostors,
        VillainCanVentAfterAwaken,
        VillainImpostorVisionAfterAwaken,
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 15);

        OptSelfAware = BooleanOptionItem.Create(
            RoleInfo, 10, OptionName.VillainSelfAware, false, false);

        OptKnowImpostors = BooleanOptionItem.Create(
            RoleInfo, 11, OptionName.VillainKnowImpostors, false, false, OptSelfAware);

        OptCanVentAfterAwaken = BooleanOptionItem.Create(
            RoleInfo, 12, OptionName.VillainCanVentAfterAwaken, false, false);

        OptImpostorVisionAfterAwaken = BooleanOptionItem.Create(
            RoleInfo, 13, OptionName.VillainImpostorVisionAfterAwaken, true, false);
    }

    private HasTask GetCurrentHasTasks()
    {
        if (!Realized && addRole != null)
            return addRole.HasTasks;
        return HasTask.True;
    }

    public override void Add()
    {
        Realized = false;
        SelfAware = OptSelfAware.GetBool();
        KnowImpostors = OptKnowImpostors.GetBool();
        CanVentAfterAwaken = OptCanVentAfterAwaken.GetBool();
        ImpostorVisionAfterAwaken = OptImpostorVisionAfterAwaken.GetBool();

        var crewPool = CustomRolesHelper.AllStandardRoles
            .Where(r => r.IsCrewmate() && r.IsEnable() && !ForbiddenDisguiseRoles.Contains(r))
            .ToArray();
        disguiseRole = crewPool.Length > 0
            ? crewPool[IRandom.Instance.Next(crewPool.Length)]
            : CustomRoles.Crewmate;

        CreateDisguiseInstance();
        RefreshMainRoleState();
        RecomputeTaskCounts();
        SendRPC();

        if (SelfAware)
        {
            Utils.SendMessage(GetString("VillainSelfAwareInfo"), Player.PlayerId);
            if (KnowImpostors) RevealImpostorsToSelf();
        }
    }

    public override void OnDestroy()
    {
        if (addRole != null)
        {
            addRole.OnDestroy();
            addRole = null;
        }
    }

    void RevealImpostorsToSelf()
    {
        foreach (var imp in AllPlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)))
            NameColorManager.Add(Player.PlayerId, imp.PlayerId, "#ff1919");
    }

    private void CreateDisguiseInstance()
    {
        if (Realized)
        {
            if (addRole != null) { addRole.OnDestroy(); addRole = null; }
            return;
        }

        if (disguiseRole is CustomRoles.Crewmate
            || !CustomRoleManager.AllRolesInfo.TryGetValue(disguiseRole, out var roleInfo))
        {
            addRole = null;
            return;
        }

        addRole = roleInfo.CreateInstance(Player);
        addRole?.Add();
        if (addRole is ISelfVoter selfVoter) selfVoter.AddSelfVoter(Player);
        if (addRole is IRoomTasker roomTasker) roomTasker.AddRoomTaker(Player.PlayerId);
    }

    private void RefreshMainRoleState()
    {
        var targetRole = Realized ? CustomRoles.Villain : disguiseRole;
        if (MyState.MainRole == targetRole) return;
        MyState.SetMainRole(targetRole);
    }

    private void RecomputeTaskCounts()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Player?.Data == null) return;
        MyTaskState.hasTasks = UtilsTask.HasTasks(Player.Data, false);
        GameData.Instance?.RecomputeTaskCounts();
    }

    private void SyncCurrentRoleType()
    {
        if (!AmongUsClient.Instance.AmHost || Player?.GetClient() == null) return;
        AntiBlackout.ResetSetRole(Player);
    }

    public override void OnSpawn(bool initialState)
    {
        if (!Realized) addRole?.OnSpawn(initialState);
        if (initialState && SelfAware && KnowImpostors && !Realized)
            RevealImpostorsToSelf();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (!Realized)
        {
            addRole?.ApplyGameOptions(opt);
            return;
        }
        opt.SetVision(ImpostorVisionAfterAwaken);
    }

    public override bool OnCompleteTask(uint taskid)
        => !Realized ? (addRole?.OnCompleteTask(taskid) ?? true) : true;

    public override bool CanTask()
        => !Realized ? (addRole?.CanTask() ?? true) : true;

    bool ISelfVoter.CanUseVoted()
        => !Realized && addRole is ISelfVoter sv && sv.CanUseVoted();

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
        => !Realized ? (addRole?.CheckVoteAsVoter(votedForId, voter) ?? true) : true;

    public override void OnStartMeeting()
    {
        if (!Realized) addRole?.OnStartMeeting();
    }

    public override void AfterMeetingTasks()
    {
        if (!Realized) addRole?.AfterMeetingTasks();
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
        => !Realized ? (addRole?.OnCheckMurderAsTarget(info) ?? true) : true;

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (!Realized) addRole?.OnMurderPlayerAsTarget(info);
    }

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
        => !Realized ? (addRole?.OnEnterVent(physics, ventId) ?? true) : true;

    public override bool CanVentMoving(PlayerPhysics physics, int ventId)
        => !Realized ? (addRole?.CanVentMoving(physics, ventId) ?? true) : true;

    public override string GetAbilityButtonText()
        => !Realized ? (addRole?.GetAbilityButtonText() ?? base.GetAbilityButtonText())
                     : base.GetAbilityButtonText();

    public override bool OverrideAbilityButton(out string text)
    {
        if (!Realized && (addRole?.OverrideAbilityButton(out var addText) ?? false))
        {
            text = addText;
            return true;
        }
        text = default;
        return false;
    }

    public override CustomRoles Misidentify()
        => !Realized ? (addRole?.Misidentify() ?? disguiseRole) : CustomRoles.NotAssigned;

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (Realized) return;
        if (!Is(seer)) return;
        if (!SelfAware) return;

        enabled = true;
        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.Villain);
        roleText = GetString("Villain");
    }

    public float CalculateKillCooldown() => TownOfHost.Options.DefaultKillCooldown;
    public bool CanUseKillButton() => Player.IsAlive() && Realized;
    public bool CanUseImpostorVentButton() => Realized && CanVentAfterAwaken;
    public bool CanUseSabotageButton() => false;

    public bool OverrideKillButton(out string text)
    {
        text = "Villain_Kill";
        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Realized) addRole?.OnFixedUpdate(player);

        if (!AmongUsClient.Instance.AmHost || Realized || !player.IsAlive()) return;
        if (!GameStates.IsInTask) return;
        if (!MyState.HasSpawned) return;

        if (PlayerCatch.AliveImpostorCount <= 0)
            Realize();
    }

    void Realize()
    {
        Realized = true;

        if (addRole != null)
        {
            addRole.OnDestroy();
            addRole = null;
        }

        RefreshMainRoleState();
        RecomputeTaskCounts();
        SyncCurrentRoleType();
        SendRPC();

        Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());

        if (!Utils.RoleSendList.Contains(Player.PlayerId))
            Utils.RoleSendList.Add(Player.PlayerId);

        Player.SetKillCooldown();
        if (AmongUsClient.Instance.AmHost)
            Player.SyncSettings();
        UtilsNotifyRoles.NotifyRoles();

        Utils.SendMessage(GetString("VillainAwaken"), Player.PlayerId);

        foreach (var pc in AllPlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (pc.GetRoleClass() is Villain other && other.Realized)
            {
                NameColorManager.Add(Player.PlayerId, pc.PlayerId, RoleInfo.RoleColorCode);
                NameColorManager.Add(pc.PlayerId, Player.PlayerId, RoleInfo.RoleColorCode);
            }
        }

        UtilsGameLog.AddGameLog("Villain",
            $"{UtilsName.GetPlayerColor(Player)} がヴィランとして覚醒した");
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!Realized)
        {
            addRole?.CheckWinner(reason);
            return;
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        var aliveVillains = AllPlayerControls
            .Where(pc => pc.IsAlive() && pc.GetRoleClass() is Villain v && v.Realized)
            .ToList();
        if (aliveVillains.Count == 0) return;

        int aliveCrew = AllPlayerControls.Count(pc => pc.IsAlive() && pc.Is(CustomRoleTypes.Crewmate));
        if (aliveVillains.Count < aliveCrew) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Villain, Player.PlayerId, true))
        {
            foreach (var pc in aliveVillains)
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            }
        }
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (!Player.IsAlive()) return "";
        if (Realized)
            return Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Villain), "(覚醒)");
        return addRole?.GetProgressText(comms, gamelog) ?? "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (Realized)
            return $"{size}<color={color}>ヴィランとして覚醒した！生存ヴィラン人数がクルー以上で勝利！</color>";

        var addText = addRole?.GetLowerText(seer, seen, isForMeeting, isForHud) ?? "";
        if (!SelfAware) return addText;

        var villainNote = $"{size}<color={color}>あなたは秘密のヴィラン。インポスターが全滅すると覚醒する。</color>";
        return addText == "" ? villainNote : addText + "\n" + villainNote;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
        => !Realized ? (addRole?.GetMark(seer, seen, isForMeeting) ?? "") : "";

    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
        => !Realized ? (addRole?.GetSuffix(seer, seen, isForMeeting) ?? "") : "";

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked(StateRpcTag);
        sender.Writer.Write(Realized);
        sender.Writer.WritePacked((int)disguiseRole);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var backup = MessageReader.Get(reader);
        var handled = false;
        try
        {
            if (reader.ReadPackedInt32() == StateRpcTag)
            {
                Realized = reader.ReadBoolean();
                disguiseRole = (CustomRoles)reader.ReadPackedInt32();
                CreateDisguiseInstance();
                RefreshMainRoleState();
                handled = true;
            }
        }
        catch
        {
        }

        if (!handled)
        {
            try { addRole?.ReceiveRPC(backup); }
            catch (Exception ex) { Logger.Error($"{ex}", "Villain"); }
        }

        backup.Recycle();
    }
}
*/