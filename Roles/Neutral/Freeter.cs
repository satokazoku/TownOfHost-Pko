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
    }

    static OptionItem OptBetCooldown;

    byte BetTargetId;
    public byte GetBetTargetId => BetTargetId;
    CustomRoles lastBetTargetRole;

    enum OptionName
    {
        FreeterBetCooldown,
    }

    private static void SetupOptionItem()
    {
        OptBetCooldown = FloatOptionItem.Create(
                RoleInfo, 10, OptionName.FreeterBetCooldown,
                new(0f, 60f, 2.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public bool CanUseKillButton() => Player.IsAlive() && BetTargetId == byte.MaxValue;
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => OptBetCooldown.GetFloat();

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
        if (BetTargetId == byte.MaxValue) return;
        if (seen.PlayerId != BetTargetId) return;

        var role = seen.GetCustomRole();
        enabled = true;
        roleText = UtilsRoleText.GetRoleName(role);
        if (ColorUtility.TryParseHtmlString(UtilsRoleText.GetRoleColorCode(role), out var color))
            roleColor = color;
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
        if (BetTargetId == byte.MaxValue) return;

        _ = new LateTask(() =>
        {
            if (BetTargetId == byte.MaxValue) return;
            NameColorManager.Add(BetTargetId, Player.PlayerId, "#32cd32");
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        }, 0.3f, "Freeter.ColorRefresh", true);
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
        if (!Player.IsAlive()) return false;
        if (BetTargetId == byte.MaxValue) return false;

        var target = GetPlayerById(BetTargetId);
        if (target == null) return false;

        if (target.Is(CustomRoles.Freeter)) return false;

        return CustomWinnerHolder.WinnerIds.Contains(BetTargetId)
            || CustomWinnerHolder.WinnerRoles.Any(role => target.Is(role));
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (BetTargetId == byte.MaxValue) return;
        if (CustomWinnerHolder.WinnerIds.Contains(Player.PlayerId)) return;

        var target = GetPlayerById(BetTargetId);
        if (target == null || target.Is(CustomRoles.Freeter)) return;

        bool employerWon = CustomWinnerHolder.WinnerIds.Contains(BetTargetId)
                        || CustomWinnerHolder.WinnerRoles.Any(role => target.Is(role));
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
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        BetTargetId = reader.ReadByte();
    }
}