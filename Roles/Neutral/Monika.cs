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

/// <summary>
/// モニカ (Neutral / キルボタン持ち)
/// ・キルボタン「抹消」で対象を新レイヤー「ゴミ箱」(半死亡)に送る
/// ・ゴミ箱に居ない生存者が2名になると特殊会議で追加勝者を選択
/// ・ゴミ箱に居ない生存者が0～1名になるとモニカ+残り全員が勝利
/// </summary>
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
            true,                              // isDesyncImpostor (キルボタン持ち)
            countType: CountTypes.Monika,      // ★ モニカはキル勢としてカウント
            from: From.ExtremeRoles
        );

    // ─── ゴミ箱レイヤー（全インスタンス共有・ホスト権威）───────────
    public static readonly HashSet<byte> MonikaTrashLayer = new();

    public static bool IsTrashed(byte playerId) => MonikaTrashLayer.Contains(playerId);
    public static bool IsTrashed(PlayerControl pc) => pc != null && MonikaTrashLayer.Contains(pc.PlayerId);

    // ─── 特殊会議（勝者追加選択）管理（静的・ニムロッド流）───────
    // byte.MaxValue = 非選択会議中
    private static byte SelectMeetingMonikaId = byte.MaxValue;
    private static bool PendingSelectMeeting = false;

    public static bool IsSelectMeeting() => SelectMeetingMonikaId != byte.MaxValue;

    // ─── オプション ──────────────────────────────────────
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

    // ─── IKiller / ILNKiller ─────────────────────────────
    public float CalculateKillCooldown() => KillCooldownValue;
    public bool CanUseKillButton() => Player.IsAlive() && !IsTrashed(Player);
    public bool CanUseImpostorVentButton() => OptionCanVent.GetBool() && !IsTrashed(Player);
    public bool CanUseSabotageButton() => OptionCanSabotage.GetBool() && !IsTrashed(Player);
    public override bool CanClickUseVentButton => OptionCanVent.GetBool() && !IsTrashed(Player);
    public override bool OnEnterVent(PlayerPhysics p, int id) => OptionCanVent.GetBool() && !IsTrashed(Player);

    public bool OverrideKillButton(out string text) { text = "Monika_Erase"; return true; }
    public bool OverrideKillButtonText(out string text) { text = GetString("MonikaEraseButton"); return true; }

    public override void Add()
    {
        MonikaTrashLayer.Clear();
        SelectMeetingMonikaId = byte.MaxValue;
        PendingSelectMeeting = false;

        // ★ ゴミ箱プレイヤーの名前マークを全員に見せる（ウィッチ/毒入りパン屋と同じ手法）
        //    CustomRoleManager.MarkOthers に登録すると、seer(見る側)に関係なく
        //    全プレイヤーの名前表示にマークが反映される。
        CustomRoleManager.MarkOthers.Add(GetTrashMarkOthers);
    }

    public override void OnDestroy()
    {
        MonikaTrashLayer.Clear();
        SelectMeetingMonikaId = byte.MaxValue;
        PendingSelectMeeting = false;
        CustomRoleManager.MarkOthers.Remove(GetTrashMarkOthers);
    }

    /// <summary>
    /// ゴミ箱レイヤーのプレイヤーの名前に付けるマーク（全員に見える）。
    /// ・会議中のみ 名前の「右」にモニカ色の × を付ける（タスクターン中は表示しない）。
    /// （NotifyRoles の名前構造 {RoleText}{Name}{DeathReason+Mark+Suffix} により Mark は名前直後に入る）
    /// </summary>
    public static string GetTrashMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seen == null) return "";
        if (!isForMeeting) return "";                 // ★ 会議中のみ表示（タスクターン中は非表示）
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

    // ═══════════════════════════════════════════════════════
    // 抹消（キルボタン）: 対象をゴミ箱レイヤーに送る
    // ═══════════════════════════════════════════════════════
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        (_, var target) = info.AttemptTuple;
        if (target == null) { info.DoKill = false; return; }

        // ★ 通常キルは行わない（抹消はゴミ箱送り）
        info.DoKill = false;

        // 既にゴミ箱の相手には効果なし（クールダウンは戻す）
        if (IsTrashed(target))
        {
            Player.SetKillCooldown();
            return;
        }

        // 抹消エフェクト（ガード演出）
        Player.RpcProtectedMurderPlayer(target);
        Player.RpcProtectedMurderPlayer(Player);

        SendToTrash(target);
        Player.SetKillCooldown();
    }

    /// <summary>対象をゴミ箱レイヤーに追加（ホストのみ）</summary>
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

    /// <summary>ゴミ箱プレイヤーが実際に死亡→レイヤー解除（死亡者扱いに移行）</summary>
    public static void OnPlayerActuallyDied(byte playerId)
    {
        if (MonikaTrashLayer.Remove(playerId))
        {
            Logger.Info($"[Monika] {playerId} がゴミ箱から死亡レイヤーへ移行", "Monika");
            if (AmongUsClient.Instance.AmHost) SendRpcStatic();
        }
    }

    // ═══════════════════════════════════════════════════════
    // 緊急会議ボタン消費オプション
    // ═══════════════════════════════════════════════════════
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
    public static class MonikaEmergencyConsumePatch
    {
        public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!ConsumeButton) return;
            if (target != null) return;                 // 死体通報は対象外（ボタンのみ）
            if (__instance == null || !__instance.Is(CustomRoles.Monika)) return;

            // 緊急会議を起こせる（＝本来の緊急会議回数を持つ）非インポスター・非ゴミ箱を候補に
            var candidates = AllAlivePlayerControls
                .Where(pc => pc.PlayerId != __instance.PlayerId
                          && !pc.GetCustomRole().IsImpostor()
                          && !IsTrashed(pc)
                          && CanEmergencyMeeting(pc))
                .ToList();

            if (candidates.Count == 0) return;
            var victim = candidates[IRandom.Instance.Next(candidates.Count)];

            // 本来追加されている緊急会議回数のみ消費（能力増加分は消費しない）
            var state = PlayerState.GetByPlayerId(victim.PlayerId);
            if (state != null && state.NumberOfRemainingButtons > 0)
                state.NumberOfRemainingButtons--;

            SendMessage(
                GetString("MonikaButtonConsumedNotify"),
                victim.PlayerId,
                ColorString(GetRoleColor(CustomRoles.Monika), GetString("Monika")));
        }

        // 緊急会議を起こせない役職はボタンを持たない → 消費対象外
        static bool CanEmergencyMeeting(PlayerControl pc)
        {
            var state = PlayerState.GetByPlayerId(pc.PlayerId);
            return state != null && state.NumberOfRemainingButtons > 0;
        }
    }

    // ═══════════════════════════════════════════════════════
    // 勝利条件チェック（ホストのタスクフェーズ中）
    // ═══════════════════════════════════════════════════════
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
        // ゴミ箱に居ないモニカが1人でなければ判定しない
        var aliveMonikas = AllAlivePlayerControls
            .Where(pc => pc.Is(CustomRoles.Monika) && !IsTrashed(pc))
            .ToList();
        if (aliveMonikas.Count != 1) return;
        if (aliveMonikas[0].PlayerId != Player.PlayerId) return;

        // ゴミ箱に居ない生存者（モニカ以外・無害/キル持ち両方含む）
        var nonTrashAlive = AllAlivePlayerControls
            .Where(pc => !IsTrashed(pc) && !pc.Is(CustomRoles.Monika))
            .ToList();

        // ★ 「単独として生き残ってもゲームが続行するか」= false の場合:
        //    モニカ以外のキルボタン持ち(インポスター等)が全滅し、
        //    残りが無害な生存者だけになった時点でモニカ+残り全員が勝利する。
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
            // ちょうど2名 → 特殊会議で追加勝者を選択
            if (nonTrashAlive.Count == 2)
                TriggerSelectMeeting(Player);
            // 3名以上はまだ勝利条件未達（何もしない）
        }
        else
        {
            // 0～1名 → モニカ + 残り全員が即勝利
            ExecuteWin(Player, nonTrashAlive);
        }
    }

    /// <summary>キルボタンを持つ陣営（モニカにとっての脅威）かどうか</summary>
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

    // ═══════════════════════════════════════════════════════
    // 特殊会議（ニムロッド流用: モニカが会議を発生させ、モニカの投票で勝者を選ぶ）
    // ═══════════════════════════════════════════════════════
    private static void TriggerSelectMeeting(PlayerControl monika)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (IsSelectMeeting() || PendingSelectMeeting) return;
        if (monika == null || !monika.IsAlive() || IsTrashed(monika)) return;

        // ★ 会議発生をこの時点で確定させておく（次フレーム以降の OnFixedUpdate で二重に走らないよう
        //    先に SelectMeetingMonikaId を立てる。IsSelectMeeting()==true になるので OnFixedUpdate は抜ける）
        PendingSelectMeeting = true;
        SelectMeetingMonikaId = monika.PlayerId;
        SendRpcStatic();

        UtilsGameLog.AddGameLog("Monika",
            $"{UtilsName.GetPlayerColor(monika)} の勝利条件達成。追加勝者選択の特殊会議を発生");

        // ニムロッド流: フラッシュ演出 → 少し待ってから強制会議
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

            // モニカが通報者となって会議を強制発生（Cancelcheck=false で残ボタン数に関係なく発生）
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

    // モニカの投票 = 追加勝者の選択
    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        var baseVote = base.ModifyVote(voterId, sourceVotedForId, isIntentional);
        if (!AmongUsClient.Instance.AmHost) return baseVote;
        if (!IsSelectMeeting() || SelectMeetingMonikaId != Player.PlayerId) return baseVote;
        if (voterId != Player.PlayerId) return baseVote;

        // スキップ(sourceVotedForId >= 15)以外なら追加勝者を確定
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

        // 会議を即終了させて勝利処理
        MeetingVoteManager.Instance?.ClearAndExile(Player.PlayerId, sourceVotedForId);
        ExecuteWin(Player, allies);

        return (baseVote.votedForId, baseVote.numVotes, false);
    }

    public override void OnStartMeeting()
    {
        // 会議開始時、選択会議でなければフラグをクリア
        if (!AmongUsClient.Instance.AmHost) return;
        if (!IsSelectMeeting())
        {
            PendingSelectMeeting = false;
        }
    }

    // ═══════════════════════════════════════════════════════
    // 勝利確定
    // ═══════════════════════════════════════════════════════
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

        // 通常の終了処理に委譲
        GameEndCheck();
    }

    static void GameEndCheck()
    {
        // 勝者は既に CustomWinnerHolder にセット済み。
        // 終了判定を明示的に走らせて GameEndChecker.Prefix にゲーム終了させる。
        if (GameManager.Instance != null)
            GameManager.Instance.LogicFlow.CheckEndCriteria();
    }

    public override void CheckWinner(GameOverReason reason)
    {
        // モニカ単独勝利以外では特別処理なし
    }

    // ═══════════════════════════════════════════════════════
    // RPC（ゴミ箱レイヤー & 選択会議状態の同期）
    // ═══════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════
    // 表示
    // ═══════════════════════════════════════════════════════
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

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱の × は「名前の右」だけ（Monika.GetTrashMarkOthers / MarkOthers が担当）。
//    会議中のみ表示・タスクターン中は非表示。左 × は付けない。
// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーの投票ブロック（発言権・投票権の剥奪）
//    ただしゴミ箱の人「への」投票は許可（srcのみ判定）
// ══════════════════════════════════════════════════════════════
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

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーの死体通報・緊急ボタンブロック
// ══════════════════════════════════════════════════════════════
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

