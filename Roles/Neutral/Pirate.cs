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

public sealed class Pirate : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Pirate),
            player => new Pirate(player),
            CustomRoles.Pirate,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            184800,
            SetupOptionItem,
            "pi",
            "#cc4b33",
            (6, 5),
            true,
            from: From.TownOfHost_Y,
            assignInfo: new RoleAssignInfo(CustomRoles.Pirate, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );

    public Pirate(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown_ = OptionKillCooldown.GetFloat();
        HasImpostorVision_ = OptionHasImpostorVision.GetBool();
        LimitTurn = OptionLimitTurn.GetInt();
        CanImpostorBeGang = OptionCanImpostorBeGang.GetBool();
        CanMadmateBeGang = OptionCanMadmateBeGang.GetBool();
        CanNeutralBeGang = OptionCanNeutralBeGang.GetBool();
        GangBuffAddons = OptionGangBuffAddon.GetNowRoleValue();

        gangPlayerId = byte.MaxValue;
        isMadeGang = false;
        turnNumber = 1;
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionLimitTurn;
    static OptionItem OptionCanImpostorBeGang;
    static OptionItem OptionCanMadmateBeGang;
    static OptionItem OptionCanNeutralBeGang;
    static AssignOptionItem OptionGangBuffAddon;

    static float KillCooldown_;
    static bool HasImpostorVision_;
    static int LimitTurn;
    static bool CanImpostorBeGang;
    static bool CanMadmateBeGang;
    static bool CanNeutralBeGang;

    public byte gangPlayerId;
    public bool isMadeGang;
    int turnNumber;

    public static List<CustomRoles> GangBuffAddons = new();

    public static readonly CustomRoles[] DefaultBuffAddons =
    [
        CustomRoles.Autopsy,
        CustomRoles.Lighting,
        CustomRoles.Moon,
        CustomRoles.Guesser,
        CustomRoles.Tiebreaker,
        CustomRoles.Opener,
        CustomRoles.Management,
        CustomRoles.Speeding,
        CustomRoles.MagicHand,
        CustomRoles.Serial,
        CustomRoles.Powerful,
        CustomRoles.PlusVote,
        CustomRoles.Seeing,
        CustomRoles.Sunglasses,
        CustomRoles.Elector,
        CustomRoles.Water,
    ];

    public static readonly CustomRoles[] RemoveAddon =
    [
        CustomRoles.Amnesia,
        CustomRoles.Workhorse,
        CustomRoles.LastImpostor,
        CustomRoles.LastNeutral,
        CustomRoles.Stack,
        CustomRoles.Jumbo,
        CustomRoles.Stamina,
        CustomRoles.Twins,
        CustomRoles.Triplets,
        CustomRoles.OneWolf,
        CustomRoles.Connecting,
    ];

    enum OptionName
    {
        PirateLimitTurn,
        PirateCanImpostorBeGang,
        PirateCanMadmateBeGang,
        PirateCanNeutralBeGang,
        PirateGangBuffAddon,
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 15);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new(0f, 180f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.ImpostorVision, true, false);
        OptionLimitTurn = IntegerOptionItem.Create(RoleInfo, 12, OptionName.PirateLimitTurn,
            new(1, 30, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionCanImpostorBeGang = BooleanOptionItem.Create(RoleInfo, 13, OptionName.PirateCanImpostorBeGang, false, false);
        OptionCanMadmateBeGang = BooleanOptionItem.Create(RoleInfo, 14, OptionName.PirateCanMadmateBeGang, true, false);
        OptionCanNeutralBeGang = BooleanOptionItem.Create(RoleInfo, 15, OptionName.PirateCanNeutralBeGang, true, false);
        OptionGangBuffAddon = AssignOptionItem.Create(
            RoleInfo, 16, OptionName.PirateGangBuffAddon,
            0, false, null,
            true, true, true, true, true,
            RemoveAddon);

        Gang.HideRoleOptions(CustomRoles.Gang);
    }

    public PlayerControl GetGang() =>
        gangPlayerId == byte.MaxValue ? null : GetPlayerById(gangPlayerId);

    public float CalculateKillCooldown() => KillCooldown_;

    public bool CanUseKillButton()
    {
        if (!Player.IsAlive()) return false;
        if (!isMadeGang) return true;
        return (GetGang()?.GetRoleClass() as Gang)?.CanKill ?? false;
    }

    public bool CanUseSabotageButton() => false;

    public bool CanUseImpostorVentButton()
    {
        if (!isMadeGang) return false;
        return (GetGang()?.GetRoleClass() as Gang)?.CanVent ?? false;
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = isMadeGang ? "キル" : "一味にする";
        return true;
    }

    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision_);

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (!isMadeGang)
        {
            info.DoKill = false;
            if (!CanBeGang(target))
            {
                killer.RpcProtectedMurderPlayer(target);
                return;
            }
            CreateGang(target);
            return;
        }

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
    }

    bool CanBeGang(PlayerControl target)
    {
        if (!CanImpostorBeGang && target.Is(CustomRoleTypes.Impostor)) return false;
        if (!CanMadmateBeGang && target.Is(CustomRoleTypes.Madmate)) return false;
        if (!CanNeutralBeGang && target.Is(CustomRoleTypes.Neutral)) return false;
        return true;
    }

    void CreateGang(PlayerControl target)
    {
        isMadeGang = true;
        gangPlayerId = target.PlayerId;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(CustomRoles.Gang, log: null);

        _ = new LateTask(() =>
        {
            if (target.GetRoleClass() is Gang gang)
                gang.SetOwner(Player.PlayerId);
        }, 0.2f, "Pirate.SetGangOwner", true);

        SendRpc();
        UtilsGameLog.AddGameLog("Pirate",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} を一味にした");
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Pirate.Notify", true);
    }

    public override void OnStartMeeting() => turnNumber++;

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (!isMadeGang && turnNumber > LimitTurn)
        {
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Suicide, Player.PlayerId);
            Logger.Info($"[Pirate] ターン超過自殺 Turn:{turnNumber} > {LimitTurn}", "Pirate");
        }
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive() || !isMadeGang) return;
        var gang = GetGang();
        if (gang == null || !gang.IsAlive()) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Pirate, Player.PlayerId, true))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.NeutralWinnerIds.Add(gangPlayerId);
            CustomWinnerHolder.WinnerIds.Add(gangPlayerId);
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (!isMadeGang)
            return $"<color={RoleInfo.RoleColorCode}>[{turnNumber}/{LimitTurn}]</color>";

        var gangRole = GetGang()?.GetRoleClass() as Gang;
        int pct = gangRole?.TaskPercent ?? 0;
        return $"<color={RoleInfo.RoleColorCode}>(一味:{pct}%)</color>";
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || gangPlayerId == byte.MaxValue || seen.PlayerId != gangPlayerId) return "";
        return $" <color={RoleInfo.RoleColorCode}>▲</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(gangPlayerId);
        sender.Writer.Write(isMadeGang);
        sender.Writer.Write(turnNumber);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        gangPlayerId = reader.ReadByte();
        isMadeGang = reader.ReadBoolean();
        turnNumber = reader.ReadInt32();
    }
}

