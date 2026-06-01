using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Police : RoleBase, IKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Police),
            player => new Police(player),
            CustomRoles.Police,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            173600,
            SetupOptionItem,
            "pol",
            "#1a6bb5",
            (3, 5),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );

    public Police(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        HandcuffCooldown = OptionHandcuffCooldown.GetFloat();
        PolicePhantomCD = OptionPhantomCooldown.GetFloat();
        MaxHandcuffs = OptionMaxHandcuffs.GetInt();
        HandcuffDuration = OptionHandcuffDuration.GetInt();

        handcuffMode = false;
        markedTargets = new();
        handcuffStock = MaxHandcuffs;
        nowcool = HandcuffCooldown;
        LastCooltime = 0;
    }

    public static Dictionary<byte, int> HandcuffedPlayers = new();

    static OptionItem OptionHandcuffCooldown;
    static float HandcuffCooldown;
    static OptionItem OptionPhantomCooldown;
    static float PolicePhantomCD;
    static OptionItem OptionMaxHandcuffs;
    static int MaxHandcuffs;
    static OptionItem OptionHandcuffDuration;
    static int HandcuffDuration;

    enum OptionName
    {
        PoliceHandcuffCooldown,
        PolicePhantomCooldown,
        PoliceMaxHandcuffs,
        PoliceHandcuffDuration,
    }

    static void SetupOptionItem()
    {
        OptionHandcuffCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.PoliceHandcuffCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.PolicePhantomCooldown,
            new(0f, 180f, 0.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        OptionMaxHandcuffs = IntegerOptionItem.Create(RoleInfo, 12, OptionName.PoliceMaxHandcuffs,
            new(1, 15, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionHandcuffDuration = IntegerOptionItem.Create(RoleInfo, 13, OptionName.PoliceHandcuffDuration,
            new(1, 10, 1), 2, false);
    }

    bool handcuffMode;
    readonly List<byte> markedTargets;
    int handcuffStock;
    float nowcool;
    int LastCooltime;

    public float CalculateKillCooldown() => handcuffMode && handcuffStock > 0 ? HandcuffCooldown : 999f;
    public bool CanUseKillButton() => Player.IsAlive() && handcuffMode && handcuffStock > 0;
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    bool IUsePhantomButton.IsPhantomRole => handcuffMode;
    bool IUsePhantomButton.IsresetAfterKill => false;
    public override bool CanUseAbilityButton() => handcuffMode && markedTargets.Count > 0;

    public override RoleTypes? AfterMeetingRole
        => handcuffMode ? RoleTypes.Phantom : RoleTypes.Engineer;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        if (handcuffMode)
            AURoleOptions.PhantomCooldown = PolicePhantomCD;
        else
        {
            AURoleOptions.EngineerCooldown = Mathf.Max(nowcool, 0.1f);
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override void Add()
    {
        handcuffMode = false;
        markedTargets.Clear();
        handcuffStock = MaxHandcuffs;
        nowcool = HandcuffCooldown;
        LastCooltime = 0;
        HandcuffedPlayers.Clear();

        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        SwitchMode(!handcuffMode);
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void SwitchMode(bool toHandcuff)
    {
        handcuffMode = toHandcuff;
        if (!Player.IsAlive()) return;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toHandcuff ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toHandcuff ? RoleTypes.Phantom : RoleTypes.Engineer,
                    Player.GetClientId());
        }

        if (toHandcuff)
        {
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (Player.IsAlive() && handcuffMode)
                {
                    AURoleOptions.PhantomCooldown = PolicePhantomCD;
                    Player.SetKillCooldown(Mathf.Max(nowcool, 0.1f), delay: true);
                    Player.RpcResetAbilityCooldown(Sync: true);
                }
            }, 0.1f, "Police.HandcuffModeReset", true);
        }
        else
        {
            markedTargets.Clear();
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (Player.IsAlive() && !handcuffMode)
                    Player.RpcResetAbilityCooldown(Sync: true);
            }, 0.1f, "Police.TaskModeReset", true);
        }
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        info.DoKill = false;

        if (!handcuffMode || handcuffStock <= 0) return;

        (_, var target) = info.AttemptTuple;

        if (markedTargets.Contains(target.PlayerId))
        {
            Utils.SendMessage(
                $"<color=#1a6bb5>{target.GetRealName()} はすでにマーク済みです</color>",
                Player.PlayerId);
            return;
        }

        markedTargets.Add(target.PlayerId);
        handcuffStock--;

        Utils.SendMessage(
            $"<color=#1a6bb5>{target.GetRealName()} をマーク！({markedTargets.Count}人) ファントムで発動 (残{handcuffStock}個)</color>",
            Player.PlayerId);

        nowcool = HandcuffCooldown;
        Player.SetKillCooldown(HandcuffCooldown, delay: true);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!handcuffMode || !Player.IsAlive()) return;
        if (markedTargets.Count == 0) return;

        foreach (var pid in markedTargets.ToArray())
        {
            HandcuffedPlayers[pid] = HandcuffDuration;

            var pc = PlayerCatch.GetPlayerById(pid);
            if (pc == null) continue;

            Utils.SendMessage(
                $"<color=#1a6bb5>【手錠】あなたに手錠がかけられました！{HandcuffDuration}ターン間キルできません</color>",
                pid);

            UtilsGameLog.AddGameLog("Police",
                $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(pc)} に手錠をかけた（{HandcuffDuration}ターン）");
        }

        markedTargets.Clear();

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive() || !handcuffMode) return;
            AURoleOptions.PhantomCooldown = PolicePhantomCD;
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.2f, "Police.PhantomCDReset", true);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;
        if (!Player.IsAlive()) return;

        if (!handcuffMode)
        {
            if (nowcool > 0) nowcool -= Time.fixedDeltaTime;
            else nowcool = 0;
            var now = (int)nowcool;
            if (now != LastCooltime)
            {
                LastCooltime = now;
                Player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    if (Player.IsAlive() && !handcuffMode)
                        Player.RpcResetAbilityCooldown(Sync: true);
                }, 0.1f, "Police.VentCDSync", true);
                if (player != PlayerControl.LocalPlayer)
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
            }
        }
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        markedTargets.Clear();

        foreach (var pid in HandcuffedPlayers.Keys.ToArray())
        {
            HandcuffedPlayers[pid]--;
            if (HandcuffedPlayers[pid] <= 0)
            {
                HandcuffedPlayers.Remove(pid);
                var pc = PlayerCatch.GetPlayerById(pid);
                if (pc != null)
                    _ = new LateTask(() => Utils.SendMessage(
                        "<color=#1a6bb5>【手錠解除】手錠が解除されました</color>", pid),
                        0.5f, $"Police.HandcuffExpire.{pid}", true);
            }
        }

        if (handcuffMode)
        {
            handcuffMode = false;
            SendRpc();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        handcuffStock = MaxHandcuffs;
        nowcool = HandcuffCooldown;
        LastCooltime = 0;

        ApplyModeOnReturn();
        SendRpc();
    }

    void ApplyModeOnReturn()
    {
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            SwitchMode(handcuffMode);
        }, Main.LagTime, "Police.AfterMeetingSwitch", true);
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(handcuffMode);
        sender.Writer.Write(handcuffStock);
        sender.Writer.Write(nowcool);
        sender.Writer.Write(markedTargets.Count);
        foreach (var pid in markedTargets) sender.Writer.Write(pid);
        sender.Writer.Write(HandcuffedPlayers.Count);
        foreach (var kv in HandcuffedPlayers)
        {
            sender.Writer.Write(kv.Key);
            sender.Writer.Write(kv.Value);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        handcuffMode = reader.ReadBoolean();
        handcuffStock = reader.ReadInt32();
        nowcool = reader.ReadSingle();
        int mc = reader.ReadInt32();
        markedTargets.Clear();
        for (int i = 0; i < mc; i++) markedTargets.Add(reader.ReadByte());
        int hc = reader.ReadInt32();
        HandcuffedPlayers.Clear();
        for (int i = 0; i < hc; i++)
        {
            byte pid = reader.ReadByte();
            int turns = reader.ReadInt32();
            HandcuffedPlayers[pid] = turns;
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        string mode = handcuffMode
            ? $"<color=#1a6bb5>[手錠]</color>"
            : "<color=#aaaaaa>[Task]</color>";
        string stock = $"<color=#1a6bb5>({handcuffStock})</color>";
        return $"{stock}{mode}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!handcuffMode)
            return $"{size}<color={color}>ペット → 手錠モードへ (手錠:{handcuffStock}個)</color>";

        if (handcuffStock <= 0 && markedTargets.Count == 0)
            return $"{size}<color=#888888>手錠がありません</color>";

        if (markedTargets.Count > 0)
            return $"{size}<color={color}>ファントム → 手錠発動！({markedTargets.Count}人マーク中) | キル → 追加マーク</color>";

        return $"{size}<color={color}>キル → ターゲットをマーク | ペット → タスクモードへ (残{handcuffStock}個)</color>";
    }

    public static string GetHandcuffMark(PlayerControl seen)
    {
        if (seen == null) return "";
        if (!HandcuffedPlayers.TryGetValue(seen.PlayerId, out int turns)) return "";
        return $" <color=#1a6bb5>🔒({turns})</color>";
    }

    public bool OverrideKillButton(out string text) { text = "Police_Mark"; return true; }
    public bool OverrideKillButtonText(out string text) { text = "マーク"; return true; }
    public override string GetAbilityButtonText() => "手錠発動";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Police_Handcuff"; return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
public static class HandcuffedKillBlockPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (__instance == null || __instance.Data == null) return true;
        if (!Police.HandcuffedPlayers.ContainsKey(__instance.PlayerId)) return true;

        _ = new LateTask(() =>
        {
            if (__instance != null && __instance.IsAlive())
                Utils.SendMessage(
                    "<color=#1a6bb5>【手錠中】手錠が外れるまでキルできません</color>",
                    __instance.PlayerId);
        }, 0.05f, $"Police.HandcuffBlock.{__instance.PlayerId}", true);

        return false;
    }
}