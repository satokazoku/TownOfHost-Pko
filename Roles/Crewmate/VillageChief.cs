using System;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Patches;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;
using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class VillageChief : RoleBase, IKiller, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(VillageChief),
            player => new VillageChief(player),
            CustomRoles.VillageChief,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            36700,
            SetupOptionItem,
            "vc",
            "#f5a623",
            (2, 0),
            true,
            from: From.SuperNewRoles
        );

    public VillageChief(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        appointMode = false;
        hasAppointed = false;
        nowcool = AppointCooldown.GetFloat();
        LastCooltime = -1;

        NextAppointCandidate = byte.MaxValue;
        nearTimer = 0f;
        spawnWaitTimer = -1f;
    }

    private static OptionItem NotifyTarget;
    private static OptionItem AppointCooldown;

    private static readonly string[] NotifyTargetOptions =
        ["None", "Everyone", "VillageChiefOnly", "SheriffOnly", "VillageChiefAndSheriff"];

    bool appointMode;
    bool hasAppointed;
    float nowcool;
    int LastCooltime;

    public byte NextAppointCandidate;
    private float nearTimer;
    private float spawnWaitTimer;
    private bool CanApproach => spawnWaitTimer >= 3f;

    private static void SetupOptionItem()
    {
        NotifyTarget = StringOptionItem.Create(
            RoleInfo, 12, "VillageChiefNotifyTarget",
            NotifyTargetOptions, 0, false
        );
        AppointCooldown = FloatOptionItem.Create(
            RoleInfo, 13, "AppointCooldown",
            new(0f, 120f, 2.5f), 30f, false
        ).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => CanUseKillButton() ? AppointCooldown.GetFloat() : 999f;
    public bool CanUseKillButton() => Player.IsAlive() && appointMode && !hasAppointed;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override bool CanUseAbilityButton() => false;

    public override bool CanClickUseVentButton => !appointMode;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    bool ISelfVoter.CanUseVoted() => Player.IsAlive() && !hasAppointed;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !Player.IsAlive() || hasAppointed) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
            {
                SendMessage(
                    "<color=#f5a623>任命候補選択モード！</color>\n" +
                    "誰かに投票 → <color=#f5a623>近接任命の候補に設定</color>\n" +
                    "スキップ → <color=#f5a623>キャンセル</color>",
                    Player.PlayerId);
                SetMode(Player, true);
                return false;
            }
            if (status is VoteStatus.Vote)
            {
                if (votedForId == Player.PlayerId || votedForId == SkipId)
                {
                    SendMessage("<color=#f5a623>その相手は選択できません。</color>", Player.PlayerId);
                    SetMode(Player, false);
                    return false;
                }
                NextAppointCandidate = votedForId;
                SendMessage(
                    "<color=#f5a623>任命候補を設定しました！</color>\n" +
                    "次のターン、候補に1.5秒近づくと自動任命します。\n" +
                    "（ペットでモード切替 → キルボタンでも任命できます）",
                    Player.PlayerId);
                SetMode(Player, false);
                return false;
            }
            if (status is VoteStatus.Skip)
            {
                NextAppointCandidate = byte.MaxValue;
                SendMessage("<color=#f5a623>近接任命をキャンセルしました。</color>", Player.PlayerId);
                SetMode(Player, false);
                return false;
            }
        }
        return true;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        if (!appointMode)
        {
            AURoleOptions.EngineerCooldown = Mathf.Max(nowcool, 0.1f);
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
    }

    public override RoleTypes? AfterMeetingRole
        => (appointMode && !hasAppointed) ? RoleTypes.Impostor : RoleTypes.Engineer;

    public override void Add()
    {
        nowcool = AppointCooldown.GetFloat();
        LastCooltime = -1;
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    private void OnPetUsed()
    {
        if (!Player.IsAlive() || hasAppointed) return;
        appointMode = !appointMode;
        ApplyModeDesync(appointMode);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        info.DoKill = false;
        if (!appointMode || hasAppointed || nowcool > 0f) return;

        (_, var target) = info.AttemptTuple;
        DoAppoint(target);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;

        if (!Player.IsAlive() && appointMode)
        {
            appointMode = false;
            ApplyModeDesync(false);
            SendRPC();
            return;
        }

        if (!Player.IsAlive()) return;

        if (nowcool > 0f) nowcool -= Time.fixedDeltaTime;
        else nowcool = 0f;

        var now = Mathf.FloorToInt(nowcool);
        if (now != LastCooltime)
        {
            LastCooltime = now;
            if (!appointMode && !hasAppointed)
            {
                Player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    if (Player.IsAlive() && !appointMode)
                        Player.RpcResetAbilityCooldown(Sync: true);
                }, 0.1f, "VillageChief.VentCDSync", true);
            }
            if (player != PlayerControl.LocalPlayer)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
        }

        if (spawnWaitTimer >= 0f && spawnWaitTimer < 3f)
            spawnWaitTimer += Time.fixedDeltaTime;

        if (!hasAppointed && NextAppointCandidate != byte.MaxValue && CanApproach)
        {
            var candidate = GetPlayerById(NextAppointCandidate);

            if (candidate == null || !candidate.IsAlive())
            {
                NextAppointCandidate = byte.MaxValue;
                nearTimer = 0f;
                SendRPC();
                return;
            }

            float dist = Vector2.Distance(Player.GetTruePosition(), candidate.GetTruePosition());
            if (dist <= 1.5f)
            {
                nearTimer += Time.fixedDeltaTime;
                if (nearTimer >= 1.5f && nowcool <= 0f)
                {
                    DoAppoint(candidate);
                    NextAppointCandidate = byte.MaxValue;
                    nearTimer = 0f;
                }
            }
            else
            {
                nearTimer = 0f;
            }
        }
    }

    private void DoAppoint(PlayerControl target)
    {
        hasAppointed = true;
        appointMode = false;
        ApplyModeDesync(false);

        if (target.GetCustomRole().IsImpostor())
        {
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
            Player.RpcMurderPlayer(Player);
            SendRPC();
            return;
        }

        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Crewmate))
        {
            SendRPC();
            return;
        }

        Sheriff.AppointedPlayerIds.Add(target.PlayerId);

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        var previousRole = target.GetCustomRole();
        target.RpcSetCustomRole(CustomRoles.Sheriff, log: null);

        foreach (var task in target.Data.Tasks.ToArray())
            target.RpcCompleteTask(task.Id);
        PlayerState.GetByPlayerId(target.PlayerId)?.GetTaskState()?.Update(target);

        target.ResetKillCooldown();
        target.SetKillCooldown();
        target.RpcResetAbilityCooldown();

        NameColorManager.Add(Player.PlayerId, target.PlayerId, "#f5a623");

        UtilsGameLog.AddGameLog(
            "VillageChief",
            $"{UtilsName.GetPlayerColor(Player)}が" +
            $"{UtilsName.GetPlayerColor(target)}({UtilsRoleText.GetRoleName(previousRole)})をシェリフに任命した"
        );

        SendRPC();
        UtilsNotifyRoles.NotifyRoles();
    }

    private void ApplyModeDesync(bool toAppointMode)
    {
        if (!Player.IsAlive()) return;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toAppointMode ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toAppointMode ? RoleTypes.Impostor : RoleTypes.Engineer,
                    Player.GetClientId());
        }

        if (toAppointMode)
        {
            Player.SetKillCooldown(Mathf.Max(nowcool, 0.1f), delay: true);
        }
        else
        {
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive() || appointMode) return;
                Player.RpcResetAbilityCooldown(Sync: true);
            }, 0.1f, "VillageChief.EngineerReset", true);
        }
    }

    public override void OnStartMeeting()
    {
        spawnWaitTimer = -1f;
        nearTimer = 0f;
    }

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        _ = new LateTask(() =>
        {
            nowcool = AppointCooldown.GetFloat();
            LastCooltime = -1;
            spawnWaitTimer = 0f;
            ApplyModeDesync(appointMode);
        }, Main.LagTime, "Reset-VillageChief");
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(appointMode);
        sender.Writer.Write(hasAppointed);
        sender.Writer.Write(nowcool);
        sender.Writer.Write(NextAppointCandidate);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        appointMode = reader.ReadBoolean();
        hasAppointed = reader.ReadBoolean();
        nowcool = reader.ReadSingle();
        NextAppointCandidate = reader.ReadByte();
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (hasAppointed) return "<color=#f5a623>(任命済)</color>";
        if (!GameStates.CalledMeeting && !gamelog)
            return ColorString(Color.yellow, appointMode ? " [任命]" : " [Task]");
        return "<color=#808080>(未任命)</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        if (hasAppointed) return "";

        string prefix = isForHud ? "" : "<size=60%>";
        string c = "#f5a623";

        if (appointMode)
            return $"{prefix}<color={c}>【任命モード】キルボタンで対象を任命</color>";

        if (NextAppointCandidate != byte.MaxValue)
        {
            var cand = GetPlayerById(NextAppointCandidate);
            string name = cand != null ? cand.Data.PlayerName : "???";
            if (!CanApproach)
                return $"{prefix}<color={c}>候補: {name} (待機中...)</color>";
            if (nowcool > 0f)
                return $"{prefix}<color={c}>候補: {name} (CD: {Mathf.CeilToInt(nowcool)}s)</color>";
            return $"{prefix}<color={c}>候補: {name} に1.5秒近づいて任命！</color>";
        }

        return $"{prefix}<color={c}>ペット→任命モード / 会議自投票→近接任命候補を設定</color>";
    }

    public override bool CanTask() => true;

    public bool OverrideKillButton(out string text)
    {
        text = "VillageChief_Appoint";
        return true;
    }
}