public sealed class Gang : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gang),
            player => new Gang(player),
            CustomRoles.Gang,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            184900,
            SetupOptionItem,
            "gng",
            "#cc4b33",
            (6, 5),
            countType: CountTypes.OutOfGame,
            from: From.TownOfHost_Y
        );

    public Gang(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        OwnerId = byte.MaxValue;
        CanVent = false;
        CanKill = false;
        hasGrantedAddon = false;
        hasSeenImpostors = false;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    static void SetupOptionItem() => HideRoleOptions(CustomRoles.Gang);

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

    public Pirate GetOwner() =>
        OwnerId == byte.MaxValue ? null : GetPlayerById(OwnerId)?.GetRoleClass() as Pirate;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player != Player || !Player.IsAlive()) return;
        if (!GameStates.IsInTask || OwnerId == byte.MaxValue) return;

        var owner = GetPlayerById(OwnerId);
        if (owner == null || !owner.IsAlive() || owner.GetRoleClass() is not Pirate)
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

        if (!CanVent && pct >= 25)
        {
            CanVent = true;
            Player.RpcSetRoleDesync(RoleTypes.Engineer, Player.GetClientId());
            Utils.SendMessage(GetString("GangVentUnlocked"), Player.PlayerId);
        }

        if (!CanKill && pct >= 50)
        {
            CanKill = true;
            Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
            foreach (var imp in AllAlivePlayerControls.Where(p => p.GetCustomRole().IsImpostor()))
                imp.RpcSetRoleDesync(RoleTypes.Scientist, Player.GetClientId());
            Player.SetKillCooldown();
            Utils.SendMessage(GetString("GangKillUnlocked"), Player.PlayerId);
        }

        if (!hasGrantedAddon && pct >= 75)
        {
            hasGrantedAddon = true;
            var pirate = GetOwner();
            if (pirate != null && pirate.Player.IsAlive())
            {
                var pool = Pirate.GangBuffAddons;
                CustomRoles addon = pool.Count > 0
                    ? pool[IRandom.Instance.Next(pool.Count)]
                    : Pirate.DefaultBuffAddons[IRandom.Instance.Next(Pirate.DefaultBuffAddons.Length)];

                pirate.Player.RpcSetCustomRole(addon);
                Utils.SendMessage(
                    string.Format(GetString("GangAddonGranted"), UtilsRoleText.GetRoleColorAndtext(addon)),
                    pirate.Player.PlayerId);
                Logger.Info($"[Gang] 海賊に属性付与: {addon}", "Gang");
            }
        }

        if (!hasSeenImpostors && MyTaskState.IsTaskFinished)
        {
            hasSeenImpostors = true;
            foreach (var imp in AllAlivePlayerControls.Where(p => p.GetCustomRole().IsImpostor()))
                NameColorManager.Add(Player.PlayerId, imp.PlayerId);
            Utils.SendMessage(GetString("GangAllTasksDone"), Player.PlayerId);
        }

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

        if (seer.GetRoleClass() is Pirate pirate && pirate.gangPlayerId == seen.PlayerId)
        {
            var g = seen.GetRoleClass() as Gang;
            if (g == null) return "";
            string marks = "";
            if (g.CanVent) marks += "<color=#00ffff>Ｖ</color>";
            if (g.CanKill) marks += $"<color={RoleInfo.RoleColorCode}>Ｋ</color>";
            if (g.hasGrantedAddon) marks += "<color=#ffff00>Ａ</color>";
            if (g.hasSeenImpostors) marks += "<color=#ff0000>Ｉ</color>";
            return marks != "" ? $" {marks}" : "";
        }

        if (seer.GetRoleClass() is Gang gang && gang.OwnerId == seen.PlayerId)
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