using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.Utils;
using static TownOfHost.UtilsRoleText;

namespace TownOfHost.Roles.Neutral;

public sealed class Monika : RoleBase, ILNKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Monika),
            player => new Monika(player),
            CustomRoles.Monika,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            284800,
            SetupOptionItem,
            "mon",
            "#e5a497",
            (5, 7),
            true,
            countType: CountTypes.Monika,
            from: From.ExtremeRoles
        );

    public static readonly HashSet<byte> MonikaTrashLayer = new();

    public static bool IsTrashed(byte playerId) => MonikaTrashLayer.Contains(playerId);
    public static bool IsTrashed(PlayerControl pc) => pc != null && MonikaTrashLayer.Contains(pc.PlayerId);

    private static byte SelectMeetingMonikaId = byte.MaxValue;
    private static bool PendingSelectMeeting = false;

    public static bool IsSelectMeeting() => SelectMeetingMonikaId != byte.MaxValue;

    static OptionItem OptionHasCustomVision;
    static OptionItem OptionVision;
    static OptionItem OptionCanVent;
    static OptionItem OptionCanSabotage;
    static OptionItem OptionConsumeButton;
    static OptionItem OptionKillCooldown;
    static OptionItem OptionGameContinues;
    static OptionItem OptionCanSeeTrash;

    public static float KillCooldownValue;
    public static bool GameContinues => OptionGameContinues?.GetBool() ?? true;
    public static bool CanSeeTrashOpt => OptionCanSeeTrash?.GetBool() ?? false;
    public static bool ConsumeButton => OptionConsumeButton?.GetBool() ?? false;

    enum OptionName
    {
        MonikaHasCustomVision,
        MonikaVision,
        MonikaCanVent,
        MonikaCanSabotage,
        MonikaConsumeButton,
        MonikaKillCooldown,
        MonikaGameContinues,
        MonikaCanSeeTrash,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MonikaKillCooldown,
            new(0f, 180f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionHasCustomVision = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MonikaHasCustomVision, false, false);
        OptionVision = FloatOptionItem.Create(RoleInfo, 12, OptionName.MonikaVision,
            new(0.1f, 5f, 0.05f), 1f, false, OptionHasCustomVision).SetValueFormat(OptionFormat.Multiplier);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MonikaCanVent, false, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 14, OptionName.MonikaCanSabotage, false, false);
        OptionConsumeButton = BooleanOptionItem.Create(RoleInfo, 15, OptionName.MonikaConsumeButton, false, false);
        OptionGameContinues = BooleanOptionItem.Create(RoleInfo, 16, OptionName.MonikaGameContinues, true, false);
        OptionCanSeeTrash = BooleanOptionItem.Create(RoleInfo, 17, OptionName.MonikaCanSeeTrash, false, false);
    }

    public Monika(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        KillCooldownValue = OptionKillCooldown.GetFloat();
    }

    public float CalculateKillCooldown() => KillCooldownValue;
    public bool CanUseKillButton() => Player.IsAlive() && !IsTrashed(Player);
    public bool CanUseImpostorVentButton() => OptionCanVent.GetBool() && !IsTrashed(Player);
    public bool CanUseSabotageButton() => OptionCanSabotage.GetBool() && !IsTrashed(Player);
    public override bool CanClickUseVentButton => OptionCanVent.GetBool() && !IsTrashed(Player);
    public override bool OnEnterVent(PlayerPhysics p, int id) => OptionCanVent.GetBool() && !IsTrashed(Player);

    public bool OverrideKillButton(out string text) { text = "Monika_Kill"; return true; }
    public bool OverrideKillButtonText(out string text) { text = GetString("MonikaEraseButton"); return true; }

    public override void Add()
    {
        MonikaTrashLayer.Clear();
        SelectMeetingMonikaId = byte.MaxValue;
        PendingSelectMeeting = false;
        CustomRoleManager.MarkOthers.Add(GetTrashMarkOthers);
    }

    public override void OnDestroy()
    {
        MonikaTrashLayer.Clear();
        SelectMeetingMonikaId = byte.MaxValue;
        PendingSelectMeeting = false;
        CustomRoleManager.MarkOthers.Remove(GetTrashMarkOthers);
    }

    public static string GetTrashMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seen == null) return "";
        if (!isForMeeting) return "";
        if (!IsTrashed(seen.PlayerId)) return "";
        return ColorString(GetRoleColor(CustomRoles.Monika), "×");
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (OptionHasCustomVision.GetBool())
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, OptionVision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, OptionVision.GetFloat());
        }
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        (_, var target) = info.AttemptTuple;
        if (target == null) { info.DoKill = false; return; }

        info.DoKill = false;

        if (IsTrashed(target))
        {
            Player.SetKillCooldown();
            return;
        }

        Player.RpcProtectedMurderPlayer(target);
        Player.RpcProtectedMurderPlayer(Player);

        SendToTrash(target);
        Player.SetKillCooldown();
    }

    public static void SendToTrash(PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (target == null || MonikaTrashLayer.Contains(target.PlayerId)) return;

        MonikaTrashLayer.Add(target.PlayerId);

        SendMessage(
            GetString("MonikaTrashedNotify"),
            target.PlayerId,
            ColorString(GetRoleColor(CustomRoles.Monika), GetString("Monika")));

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(target)} をゴミ箱レイヤーへ送った");

        SendRpcStatic();
        UtilsNotifyRoles.NotifyRoles(NoCache: true);
    }

    public static void OnPlayerActuallyDied(byte playerId)
    {
        if (MonikaTrashLayer.Remove(playerId))
        {
            Logger.Info($"[Monika] {playerId} がゴミ箱から死亡レイヤーへ移行", "Monika");
            if (AmongUsClient.Instance.AmHost) SendRpcStatic();
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
    public static class MonikaEmergencyConsumePatch
    {
        public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!ConsumeButton) return;
            if (target != null) return;
            if (__instance == null || !__instance.Is(CustomRoles.Monika)) return;

            var candidates = AllAlivePlayerControls
                .Where(pc => pc.PlayerId != __instance.PlayerId
                          && !pc.GetCustomRole().IsImpostor()
                          && !IsTrashed(pc)
                          && CanEmergencyMeeting(pc))
                .ToList();

            if (candidates.Count == 0) return;
            var victim = candidates[IRandom.Instance.Next(candidates.Count)];

            var state = PlayerState.GetByPlayerId(victim.PlayerId);
            if (state != null && state.NumberOfRemainingButtons > 0)
                state.NumberOfRemainingButtons--;

            SendMessage(
                GetString("MonikaButtonConsumedNotify"),
                victim.PlayerId,
                ColorString(GetRoleColor(CustomRoles.Monika), GetString("Monika")));
        }

        static bool CanEmergencyMeeting(PlayerControl pc)
        {
            var state = PlayerState.GetByPlayerId(pc.PlayerId);
            return state != null && state.NumberOfRemainingButtons > 0;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (player == null || player != Player) return;
        if (!Player.IsAlive() || IsTrashed(Player)) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;
        if (IsSelectMeeting() || PendingSelectMeeting) return;

        CheckWinConditions();
    }

    private void CheckWinConditions()
    {
        var aliveMonikas = AllAlivePlayerControls
            .Where(pc => pc.Is(CustomRoles.Monika) && !IsTrashed(pc))
            .ToList();
        if (aliveMonikas.Count != 1) return;
        if (aliveMonikas[0].PlayerId != Player.PlayerId) return;

        var nonTrashAlive = AllAlivePlayerControls
            .Where(pc => !IsTrashed(pc) && !pc.Is(CustomRoles.Monika))
            .ToList();

        if (!GameContinues && nonTrashAlive.Count >= 2)
        {
            bool anyKiller = nonTrashAlive.Any(pc => IsKillerCount(pc));
            if (!anyKiller)
            {
                ExecuteWin(Player, nonTrashAlive);
                return;
            }
        }

        if (nonTrashAlive.Count >= 2)
        {
            if (nonTrashAlive.Count == 2)
                TriggerSelectMeeting(Player);
        }
        else
        {
            ExecuteWin(Player, nonTrashAlive);
        }
    }

    private static bool IsKillerCount(PlayerControl pc)
    {
        return pc.GetCountTypes() switch
        {
            CountTypes.Impostor => true,
            CountTypes.Jackal => true,
            CountTypes.Remotekiller => true,
            CountTypes.GrimReaper => true,
            CountTypes.MilkyWay => true,
            CountTypes.Pavlov => true,
            CountTypes.StandMaster => true,
            CountTypes.Villain => true,
            _ => false,
        };
    }

    private static void TriggerSelectMeeting(PlayerControl monika)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (IsSelectMeeting() || PendingSelectMeeting) return;
        if (monika == null || !monika.IsAlive() || IsTrashed(monika)) return;

        PendingSelectMeeting = true;
        SelectMeetingMonikaId = monika.PlayerId;
        SendRpcStatic();

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(monika)} の勝利条件達成。追加勝者選択の特殊会議を発生");

        _ = new LateTask(() => Utils.AllPlayerKillFlash(), 0.4f, "Monika.SelectFlash", true);

        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (monika == null || !monika.IsAlive() || IsTrashed(monika))
            {
                PendingSelectMeeting = false;
                SelectMeetingMonikaId = byte.MaxValue;
                SendRpcStatic();
                return;
            }
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
            {
                PendingSelectMeeting = false;
                return;
            }

            PendingSelectMeeting = false;

            ReportDeadBodyPatch.ExReportDeadBody(
                monika, null, false,
                GetString("MonikaSelectMeetingTitle"),
                RoleInfo.RoleColorCode);
        }, 1.5f, "Monika.TriggerSelectMeeting", true);
    }

    public override string MeetingAddMessage()
    {
        if (!Player.IsAlive()) return "";
        if (!IsSelectMeeting() || SelectMeetingMonikaId != Player.PlayerId) return "";

        string c = RoleInfo.RoleColorCode;
        return $"<color={c}><size=90%>☆ {GetString("MonikaSelectMeetingTitle")} ☆</size></color>\n" +
               $"<size=70%>{GetString("MonikaSelectMeetingText")}</size>\n";
    }

    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        var baseVote = base.ModifyVote(voterId, sourceVotedForId, isIntentional);
        if (!AmongUsClient.Instance.AmHost) return baseVote;
        if (!IsSelectMeeting() || SelectMeetingMonikaId != Player.PlayerId) return baseVote;
        if (voterId != Player.PlayerId) return baseVote;

        PlayerControl ally = null;
        if (sourceVotedForId < 15)
        {
            var target = GetPlayerById(sourceVotedForId);
            if (target != null && target.IsAlive() && !IsTrashed(target) && !target.Is(CustomRoles.Monika))
                ally = target;
        }

        var allies = new List<PlayerControl>();
        if (ally != null) allies.Add(ally);

        SelectMeetingMonikaId = byte.MaxValue;
        SendRpcStatic();

        MeetingVoteManager.Instance?.ClearAndExile(Player.PlayerId, sourceVotedForId);
        ExecuteWin(Player, allies);

        return (baseVote.votedForId, baseVote.numVotes, false);
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsSelectMeeting())
        {
            PendingSelectMeeting = false;
        }
    }

    private static void ExecuteWin(PlayerControl monika, List<PlayerControl> allies)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Monika, monika.PlayerId, AddWin: false);
        CustomWinnerHolder.NeutralWinnerIds.Add(monika.PlayerId);
        CustomWinnerHolder.WinnerIds.Add(monika.PlayerId);

        if (allies != null)
            foreach (var ally in allies)
                if (ally != null)
                    CustomWinnerHolder.WinnerIds.Add(ally.PlayerId);

        string allyNames = (allies != null && allies.Count > 0)
            ? string.Join(", ", allies.Select(pc => UtilsName.GetPlayerColor(pc, true)))
            : "なし";

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(monika)} 勝利！ 同伴勝利: {allyNames}");

        SelectMeetingMonikaId = byte.MaxValue;
        PendingSelectMeeting = false;
        SendRpcStatic();

        GameEndCheck();
    }

    static void GameEndCheck()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LogicFlow.CheckEndCriteria();
    }

    public override void CheckWinner(GameOverReason reason)
    {
    }

    static void SendRpcStatic()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var monika = AllPlayerControls.FirstOrDefault(pc => pc.Is(CustomRoles.Monika));
        (monika?.GetRoleClass() as Monika)?.SendRpc();
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SelectMeetingMonikaId);
        sender.Writer.Write(MonikaTrashLayer.Count);
        foreach (var id in MonikaTrashLayer) sender.Writer.Write(id);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        SelectMeetingMonikaId = reader.ReadByte();
        int count = reader.ReadInt32();
        MonikaTrashLayer.Clear();
        for (int i = 0; i < count; i++) MonikaTrashLayer.Add(reader.ReadByte());
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        int trashed = MonikaTrashLayer.Count;
        return trashed > 0 ? $"<color={RoleInfo.RoleColorCode}>(×{trashed})</color>" : "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (isForMeeting && IsSelectMeeting() && SelectMeetingMonikaId == Player.PlayerId)
            return $"{size}<color={color}>{GetString("MonikaSelectMeetingText")}</color>";
        if (isForMeeting) return "";

        return $"{size}<color={color}>{GetString("MonikaLowerText")} (×{MonikaTrashLayer.Count})</color>";
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
public static class MonikaTrashedVoteBlockPatch
{
    public static bool Prefix([HarmonyArgument(0)] byte srcPlayerId)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!Monika.IsTrashed(srcPlayerId)) return true;
        Logger.Info($"[Monika] ゴミ箱プレイヤー {srcPlayerId} の投票をブロック", "Monika");
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
public static class MonikaTrashedReportBlockPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (__instance == null) return true;
        if (!Monika.IsTrashed(__instance.PlayerId)) return true;
        Logger.Info($"[Monika] ゴミ箱プレイヤー {__instance.PlayerId} の通報をブロック", "Monika");
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class MonikaTrashDeathPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        Monika.OnPlayerActuallyDied(__instance.PlayerId);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class MonikaTrashVisibilityPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        var local = PlayerControl.LocalPlayer;
        if (local == null || !local.Is(CustomRoles.Monika)) return;

        bool shouldHide =
            !Monika.CanSeeTrashOpt
            && GameStates.IsInTask
            && !GameStates.IsMeeting
            && Monika.IsTrashed(__instance.PlayerId)
            && __instance.PlayerId != local.PlayerId;

        if (__instance.cosmetics == null || __instance.cosmetics.currentBodySprite == null) return;

        if (shouldHide)
        {
            if (!_hiddenByMonika.Contains(__instance.PlayerId))
            {
                __instance.cosmetics.currentBodySprite.BodySprite.enabled = false;
                __instance.cosmetics.gameObject.SetActive(false);
                __instance.cosmetics.ToggleNameVisible(false);
                _hiddenByMonika.Add(__instance.PlayerId);
            }
        }
        else
        {
            if (_hiddenByMonika.Contains(__instance.PlayerId))
            {
                __instance.cosmetics.currentBodySprite.BodySprite.enabled = true;
                __instance.cosmetics.gameObject.SetActive(true);
                __instance.cosmetics.ToggleNameVisible(true);
                _hiddenByMonika.Remove(__instance.PlayerId);
            }
        }
    }

    static readonly HashSet<byte> _hiddenByMonika = new();
}