/*using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Neutral;

internal static class ThreePigsData
{
    static readonly List<byte> members = new();
    public static IReadOnlyList<byte> Members => members;

    public static void AddMember(byte id)
    {
        if (!members.Contains(id)) members.Add(id);
    }

    public static bool IsTeammate(byte a, byte b)
        => a != b && members.Contains(a) && members.Contains(b);

    public static bool IsTaskConditionMet(RoleBase role, int percent)
    {
        var state = role.MyTaskState;
        if (state == null || state.AllTasksCount <= 0) return false;
        int need = Mathf.CeilToInt(state.AllTasksCount * percent / 100f);
        return state.CompletedTasksCount >= need;
    }

    [Attributes.GameModuleInitializer]
    public static void Init() => members.Clear();
}

public sealed class TheFirstLittlePig : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(TheFirstLittlePig),
            player => new TheFirstLittlePig(player),
            CustomRoles.TheFirstLittlePig,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            55700,
            SetupOptionItem,
            "fp",
            "#ff637b",
            (7, 4),
            true,
            countType: CountTypes.None,
            from: From.SuperNewRoles,
            tab: TabGroup.Combinations,
            assignInfo: new RoleAssignInfo(CustomRoles.TheFirstLittlePig, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = new CustomRoles[3]
                {
                    CustomRoles.TheFirstLittlePig,
                    CustomRoles.TheSecondLittlePig,
                    CustomRoles.TheThirdLittlePig
                }
            },
            combination: CombinationRoles.TheThreeLittlePigs
        );

    public TheFirstLittlePig(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        flashTimer = 0f;
        conditionMet = false;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    // Pig1
    public static OptionItem OptionPig1TaskPercent;
    public static OptionItem OptionPig1FlashInterval;
    // Pig2
    public static OptionItem OptionPig2TaskPercent;
    public static OptionItem OptionPig2GuardCount;
    // Pig3
    public static OptionItem OptionPig3TaskPercent;
    public static OptionItem OptionPig3CounterCount;

    public static int Pig1TaskPercent;
    public static float Pig1FlashInterval;
    public static int Pig2TaskPercent;
    public static int Pig2GuardCount;
    public static int Pig3TaskPercent;
    public static int Pig3CounterCount;

    enum OptionName
    {
        TheFirstLittlePigTaskPercent,
        TheFirstLittlePigFlashInterval,
        TheSecondLittlePigTaskPercent,
        TheSecondLittlePigGuardCount,
        TheThirdLittlePigTaskPercent,
        TheThirdLittlePigCounterCount,
    }

    static void SetupOptionItem()
    {
        // 1番目の仔豚設定
        OptionPig1TaskPercent = IntegerOptionItem.Create(RoleInfo, 10, OptionName.TheFirstLittlePigTaskPercent,
            new(0, 100, 5), 30, false).SetValueFormat(OptionFormat.Percent);
        OptionPig1FlashInterval = FloatOptionItem.Create(RoleInfo, 11, OptionName.TheFirstLittlePigFlashInterval,
            new(5f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);

        // 2番目の仔豚設定
        ObjectOptionitem.Create(RoleInfo, 12, "TheSecondLittlePigSetting", true, null)
            .SetOptionName(() => "2nd Pig Setting");
        OptionPig2TaskPercent = IntegerOptionItem.Create(RoleInfo, 13, OptionName.TheSecondLittlePigTaskPercent,
            new(0, 100, 5), 60, false).SetValueFormat(OptionFormat.Percent);
        OptionPig2GuardCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.TheSecondLittlePigGuardCount,
            new(1, 15, 1), 1, false).SetValueFormat(OptionFormat.Times);

        // 3番目の仔豚設定
        ObjectOptionitem.Create(RoleInfo, 15, "TheThirdLittlePigSetting", true, null)
            .SetOptionName(() => "3rd Pig Setting");
        OptionPig3TaskPercent = IntegerOptionItem.Create(RoleInfo, 16, OptionName.TheThirdLittlePigTaskPercent,
            new(0, 100, 5), 100, false).SetValueFormat(OptionFormat.Percent);
        OptionPig3CounterCount = IntegerOptionItem.Create(RoleInfo, 17, OptionName.TheThirdLittlePigCounterCount,
            new(1, 15, 1), 1, false).SetValueFormat(OptionFormat.Times);

        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void Add()
    {
        Pig1TaskPercent = OptionPig1TaskPercent.GetInt();
        Pig1FlashInterval = OptionPig1FlashInterval.GetFloat();
        Pig2TaskPercent = OptionPig2TaskPercent.GetInt();
        Pig2GuardCount = OptionPig2GuardCount.GetInt();
        Pig3TaskPercent = OptionPig3TaskPercent.GetInt();
        Pig3CounterCount = OptionPig3CounterCount.GetInt();

        ThreePigsData.AddMember(Player.PlayerId);
        flashTimer = Pig1FlashInterval;
        conditionMet = false;
    }

    float flashTimer;
    bool conditionMet;

    public override void OnSpawn(bool initialState = false)
    {
        _ = new LateTask(() =>
        {
            foreach (var id in ThreePigsData.Members)
            {
                if (id != Player.PlayerId)
                    NameColorManager.Add(Player.PlayerId, id, RoleInfo.RoleColorCode);
            }
        }, 2f, "ThreePigs.P1.NameColor", true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive() || GameStates.IsMeeting || !GameStates.IsInTask) return;

        bool met = ThreePigsData.IsTaskConditionMet(this, Pig1TaskPercent);
        if (!met) { conditionMet = false; return; }

        if (!conditionMet)
        {
            conditionMet = true;
            SendMessage(GetString("TheFirstLittlePigAbilityReady"), Player.PlayerId);
        }

        flashTimer -= Time.fixedDeltaTime;
        if (flashTimer <= 0f)
        {
            flashTimer = Pig1FlashInterval;
            Utils.AllPlayerKillFlash();
            SendMessage(GetString("TheFirstLittlePigFlash"), Player.PlayerId);
        }
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor) return false;
        return Player.IsAlive();
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId == seen.PlayerId) return "";
        if (!ThreePigsData.IsTeammate(seer.PlayerId, seen.PlayerId)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>①</color>"
            : $" <color=#888888>①</color>";
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!seer.Is(CustomRoles.TheFirstLittlePig)) return "";
        return "";
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (ThreePigsData.IsTeammate(Player.PlayerId, seen.PlayerId))
            enabled = true;
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int total = MyTaskState?.AllTasksCount ?? 0;
        if (total <= 0) return "";
        int need = Mathf.CeilToInt(total * Pig1TaskPercent / 100f);
        int done = MyTaskState.CompletedTasksCount;
        string c = done >= need ? RoleInfo.RoleColorCode : "#888888";
        return $"<color={c}>({done}/{need})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";
        bool met = ThreePigsData.IsTaskConditionMet(this, Pig1TaskPercent);
        return met
            ? $"{size}<color={RoleInfo.RoleColorCode}>【警戒フラッシュ発動中】</color>"
            : $"{size}<color=#888888>タスクを達成するとフラッシュが発動</color>";
    }
}

public sealed class TheSecondLittlePig : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(TheSecondLittlePig),
            player => new TheSecondLittlePig(player),
            CustomRoles.TheSecondLittlePig,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            570600,
            null,
            "sp",
            "#ff637b",
            (7, 5),
            true,
            countType: CountTypes.None,
            from: From.SuperNewRoles,
            tab: TabGroup.Combinations,
            combination: CombinationRoles.TheThreeLittlePigs
        );

    public TheSecondLittlePig(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        remainGuards = 0;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    int remainGuards;

    public override void Add()
    {
        ThreePigsData.AddMember(Player.PlayerId);
        remainGuards = TheFirstLittlePig.Pig2GuardCount;
    }

    public override void OnSpawn(bool initialState = false)
    {
        _ = new LateTask(() =>
        {
            foreach (var id in ThreePigsData.Members)
            {
                if (id != Player.PlayerId)
                    NameColorManager.Add(Player.PlayerId, id, RoleInfo.RoleColorCode);
            }
        }, 2f, "ThreePigs.P2.NameColor", true);
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (remainGuards <= 0) return true;
        if (!ThreePigsData.IsTaskConditionMet(this, TheFirstLittlePig.Pig2TaskPercent)) return true;

        (var killer, var target) = info.AttemptTuple;
        remainGuards--;
        info.DoKill = false;

        killer.RpcProtectedMurderPlayer(target);
        target.RpcProtectedMurderPlayer(target);
        killer.ResetKillCooldown();

        SendMessage(
            string.Format(GetString("TheSecondLittlePigGuarded"), remainGuards),
            Player.PlayerId);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return true;
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor) return false;
        return Player.IsAlive();
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId == seen.PlayerId) return "";
        if (!ThreePigsData.IsTeammate(seer.PlayerId, seen.PlayerId)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>②</color>"
            : $" <color=#888888>②</color>";
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.PlayerId == seen.PlayerId) return "";
        if (!seer.Is(CustomRoles.TheFirstLittlePig) && !seer.Is(CustomRoles.TheThirdLittlePig)) return "";
        if (!ThreePigsData.IsTeammate(seer.PlayerId, seen.PlayerId)) return "";
        if (!seen.Is(CustomRoles.TheSecondLittlePig)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>②</color>"
            : $" <color=#888888>②</color>";
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (ThreePigsData.IsTeammate(Player.PlayerId, seen.PlayerId))
            enabled = true;
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int total = MyTaskState?.AllTasksCount ?? 0;
        if (total <= 0) return "";
        int need = Mathf.CeilToInt(total * TheFirstLittlePig.Pig2TaskPercent / 100f);
        int done = MyTaskState.CompletedTasksCount;
        string c = done >= need ? RoleInfo.RoleColorCode : "#888888";
        return $"<color={c}>({done}/{need}) ガード:{remainGuards}</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";
        bool met = ThreePigsData.IsTaskConditionMet(this, TheFirstLittlePig.Pig2TaskPercent);
        return met
            ? $"{size}<color={RoleInfo.RoleColorCode}>【キルガード発動中】残り{remainGuards}回</color>"
            : $"{size}<color=#888888>タスクを達成するとガードが発動</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(remainGuards);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainGuards = reader.ReadInt32();
    }
}

public sealed class TheThirdLittlePig : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(TheThirdLittlePig),
            player => new TheThirdLittlePig(player),
            CustomRoles.TheThirdLittlePig,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            570700,
            null,
            "tp",
            "#ff637b",
            (7, 6),
            true,
            countType: CountTypes.None,
            from: From.SuperNewRoles,
            tab: TabGroup.Combinations,
            combination: CombinationRoles.TheThreeLittlePigs
        );

    public TheThirdLittlePig(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        remainCounters = 0;
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    int remainCounters;

    public override void Add()
    {
        ThreePigsData.AddMember(Player.PlayerId);
        remainCounters = TheFirstLittlePig.Pig3CounterCount;
    }

    public override void OnSpawn(bool initialState = false)
    {
        _ = new LateTask(() =>
        {
            foreach (var id in ThreePigsData.Members)
            {
                if (id != Player.PlayerId)
                    NameColorManager.Add(Player.PlayerId, id, RoleInfo.RoleColorCode);
            }
        }, 2f, "ThreePigs.P3.NameColor", true);
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (remainCounters <= 0) return true;
        if (!ThreePigsData.IsTaskConditionMet(this, TheFirstLittlePig.Pig3TaskPercent)) return true;

        (var killer, var target) = info.AttemptTuple;
        remainCounters--;
        info.DoKill = false;

        killer.RpcProtectedMurderPlayer(target);
        target.RpcProtectedMurderPlayer(target);
        killer.ResetKillCooldown();

        if (AmongUsClient.Instance.AmHost && killer.IsAlive())
        {
            var state = PlayerState.GetByPlayerId(killer.PlayerId);
            if (state != null) state.DeathReason = CustomDeathReason.Counter;
            killer.SetRealKiller(Player);
            Player.RpcMurderPlayer(killer);
        }

        SendMessage(
            string.Format(GetString("TheThirdLittlePigCountered"), remainCounters),
            Player.PlayerId);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return true;
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor) return false;
        return Player.IsAlive();
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId == seen.PlayerId) return "";
        if (!ThreePigsData.IsTeammate(seer.PlayerId, seen.PlayerId)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>③</color>"
            : $" <color=#888888>③</color>";
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.PlayerId == seen.PlayerId) return "";
        if (!seer.Is(CustomRoles.TheFirstLittlePig) && !seer.Is(CustomRoles.TheSecondLittlePig)) return "";
        if (!ThreePigsData.IsTeammate(seer.PlayerId, seen.PlayerId)) return "";
        if (!seen.Is(CustomRoles.TheThirdLittlePig)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>③</color>"
            : $" <color=#888888>③</color>";
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (ThreePigsData.IsTeammate(Player.PlayerId, seen.PlayerId))
            enabled = true;
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int total = MyTaskState?.AllTasksCount ?? 0;
        if (total <= 0) return "";
        int need = Mathf.CeilToInt(total * TheFirstLittlePig.Pig3TaskPercent / 100f);
        int done = MyTaskState.CompletedTasksCount;
        string c = done >= need ? RoleInfo.RoleColorCode : "#888888";
        return $"<color={c}>({done}/{need}) カウンター:{remainCounters}</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        string size = isForHud ? "" : "<size=60%>";
        bool met = ThreePigsData.IsTaskConditionMet(this, TheFirstLittlePig.Pig3TaskPercent);
        return met
            ? $"{size}<color={RoleInfo.RoleColorCode}>【カウンター発動中】残り{remainCounters}回</color>"
            : $"{size}<color=#888888>タスクを達成するとカウンターが発動</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(remainCounters);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainCounters = reader.ReadInt32();
    }
}*/