// ══════════════════════════════════════════════════════════════
// ★ ゴミ箱プレイヤーが実際に死亡した時、ゴミ箱レイヤーを解除（死亡者へ移行）
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class MonikaTrashDeathPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        Monika.OnPlayerActuallyDied(__instance.PlayerId);
    }
}

// ══════════════════════════════════════════════════════════════
// ★ 視認制御:「モニカがゴミ箱のプレイヤーを見ることができるか」= オフ の場合、
//    モニカのクライアント上でのみ、タスクフェーズ中のゴミ箱プレイヤーの
//    プレイヤー本体を非表示にする（ローカルのみ・他クライアントには影響しない）。
//    ※プレイヤー実体のローカル可視制御(InvisiblePatch と同じ手法)を流用。
// ══════════════════════════════════════════════════════════════
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class MonikaTrashVisibilityPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        var local = PlayerControl.LocalPlayer;
        if (local == null || !local.Is(CustomRoles.Monika)) return;

        // オプションがオン（見える）なら常に表示に戻す
        // オフ（見えない）でも、会議中やロビー中は名前・投票に必要なので通常表示
        bool shouldHide =
            !Monika.CanSeeTrashOpt
            && GameStates.IsInTask
            && !GameStates.IsMeeting
            && Monika.IsTrashed(__instance.PlayerId)
            && __instance.PlayerId != local.PlayerId;

        var body = __instance.cosmetics?.currentBodySprite?.BodySprite;
        if (body == null) return;

        // このパッチが隠したプレイヤーを記録し、復帰も自分の責務のみに限定する
        //（他役職の透明化と競合しないため）
        if (shouldHide)
        {
            if (body.enabled)
            {
                body.enabled = false;
                __instance.cosmetics.ToggleNameVisible(false);
                _hiddenByMonika.Add(__instance.PlayerId);
            }
        }
        else
        {
            // 会議入り / CanSeeTrashオン / ゴミ箱解除 → Monikaが隠していたなら元に戻す
            if (_hiddenByMonika.Contains(__instance.PlayerId))
            {
                body.enabled = true;
                __instance.cosmetics.ToggleNameVisible(true);
                _hiddenByMonika.Remove(__instance.PlayerId);
            }
        }
    }

    static readonly HashSet<byte> _hiddenByMonika = new();
}
