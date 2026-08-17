using System;
<<<<<<< HEAD
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Madmate;
using static UnityEngine.GraphicsBuffer;
=======
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
>>>>>>> dcd67e7ec6cde3d9d9f89ff2ca548e7182003ccf

namespace TownOfHost.Roles.Crewmate;

public sealed class Observer : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Observer),
            player => new Observer(player),
            CustomRoles.Observer,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            33900,
            SetupOptionItem,
            "Observer",
            "#8a99b7",
            (3, 9),
            false,
            from: From.TownOfHost_K
        );
    public Observer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        ObserverTarget = byte.MaxValue;
        RemainingMonitoring = OptionMaxMonitoring.GetInt();
        Awakened = !OptAwakening.GetBool() || OptAwakeningTaskCount.GetInt() < 1;
    }

    private byte ObserverTarget;
    bool Awakened;
    static OptionItem OptAwakening;
    static OptionItem OptAwakeningTaskCount;

    public int RemainingMonitoring { get; private set; }

    public static OptionItem OptionMaxMonitoring;

    private static void SetupOptionItem()
    {
        OptionMaxMonitoring = IntegerOptionItem.Create(RoleInfo, 10, Option.maxMonitoringCount, new(0, 99, 1), 10, false)
            .SetValueFormat(OptionFormat.Times);
        OptAwakening = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.TaskAwakening, false, false);
        OptAwakeningTaskCount = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.AwakeningTaskcount, new(0f, 255f, 1f), 5f, false, OptAwakening);
    }

    enum Option
    {
        maxMonitoringCount, // Observerがキル検知できる回数
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        // Observer本人かつ、残回数が1以上なら→投票先をObserverTargetに設定
        if (Is(voter) && RemainingMonitoring >= 1 && Awakened)
        {
            ObserverTarget = votedForId;
<<<<<<< HEAD
            var target = PlayerCatch.GetPlayerById(ObserverTarget);
            if (target.Is(CustomRoles.Madpsycho))
            {
                if (Madpsycho.CanPsycho)
                {
                    PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                    target.RpcMurderPlayer(Player);
                    string send = "";
                    if (!Player.IsAlive())
                    {
                        send = string.Format(GetString("Rpsych"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(target, true));
                    }
                    else
                    {
                        send = string.Format(GetString("RMeetingKill"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(Player, true));
                    }
                    foreach (var spl in PlayerCatch.AllPlayerControls.Where(pc => !pc.IsAlive())) Utils.SendMessage(send, spl.PlayerId, GetString("RMSKillTitle"));
                }
            }
=======
>>>>>>> dcd67e7ec6cde3d9d9f89ff2ca548e7182003ccf
        }

        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (RemainingMonitoring <= 0) return;
        if (ObserverTarget == byte.MaxValue) return;
        if (Player.IsAlive() is false) return;

        var target = PlayerCatch.GetPlayerById(ObserverTarget);
        if (target == null) return;

        if (!target.IsAlive())
        {
            // 死亡検知 → 即1回だけキルフラッシュ
            Utils.AllPlayerKillFlash();
            Utils.SendMessage($"{UtilsName.GetPlayerColor(target)} が死亡しました（by Observer）", Player.PlayerId);

            // 状態リセット＆残回数を減らす
            ObserverTarget = byte.MaxValue;
            RemainingMonitoring = Math.Max(0, RemainingMonitoring - 1);
            SendRpc();
        }
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (!Awakened && MyTaskState.HasCompletedEnoughCountOfTasks(OptAwakeningTaskCount.GetInt()))
        {
            Awakened = true;

            if (!Utils.RoleSendList.Contains(Player.PlayerId))
                Utils.RoleSendList.Add(Player.PlayerId);
        }

        return true;
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(RemainingMonitoring);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        RemainingMonitoring = reader.ReadInt32();
    }
    public override CustomRoles Misidentify() => Awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override string GetProgressText(bool comms = false, bool GameLog = false) => $"<{RoleInfo.RoleColorCode}>({RemainingMonitoring})</color>";
}