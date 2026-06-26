using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Patches;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;
using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Neutral;

public sealed class Onmyoji : RoleBase, IKiller, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Onmyoji),
            player => new Onmyoji(player),
            CustomRoles.Onmyoji,
            () => (OptionCanUseVent?.GetBool() ?? true) ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            53300,
            SetupOptionItem,
            "oy",
            "#9b59b6",
            (6, 1),
            true,
            countType: CountTypes.None,
            from: From.SuperNewRoles
        );

    static OptionItem OptionWinTaskCount;
    static OptionItem OptionCanUseVent;
    static OptionItem OptionVentCooldown;
    static OptionItem OptionVentDuration;
    static OptionItem OptionImpostorVision;
    static OptionItem OptionCanCreateShikigami;
    static OptionItem OptionCreateShikigamiCooldown;
    static OptionItem OptionNeedTaskToWin;
    static OptionItem OptionCanHijackCrewWin;
    static OptionItem OptionDisableReport;
    static OptionItem OptionDisableEmergencyMeeting;
    static OptionItem OptionShikigamiShiftCooldown;
    static OptionItem OptionShikigamiSuicideCooldown;

    public List<byte> ShikigamiIds;
    bool hasCompletedTaskRequirement;
    bool nominateMode;

    public byte NextShikigamiCandidate;
    float nearTimer;
    float createCooldownTimer;
    float spawnWaitTimer;
    bool SkCanApproach => spawnWaitTimer >= 3f;

    int lastCDDisplay = -1;

    enum OptionName
    {
        OnmyojiWinTaskCount,
        OnmyojiCanCreateShikigami,
        OnmyojiCreateShikigamiCooldown,
        OnmyojiNeedTaskToWin,
        OnmyojiCanHijackCrewWin,
        OnmyojiDisableReport,
        OnmyojiDisableEmergencyMeeting,
        OnmyojiShikigamiShiftCooldown,
        OnmyojiShikigamiSuicideCooldown,
    }

    public Onmyoji(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        ShikigamiIds = new();
        hasCompletedTaskRequirement = !(OptionNeedTaskToWin?.GetBool() ?? false);
        nominateMode = false;
        NextShikigamiCandidate = byte.MaxValue;
        nearTimer = 0f;
        createCooldownTimer = 0f;
        spawnWaitTimer = -1f;
        lastCDDisplay = -1;
        MyTaskState.NeedTaskCount = OptionWinTaskCount.GetInt();
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 8, defo: 15);
        OptionWinTaskCount = IntegerOptionItem.Create(RoleInfo, 9, OptionName.OnmyojiWinTaskCount, new(1, 99, 1), 5, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanUseVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, true, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 11, "OnmyojiVentCooldown", new(0f, 60f, 2.5f), 15f, false, OptionCanUseVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionVentDuration = FloatOptionItem.Create(RoleInfo, 12, "OnmyojiVentDuration", new(0f, 60f, 2.5f), 10f, false, OptionCanUseVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionImpostorVision = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.ImpostorVision, true, false);
        OptionCanCreateShikigami = BooleanOptionItem.Create(RoleInfo, 14, OptionName.OnmyojiCanCreateShikigami, true, false);
        OptionCreateShikigamiCooldown = FloatOptionItem.Create(RoleInfo, 15, OptionName.OnmyojiCreateShikigamiCooldown,
            new(0f, 60f, 2.5f), 20f, false, OptionCanCreateShikigami)
            .SetValueFormat(OptionFormat.Seconds);
        OptionNeedTaskToWin = BooleanOptionItem.Create(RoleInfo, 16, OptionName.OnmyojiNeedTaskToWin, true, false);
        OptionCanHijackCrewWin = BooleanOptionItem.Create(RoleInfo, 17, OptionName.OnmyojiCanHijackCrewWin, true, false);
        OptionDisableReport = BooleanOptionItem.Create(RoleInfo, 18, OptionName.OnmyojiDisableReport, false, false);
        OptionDisableEmergencyMeeting = BooleanOptionItem.Create(RoleInfo, 19, OptionName.OnmyojiDisableEmergencyMeeting, false, false);
        OptionShikigamiShiftCooldown = FloatOptionItem.Create(RoleInfo, 20, OptionName.OnmyojiShikigamiShiftCooldown, new(0f, 60f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionShikigamiSuicideCooldown = FloatOptionItem.Create(RoleInfo, 21, OptionName.OnmyojiShikigamiSuicideCooldown, new(0f, 60f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideTasksData.Create(RoleInfo, 30);
        PavlovDog.HideRoleOptions(CustomRoles.Shikigami);
    }

    public static float GetShikigamiShiftCooldown() => OptionShikigamiShiftCooldown?.GetFloat() ?? 20f;
    public static float GetShikigamiSuicideCooldown() => OptionShikigamiSuicideCooldown?.GetFloat() ?? 10f;

    public float CalculateKillCooldown() => OptionCreateShikigamiCooldown?.GetFloat() ?? 20f;

    public bool CanUseKillButton()
        => Player.IsAlive()
        && nominateMode
        && OptionCanCreateShikigami.GetBool()
        && ShikigamiIds.Count < 1;

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    bool ISelfVoter.CanUseVoted()
        => Player.IsAlive()
        && OptionCanCreateShikigami.GetBool()
        && ShikigamiIds.Count < 1;

    public override void Add()
    {
        nominateMode = false;
        lastCDDisplay = -1;
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        NameColorManager.RemoveAll(Player.PlayerId);
        foreach (var id in ShikigamiIds)
            TargetArrow.Remove(Player.PlayerId, id);
    }

    private void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        if (!OptionCanCreateShikigami.GetBool()) return;
        if (ShikigamiIds.Count >= 1) return;

        nominateMode = !nominateMode;
        ApplyModeDesync(nominateMode);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;
        info.DoKill = false;

        if (!nominateMode || ShikigamiIds.Count >= 1) return;

        (_, var target) = info.AttemptTuple;
        if (!IsValidShikigamiTarget(target)) return;

        nominateMode = false;
        ApplyModeDesync(false);
        AddShikigami(target);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter)) return true;
        if (!Player.IsAlive() || !OptionCanCreateShikigami.GetBool() || ShikigamiIds.Count >= 1) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
            {
                NextShikigamiCandidate = byte.MaxValue;
                SendMessage(
                    "<color=#9b59b6>【式神候補選択モード】</color>\n" +
                    "誰かに投票 → 近接指名の候補に設定\n" +
                    "スキップ → キャンセル\n" +
                    "（ペットでモード切替→キルボタンでも指名可）",
                    Player.PlayerId);
                SetMode(Player, true);
                SendRPC();
                return false;
            }
            if (status is VoteStatus.Skip)
            {
                NextShikigamiCandidate = byte.MaxValue;
                SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                SetMode(Player, false);
                SendRPC();
                return false;
            }
            if (status is VoteStatus.Vote)
            {
                var target = GetPlayerById(votedForId);
                if (target == null || !target.IsAlive() || votedForId == Player.PlayerId)
                {
                    SendMessage("<color=#9b59b6>その相手は式神にできません。</color>", Player.PlayerId);
                    SetMode(Player, false);
                    return false;
                }
                if (!IsValidShikigamiTarget(target))
                {
                    SendMessage("<color=#9b59b6>キル能力を持たない相手のみ式神にできます。</color>", Player.PlayerId);
                    SetMode(Player, false);
                    return false;
                }
                NextShikigamiCandidate = votedForId;
                nearTimer = 0f;
                SendMessage(
                    $"<color=#9b59b6>【式神候補設定】</color>\n" +
                    $"{UtilsName.GetPlayerColor(target, true)} を候補に設定しました。\n" +
                    $"次ターン、1.5秒近づいて式神作成！",
                    Player.PlayerId);
                SetMode(Player, false);
                SendRPC();
                return false;
            }
        }
        return true;
    }

    public override bool CanClickUseVentButton => !nominateMode && (OptionCanUseVent?.GetBool() ?? true);
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (nominateMode) return false;
        return true;
    }

    public override RoleTypes? AfterMeetingRole
        => nominateMode
            ? RoleTypes.Impostor
            : ((OptionCanUseVent?.GetBool() ?? true) ? RoleTypes.Engineer : RoleTypes.Crewmate);

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionImpostorVision.GetBool());
        if (OptionCanUseVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = OptionVentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = OptionVentDuration.GetFloat();
        }
        if (OptionDisableEmergencyMeeting.GetBool())
            opt.SetInt(Int32OptionNames.NumEmergencyMeetings, 0);
    }

    public override void OnSpawn(bool initialState)
    {
        if (initialState)
        {
            ShikigamiIds.Clear();
            hasCompletedTaskRequirement = !(OptionNeedTaskToWin?.GetBool() ?? false);
            nominateMode = false;
            NextShikigamiCandidate = byte.MaxValue;
            nearTimer = 0f;
            createCooldownTimer = OptionCreateShikigamiCooldown.GetFloat();
            spawnWaitTimer = -1f;
            lastCDDisplay = -1;
            NameColorManager.RemoveAll(Player.PlayerId);
        }
        RefreshStarReadingTargets();
    }

    public override void OnStartMeeting()
    {
        if (nominateMode) { nominateMode = false; SendRPC(); }
        NextShikigamiCandidate = byte.MaxValue;
        nearTimer = 0f;
        spawnWaitTimer = -1f;
        RefreshStarReadingTargets();
    }

    public override void AfterMeetingTasks()
    {
        spawnWaitTimer = 0f;
        createCooldownTimer = OptionCreateShikigamiCooldown.GetFloat();
        nearTimer = 0f;
        lastCDDisplay = -1;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        ApplyModeDesync(nominateMode);
        Player.RpcResetAbilityCooldown();
        RefreshStarReadingTargets();

        foreach (var id in ShikigamiIds)
        {
            var sk = GetPlayerById(id);
            if (sk == null || !sk.IsAlive()) continue;
            TargetArrow.Add(Player.PlayerId, id);
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (!Player.IsAlive()) return;

        if (createCooldownTimer > 0f)
            createCooldownTimer = Mathf.Max(0f, createCooldownTimer - Time.fixedDeltaTime);

        if (spawnWaitTimer >= 0f && spawnWaitTimer < 3f)
            spawnWaitTimer = Mathf.Min(spawnWaitTimer + Time.fixedDeltaTime, 3f);

        var now = Mathf.CeilToInt(createCooldownTimer);
        if (now != lastCDDisplay)
        {
            lastCDDisplay = now;

            if (nominateMode && now <= 0)
                Player.SetKillCooldown(0.5f);

            if (player != PlayerControl.LocalPlayer)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }

        if (OptionCanCreateShikigami.GetBool() && ShikigamiIds.Count < 1
            && NextShikigamiCandidate != byte.MaxValue && SkCanApproach && createCooldownTimer <= 0f)
        {
            var target = GetPlayerById(NextShikigamiCandidate);

            if (target == null || !target.IsAlive() || !IsValidShikigamiTarget(target))
            {
                NextShikigamiCandidate = byte.MaxValue;
                nearTimer = 0f;
                SendRPC();
            }
            else
            {
                float dist = Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition());
                if (dist <= 1.5f)
                {
                    nearTimer += Time.fixedDeltaTime;
                    if (nearTimer >= 1.5f)
                    {
                        NextShikigamiCandidate = byte.MaxValue;
                        nearTimer = 0f;
                        AddShikigami(target);
                    }
                }
                else { nearTimer = 0f; }
            }
        }
    }

    private void ApplyModeDesync(bool toNominateMode)
    {
        if (Is(PlayerControl.LocalPlayer)) return;
        if (!Player.IsAlive()) return;

        var baseRole = (OptionCanUseVent?.GetBool() ?? true) ? RoleTypes.Engineer : RoleTypes.Crewmate;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toNominateMode ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toNominateMode ? RoleTypes.Impostor : baseRole,
                    Player.GetClientId());
        }

        if (toNominateMode)
        {
            Player.SetKillCooldown(Mathf.Max(createCooldownTimer, 0.1f), delay: true);
        }
        else
        {
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive() || nominateMode) return;
                Player.RpcResetAbilityCooldown(Sync: true);
            }, 0.1f, "Onmyoji.VentReset", true);
        }
    }

    bool IsValidShikigamiTarget(PlayerControl target)
    {
        if (target == null || !target.IsAlive()) return false;
        if (target.PlayerId == Player.PlayerId) return false;
        if (target.Is(CustomRoles.PavlovOwner)) return true;
        if (IsOnmyojiKillerTarget(target)) return false;
        return true;
    }

    void AddShikigami(PlayerControl target)
    {
        if (ShikigamiIds.Count >= 1) return;
        if (!IsValidShikigamiTarget(target)) return;
        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Jackal)) return;

        ShikigamiIds.Add(target.PlayerId);
        TargetArrow.Add(Player.PlayerId, target.PlayerId);

        if (!RoleSendList.Contains(target.PlayerId))
            RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(CustomRoles.Shikigami, log: null);
        _ = new LateTask(() =>
        {
            if (target.GetRoleClass() is Shikigami sk)
                sk.SetOwner(Player.PlayerId);
        }, 0.1f, "Onmyoji.SetOwner", true);

        NameColorManager.Add(Player.PlayerId, target.PlayerId, "#9b59b6");
        createCooldownTimer = OptionCreateShikigamiCooldown.GetFloat();

        SendRPC();
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Onmyoji.Shikigami", true);
    }

    void RefreshStarReadingTargets()
    {
        NameColorManager.RemoveAll(Player.PlayerId);
        if (!Player.IsAlive()) return;

        foreach (var pc in AllPlayerControls)
        {
            if (pc == null || pc.PlayerId == Player.PlayerId || !pc.IsAlive()) continue;
            if (!IsStarReadingTarget(pc)) continue;
            NameColorManager.Add(Player.PlayerId, pc.PlayerId, UtilsRoleText.GetRoleColorCode(pc.GetCustomRole()));
        }
        foreach (var id in ShikigamiIds)
            NameColorManager.Add(Player.PlayerId, id, "#9b59b6");
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(OptionWinTaskCount.GetInt()))
            hasCompletedTaskRequirement = true;
        return true;
    }

    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (reporter == null || reporter.PlayerId != Player.PlayerId) return false;
        if (!OptionDisableReport.GetBool()) return false;
        reason = DontReportreson.CantUseButton;
        return true;
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player == null) return;
        if (player.PlayerId == NextShikigamiCandidate)
        {
            NextShikigamiCandidate = byte.MaxValue;
            nearTimer = 0f;
            SendRPC();
        }
        ShikigamiIds.Remove(player.PlayerId);
        TargetArrow.Remove(Player.PlayerId, player.PlayerId);
    }

    bool CanWinNow()
    {
        if (!(OptionNeedTaskToWin?.GetBool() ?? false)) return true;
        return hasCompletedTaskRequirement
            || MyTaskState.HasCompletedEnoughCountOfTasks(OptionWinTaskCount?.GetInt() ?? 0);
    }

    public static bool TryTakeOverCrewWin(ref GameOverReason reason)
    {
        var currentWinner = CustomWinnerHolder.WinnerTeam;
        if (currentWinner is CustomWinner.Default or CustomWinner.Onmyoji) return false;
        if (currentWinner is CustomWinner.Crewmate && !(OptionCanHijackCrewWin?.GetBool() ?? true)) return false;

        foreach (var pc in AllPlayerControls)
        {
            if (pc == null || !pc.IsAlive()) continue;
            if (pc.GetRoleClass() is not Onmyoji onmyoji) continue;
            if (!onmyoji.CanWinNow()) continue;
            if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Onmyoji, pc.PlayerId, true)) continue;

            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Onmyoji);
            CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

            foreach (var id in onmyoji.ShikigamiIds)
            {
                var sk = GetPlayerById(id);
                if (sk == null || sk.Data?.Disconnected == true || !sk.Is(CustomRoles.Shikigami)) continue;
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Shikigami);
                CustomWinnerHolder.NeutralWinnerIds.Add(id);
                CustomWinnerHolder.WinnerIds.Add(id);
            }

            reason = GameOverReason.CrewmatesByVote;
            return true;
        }
        return false;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || !Player.IsAlive()) return "";

        if (seer.PlayerId != seen.PlayerId)
        {
            if (!IsStarReadingTarget(seen)) return "";
            return $" <color={UtilsRoleText.GetRoleColorCode(seen.GetCustomRole())}>★</color>";
        }
        if (isForMeeting) return "";

        var arrows = "";
        foreach (var id in ShikigamiIds)
        {
            var sk = GetPlayerById(id);
            if (sk == null || !sk.IsAlive()) continue;
            arrows += TargetArrow.GetArrows(seer, id);
        }
        return arrows != "" ? $"<color=#9b59b6>{arrows}</color>" : "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || !Player.IsAlive()) return "";
        if (!OptionCanCreateShikigami.GetBool() || ShikigamiIds.Count >= 1) return "";

        string size = isForHud ? "" : "<size=60%>";

        if (nominateMode)
            return $"{size}<color=#9b59b6>【指名モード】キルボタンで式神指定！</color>";

        if (NextShikigamiCandidate != byte.MaxValue)
        {
            var target = GetPlayerById(NextShikigamiCandidate);
            string name = target != null ? target.Data.PlayerName : "???";
            if (!SkCanApproach)
                return $"{size}<color=#9b59b6>候補: {name} (待機中...)</color>";
            if (createCooldownTimer > 0f)
                return $"{size}<color=#9b59b6>候補: {name} (CD:{Mathf.CeilToInt(createCooldownTimer)}s)</color>";
            float prog = Mathf.Min(nearTimer, 1.5f);
            return $"{size}<color=#9b59b6>{name}に近づき中 {prog:F1}/1.5s</color>";
        }

        if (isForMeeting)
            return $"{size}<color=#9b59b6>自投票→式神候補を選択</color>";

        return $"{size}<color=#9b59b6>ペット→指名モード / 会議自投票→近接指名</color>";
    }

    public override string GetProgressText(bool comms = false, bool gameLog = false)
    {
        var ready = CanWinNow() ? "#9b59b6" : "#5e5e5e";

        var progress = ColorString(
            ShikigamiIds.Count < 1 ? UnityEngine.ColorUtility.TryParseHtmlString(ready, out var c) ? c : Color.white : Color.gray,
            $"式:{ShikigamiIds.Count}/1");

        if (!GameStates.CalledMeeting && !gameLog && ShikigamiIds.Count < 1 && OptionCanCreateShikigami.GetBool())
        {
            progress += ColorString(Color.yellow,
                nominateMode
                    ? $" [指名]<color=#ffffff>({lastCDDisplay})</color>"
                    : $" [Task]<color=#ffffff>({lastCDDisplay})</color>");
        }

        return progress;
    }

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ShikigamiIds.Count);
        foreach (var id in ShikigamiIds)
            sender.Writer.Write(id);
        sender.Writer.Write(hasCompletedTaskRequirement);
        sender.Writer.Write(nominateMode);
        sender.Writer.Write(createCooldownTimer);
        sender.Writer.Write(NextShikigamiCandidate);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        int count = reader.ReadInt32();
        ShikigamiIds = new();
        for (int i = 0; i < count; i++)
            ShikigamiIds.Add(reader.ReadByte());
        hasCompletedTaskRequirement = reader.ReadBoolean();
        nominateMode = reader.ReadBoolean();
        createCooldownTimer = reader.ReadSingle();
        if (reader.BytesRemaining > 0)
            NextShikigamiCandidate = reader.ReadByte();
    }

    public override string GetAbilityButtonText() => GetString("OnmyojiAbilityButtonText");
    public bool OverrideKillButton(out string text) { text = "Onmyoji_Nominate"; return true; }

    bool IsStarReadingTarget(PlayerControl target)
    {
        if (target == null || !target.IsAlive()) return false;
        if (target.Is(CustomRoles.PavlovOwner)) return false;
        return IsOnmyojiKillerTarget(target);
    }

    bool IsOnmyojiKillerTarget(PlayerControl target)
    {
        if (target == null) return false;
        if (target.Is(CustomRoleTypes.Impostor)) return true;
        return target.GetCustomRole() is
            CustomRoles.CountKiller or CustomRoles.Strawdoll or
            CustomRoles.Jackal or CustomRoles.JackalHadouHo or CustomRoles.JackalMafia or CustomRoles.JackalAlien or
            CustomRoles.DoppelGanger or CustomRoles.GrimReaper or CustomRoles.Remotekiller or
            CustomRoles.Egoist or CustomRoles.Eater or CustomRoles.PavlovDog or
            CustomRoles.Sheriff or CustomRoles.SwitchSheriff or
            CustomRoles.WolfBoy or CustomRoles.JackalWolf or CustomRoles.Stand;
    }
}

