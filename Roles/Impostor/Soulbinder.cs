using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using Rewired.Utils.Classes.Data;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Roles.Crewmate.AllArounder;
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
            OptionSort: (7, 12)//,
            /*assignInfo: new RoleAssignInfo(CustomRoles.Soulbinder, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1)
            }*/
        );

    public Soulbinder(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();

        SoulSlavePlayerId = byte.MaxValue;
        isMadeSoulSlave = false;

        Arrow = "";
    }

    static OptionItem OptionKillCooldown;
    public static OptionItem OptionMinKillCool;
    public static OptionItem OptionDecreaseKillCool;
    public static OptionItem OptionNumCommonTasks;
    public static OptionItem OptionNumLongTasks;
    public static OptionItem OptionNumShortTasks;
    public static OptionItem OptionSlaveAngelCool;
    public static OptionItem OptionArrowtime;

    public float KillCooldown_;

    public byte SoulSlavePlayerId;
    public bool isMadeSoulSlave;
    string Arrow;

    enum OptionName
    {
        WorkhorseNumCommonTasks,
        WorkhorseNumLongTasks,
        WorkhorseNumShortTasks,
        HateKillerMinimumKillCool,
        SoulbinderDecreaseKillCool,
        SoulbinderSlaveAngelCool,
        SoulSlaveArrowtime
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 2.5f), 35f, false)
            . SetValueFormat(OptionFormat.Seconds);
        OptionMinKillCool = FloatOptionItem.Create(RoleInfo, 11, OptionName.HateKillerMinimumKillCool, new(0f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDecreaseKillCool = FloatOptionItem.Create(RoleInfo, 12, OptionName.SoulbinderDecreaseKillCool, new(0.5f, 180f, 0.5f), 1f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSlaveAngelCool = FloatOptionItem.Create(RoleInfo, 13, OptionName.SoulbinderSlaveAngelCool, new(0.5f, 180f, 0.5f), 35f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionArrowtime = FloatOptionItem.Create(RoleInfo, 14, OptionName.SoulSlaveArrowtime, new(0.5f, 180f, 0.5f), 7f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionNumCommonTasks = IntegerOptionItem.Create(RoleInfo, 15, OptionName.WorkhorseNumCommonTasks, new(0, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionNumLongTasks = IntegerOptionItem.Create(RoleInfo, 16, OptionName.WorkhorseNumLongTasks, new(0, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);
        OptionNumShortTasks = IntegerOptionItem.Create(RoleInfo, 17, OptionName.WorkhorseNumShortTasks, new(0, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Pieces);

        SoulSlave.HideRoleOptions(CustomRoles.SoulSlave);
    }
    public float CalculateKillCooldown() => KillCooldown_;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;

        if (!isMadeSoulSlave)
        {
            CreateSoulSlave(target);
        }
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
            if (target.GetRoleClass() is SoulSlave ss)
            {
                ss.SetOwner(Player.PlayerId);
                SoulSlave.Settasks(target);
            }
        }, 0.2f, "Soulbinder.SetSoulSlaveOwner", true);

        SendRpc();
        UtilsGameLog.AddGameLog("Soulbinder",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} をソウルスレイブにした");
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Soulbinder.Notify", true);
    }
    public void CreateArrow(PlayerControl target)
    {
        TargetArrow.Add(Player.PlayerId, target.PlayerId);

        _ = new LateTask(() =>
        {
            Logger.Info($"{TargetArrow.GetArrows(Player, target.PlayerId)}", "Soulbinder");
            Arrow = TargetArrow.GetArrows(Player, target.PlayerId);
            Logger.Info($"{Arrow}", "Soulbinder");
        }, 0.2f, "Soulbinder.RemoveArrow", true);
        _ = new LateTask(() =>
        {
            TargetArrow.Remove(target.PlayerId, Player.PlayerId);
        }, 0.5f, "Soulbinder.RemoveArrow", true);

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        _ = new LateTask(() =>
        {
            Arrow = "";
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }, OptionArrowtime.GetFloat(), "Soulbinder.RemoveArrow", true);
    }
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;
        //seerおよびseenが自分である場合以外は関係なし
        if (!Is(seer) || !Is(seen)) return "";
        if (!Player.IsAlive()) return "";

        return $"<color={RoleInfo.RoleColorCode}>{Arrow}</color>";
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || SoulSlavePlayerId == byte.MaxValue || seen.PlayerId != SoulSlavePlayerId) return "";
        return $" <color={RoleInfo.RoleColorCode}>▲</color>";
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;

        return $"<color={RoleInfo.RoleColorCode}>キルクール:{KillCooldown_}秒</color>";
    }
    public override void AfterMeetingTasks()
    {
        Arrow = "";
    }
    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SoulSlavePlayerId);
        sender.Writer.Write(isMadeSoulSlave);
        sender.Writer.Write(KillCooldown_);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        SoulSlavePlayerId = reader.ReadByte();
        isMadeSoulSlave = reader.ReadBoolean();
        KillCooldown_ = reader.ReadSingle();
    }
}

