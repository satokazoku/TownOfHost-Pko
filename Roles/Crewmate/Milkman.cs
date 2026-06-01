using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Milkman : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Milkman),
            player => new Milkman(player),
            CustomRoles.Milkman,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            173500,
            SetupOptionItem,
            "mlk",
            "#f0f0e0",
            (4, 2),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );

    public Milkman(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        DeliveryCooldown = OptionDeliveryCooldown.GetFloat();
        MilkPerTask = OptionMilkPerTask.GetInt();

        deliveryMode = false;
        milkCount = 0;
        nowcool = DeliveryCooldown;
        LastCooltime = 0;
        pendingNotify = new();
        allDeliveredIds = new();
        rottenRecipients = new();
        diedThisRound = false;
    }

    static OptionItem OptionDeliveryCooldown;
    static float DeliveryCooldown;
    static OptionItem OptionMilkPerTask;
    static int MilkPerTask;
    static OptionItem OptionEnableRottenMilk;
    static OptionItem OptionRottenMilkChance;

    enum OptionName
    {
        MilkmanDeliveryCooldown,
        MilkmanMilkPerTask,
        MilkmanEnableRottenMilk,
        MilkmanRottenMilkChance,
    }

    static void SetupOptionItem()
    {
        OptionDeliveryCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MilkmanDeliveryCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionMilkPerTask = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MilkmanMilkPerTask,
            new(1, 99, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionEnableRottenMilk = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MilkmanEnableRottenMilk, false, false);
        OptionRottenMilkChance = IntegerOptionItem.Create(RoleInfo, 13, OptionName.MilkmanRottenMilkChance,
            new(1, 100, 1), 5, false, OptionEnableRottenMilk).SetValueFormat(OptionFormat.Percent);
    }

    bool deliveryMode;
    int milkCount;
    float nowcool;
    int LastCooltime;

    readonly HashSet<byte> pendingNotify;
    readonly HashSet<byte> allDeliveredIds;
    readonly HashSet<byte> rottenRecipients;
    bool diedThisRound;

    public float CalculateKillCooldown() => CanUseKillButton() ? DeliveryCooldown : 999f;
    public bool CanUseKillButton()
        => Player.IsAlive() && deliveryMode && milkCount > 0;
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    public override void Add()
    {
        deliveryMode = false;
        milkCount = 0;
        nowcool = DeliveryCooldown;
        LastCooltime = 0;
        pendingNotify.Clear();
        allDeliveredIds.Clear();
        rottenRecipients.Clear();
        diedThisRound = false;

        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override RoleTypes? AfterMeetingRole
        => deliveryMode ? RoleTypes.Impostor : RoleTypes.Crewmate;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    private void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        SwitchMode(!deliveryMode);
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private void SwitchMode(bool toDelivery)
    {
        deliveryMode = toDelivery;
        if (!Player.IsAlive()) return;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toDelivery ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toDelivery ? RoleTypes.Impostor : RoleTypes.Crewmate,
                    Player.GetClientId());
        }

        if (toDelivery)
        {
            Player.SetKillCooldown(Mathf.Max(nowcool, 0.1f), delay: true);
        }
        else
        {
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (Player.IsAlive() && !deliveryMode)
                    Player.RpcResetAbilityCooldown(Sync: true);
            }, 0.1f, "Milkman.TaskReset", true);
        }
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        info.DoKill = false;
        if (!deliveryMode || milkCount <= 0) return;

        (_, var target) = info.AttemptTuple;

        if (allDeliveredIds.Contains(target.PlayerId))
        {
            Utils.SendMessage(
                $"<color={RoleInfo.RoleColorCode}>{target.GetRealName()} にはすでに配達済みです</color>",
                Player.PlayerId);
            return;
        }

        milkCount--;
        allDeliveredIds.Add(target.PlayerId);

        bool isRotten = OptionEnableRottenMilk.GetBool()
            && IRandom.Instance.Next(1, 101) <= OptionRottenMilkChance.GetInt();

        if (isRotten)
            rottenRecipients.Add(target.PlayerId);
        else
            pendingNotify.Add(target.PlayerId);

        nowcool = DeliveryCooldown;
        Player.SetKillCooldown(DeliveryCooldown, delay: true);

        UtilsGameLog.AddGameLog("Milkman",
            $"{UtilsName.GetPlayerColor(Player)} -> {UtilsName.GetPlayerColor(target)} 牛乳配達{(isRotten ? "【腐】" : "")}");

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        milkCount += MilkPerTask;

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return true;
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (Player.IsAlive())
        {
            string myName = Player.GetRealName();
            foreach (var pid in pendingNotify.ToArray())
            {
                byte capturedPid = pid;
                _ = new LateTask(() =>
                {
                    var pc = PlayerCatch.GetPlayerById(capturedPid);
                    if (pc == null || !pc.IsAlive()) return;
                    Utils.SendMessage(
                        $"【===牛乳屋生存中===】\n{myName}印の牛乳が配られた",
                        capturedPid);
                }, 3f, $"Milkman.NotifySurvive.{pid}", true);
            }
            pendingNotify.Clear();
        }

        if (!Player.IsAlive() && diedThisRound && allDeliveredIds.Count > 0)
        {
            foreach (var pid in allDeliveredIds.ToArray())
            {
                byte capturedPid = pid;
                _ = new LateTask(() =>
                {
                    var pc = PlayerCatch.GetPlayerById(capturedPid);
                    if (pc == null || !pc.IsAlive() || pc.Data.Disconnected) return;
                    Utils.SendMessage(
                        "【===牛乳屋が死亡した===】\n牛乳屋が空き瓶を回収して逝ったようだ...",
                        capturedPid);
                }, 3f, $"Milkman.NotifyDeath.{pid}", true);
            }
            diedThisRound = false;
        }

        if (deliveryMode)
        {
            deliveryMode = false;
            SendRpc();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var pid in rottenRecipients.ToArray())
        {
            var pc = PlayerCatch.GetPlayerById(pid);
            if (pc == null || !pc.IsAlive()) continue;
            pc.SetRealKiller(Player);
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Poisoned, pid);
        }
        rottenRecipients.Clear();

        if (!Player.IsAlive() && !diedThisRound)
            diedThisRound = true;

        if (!Player.IsAlive()) return;

        SwitchMode(deliveryMode);
        SendRpc();
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (info.IsSuicide) return;
        diedThisRound = true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;
        if (!player.IsAlive()) return;

        if (nowcool > 0) nowcool -= Time.fixedDeltaTime;
        else nowcool = 0;

        var now = (int)nowcool;
        if (now != LastCooltime)
        {
            if (now <= 0 && deliveryMode)
                player.SetKillCooldown(0.5f);
            LastCooltime = now;
            if (player != PlayerControl.LocalPlayer)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(deliveryMode);
        sender.Writer.Write(milkCount);
        sender.Writer.Write(nowcool);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        deliveryMode = reader.ReadBoolean();
        milkCount = reader.ReadInt32();
        nowcool = reader.ReadSingle();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        string mode = deliveryMode
            ? $"<color={RoleInfo.RoleColorCode}>[配達]</color>"
            : "<color=#aaaaaa>[Task]</color>";
        return $"<color={RoleInfo.RoleColorCode}>(牛乳:{milkCount})</color>{mode}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (deliveryMode)
        {
            if (milkCount <= 0)
                return $"{size}<color=#888888>牛乳がありません（タスクモードに切替でタスクを完了させよう）</color>";
            return $"{size}<color={color}>キルボタン → 牛乳配達 (残{milkCount}本) | ペット → タスクモードへ</color>";
        }
        return $"{size}<color={color}>タスク完了で牛乳獲得 | ペット → 配達モードへ (牛乳:{milkCount}本)</color>";
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Milkman_Deliver";
        return true;
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = "配達";
        return true;
    }
}