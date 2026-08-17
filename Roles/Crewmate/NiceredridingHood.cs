/*
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using System.Collections.Generic;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceredridingHood : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceredridingHood),
            player => new NiceredridingHood(player),
            CustomRoles.NiceRedRidingHood,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            37200,
            SetupOptionItem,
            "nrrh",
            "#fa8072",
            (5, 3),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.SuperNewRoles
        );

    public NiceredridingHood(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        remainingReviveCount = OptionReviveCount.GetInt();
        isKillerDeathRevive = OptionIsKillerDeathRevive.GetBool();
        killerPlayerId = byte.MaxValue;
        isRevivable = false;
        killerExiledThisMeeting = false;
    }

    static OptionItem OptionReviveCount;
    static OptionItem OptionIsKillerDeathRevive;

    enum OptionName
    {
        NiceRedRidingHoodReviveCount,
        NiceRedRidingHoodIsKillerDeathRevive,
    }

    int remainingReviveCount;
    bool isKillerDeathRevive;
    byte killerPlayerId;
    bool isRevivable;
    bool killerExiledThisMeeting;

    static void SetupOptionItem()
    {
        OptionReviveCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.NiceRedRidingHoodReviveCount,
            new(1, 15, 1), 1, false).SetValueFormat(OptionFormat.Times);
        OptionIsKillerDeathRevive = BooleanOptionItem.Create(RoleInfo, 11,
            OptionName.NiceRedRidingHoodIsKillerDeathRevive, true, false);
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (remainingReviveCount <= 0) return true;

        (var killer, _) = info.AttemptTuple;
        if (killer == null || killer.PlayerId == Player.PlayerId) return true;

        killerPlayerId = killer.PlayerId;
        isRevivable = true;

        Logger.Info(
            $"[NiceRedRidingHood] {Player.Data.GetLogPlayerName()} が " +
            $"{killer.Data.GetLogPlayerName()} に殺された (復活待機)",
            "NiceRedRidingHood");
        SendRpc();

        return true;
    }

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie,
        Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!isRevivable || killerPlayerId == byte.MaxValue) return false;

        if (Exiled != null && Exiled.PlayerId == killerPlayerId)
        {
            killerExiledThisMeeting = true;
            Logger.Info("[NiceRedRidingHood] キラー追放確認 → 復活フラグON", "NiceRedRidingHood");
        }

        return false;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!isRevivable || killerPlayerId == byte.MaxValue) return;

        bool shouldRevive = killerExiledThisMeeting;
        killerExiledThisMeeting = false;

        if (!shouldRevive && isKillerDeathRevive)
        {
            var killer = GetPlayerById(killerPlayerId);
            if (killer != null && !killer.IsAlive())
            {
                shouldRevive = true;
                Logger.Info("[NiceRedRidingHood] キラー死亡確認 → 復活フラグON", "NiceRedRidingHood");
            }
        }

        if (shouldRevive) DoRevive();
    }

    private void DoRevive()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!isRevivable || remainingReviveCount <= 0) return;

        remainingReviveCount--;
        isRevivable = false;
        killerPlayerId = byte.MaxValue;
        SendRpc();

        _ = new LateTask(() =>
        {
            if (Player == null) return;

            var state = PlayerState.GetByPlayerId(Player.PlayerId);
            if (state != null)
            {
                state.IsDead = false;
                state.DeathReason = CustomDeathReason.etc;
            }

            Player.Revive();
            Player.Data.IsDead = false;

            UtilsGameLog.AddGameLog("NiceRedRidingHood",
                $"{UtilsName.GetPlayerColor(Player)} が復活した (残り{remainingReviveCount}回)");
            UtilsNotifyRoles.NotifyRoles();

            Utils.SendMessage(GetString("NiceRedRidingHoodRevived"), Player.PlayerId);

            Logger.Info(
                $"[NiceRedRidingHood] 復活完了 残り{remainingReviveCount}回",
                "NiceRedRidingHood");

        }, Main.LagTime + 0.2f, "NiceRedRidingHood.Revive", true);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (remainingReviveCount <= 0) return "";
        string color = isRevivable ? "#ff4444" : RoleInfo.RoleColorCode;
        return $"<color={color}>({remainingReviveCount})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId) return "";

        if (!Player.IsAlive() && isRevivable && killerPlayerId != byte.MaxValue)
        {
            var killer = GetPlayerById(killerPlayerId);
            string killerName = killer?.Data?.PlayerName ?? "???";
            string size = isForHud ? "" : "<size=60%>";
            string cond = isKillerDeathRevive ? "死亡" : "追放";
            return $"{size}<color={RoleInfo.RoleColorCode}>{killerName} の{cond}で復活！</color>";
        }

        if (Player.IsAlive() && !isForMeeting)
        {
            string size = isForHud ? "" : "<size=60%>";
            return $"{size}<color={RoleInfo.RoleColorCode}>" +
                   $"自分を殺した相手が" +
                   $"{(isKillerDeathRevive ? "死亡" : "追放")}されると復活</color>";
        }

        return "";
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(remainingReviveCount);
        sender.Writer.Write(isRevivable);
        sender.Writer.Write(killerPlayerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainingReviveCount = reader.ReadInt32();
        isRevivable = reader.ReadBoolean();
        killerPlayerId = reader.ReadByte();
    }
}
*/