public sealed class Shikigami : RoleBase, IUsePhantomButton, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Shikigami),
            player => new Shikigami(player),
            CustomRoles.Shikigami,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            54300,
            SetupOptionItem,
            "sk",
            "#9b59b6",
            (6, 1),
            from: From.SuperNewRoles,
            isDesyncImpostor: true,
            countType: CountTypes.Crew
        );

    public byte OwnerId;
    bool isShifted;
    float suicideCooldownTimer;
    float unresolvedOwnerGraceTimer;
    bool petActionRegistered;
    bool wasPetting;
    float petInputDebounceTimer;
    readonly Dictionary<byte, Vector2> deadBodyPositions;

    enum RPCType { SyncState, AddDeadBodyArrow, RemoveDeadBodyArrow, ClearDeadBodyArrows }

    public Shikigami(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        OwnerId = byte.MaxValue;
        isShifted = false;
        suicideCooldownTimer = 0f;
        unresolvedOwnerGraceTimer = 1.5f;
        petActionRegistered = false;
        wasPetting = false;
        petInputDebounceTimer = 0f;
        deadBodyPositions = new();
        EnsurePetActionRegistered();
    }

    private static void SetupOptionItem() { PavlovDog.HideRoleOptions(CustomRoles.Shikigami); }

    public override void OnSpawn(bool initialState)
    {
        if (initialState)
        {
            isShifted = false;
            suicideCooldownTimer = 0f;
            OwnerId = byte.MaxValue;
            unresolvedOwnerGraceTimer = 1.5f;
            petActionRegistered = false;
            wasPetting = false;
            petInputDebounceTimer = 0f;
            deadBodyPositions.Clear();
        }
        (this as IUsePhantomButton).Init(Player);
        IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
        Player.RpcResetAbilityCooldown();
        EnsurePetActionRegistered();
        if (OwnerId != byte.MaxValue) TargetArrow.Add(Player.PlayerId, OwnerId);
    }

    public override void OnDestroy()
    {
        if (petActionRegistered) { PetActionManager.Unregister(Player.PlayerId); petActionRegistered = false; }
        ClearDeadBodyArrows();
        if (OwnerId != byte.MaxValue) { TargetArrow.Remove(Player.PlayerId, OwnerId); NameColorManager.Remove(Player.PlayerId, OwnerId); }
    }

    public void SetOwner(byte ownerId)
    {
        if (OwnerId == ownerId) return;
        OwnerId = ownerId;
        unresolvedOwnerGraceTimer = 0f;
        EnsurePetActionRegistered();
        TargetArrow.Add(Player.PlayerId, OwnerId);
        NameColorManager.Add(Player.PlayerId, OwnerId, "#9b59b6");
        SendStateRPC();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Onmyoji.GetShikigamiShiftCooldown();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        EnsurePetActionRegistered();
        if (OwnerId != byte.MaxValue) TargetArrow.Add(Player.PlayerId, OwnerId);
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        if (OwnerId == byte.MaxValue)
        {
            TryResolveOwnerFromOnmyoji();
            if (OwnerId == byte.MaxValue)
                unresolvedOwnerGraceTimer = Mathf.Max(0f, unresolvedOwnerGraceTimer - Time.fixedDeltaTime);
        }
        if (ShouldFollowOwnerDeath()) { FollowOwnerDeath(); return; }
        HandlePetFallback();
        if (suicideCooldownTimer > 0f) suicideCooldownTimer = Mathf.Max(0f, suicideCooldownTimer - Time.fixedDeltaTime);
    }

    public bool? CheckKillFlash(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return false;
        var dead = info.AppearanceTarget;
        if (dead == null) return false;
        AddDeadBodyArrow(dead.PlayerId, dead.GetTruePosition());
        return false;
    }

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        if (suicideCooldownTimer > 0f) { SendMessage($"<color=#9b59b6>自決クール中: {Mathf.CeilToInt(suicideCooldownTimer)}秒</color>", Player.PlayerId); return; }
        suicideCooldownTimer = Onmyoji.GetShikigamiSuicideCooldown();
        var state = PlayerState.GetByPlayerId(Player.PlayerId);
        if (state != null) state.DeathReason = CustomDeathReason.Suicide;
        Player.SetRealKiller(Player);
        Player.RpcMurderPlayerV2(Player);
    }

    internal void HandlePetAction() => OnPet();

    void HandlePetFallback()
    {
        if (GameStates.IsLobby || GameStates.IsMeeting) { wasPetting = Player.petting; return; }
        if (petInputDebounceTimer > 0f) petInputDebounceTimer = Mathf.Max(0f, petInputDebounceTimer - Time.fixedDeltaTime);
        var pettingNow = Player.petting;
        if (pettingNow && !wasPetting && petInputDebounceTimer <= 0f) { petInputDebounceTimer = 0.35f; OnPet(); }
        wasPetting = pettingNow;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false; ResetCooldown = false;
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        if (OwnerId == byte.MaxValue) TryResolveOwnerFromOnmyoji();
        if (OwnerId == byte.MaxValue) { SendMessage("<color=#9b59b6>陰陽師が見つかりません。</color>", Player.PlayerId); return; }
        var owner = GetPlayerById(OwnerId);
        if (owner == null) { SendMessage("<color=#9b59b6>陰陽師が見つかりません。</color>", Player.PlayerId); return; }
        isShifted = !isShifted;
        if (isShifted) Player.RpcShapeshift(owner, false); else Player.RpcShapeshift(Player, false);
        ResetCooldown = true;
        SendStateRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public bool UseOneclickButton => true;
    public bool IsPhantomRole => true;
    public bool IsresetAfterKill => false;

    public override void OnStartMeeting() => ClearDeadBodyArrows();
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => ClearDeadBodyArrows();
    public override void AfterMeetingTasks() { if (OwnerId != byte.MaxValue) TargetArrow.Add(Player.PlayerId, OwnerId); }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player == null) return;
        if (player.PlayerId == OwnerId) { TargetArrow.Remove(Player.PlayerId, OwnerId); NameColorManager.Remove(Player.PlayerId, OwnerId); OwnerId = byte.MaxValue; }
        RemoveDeadBodyArrow(player.PlayerId);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || isForMeeting || !Player.IsAlive() || seer.PlayerId != seen.PlayerId) return "";
        var result = "";
        if (OwnerId != byte.MaxValue) { var owner = GetPlayerById(OwnerId); if (owner != null && owner.IsAlive()) result += $"<color=#9b59b6>{TargetArrow.GetArrows(seer, OwnerId)}</color>"; }
        if (deadBodyPositions.Count > 0) { var arrows = ""; foreach (var pos in deadBodyPositions.Values) arrows += GetArrow.GetArrows(seer, pos); if (arrows != "") result += $"<color=#8B4513>{arrows}</color>"; }
        return result;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer.PlayerId != seen.PlayerId || !Is(seer) || !Player.IsAlive()) return "";
        var cd = Mathf.CeilToInt(Mathf.Max(0f, suicideCooldownTimer));
        return isForHud ? "" : $"<size=60%>Pet:自決 ({cd}s)</size>";
    }

    void AddDeadBodyArrow(byte id, Vector2 pos, bool sync = true)
    {
        if (deadBodyPositions.TryGetValue(id, out var old)) GetArrow.Remove(Player.PlayerId, old);
        deadBodyPositions[id] = pos; GetArrow.Add(Player.PlayerId, pos);
        if (sync) RpcAddDeadBodyArrow(id, pos);
    }

    void RemoveDeadBodyArrow(byte id, bool sync = true)
    {
        if (!deadBodyPositions.TryGetValue(id, out var pos)) return;
        GetArrow.Remove(Player.PlayerId, pos); deadBodyPositions.Remove(id);
        if (sync) RpcRemoveDeadBodyArrow(id);
    }

    void ClearDeadBodyArrows(bool sync = true)
    {
        foreach (var pos in deadBodyPositions.Values) GetArrow.Remove(Player.PlayerId, pos);
        deadBodyPositions.Clear(); if (sync) RpcClearDeadBodyArrows();
    }

    void RpcAddDeadBodyArrow(byte id, Vector2 pos) { if (!AmongUsClient.Instance.AmHost) return; using var s = CreateSender(); s.Writer.WritePacked((int)RPCType.AddDeadBodyArrow); s.Writer.Write(id); NetHelpers.WriteVector2(pos, s.Writer); }
    void RpcRemoveDeadBodyArrow(byte id) { if (!AmongUsClient.Instance.AmHost) return; using var s = CreateSender(); s.Writer.WritePacked((int)RPCType.RemoveDeadBodyArrow); s.Writer.Write(id); }
    void RpcClearDeadBodyArrows() { if (!AmongUsClient.Instance.AmHost) return; using var s = CreateSender(); s.Writer.WritePacked((int)RPCType.ClearDeadBodyArrows); }

    void SendStateRPC()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPCType.SyncState);
        sender.Writer.Write(OwnerId);
        sender.Writer.Write(isShifted);
        sender.Writer.Write(suicideCooldownTimer);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        switch ((RPCType)reader.ReadPackedInt32())
        {
            case RPCType.SyncState: OwnerId = reader.ReadByte(); isShifted = reader.ReadBoolean(); suicideCooldownTimer = reader.ReadSingle(); break;
            case RPCType.AddDeadBodyArrow: AddDeadBodyArrow(reader.ReadByte(), NetHelpers.ReadVector2(reader), sync: false); break;
            case RPCType.RemoveDeadBodyArrow: RemoveDeadBodyArrow(reader.ReadByte(), sync: false); break;
            case RPCType.ClearDeadBodyArrows: ClearDeadBodyArrows(sync: false); break;
        }
    }

    public override string GetAbilityButtonText() => GetString("ShikigamiTransformButtonText");

    void EnsurePetActionRegistered()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || petActionRegistered || Player == null) return;
        PetActionManager.Register(Player.PlayerId, OnPet); petActionRegistered = true;
    }

    bool ShouldFollowOwnerDeath()
    {
        if (OwnerId == byte.MaxValue) return unresolvedOwnerGraceTimer <= 0f && !HasOnmyojiLink();
        var owner = GetPlayerById(OwnerId);
        return owner == null || !owner.IsAlive() || !owner.Is(CustomRoles.Onmyoji);
    }

    void FollowOwnerDeath()
    {
        if (!Player.IsAlive()) return;
        var state = PlayerState.GetByPlayerId(Player.PlayerId);
        if (state != null) state.DeathReason = CustomDeathReason.FollowingSuicide;
        var owner = OwnerId == byte.MaxValue ? null : GetPlayerById(OwnerId);
        Player.SetRealKiller(owner ?? Player);
        Player.RpcMurderPlayerV2(Player);
    }

    bool HasOnmyojiLink() { foreach (var pc in AllPlayerControls) { if (pc?.GetRoleClass() is Onmyoji o && o.ShikigamiIds.Contains(Player.PlayerId)) return true; } return false; }

    void TryResolveOwnerFromOnmyoji() { foreach (var pc in AllPlayerControls) { if (pc?.GetRoleClass() is Onmyoji o && o.ShikigamiIds.Contains(Player.PlayerId)) { SetOwner(pc.PlayerId); return; } } }
}
