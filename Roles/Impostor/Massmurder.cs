using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

/// <summary>
/// マスマーダー (Mass Murder)
/// インポスター役職。ファントムボタンで現在の部屋を「死の床」に設定。
/// 死の床内でキル → キルクール大幅短縮
/// 死の床外でキル → キルクール増加
/// </summary>
public sealed class MassMurder : RoleBase, IKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MassMurder),
            player => new MassMurder(player),
            CustomRoles.MassMurder,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            15000,
            SetupOptionItem,
            "mm",
            "#8b0000",
            (8, 8),
            true,
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public MassMurder(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();
        DeathBedKillCooldown = OptionDeathBedKillCooldown.GetFloat();
        OutsideKillCooldown = OptionOutsideKillCooldown.GetFloat();
        deathBedRoom = null;
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionDeathBedKillCooldown;
    static OptionItem OptionOutsideKillCooldown;
    static OptionItem OptionDeathBedSetCount;   // ★ 追加

    static float KillCooldown_;
    static float DeathBedKillCooldown;
    static float OutsideKillCooldown;
    static int DeathBedSetCount;                // ★ 追加

    // 死の床として設定された部屋
    SystemTypes? deathBedRoom;
    int remainingSetCount;                      // ★ 追加: 残り設定回数

    enum OptionName
    {
        MassMurderDeathBedKillCooldown,
        MassMurderOutsideKillCooldown,
        MassMurderDeathBedSetCount,             // ★ 追加
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 1f), 35f, false).SetValueFormat(OptionFormat.Seconds);
        OptionDeathBedKillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.MassMurderDeathBedKillCooldown,
            new(0f, 180f, 0.5f), 0f, false).SetValueFormat(OptionFormat.Seconds);
        OptionOutsideKillCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.MassMurderOutsideKillCooldown,
            new(0f, 180f, 1f), 45f, false).SetValueFormat(OptionFormat.Seconds);
        // ★ 死の床設定回数
        OptionDeathBedSetCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.MassMurderDeathBedSetCount,
            new(1, 100, 1), 2, false).SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();
        DeathBedKillCooldown = OptionDeathBedKillCooldown.GetFloat();
        OutsideKillCooldown = OptionOutsideKillCooldown.GetFloat();
        DeathBedSetCount = OptionDeathBedSetCount.GetInt();  // ★
        deathBedRoom = null;
        remainingSetCount = DeathBedSetCount;                 // ★
    }

    // ── IUsePhantomButton ─────────────────────────────────────────
    public bool IsPhantomRole => true;
    public bool IsresetAfterKill => false;
    public bool UseOneclickButton => true;  // ワンクリックで発動

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = KillCooldown_;
        AURoleOptions.PhantomDuration = 0f;  // 透明化しない
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (!Player.IsAlive()) return;

        // ★ 設定回数が残っていなければ何もしない
        if (remainingSetCount <= 0)
        {
            Logger.Info($"[MassMurder] 設定回数が残っていません", "MassMurder");
            return;
        }

        // 現在の部屋を取得（部屋外なら何もしない・設定回数も消費しない）
        var room = Player.GetPlainShipRoom();
        if (room == null)
        {
            Logger.Info($"[MassMurder] 部屋外のためスキップ", "MassMurder");
            return;
        }

        remainingSetCount--;          // ★ 消費
        deathBedRoom = room.RoomId;
        ResetCooldown = true;

        SendRpc();
        Logger.Info($"[MassMurder] {Player.GetNameWithRole()} → 死の床: {deathBedRoom} (残{remainingSetCount}回)", "MassMurder");
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    // ── IKiller ───────────────────────────────────────────────────
    public float CalculateKillCooldown() => KillCooldown_;
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => false;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller)) return;
        if (!deathBedRoom.HasValue) return;  // 死の床未設定: クール変動なし

        var (killer, target) = info.AttemptTuple;

        var killerRoom = killer.GetPlainShipRoom();
        bool inDeathBed = killerRoom != null && killerRoom.RoomId == deathBedRoom.Value;

        // キル後にクールを上書き
        float cd = inDeathBed ? DeathBedKillCooldown : OutsideKillCooldown;
        _ = new LateTask(() =>
        {
            if (killer.IsAlive())
                killer.SetKillCooldown(Mathf.Max(cd, 0.1f));
        }, 0.1f, "MassMurder.AdjustCD", true);

        UtilsGameLog.AddGameLog("MassMurder",
            $"{UtilsName.GetPlayerColor(killer)} → {UtilsName.GetPlayerColor(target)}" +
            $" [{(inDeathBed ? "死の床" : "通常")}] → CD:{cd}s");
    }

    public override void AfterMeetingTasks()
    {
        // 死の床は会議をまたいで持続。クールだけリセット。
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        Player.RpcResetAbilityCooldown();
    }

    // ── 表示 ──────────────────────────────────────────────────────
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        // ★ 残り設定回数を表示
        string countStr = $"({remainingSetCount})";
        if (!deathBedRoom.HasValue)
            return $"<color={RoleInfo.RoleColorCode}>床:未設定 {countStr}</color>";
        return $"<color={RoleInfo.RoleColorCode}>[床:{deathBedRoom.Value}] {countStr}</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!deathBedRoom.HasValue)
            return $"{size}<color={color}>ファントムボタン → 今いる部屋を死の床に設定 (残{remainingSetCount}回)</color>";

        string canSet = remainingSetCount > 0
            ? $"再設定可 残{remainingSetCount}回 | "
            : "設定回数なし | ";
        return $"{size}<color={color}>死の床: {deathBedRoom.Value} | {canSet}" +
               $"床内CD:{DeathBedKillCooldown}s / 床外CD:{OutsideKillCooldown}s</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(deathBedRoom.HasValue);
        if (deathBedRoom.HasValue)
            sender.Writer.Write((int)deathBedRoom.Value);
        sender.Writer.Write(remainingSetCount);  // ★
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        bool hasRoom = reader.ReadBoolean();
        deathBedRoom = hasRoom ? (SystemTypes?)((SystemTypes)reader.ReadInt32()) : null;
        remainingSetCount = reader.ReadInt32();  // ★
    }
}