public sealed class SoulSlave : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SoulSlave),
            player => new SoulSlave(player),
            CustomRoles.SoulSlave,
            () => RoleTypes.GuardianAngel,
            CustomRoleTypes.Madmate,
            127200,
            SetupOptionItem,
            "sls",
            OptionSort: (6, 5),
            countType: CountTypes.OutOfGame
        );

    public SoulSlave(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        OwnerId = byte.MaxValue;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    static void SetupOptionItem() => HideRoleOptions(CustomRoles.SoulSlave);

    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances?.TryGetValue(role, out var sp) == true) sp.SetHidden(true);
        if (Options.CustomRoleCounts?.TryGetValue(role, out var cp) == true) cp.SetHidden(true);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.GuardianAngelCooldown = Soulbinder.OptionSlaveAngelCool.GetFloat();
    }
    public static void Settasks(PlayerControl pc)
    {
        var taskState = pc.GetPlayerTaskState();
        taskState.AllTasksCount = Soulbinder.OptionNumCommonTasks.GetInt() + Soulbinder.OptionNumLongTasks.GetInt()+ Soulbinder.OptionNumShortTasks.GetInt();

        if (AmongUsClient.Instance.AmHost)
        {
            pc.Data.RpcSetTasks(Array.Empty<byte>()); 
            pc.SyncSettings();
            UtilsNotifyRoles.NotifyRoles();
        }
    }
    public byte OwnerId;
    public override void OnDestroy() => CustomRoleManager.MarkOthers.Remove(GetMarkOthers);

    public void SetOwner(byte ownerId) { OwnerId = ownerId; SendRpc(); }

    public Soulbinder GetOwner() =>
        OwnerId == byte.MaxValue ? null : GetPlayerById(OwnerId)?.GetRoleClass() as Soulbinder;
    public static (bool, int, int, int) TaskData =>
    (false, Soulbinder.OptionNumCommonTasks.GetInt(), Soulbinder.OptionNumLongTasks.GetInt(), Soulbinder.OptionNumShortTasks.GetInt());
    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (GetPlayerById(OwnerId).GetRoleClass() is Soulbinder sb)
        {
            sb.KillCooldown_ -= Soulbinder.OptionDecreaseKillCool.GetFloat();
            if (sb.KillCooldown_ < Soulbinder.OptionMinKillCool.GetFloat())
            {
                sb.KillCooldown_ = Soulbinder.OptionMinKillCool.GetFloat();
            }
        }
        return true;
    }
    public static void UseAbility(PlayerControl pc, PlayerControl target)
    {
        Logger.Info("UseAbility", "Soulslave");

        if (pc.GetRoleClass() is SoulSlave ssl)
        {
            var Owner = PlayerCatch.GetPlayerById(ssl.OwnerId);
            if (Owner.GetRoleClass() is Soulbinder sli)
            {
                sli.CreateArrow(target);
            }
        }
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.GetRoleClass() is SoulSlave SoulSlave && SoulSlave.OwnerId == seen.PlayerId)
            return $" <color={RoleInfo.RoleColorCode}>★</color>";

        return "";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(OwnerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        OwnerId = reader.ReadByte();
    }
}