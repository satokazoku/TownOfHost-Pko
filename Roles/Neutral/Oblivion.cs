/*
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Oblivion : RoleBase, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Oblivion),
            player => new Oblivion(player),
            CustomRoles.Oblivion,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            53200,
            SetUpOptionItem,
            "ob",
            "#808080",
            (7, 2),
            true,
            from: From.SuperNewRoles
        );

    public Oblivion(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        hasTransformed = false;
        pendingDeadPlayerId = byte.MaxValue;
    }

    static OptionItem OptionShowArrow;
    static bool ShowArrow;

    static void SetUpOptionItem()
    {
        OptionShowArrow = BooleanOptionItem.Create(
            RoleInfo, 10, "OblivionShowArrow", true, false);
    }

    bool hasTransformed;
    byte pendingDeadPlayerId;
    readonly Dictionary<byte, Vector2> deadBodyPositions = new();

    public override void Add()
    {
        ShowArrow = OptionShowArrow.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public override void OnDestroy()
    {
        ClearAllBodyArrows();
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ClearAllBodyArrows();

        if (!AmongUsClient.Instance.AmHost) return;
        if (hasTransformed) return;
        if (reporter == null || reporter.PlayerId != Player.PlayerId) return;
        if (target == null || target.Disconnected) return;

        var deadPlayer = GetPlayerById(target.PlayerId);
        if (deadPlayer == null) return;

        var newRole = deadPlayer.GetCustomRole();
        if (!IsValidRoleToInherit(newRole)) return;

        pendingDeadPlayerId = target.PlayerId;
        Logger.Info($"Oblivion: pending transform → {newRole} (from {target.PlayerName})", "Oblivion");
        SendRPC();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (pendingDeadPlayerId == byte.MaxValue) return;

        var deadPlayerId = pendingDeadPlayerId;
        pendingDeadPlayerId = byte.MaxValue;

        var deadPlayer = GetPlayerById(deadPlayerId);
        if (deadPlayer == null) { SendRPC(); return; }

        var newRole = deadPlayer.GetCustomRole();
        if (!IsValidRoleToInherit(newRole)) { SendRPC(); return; }

        hasTransformed = true;

        if (!Utils.RoleSendList.Contains(Player.PlayerId))
            Utils.RoleSendList.Add(Player.PlayerId);

        Player.RpcSetCustomRole(newRole, log: null);

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;

            var roleClass = Player.GetRoleClass();
            roleClass?.StartGameTasks();

            Player.MarkDirtySettings();
            Player.SyncSettings();
            Player.ResetKillCooldown();
            Player.SetKillCooldown(delay: true, force: true);
            Player.RpcResetAbilityCooldown(Sync: true);

            GameData.Instance?.RecomputeTaskCounts();
            GameManager.Instance?.CheckTaskCompletion();

            UtilsNotifyRoles.NotifyRoles(ForceLoop: true, SpecifySeer: Player);
        }, 0.15f, "Oblivion.Transform", true);

        SendRPC();

        Utils.SendMessage(
            string.Format(GetString("OblivionTransformed"), UtilsRoleText.GetRoleName(newRole)),
            Player.PlayerId);

        UtilsGameLog.AddGameLog(
            "Oblivion",
            $"{UtilsName.GetPlayerColor(Player)} → {UtilsRoleText.GetRoleName(newRole)} " +
            $"(死体: {UtilsName.GetPlayerColor(deadPlayer)})");

        UtilsNotifyRoles.NotifyRoles(ForceLoop: true, OnlyMeName: true, SpecifySeer: Player);
    }

    public bool? CheckKillFlash(MurderInfo info)
    {
        if (!ShowArrow) return false;
        if (!Player.IsAlive() || hasTransformed) return false;

        var dead = info.AppearanceTarget;
        if (dead == null) return false;

        AddDeadBodyArrow(dead.PlayerId, dead.GetTruePosition());
        return false;
    }

    public override void OnStartMeeting()
    {
        ClearAllBodyArrows();
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        if (player == null) return;
        RemoveDeadBodyArrow(player.PlayerId);

        if (pendingDeadPlayerId == player.PlayerId)
        {
            pendingDeadPlayerId = byte.MaxValue;
            SendRPC();
        }
    }

    static bool IsValidRoleToInherit(CustomRoles role)
        => role is not (CustomRoles.GM or CustomRoles.NotAssigned or CustomRoles.Oblivion);

    void AddDeadBodyArrow(byte playerId, Vector2 pos)
    {
        if (deadBodyPositions.TryGetValue(playerId, out var old))
            GetArrow.Remove(Player.PlayerId, old);

        deadBodyPositions[playerId] = pos;
        GetArrow.Add(Player.PlayerId, pos);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void RemoveDeadBodyArrow(byte playerId)
    {
        if (!deadBodyPositions.TryGetValue(playerId, out var pos)) return;
        GetArrow.Remove(Player.PlayerId, pos);
        deadBodyPositions.Remove(playerId);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void ClearAllBodyArrows()
    {
        foreach (var pos in deadBodyPositions.Values)
            GetArrow.Remove(Player.PlayerId, pos);
        deadBodyPositions.Clear();
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting || !Player.IsAlive() || !Is(seer) || !Is(seen)) return "";
        if (hasTransformed || !ShowArrow || deadBodyPositions.Count == 0) return "";

        var arrows = "";
        foreach (var pos in deadBodyPositions.Values)
            arrows += GetArrow.GetArrows(seer, pos);

        return arrows == "" ? "" : $"<color=#808080>{arrows}</color>";
    }

    public override string GetProgressText(bool comms = false, bool gameLog = false)
    {
        if (hasTransformed) return "";
        return "<color=#b0b0d0>(未変化)</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || hasTransformed) return "";

        string pre = isForHud ? "" : "<size=60%>";
        if (pendingDeadPlayerId != byte.MaxValue)
            return $"{pre}<color=#808080>会議後に役職が変化する...</color>";
        return $"{pre}<color=#808080>死体を通報すると役職を引き継ぐ</color>";
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(hasTransformed);
        sender.Writer.Write(pendingDeadPlayerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        hasTransformed = reader.ReadBoolean();
        pendingDeadPlayerId = reader.ReadByte();
    }
}
*/
