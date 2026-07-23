using AmongUs.GameOptions;
using Hazel;
using System.Linq;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;

namespace TownOfHost.Roles.Neutral;

public sealed class Freeter : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Freeter),
            player => new Freeter(player),
            CustomRoles.Freeter,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            51600,
            SetupOptionItem,
            "tt",
            "#32cd32",
            (6, 2),
            from: From.SuperNewRoles,
            isDesyncImpostor: true
        );

    public Freeter(PlayerControl player)
        : base(RoleInfo, player)
    {
        BetTargetId = byte.MaxValue;
        hasBeenEmployed = false;
        unemployedTurns = 0;
        lastBetTargetRole = CustomRoles.NotAssigned;
        initialBetTargetRole = CustomRoles.NotAssigned;
    }

    static OptionItem OptUnemployedDeathTurns;
    static OptionItem OptInitialCooldown;
    static OptionItem OptFinalCooldown;
    static OptionItem OptSeeEmployerRole;
    static OptionItem OptSurvivalRequiredForWin;

    byte BetTargetId;
    public byte GetBetTargetId => BetTargetId;
    CustomRoles lastBetTargetRole;
    CustomRoles initialBetTargetRole;
    bool hasBeenEmployed;
    int unemployedTurns;

    enum OptionName
    {
        FreeterUnemployedDeathTurns,
        FreeterInitialCooldown,
        FreeterFinalCooldown,
        FreeterSeeEmployerRole,
        FreeterSurvivalRequiredForWin,
    }

    private static void SetupOptionItem()
    {
        OptUnemployedDeathTurns = IntegerOptionItem.Create(
                RoleInfo, 10, OptionName.FreeterUnemployedDeathTurns,
                new(0, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Times)
            .SetZeroNotation(OptionZeroNotation.Infinity);

        OptInitialCooldown = FloatOptionItem.Create(
                RoleInfo, 11, OptionName.FreeterInitialCooldown,
                new(0f, 60f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);

        OptFinalCooldown = FloatOptionItem.Create(
                RoleInfo, 12, OptionName.FreeterFinalCooldown,
                new(0f, 120f, 2.5f), 90f, false)
            .SetValueFormat(OptionFormat.Seconds);

        OptSeeEmployerRole = BooleanOptionItem.Create(
            RoleInfo, 13, OptionName.FreeterSeeEmployerRole, true, false);

        OptSurvivalRequiredForWin = BooleanOptionItem.Create(
            RoleInfo, 14, OptionName.FreeterSurvivalRequiredForWin, false, false);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public bool CanUseKillButton() => Player.IsAlive() && BetTargetId == byte.MaxValue;
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() =>
        hasBeenEmployed ? OptFinalCooldown.GetFloat() : OptInitialCooldown.GetFloat();

    public bool OverrideKillButtonText(out string text)
    {
        text = BetTargetId == byte.MaxValue ? "就職" : "就職済み";
        return true;
    }
    public bool OverrideKillButton(out string text)
    {
        text = "Freeter_Job";
        return true;
    }

    public override void OverrideDisplayRoleNameAsSeer(
        PlayerControl seen,
        ref bool enabled,
        ref Color roleColor,
        ref string roleText,
        ref bool addon)
    {
        if (!OptSeeEmployerRole.GetBool()) return;
        if (BetTargetId == byte.MaxValue) return;
        if (seen.PlayerId != BetTargetId) return;

        var role = seen.GetCustomRole();
        enabled = true;
        roleText = UtilsRoleText.GetRoleName(role);
        if (ColorUtility.TryParseHtmlString(UtilsRoleText.GetRoleColorCode(role), out var color))
            roleColor = color;
        addon = true;
    }

    public override void OverrideDisplayRoleNameAsSeen(
        PlayerControl seer,
        ref bool enabled,
        ref Color roleColor,
        ref string roleText,
        ref bool addon)
    {
        if (BetTargetId == byte.MaxValue) return;
        if (seer.PlayerId != BetTargetId) return;

        enabled = true;
        roleText = UtilsRoleText.GetRoleName(CustomRoles.Freeter);
        if (ColorUtility.TryParseHtmlString(RoleInfo.RoleColorCode, out var c))
            roleColor = c;
        addon = true;
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (freeter, target) = info.AttemptTuple;
        info.DoKill = false;

        if (BetTargetId != byte.MaxValue) return;
        if (target.PlayerId == freeter.PlayerId) return;

        var closest = GetClosestPlayerInRange();
        closest ??= target;

        BetTargetId = closest.PlayerId;
        lastBetTargetRole = closest.GetCustomRole();
        initialBetTargetRole = lastBetTargetRole;
        hasBeenEmployed = true;
        unemployedTurns = 0;

        NameColorManager.Add(closest.PlayerId, Player.PlayerId, "#32cd32");

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
        UtilsOption.MarkEveryoneDirtySettings();

        SendRPC();
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player), 0.2f, "Freeter Bet");

        freeter.ResetKillCooldown();
        freeter.SetKillCooldown();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (BetTargetId == byte.MaxValue) return;
        if (!AmongUsClient.Instance.AmHost) return;

        var target = GetPlayerById(BetTargetId);

        if (target == null || !target.IsAlive())
        {
            NameColorManager.Remove(BetTargetId, Player.PlayerId);
            BetTargetId = byte.MaxValue;
            lastBetTargetRole = CustomRoles.NotAssigned;
            initialBetTargetRole = CustomRoles.NotAssigned;
            SendRPC();
            SendMessage(GetString("Freeter_BetTargetDead"), Player.PlayerId);
            Player.ResetKillCooldown();
            Player.SetKillCooldown();
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player), 0.2f, "Freeter Reset");
            return;
        }

        var currentRole = target.GetCustomRole();
        if (currentRole != lastBetTargetRole)
        {
            lastBetTargetRole = currentRole;
            NameColorManager.Add(BetTargetId, Player.PlayerId, "#32cd32");
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player), 0.2f, "Freeter ColorFix");
        }
    }

    public override void OnStartMeeting()
    {
        if (BetTargetId == byte.MaxValue) return;
        NameColorManager.Add(BetTargetId, Player.PlayerId, "#32cd32");
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (BetTargetId == byte.MaxValue)
        {
            CheckUnemployedDeath();
            return;
        }

        _ = new LateTask(() =>
        {
            if (BetTargetId == byte.MaxValue) return;
            NameColorManager.Add(BetTargetId, Player.PlayerId, "#32cd32");
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        }, 0.3f, "Freeter.ColorRefresh", true);
    }

    private void CheckUnemployedDeath()
    {
        if (!Player.IsAlive()) return;

        int deathTurns = OptUnemployedDeathTurns.GetInt();
        if (deathTurns <= 0) return;

        unemployedTurns++;
        SendRPC();

        if (unemployedTurns < deathTurns) return;

        var state = PlayerState.GetByPlayerId(Player.PlayerId);
        state.DeathReason = CustomDeathReason.Suicide;
        state.SetDead();
        Player.RpcMurderPlayerV2(Player);
        SendMessage(GetString("Freeter_UnemployedDeath"), Player.PlayerId);
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (BetTargetId != byte.MaxValue) return "";

        int deathTurns = OptUnemployedDeathTurns.GetInt();
        if (deathTurns <= 0) return "";

        string sz = isForHud ? "" : "<size=60%>";
        int remain = Mathf.Max(0, deathTurns - unemployedTurns);
        return $"{sz}<color=#ff8800>無職状態: あと{remain}ターンで死亡</color>";
    }

    public override void ChengeRoleAdd()
    {
        base.ChengeRoleAdd();
        if (BetTargetId == byte.MaxValue) return;

        var target = GetPlayerById(BetTargetId);
        if (target == null) return;

        var role = target.GetCustomRole();
        if (role.IsImpostor())
            target.RpcSetRoleDesync(RoleTypes.Impostor, target.GetClientId());
        else
            target.RpcSetRoleDesync(RoleTypes.Crewmate, target.GetClientId());
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (OptSurvivalRequiredForWin.GetBool() && !Player.IsAlive()) return false;
        if (BetTargetId == byte.MaxValue) return false;

        var target = GetPlayerById(BetTargetId);
        if (target == null) return false;

        if (target.Is(CustomRoles.Freeter)) return false;

        return CustomWinnerHolder.WinnerIds.Contains(BetTargetId)
            || CustomWinnerHolder.WinnerRoles.Any(role =>
                target.Is(role)
                || role == initialBetTargetRole
                || role == lastBetTargetRole);
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (OptSurvivalRequiredForWin.GetBool() && !Player.IsAlive()) return;
        if (BetTargetId == byte.MaxValue) return;
        if (CustomWinnerHolder.WinnerIds.Contains(Player.PlayerId)) return;

        var target = GetPlayerById(BetTargetId);
        if (target == null || target.Is(CustomRoles.Freeter)) return;

        bool employerWon = CustomWinnerHolder.WinnerIds.Contains(BetTargetId)
                        || CustomWinnerHolder.WinnerRoles.Any(role =>
                            target.Is(role)
                            || role == initialBetTargetRole
                            || role == lastBetTargetRole);
        if (!employerWon) return;

        CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        if (!CustomWinnerHolder.AdditionalWinnerRoles.Contains(CustomRoles.Freeter))
            CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Freeter);
    }

    private PlayerControl GetClosestPlayerInRange()
    {
        const float maxDist = 1f;
        PlayerControl closest = null;
        float minDist = float.MaxValue;

        foreach (var pc in AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            float dist = Vector2.Distance(Player.GetTruePosition(), pc.GetTruePosition());
            if (dist < maxDist && dist < minDist)
            {
                minDist = dist;
                closest = pc;
            }
        }
        return closest;
    }

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(BetTargetId);
        sender.Writer.Write(hasBeenEmployed);
        sender.Writer.Write(unemployedTurns);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        BetTargetId = reader.ReadByte();
        hasBeenEmployed = reader.ReadBoolean();
        unemployedTurns = reader.ReadInt32();
    }
}