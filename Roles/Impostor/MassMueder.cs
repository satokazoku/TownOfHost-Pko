using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class MassMueder : RoleBase, IKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MassMueder),
            player => new MassMueder(player),
            CustomRoles.MassMurder,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            15000,
            SetupOptionItem,
            "mm",
            OptionSort: (6, 8),
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );

    public MassMueder(PlayerControl player)
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
    static OptionItem OptionDeathBedSetCount;

    static float KillCooldown_;
    static float DeathBedKillCooldown;
    static float OutsideKillCooldown;
    static int DeathBedSetCount;

    SystemTypes? deathBedRoom;
    int remainingSetCount;

    enum OptionName
    {
        MassMurderDeathBedKillCooldown,
        MassMurderOutsideKillCooldown,
        MassMurderDeathBedSetCount,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 1f), 35f, false).SetValueFormat(OptionFormat.Seconds);
        OptionDeathBedKillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.MassMurderDeathBedKillCooldown,
            new(0f, 180f, 0.5f), 0f, false).SetValueFormat(OptionFormat.Seconds);
        OptionOutsideKillCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.MassMurderOutsideKillCooldown,
            new(0f, 180f, 1f), 45f, false).SetValueFormat(OptionFormat.Seconds);
        OptionDeathBedSetCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.MassMurderDeathBedSetCount,
            new(1, 100, 1), 2, false).SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();
        DeathBedKillCooldown = OptionDeathBedKillCooldown.GetFloat();
        OutsideKillCooldown = OptionOutsideKillCooldown.GetFloat();
        DeathBedSetCount = OptionDeathBedSetCount.GetInt();
        deathBedRoom = null;
        remainingSetCount = DeathBedSetCount;
    }

    public bool IsPhantomRole => true;
    public bool IsresetAfterKill => false;
    public bool UseOneclickButton => true;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = KillCooldown_;
        AURoleOptions.PhantomDuration = 0f; 
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (!Player.IsAlive()) return;

        if (remainingSetCount <= 0)
        {
            Logger.Info($"[MassMurder] 設定回数が残っていません", "MassMurder");
            return;
        }

        var room = Player.GetPlainShipRoom();
        if (room == null)
        {
            Logger.Info($"[MassMurder] 部屋外のためスキップ", "MassMurder");
            return;
        }

        remainingSetCount--;
        deathBedRoom = room.RoomId;
        ResetCooldown = true;

        SendRpc();
        Logger.Info($"[MassMurder] {Player.GetNameWithRole()} → 死の床: {deathBedRoom} (残{remainingSetCount}回)", "MassMurder");
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public float CalculateKillCooldown() => KillCooldown_;
    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => false;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller)) return;
        if (!deathBedRoom.HasValue) return;

        var (killer, target) = info.AttemptTuple;

        var killerRoom = killer.GetPlainShipRoom();
        bool inDeathBed = killerRoom != null && killerRoom.RoomId == deathBedRoom.Value;

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
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        Player.RpcResetAbilityCooldown();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
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
        sender.Writer.Write(remainingSetCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        bool hasRoom = reader.ReadBoolean();
        deathBedRoom = hasRoom ? (SystemTypes?)((SystemTypes)reader.ReadInt32()) : null;
        remainingSetCount = reader.ReadInt32();
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "MassMurder_Ability";
        return true;
    }
}