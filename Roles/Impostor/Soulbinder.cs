using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Soulbinder : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Soulbinder),
            player => new Soulbinder(player),
            CustomRoles.Soulbinder,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            127100,
            SetupOptionItem,
            "slb",
            OptionSort: (7, 12),
            assignInfo: new RoleAssignInfo(CustomRoles.Soulbinder, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );

    public Soulbinder(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();

        SoulSlavePlayerId = byte.MaxValue;
        isMadeSoulSlave = false;
    }

    static OptionItem OptionKillCooldown;

    static float KillCooldown_;

    public byte SoulSlavePlayerId;
    public bool isMadeSoulSlave;

    enum OptionName
    {

    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 2.5f), 35f, false).SetValueFormat(OptionFormat.Seconds);

        SoulSlave.HideRoleOptions(CustomRoles.SoulSlave);
    }

    public PlayerControl GetSoulSlave() =>
        SoulSlavePlayerId == byte.MaxValue ? null : GetPlayerById(SoulSlavePlayerId);

    public float CalculateKillCooldown() => KillCooldown_;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (!isMadeSoulSlave)
        {
            CreateSoulSlave(target);
            return;
        }
        if (target.Is(CustomRoleTypes.Impostor))
        {
            info.DoKill = false;
        }

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
    }

    void CreateSoulSlave(PlayerControl target)
    {
        isMadeSoulSlave = true;
        SoulSlavePlayerId = target.PlayerId;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(CustomRoles.SoulSlave, log: null);

        _ = new LateTask(() =>
        {
            if (target.GetRoleClass() is SoulSlave SoulSlave)
                SoulSlave.SetOwner(Player.PlayerId);
        }, 0.2f, "Soulbinder.SetSoulSlaveOwner", true);

        SendRpc();
        UtilsGameLog.AddGameLog("Soulbinder",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} を一味にした");
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Soulbinder.Notify", true);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";

        var SoulSlaveRole = GetSoulSlave()?.GetRoleClass() as SoulSlave;
        int pct = SoulSlaveRole?.TaskPercent ?? 0;
        return $"<color={RoleInfo.RoleColorCode}>(ソウルスレイプ:{pct}%)</color>";
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || SoulSlavePlayerId == byte.MaxValue || seen.PlayerId != SoulSlavePlayerId) return "";
        return $" <color={RoleInfo.RoleColorCode}>▲</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SoulSlavePlayerId);
        sender.Writer.Write(isMadeSoulSlave);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        SoulSlavePlayerId = reader.ReadByte();
        isMadeSoulSlave = reader.ReadBoolean();
    }
}

public sealed class SoulSlave : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SoulSlave),
            player => new SoulSlave(player),
            CustomRoles.SoulSlave,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            127200,
            SetupOptionItem,
            "gng",
            OptionSort: (6, 5),
            countType: CountTypes.OutOfGame
        );

    public SoulSlave(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        OwnerId = byte.MaxValue;
        CanVent = false;
        CanKill = false;
        hasGrantedAddon = false;
        hasSeenImpostors = false;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    static void SetupOptionItem() => HideRoleOptions(CustomRoles.SoulSlave);

    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances?.TryGetValue(role, out var sp) == true) sp.SetHidden(true);
        if (Options.CustomRoleCounts?.TryGetValue(role, out var cp) == true) cp.SetHidden(true);
    }

    public byte OwnerId;
    public bool CanVent;
    public bool CanKill;
    bool hasGrantedAddon;
    bool hasSeenImpostors;

    public int TaskPercent
    {
        get
        {
            if (MyTaskState.AllTasksCount <= 0) return 0;
            return MyTaskState.CompletedTasksCount * 100 / MyTaskState.AllTasksCount;
        }
    }

    public override void OnDestroy() => CustomRoleManager.MarkOthers.Remove(GetMarkOthers);

    public void SetOwner(byte ownerId) { OwnerId = ownerId; SendRpc(); }

    public Soulbinder GetOwner() =>
        OwnerId == byte.MaxValue ? null : GetPlayerById(OwnerId)?.GetRoleClass() as Soulbinder;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player != Player || !Player.IsAlive()) return;
        if (!GameStates.IsInTask || OwnerId == byte.MaxValue) return;

        var owner = GetPlayerById(OwnerId);
        if (owner == null || !owner.IsAlive() || owner.GetRoleClass() is not Soulbinder)
        {
            var state = PlayerState.GetByPlayerId(Player.PlayerId);
            if (state != null) state.DeathReason = CustomDeathReason.FollowingSuicide;
            Player.SetRealKiller(owner ?? Player);
            Player.RpcMurderPlayerV2(Player);
        }
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        int pct = TaskPercent;

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return true;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        if (CanKill)
        {
            Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
            foreach (var imp in AllAlivePlayerControls.Where(p => p.GetCustomRole().IsImpostor()))
                imp.RpcSetRoleDesync(RoleTypes.Scientist, Player.GetClientId());
            Player.SetKillCooldown();
        }
        else if (CanVent)
        {
            Player.RpcSetRoleDesync(RoleTypes.Engineer, Player.GetClientId());
        }
        Player.MarkDirtySettings();
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (OwnerId == byte.MaxValue) return false;
        return CustomWinnerHolder.WinnerIds.Contains(OwnerId);
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        if (seer.GetRoleClass() is Soulbinder Soulbinder && Soulbinder.SoulSlavePlayerId == seen.PlayerId)
        {
            var g = seen.GetRoleClass() as SoulSlave;
            if (g == null) return "";
            string marks = "";
            if (g.CanVent) marks += "<color=#00ffff>Ｖ</color>";
            if (g.CanKill) marks += $"<color={RoleInfo.RoleColorCode}>Ｋ</color>";
            if (g.hasGrantedAddon) marks += "<color=#ffff00>Ａ</color>";
            if (g.hasSeenImpostors) marks += "<color=#ff0000>Ｉ</color>";
            return marks != "" ? $" {marks}" : "";
        }

        if (seer.GetRoleClass() is SoulSlave SoulSlave && SoulSlave.OwnerId == seen.PlayerId)
            return $" <color={RoleInfo.RoleColorCode}>★</color>";

        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        int pct = TaskPercent;
        string col = pct >= 75 ? RoleInfo.RoleColorCode
                   : pct >= 50 ? "#ffaa00"
                   : pct >= 25 ? "#00ffff"
                   : "#888888";
        return $"<color={col}>({pct}%)</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;
        int pct = TaskPercent;

        string ability = CanKill ? "<color=#cc4b33>キル</color> "
                       : CanVent ? "<color=#00ffff>ベント</color> "
                       : "";
        return $"{size}<color={color}>タスク: {pct}% {ability}| 海賊に忠誠を！</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(OwnerId);
        sender.Writer.Write(CanVent);
        sender.Writer.Write(CanKill);
        sender.Writer.Write(hasGrantedAddon);
        sender.Writer.Write(hasSeenImpostors);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        OwnerId = reader.ReadByte();
        CanVent = reader.ReadBoolean();
        CanKill = reader.ReadBoolean();
        hasGrantedAddon = reader.ReadBoolean();
        hasSeenImpostors = reader.ReadBoolean();
    }
}