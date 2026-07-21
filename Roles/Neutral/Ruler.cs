using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Modules.ChatManager;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Modules.SelfVoteManager;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Ruler : RoleBase, ISelfVoter, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Ruler),
            player => new Ruler(player),
            CustomRoles.Ruler,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            56100,
            SetupOptionItem,
            "Ru",
            "#f0e030",
            (5, 7),
            true,
            countType: CountTypes.Crew,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.TownOfHost_Pko
        );

    static readonly HashSet<Ruler> Instances = new();

    static OptionItem OptionRequiredRuleUseCount;
    static OptionItem OptionRuleAddLimit;
    static OptionItem OptionSoloWin;
    static OptionItem OptionShowAliveNotice;
    static OptionItem OptionPassByPlayerCount;
    static OptionItem OptionKillCooldownDecrease;
    static OptionItem OptionKillCooldownIncrease;
    static OptionItem OptionVisionDecreaseRate;
    static OptionItem OptionRandomTogetherSeconds;
    const float PassByTrackingGraceSeconds = 3f;
    const float RuleContactDistance = 1.5f;
    const string RuleMessageTitle = "<#f0e030>【===== Rule =====】</color>";

    bool isSelectingTarget;
    byte ruleChooserTargetId;
    byte pendingRuleChooserId;
    byte activeRuleChooserId;
    RulerRule pendingRule;
    RulerRule activeRule;
    float passByTrackingReadyAt;
    byte randomTogetherTargetId;
    bool gameEndRuleApplied;
    int usedRuleCount;
    readonly List<RulerRule> ruleCandidates;
    readonly Dictionary<byte, HashSet<byte>> crossedPlayers;
    readonly Dictionary<byte, float> randomTogetherTimers;

    enum OptionName
    {
        RulerRequiredRuleUseCount,
        RulerRuleAddLimit,
        RulerSoloWin,
        RulerShowAliveNotice,
        RulerPassByPlayerCount,
        RulerKillCooldownDecrease,
        RulerKillCooldownIncrease,
        RulerVisionDecreaseRate,
        RulerRandomTogetherSeconds,
    }

    enum RPCType
    {
        SyncState,
    }

    enum RulerRule
    {
        None = 0,
        PassByPlayers = 1,
        KillCooldownDecrease = 2,
        KillCooldownIncrease = 3,
        CrewVisionDecrease = 4,
        NonCrewVisionDecrease = 5,
        GameEndAllEmptiness = 6,
        GameEndAllWin = 7,
        RandomTogetherDeath = 8,
    }

    public Ruler(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        isSelectingTarget = false;
        ruleChooserTargetId = byte.MaxValue;
        pendingRuleChooserId = byte.MaxValue;
        activeRuleChooserId = byte.MaxValue;
        pendingRule = RulerRule.None;
        activeRule = RulerRule.None;
        passByTrackingReadyAt = 0f;
        randomTogetherTargetId = byte.MaxValue;
        gameEndRuleApplied = false;
        usedRuleCount = 0;
        ruleCandidates = new();
        crossedPlayers = new();
        randomTogetherTimers = new();
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, CustomRoles.Ruler, () => OptionSoloWin?.GetBool() == true, defo: 15);
        OptionRequiredRuleUseCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.RulerRequiredRuleUseCount, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
        OptionRuleAddLimit = IntegerOptionItem.Create(RoleInfo, 17, OptionName.RulerRuleAddLimit, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
        OptionSoloWin = BooleanOptionItem.Create(RoleInfo, 11, OptionName.RulerSoloWin, false, false);
        OptionShowAliveNotice = BooleanOptionItem.Create(RoleInfo, 12, OptionName.RulerShowAliveNotice, true, false);
        OptionPassByPlayerCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.RulerPassByPlayerCount, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Players);
        OptionKillCooldownDecrease = FloatOptionItem.Create(RoleInfo, 14, OptionName.RulerKillCooldownDecrease, new(2.5f, 60f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillCooldownIncrease = FloatOptionItem.Create(RoleInfo, 15, OptionName.RulerKillCooldownIncrease, new(2.5f, 60f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionVisionDecreaseRate = IntegerOptionItem.Create(RoleInfo, 16, OptionName.RulerVisionDecreaseRate, new(5, 100, 5), 50, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionRandomTogetherSeconds = FloatOptionItem.Create(RoleInfo, 18, OptionName.RulerRandomTogetherSeconds, new(2.5f, 60f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideTasksData.Create(RoleInfo, 20, tasks: (true, 1, 1, 1));
    }

    [Attributes.GameModuleInitializer]
    public static void Init()
    {
        Instances.Clear();
    }

    public override void Add()
    {
        Instances.Add(this);
        CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
        SendRPC();
    }

    public override void OnDestroy()
    {
        Instances.Remove(this);
        if (Instances.Count == 0)
        {
            CustomRoleManager.OnFixedUpdateOthers.Remove(OnFixedUpdateOthers);
            CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
        }
    }

    bool ISelfVoter.CanUseVoted() => CanStartRuleSelection() || isSelectingTarget;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !Player.IsAlive()) return true;
        if (!CanStartRuleSelection() && !isSelectingTarget) return true;
        if (!CheckSelfVoteMode(Player, votedForId, out var status)) return true;

        switch (status)
        {
            case VoteStatus.Self:
                ShowTargetSelectMessage();
                isSelectingTarget = true;
                SetMode(Player, true);
                SendRPC();
                return false;
            case VoteStatus.Skip:
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                isSelectingTarget = false;
                SetMode(Player, false);
                SendRPC();
                return false;
            case VoteStatus.Vote:
                SelectRuleChooser(PlayerCatch.GetPlayerById(votedForId));
                isSelectingTarget = false;
                SetMode(Player, false);
                SendRPC();
                return false;
            default:
                return true;
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (pendingRule == RulerRule.None)
        {
            ClearRuleRequest(notifyCancel: ruleChooserTargetId != byte.MaxValue);
            return;
        }

        if (!Player.IsAlive())
        {
            ClearRuleRequest(notifyCancel: true);
            return;
        }

        activeRule = pendingRule;
        activeRuleChooserId = pendingRuleChooserId;
        pendingRule = RulerRule.None;
        pendingRuleChooserId = byte.MaxValue;
        ruleChooserTargetId = byte.MaxValue;
        ruleCandidates.Clear();

        if (activeRule == RulerRule.PassByPlayers)
        {
            ResetCrossedPlayers();
            passByTrackingReadyAt = Time.time + PassByTrackingGraceSeconds;
        }
        else if (activeRule == RulerRule.RandomTogetherDeath)
        {
            passByTrackingReadyAt = 0f;
            SelectRandomTogetherTarget();
        }
        else
        {
            passByTrackingReadyAt = 0f;
            ResetRandomTogetherState();
        }
        gameEndRuleApplied = false;

        ResetRuleTasks();
        Utils.SendMessage(string.Format(GetString("RulerRuleActivated"), GetRuleText(activeRule)), title: RuleMessageTitle);
        SendRandomTogetherTargetNotice();
        SyncActiveRuleOptions();
        SendRPC();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        ExpireActiveRule();
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost || activeRule == RulerRule.None) return;
        ExpireActiveRule();
    }

    public override string MeetingAddMessage()
    {
        if (!OptionShowAliveNotice.GetBool() || !Player.IsAlive()) return "";
        return MeetingStates.FirstMeeting ? GetString("RulerMeetingFirst") : GetString("RulerMeetingAlive");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var required = GetRequiredRuleUseCount();
        var color = CanWinNow() ? RoleInfo.RoleColor : Color.gray;
        return Utils.ColorString(color, $"({Math.Min(usedRuleCount, required)}/{required})");
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seen == seer && !OptionSoloWin.GetBool() && CanWinNow()) return Utils.AdditionalAliveWinnerMark;
        return "";
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost || !OptionSoloWin.GetBool() || !CanWinNow()) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Ruler, Player.PlayerId, false, CustomRoles.Ruler))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }
    }

    public static void ApplyGameEndRules()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var activeGameEndRules = Instances
            .Where(instance => !instance.gameEndRuleApplied
                && (instance.activeRule == RulerRule.GameEndAllEmptiness || instance.activeRule == RulerRule.GameEndAllWin))
            .ToArray();
        if (activeGameEndRules.Length == 0) return;

        foreach (var ruler in activeGameEndRules)
            ruler.gameEndRuleApplied = true;

        if (activeGameEndRules.Any(instance => instance.activeRule == RulerRule.GameEndAllEmptiness))
        {
            ApplyEveryoneBecomesEmptinessRule();
            return;
        }

        ApplyEveryoneWinsRule();
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (OptionSoloWin.GetBool() || !CanWinNow()) return false;
        winnerRole = CustomRoles.Ruler;
        return true;
    }

    bool CanStartRuleSelection()
        => Player.IsAlive()
            && Canuseability()
            && IsTaskFinished
            && usedRuleCount < GetRuleAddLimit()
            && activeRule == RulerRule.None
            && pendingRule == RulerRule.None
            && ruleChooserTargetId == byte.MaxValue;

    bool CanWinNow()
        => Player.IsAlive() && usedRuleCount >= GetRequiredRuleUseCount();

    static int GetRequiredRuleUseCount() => OptionRequiredRuleUseCount?.GetInt() ?? 2;
    static int GetRuleAddLimit() => OptionRuleAddLimit?.GetInt() ?? 2;
    static float GetRandomTogetherSeconds() => OptionRandomTogetherSeconds?.GetFloat() ?? 10f;

    void ShowTargetSelectMessage()
    {
        var sb = new StringBuilder();
        sb.Append(GetString("RulerSelectTargetMessage")).Append('\n');
        foreach (var pc in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.PlayerId != Player.PlayerId && !pc.Is(CustomRoles.GM)))
            sb.Append(pc.PlayerId).Append(": ").Append(UtilsName.GetPlayerColor(pc, true)).Append('\n');
        Utils.SendMessage(sb.ToString(), Player.PlayerId);
    }

    void SelectRuleChooser(PlayerControl target)
    {
        if (!CanSelectRuleChooser(target))
        {
            Utils.SendMessage(GetString("RulerTargetInvalid"), Player.PlayerId);
            return;
        }

        ruleChooserTargetId = target.PlayerId;
        pendingRuleChooserId = byte.MaxValue;
        pendingRule = RulerRule.None;
        ruleCandidates.Clear();
        ruleCandidates.AddRange(CreateRuleCandidates());

        Utils.SendMessage(BuildRuleMenu(), target.PlayerId, RuleMessageTitle);
        Utils.SendMessage(string.Format(GetString("RulerTargetSelected"), UtilsName.GetPlayerColor(target, true)), Player.PlayerId);
    }

    static bool CanSelectRuleChooser(PlayerControl target)
        => target != null && target.IsAlive() && !target.Is(CustomRoles.GM);

    static IEnumerable<RulerRule> CreateRuleCandidates()
    {
        var pool = EnumHelper.GetAllValues<RulerRule>().Where(rule => rule != RulerRule.None).ToList();
        var result = new List<RulerRule>();
        var random = IRandom.Instance;

        while (result.Count < 3 && pool.Count > 0)
        {
            var index = random.Next(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    string BuildRuleMenu()
    {
        var rules = ruleCandidates
            .Select(rule => GetRuleText(rule))
            .Concat(Enumerable.Repeat("", 3))
            .Take(3)
            .ToArray();
        return string.Format(GetString("RulerRuleMenu"), rules[0], rules[1], rules[2], "/cmd ruler");
    }

    public static bool HandleRuleCommand(PlayerControl sender, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        var ruler = Instances.FirstOrDefault(instance =>
            instance.ruleChooserTargetId == sender.PlayerId
            && instance.pendingRule == RulerRule.None
            && instance.ruleCandidates.Count > 0);

        if (ruler == null)
        {
            Utils.SendMessage(GetString("RulerNoPendingRule"), sender.PlayerId, RuleMessageTitle);
            return true;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out var selected) || selected < 1 || selected > ruler.ruleCandidates.Count)
        {
            Utils.SendMessage(GetString("RulerRuleCommandHelp"), sender.PlayerId, RuleMessageTitle);
            return true;
        }

        ruler.ChooseRule(sender, ruler.ruleCandidates[selected - 1]);
        return true;
    }

    void ChooseRule(PlayerControl sender, RulerRule rule)
    {
        if (pendingRule != RulerRule.None)
        {
            Utils.SendMessage(GetString("RulerRuleAlreadyChosen"), sender.PlayerId, RuleMessageTitle);
            return;
        }

        pendingRule = rule;
        pendingRuleChooserId = sender.PlayerId;
        var ruleText = GetRuleText(rule);

        Utils.SendMessage(string.Format(GetString("RulerRuleChosen"), ruleText), sender.PlayerId, RuleMessageTitle);
        Utils.SendMessage(string.Format(GetString("RulerRuleChosenForRuler"), UtilsName.GetPlayerColor(sender, true), ruleText), Player.PlayerId, RuleMessageTitle);
        Utils.SendMessage(string.Format(GetString("RulerRuleWillApply"), ruleText), title: RuleMessageTitle);
        SendRPC();
    }

    void ClearRuleRequest(bool notifyCancel)
    {
        if (notifyCancel)
            Utils.SendMessage(GetString("RulerRuleCancelled"), Player.PlayerId, RuleMessageTitle);

        ruleChooserTargetId = byte.MaxValue;
        pendingRuleChooserId = byte.MaxValue;
        pendingRule = RulerRule.None;
        ruleCandidates.Clear();
        isSelectingTarget = false;
        SendRPC();
    }

    void ExpireActiveRule()
    {
        if (activeRule == RulerRule.None) return;

        if (activeRule == RulerRule.PassByPlayers)
            KillPlayersWithoutEnoughPassBy();

        var expiredRule = activeRule;
        activeRule = RulerRule.None;
        activeRuleChooserId = byte.MaxValue;
        passByTrackingReadyAt = 0f;
        ResetRandomTogetherState();
        gameEndRuleApplied = false;
        crossedPlayers.Clear();
        usedRuleCount = Math.Min(GetRuleAddLimit(), usedRuleCount + 1);

        Utils.SendMessage(string.Format(GetString("RulerRuleExpired"), GetRuleText(expiredRule)), title: RuleMessageTitle);
        SyncActiveRuleOptions();
        SendRPC();
    }

    void KillPlayersWithoutEnoughPassBy()
    {
        var required = OptionPassByPlayerCount.GetInt();
        var countLog = string.Join(", ", PlayerCatch.AllAlivePlayerControls
            .Where(pc => !pc.Is(CustomRoles.GM))
            .Select(pc =>
            {
                var count = crossedPlayers.TryGetValue(pc.PlayerId, out var metPlayers) ? metPlayers.Count : 0;
                return $"{pc.PlayerId}:{count}";
            }));
        Logger.Info($"PassBy required={required}, counts=[{countLog}]", "Ruler");

        var targets = PlayerCatch.AllAlivePlayerControls
            .Where(pc => !pc.Is(CustomRoles.GM))
            .Where(pc => !crossedPlayers.TryGetValue(pc.PlayerId, out var metPlayers) || metPlayers.Count < required)
            .ToArray();

        if (targets.Length == 0) return;

        Utils.SendMessage(string.Format(GetString("RulerCrossDeathNotice"), required), title: RuleMessageTitle);
        foreach (var target in targets)
        {
            if (!target.IsAlive()) continue;
            KillRuleBreaker(target);
            UtilsGameLog.AddGameLog("Ruler", $"{UtilsName.GetPlayerColor(target, true)} [{Utils.GetVitalText(target.PlayerId, true)}]");
        }
    }

    void KillRuleBreaker(PlayerControl target)
    {
        if (target == null || !target.IsAlive()) return;

        var state = PlayerState.GetByPlayerId(target.PlayerId);
        if (state == null || state.IsDead) return;

        state.DeathReason = CustomDeathReason.RuleViolation;
        CustomRoleManager.CheckMurderInfos[target.PlayerId] =
            new MurderInfo(Player, target, target, target, true, 2, 0, CustomDeathReason.RuleViolation);

        if (MeetingHud.Instance != null)
        {
            target.SetRealKiller(Player);
            MeetingVoteManager.ResetVoteManager(target.PlayerId);
            if (!target.IsModClient() && !target.AmOwner) target.RpcMeetingKill(target);
            CustomRoleManager.OnMurderPlayer(target, target);
            _ = new LateTask(() => ChatManager.OnDisconnectOrDeadPlayer(target.PlayerId), 0.1f, "RulerCrossDeathChatSync");
            return;
        }

        target.RpcMurderPlayer(target);
        state.DeathReason = CustomDeathReason.RuleViolation;
        state.SetDead();
        target.SetRealKiller(Player, true);
    }

    void ResetCrossedPlayers()
    {
        crossedPlayers.Clear();
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            crossedPlayers[pc.PlayerId] = new();
    }

    void SelectRandomTogetherTarget()
    {
        ResetRandomTogetherState();
        var candidates = PlayerCatch.AllAlivePlayerControls
            .Where(pc => !pc.Is(CustomRoles.GM))
            .ToArray();

        if (candidates.Length == 0) return;
        randomTogetherTargetId = candidates[IRandom.Instance.Next(candidates.Length)].PlayerId;
    }

    void SendRandomTogetherTargetNotice()
    {
        if (activeRule != RulerRule.RandomTogetherDeath || randomTogetherTargetId == byte.MaxValue) return;

        Utils.SendMessage(
            string.Format(GetString("RulerRandomTogetherTargetNotice"), UtilsName.GetPlayerColor(randomTogetherTargetId, true), GetRandomTogetherSeconds()),
            title: RuleMessageTitle);
    }

    void ResetRandomTogetherState()
    {
        randomTogetherTargetId = byte.MaxValue;
        randomTogetherTimers.Clear();
    }

    static void OnFixedUpdateOthers(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null || !player.IsAlive()) return;

        foreach (var ruler in Instances)
        {
            switch (ruler.activeRule)
            {
                case RulerRule.PassByPlayers:
                    ruler.RecordPassBy(player);
                    break;
                case RulerRule.RandomTogetherDeath:
                    ruler.RecordRandomTogether(player);
                    break;
            }
        }
    }

    void RecordPassBy(PlayerControl player)
    {
        if (Time.time < passByTrackingReadyAt) return;

        if (!crossedPlayers.ContainsKey(player.PlayerId))
            crossedPlayers[player.PlayerId] = new();

        var required = OptionPassByPlayerCount.GetInt();
        foreach (var other in PlayerCatch.AllAlivePlayerControls)
        {
            if (other.PlayerId == player.PlayerId) continue;
            if (Vector2.Distance(player.GetTruePosition(), other.GetTruePosition()) > RuleContactDistance) continue;

            if (AddCrossedPlayer(player, other, required))
                ShowPassByRuleCompletedEffect(player);
            if (AddCrossedPlayer(other, player, required))
                ShowPassByRuleCompletedEffect(other);
        }
    }

    bool AddCrossedPlayer(PlayerControl player, PlayerControl other, int required)
    {
        if (!crossedPlayers.TryGetValue(player.PlayerId, out var metPlayers))
            crossedPlayers[player.PlayerId] = metPlayers = new();

        var wasCompleted = metPlayers.Count >= required;
        if (!metPlayers.Add(other.PlayerId)) return false;
        return !wasCompleted && metPlayers.Count >= required;
    }

    static void ShowPassByRuleCompletedEffect(PlayerControl target)
    {
        if (target == null || !target.IsAlive()) return;
        target.RpcProtectedMurderPlayer();
    }

    void RecordRandomTogether(PlayerControl player)
    {
        if (randomTogetherTargetId == byte.MaxValue) return;
        if (player.PlayerId == randomTogetherTargetId || player.Is(CustomRoles.GM)) return;

        var target = PlayerCatch.GetPlayerById(randomTogetherTargetId);
        if (target == null || !target.IsAlive())
        {
            randomTogetherTimers.Clear();
            return;
        }

        if (Vector2.Distance(player.GetTruePosition(), target.GetTruePosition()) > RuleContactDistance)
        {
            randomTogetherTimers.Remove(player.PlayerId);
            return;
        }

        var elapsed = randomTogetherTimers.TryGetValue(player.PlayerId, out var current)
            ? current + Time.fixedDeltaTime
            : Time.fixedDeltaTime;

        if (elapsed < GetRandomTogetherSeconds())
        {
            randomTogetherTimers[player.PlayerId] = elapsed;
            return;
        }

        randomTogetherTimers.Remove(player.PlayerId);
        Utils.SendMessage(
            string.Format(GetString("RulerRandomTogetherDeathNotice"), UtilsName.GetPlayerColor(player, true), UtilsName.GetPlayerColor(target, true)),
            title: RuleMessageTitle);
        KillRuleBreaker(player);
        UtilsGameLog.AddGameLog("Ruler", $"{UtilsName.GetPlayerColor(player, true)} [{Utils.GetVitalText(player.PlayerId, true)}]");
    }

    static void ApplyEveryoneWinsRule()
    {
        if (CustomWinnerHolder.WinnerTeam is CustomWinner.None or CustomWinner.Default or CustomWinner.Draw)
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Ruler);

        CustomWinnerHolder.ForceEveryoneWinsText = true;
        foreach (var pc in PlayerCatch.AllPlayerControls.Where(IsGamePlayer))
        {
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            CustomWinnerHolder.CantWinPlayerIds.Remove(pc.PlayerId);
        }

        UtilsGameLog.AddGameLog("Ruler", GetString("RulerGameEndAllWinLog"));
    }

    static void ApplyEveryoneBecomesEmptinessRule()
    {
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);

        foreach (var pc in PlayerCatch.AllPlayerControls.Where(IsGamePlayer))
        {
            pc.RpcSetCustomRole(CustomRoles.Emptiness, true, null);
            CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
        }

        UtilsGameLog.AddGameLog("Ruler", GetString("RulerGameEndAllEmptinessLog"));
    }

    static bool IsGamePlayer(PlayerControl player)
        => player != null && !player.Is(CustomRoles.GM);

    public static float ApplyKillCooldownRule(float current)
    {
        foreach (var ruler in Instances)
        {
            current = ruler.activeRule switch
            {
                RulerRule.KillCooldownDecrease => current - OptionKillCooldownDecrease.GetFloat(),
                RulerRule.KillCooldownIncrease => current + OptionKillCooldownIncrease.GetFloat(),
                _ => current,
            };
        }
        return Mathf.Max(0f, current);
    }

    public static void ApplyVisionRule(PlayerControl player, IGameOptions opt)
    {
        if (player == null || opt == null || !player.IsAlive()) return;

        foreach (var ruler in Instances)
        {
            if (ruler.activeRule is not RulerRule.CrewVisionDecrease and not RulerRule.NonCrewVisionDecrease) continue;
            if (!ShouldApplyVisionRule(player, ruler.activeRule)) continue;

            var factor = Mathf.Clamp01(1f - OptionVisionDecreaseRate.GetInt() * 0.01f);
            opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod) * factor);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) * factor);
        }
    }

    static bool ShouldApplyVisionRule(PlayerControl player, RulerRule rule)
    {
        var isCrewmate = player.Is(CustomRoleTypes.Crewmate);
        return rule switch
        {
            RulerRule.CrewVisionDecrease => isCrewmate,
            RulerRule.NonCrewVisionDecrease => !isCrewmate,
            _ => false,
        };
    }

    static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seen != seer) return "";

        var activeTexts = Instances
            .Where(instance => instance.activeRule != RulerRule.None)
            .Select(instance => instance.GetRuleText(instance.activeRule))
            .ToArray();

        if (activeTexts.Length == 0) return "";

        var text = string.Join("\n", activeTexts);
        return isForHud
            ? $"<{RoleInfo.RoleColorCode}>{text}</color>"
            : $"<size=50%><{RoleInfo.RoleColorCode}>{text}</color></size>";
    }

    string GetRuleText(RulerRule rule)
        => rule switch
        {
            RulerRule.PassByPlayers => string.Format(GetString("RulerRule.PassByPlayers"), OptionPassByPlayerCount.GetInt()),
            RulerRule.KillCooldownDecrease => string.Format(GetString("RulerRule.KillCooldownDecrease"), OptionKillCooldownDecrease.GetFloat()),
            RulerRule.KillCooldownIncrease => string.Format(GetString("RulerRule.KillCooldownIncrease"), OptionKillCooldownIncrease.GetFloat()),
            RulerRule.CrewVisionDecrease => string.Format(GetString("RulerRule.CrewVisionDecrease"), OptionVisionDecreaseRate.GetInt()),
            RulerRule.NonCrewVisionDecrease => string.Format(GetString("RulerRule.NonCrewVisionDecrease"), OptionVisionDecreaseRate.GetInt()),
            RulerRule.GameEndAllEmptiness => GetString("RulerRule.GameEndAllEmptiness"),
            RulerRule.GameEndAllWin => GetString("RulerRule.GameEndAllWin"),
            RulerRule.RandomTogetherDeath => string.Format(GetString("RulerRule.RandomTogetherDeath"), GetRandomTogetherSeconds()),
            _ => GetString("None"),
        };

    void ResetRuleTasks()
    {
        Player.Data.RpcSetTasks(Array.Empty<byte>());
        MyTaskState.CompletedTasksCount = 0;
        Player.SyncSettings();
        GameData.Instance?.RecomputeTaskCounts();
    }

    static void SyncActiveRuleOptions()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
        GameOptionsSender.SendAllGameOptions();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPCType.SyncState);
        sender.Writer.Write(usedRuleCount);
        sender.Writer.Write(isSelectingTarget);
        sender.Writer.Write(ruleChooserTargetId);
        sender.Writer.Write(pendingRuleChooserId);
        sender.Writer.Write(activeRuleChooserId);
        sender.Writer.WritePacked((int)pendingRule);
        sender.Writer.WritePacked((int)activeRule);
        sender.Writer.Write(randomTogetherTargetId);
        sender.Writer.Write(gameEndRuleApplied);
        sender.Writer.WritePacked(ruleCandidates.Count);
        foreach (var rule in ruleCandidates)
            sender.Writer.WritePacked((int)rule);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if ((RPCType)reader.ReadPackedInt32() != RPCType.SyncState) return;

        usedRuleCount = reader.ReadInt32();
        isSelectingTarget = reader.ReadBoolean();
        ruleChooserTargetId = reader.ReadByte();
        pendingRuleChooserId = reader.ReadByte();
        activeRuleChooserId = reader.ReadByte();
        pendingRule = (RulerRule)reader.ReadPackedInt32();
        activeRule = (RulerRule)reader.ReadPackedInt32();
        randomTogetherTargetId = reader.ReadByte();
        gameEndRuleApplied = reader.ReadBoolean();

        ruleCandidates.Clear();
        var candidateCount = reader.ReadPackedInt32();
        for (var i = 0; i < candidateCount; i++)
            ruleCandidates.Add((RulerRule)reader.ReadPackedInt32());
    }
}
