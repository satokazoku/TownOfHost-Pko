using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Apprentice : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Apprentice),
            player => new Apprentice(player),
            CustomRoles.Apprentice,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            30400,
            SetupOptionItem,
            "ap",
            "#c8a46e",
            (5, 5),
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );

    public Apprentice(PlayerControl player)
        : base(RoleInfo, player)
    {
        masterPlayerId = byte.MaxValue;
        masterOriginalRole = CustomRoles.NotAssigned;
        hasInherited = false;
        isTaskComplete = false;
    }

    static OptionItem OptionCanVent;
    static OptionItem OptionVentCooldown;
    static OptionItem OptionVentMaxTime;
    static OptionItem OptionRequiredTaskCount;
    static OverrideTasksData Tasks;

    enum OptionName
    {
        CanVent,
        VentCooldown,
        ApprenticeInVentMaxTime,
        ApprenticeRequiredTaskCount,
    }

    static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, OptionName.CanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.VentCooldown,
            new(0f, 180f, 0.5f), 15f, false, OptionCanVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionVentMaxTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.ApprenticeInVentMaxTime,
            new(0.5f, 180f, 0.5f), 5f, false, OptionCanVent)
            .SetZeroNotation(OptionZeroNotation.Infinity)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRequiredTaskCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.ApprenticeRequiredTaskCount,
            new(0, 20, 1), 0, false)
            .SetValueFormat(OptionFormat.Times);
        Tasks = OverrideTasksData.Create(RoleInfo, 20);
    }

    byte masterPlayerId;
    CustomRoles masterOriginalRole;
    bool hasInherited;
    bool isTaskComplete;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (OptionCanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = OptionVentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = OptionVentMaxTime.GetFloat();
        }
    }

    public override bool CanClickUseVentButton => OptionCanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => OptionCanVent.GetBool();

    public override void Add()
    {
        hasInherited = false;
        isTaskComplete = false;
        masterPlayerId = byte.MaxValue;
        masterOriginalRole = CustomRoles.NotAssigned;

        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.AmHost) return;

            var crewCandidates = PlayerCatch.AllPlayerControls
                .Where(pc => pc.PlayerId != Player.PlayerId
                          && pc.GetCustomRole().GetCustomRoleTypes() == CustomRoleTypes.Crewmate
                          && !pc.Is(CustomRoles.GM)
                          && pc.IsAlive())
                .ToArray();

            if (crewCandidates.Length == 0) return;

            var master = crewCandidates[IRandom.Instance.Next(crewCandidates.Length)];
            masterPlayerId = master.PlayerId;
            masterOriginalRole = master.GetCustomRole();

            Logger.Info($"[Apprentice] {Player.Data.GetLogPlayerName()} の親方: " +
                        $"{master.Data.GetLogPlayerName()} ({masterOriginalRole})", "Apprentice");
            SendRpc();
        }, 3f, "Apprentice.AssignMaster", true);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (hasInherited) return true;

        int required = OptionRequiredTaskCount.GetInt();

        bool conditionMet = required <= 0
            ? MyTaskState.IsTaskFinished
            : MyTaskState.CompletedTasksCount >= required;

        if (conditionMet && !isTaskComplete)
        {
            isTaskComplete = true;
            SendRpc();
            TryInherit();
        }
        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (hasInherited) return;
        if (masterPlayerId == byte.MaxValue) return;
        if (!GameStates.IsInTask) return;

        var master = PlayerCatch.GetPlayerById(masterPlayerId);
        if (master != null && !master.IsAlive())
            TryInherit();
    }

    void TryInherit()
    {
        if (hasInherited) return;
        if (!isTaskComplete) return;

        var master = PlayerCatch.GetPlayerById(masterPlayerId);
        if (master != null && master.IsAlive()) return;

        hasInherited = true;

        var inheritRole = masterOriginalRole;
        if (inheritRole == CustomRoles.NotAssigned
            || inheritRole.GetCustomRoleTypes() != CustomRoleTypes.Crewmate)
        {
            inheritRole = CustomRoles.Crewmate;
        }

        Logger.Info($"[Apprentice] {Player.Data.GetLogPlayerName()} が {inheritRole} を継承！", "Apprentice");
        UtilsGameLog.AddGameLog("Apprentice",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsRoleText.GetRoleName(inheritRole)} を継承しました");

        if (!Utils.RoleSendList.Contains(Player.PlayerId))
            Utils.RoleSendList.Add(Player.PlayerId);

        Player.RpcSetCustomRole(inheritRole, log: null);
        UtilsOption.MarkEveryoneDirtySettings();
        UtilsNotifyRoles.NotifyRoles();

        SendRpc();
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(masterPlayerId);
        sender.Writer.Write((int)masterOriginalRole);
        sender.Writer.Write(hasInherited);
        sender.Writer.Write(isTaskComplete);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        masterPlayerId = reader.ReadByte();
        masterOriginalRole = (CustomRoles)reader.ReadInt32();
        hasInherited = reader.ReadBoolean();
        isTaskComplete = reader.ReadBoolean();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive() || hasInherited) return "";

        int required = OptionRequiredTaskCount.GetInt();
        int completed = MyTaskState.CompletedTasksCount;
        int total = required <= 0 ? MyTaskState.AllTasksCount : required;

        if (isTaskComplete)
            return $"<color=#00ff88>({completed}/{total}✓)</color>";

        return $"<color=#888888>({completed}/{total})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";
        if (hasInherited) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        int required = OptionRequiredTaskCount.GetInt();
        int completed = MyTaskState.CompletedTasksCount;
        int total = required <= 0 ? MyTaskState.AllTasksCount : required;

        if (!isTaskComplete)
            return $"{size}<color={color}>タスクを完了させよう ({completed}/{total})</color>";

        return $"{size}<color={color}>準備完了。条件を満たせば能力を継承する。</color>";
    }
}