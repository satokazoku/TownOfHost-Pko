using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Duelist : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Duelist),
            player => new Duelist(player),
            CustomRoles.Duelist,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            485300,
            SetupOptionItem,
            "dl",
            "#ff6347",
            (4, 9),
            from: From.TownOfHost_Y
        );

    public Duelist(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        archenemyPlayerId = byte.MaxValue;
        hasChosenArchenemy = false;
        inSelectionMode = false;

        Duelists.Add(this);
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        Duelists.Remove(this);
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    static OptionItem OptionMeetingLimit;
    public static int MeetingLimit;

    enum OptionName { DuelistMeetingLimit }

    static void SetupOptionItem()
    {
        OptionMeetingLimit = IntegerOptionItem.Create(
            RoleInfo, 10, OptionName.DuelistMeetingLimit,
            new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.day);
    }

    private static readonly HashSet<Duelist> Duelists = new();

    byte archenemyPlayerId;
    bool hasChosenArchenemy;
    bool inSelectionMode;

    private PlayerControl Archenemy
        => archenemyPlayerId == byte.MaxValue ? null : GetPlayerById(archenemyPlayerId);

    public override void Add()
    {
        MeetingLimit = OptionMeetingLimit.GetInt();
    }

    public override void OnStartMeeting()
    {
        inSelectionMode = false;
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !Player.IsAlive()) return true;

        if (hasChosenArchenemy) return true;

        if (!inSelectionMode)
        {
            if (votedForId == Player.PlayerId)
            {
                inSelectionMode = true;
                int left = MeetingLimit - UtilsGameLog.day;
                string warn = left <= 0
                    ? "<color=#ff1919>！期限切れ！</color>"
                    : $"残り{left}ターン";
                SendMessage(
                    $"<color={RoleInfo.RoleColorCode}>" +
                    $"【選択モード】誰かに投票して宿敵を指定してください\n" +
                    $"自投票・スキップでキャンセル ({warn})</color>",
                    Player.PlayerId);
                return false;
            }
            return true;
        }
        else
        {
            if (votedForId == Player.PlayerId || votedForId >= 253)
            {
                inSelectionMode = false;
                SendMessage(
                    $"<color={RoleInfo.RoleColorCode}>宿敵選択をキャンセルしました</color>",
                    Player.PlayerId);
                return false;
            }

            var target = GetPlayerById(votedForId);
            if (target == null || !target.IsAlive())
            {
                inSelectionMode = false;
                return false;
            }

            hasChosenArchenemy = true;
            archenemyPlayerId = votedForId;
            inSelectionMode = false;
            SendRpc();

            SendMessage(
                $"<color={RoleInfo.RoleColorCode}>{target.Data.PlayerName} を宿敵に指定！\n" +
                $"相手が死亡すれば追加勝利！</color>",
                Player.PlayerId);
            SendMessage(
                $"<color={RoleInfo.RoleColorCode}>あなたは {Player.Data.PlayerName} に宿敵として指定されました。\n" +
                $"相手が死亡すれば追加勝利！</color>",
                target.PlayerId);

            UtilsNotifyRoles.NotifyRoles();
            return false;
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (hasChosenArchenemy) return;
        if (!Player.IsAlive()) return;

        if (MeetingLimit > UtilsGameLog.day) return;

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            var state = PlayerState.GetByPlayerId(Player.PlayerId);
            if (state != null) state.DeathReason = CustomDeathReason.Suicide;
            Player.SetRealKiller(Player);
            Player.RpcMurderPlayerV2(Player);
            UtilsGameLog.AddGameLog("Duelist",
                $"{UtilsName.GetPlayerColor(Player)} は期限内に宿敵を決めず自爆した");
        }, 0.1f, "Duelist.Suicide", true);
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (archenemyPlayerId == byte.MaxValue) return false;
        var ae = Archenemy;
        return Player.IsAlive() && (ae == null || !ae.IsAlive());
    }

    public static bool ArchenemyCheckWin(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return false;
        foreach (var duelist in Duelists)
        {
            if (duelist.archenemyPlayerId == byte.MaxValue) continue;
            if (pc.PlayerId == duelist.archenemyPlayerId && !duelist.Player.IsAlive())
                return true;
        }
        return false;
    }

    public static bool CheckNotify(PlayerControl pc)
    {
        foreach (var duelist in Duelists)
        {
            if (pc.PlayerId == duelist.archenemyPlayerId || pc.PlayerId == duelist.Player.PlayerId)
                return true;
        }
        return false;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || archenemyPlayerId == byte.MaxValue) return "";
        if (seen.PlayerId == archenemyPlayerId)
            return ColorString(RoleInfo.RoleColor, "χ");
        return "";
    }

    public string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (archenemyPlayerId == byte.MaxValue) return "";
        if (seer.PlayerId == archenemyPlayerId && seen.PlayerId == Player.PlayerId)
            return ColorString(RoleInfo.RoleColor, "χ");
        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!hasChosenArchenemy)
        {
            bool danger = UtilsGameLog.day >= MeetingLimit;
            string c = danger ? "#ff1919" : RoleInfo.RoleColorCode;
            return $"<color={c}>({UtilsGameLog.day}/{MeetingLimit})</color>";
        }
        if (archenemyPlayerId == byte.MaxValue)
            return $"<color=#888888>(自爆)</color>";

        var ae = Archenemy;
        bool dead = ae == null || !ae.IsAlive();
        return dead
            ? $"<color={RoleInfo.RoleColorCode}>(宿敵†✓)</color>"
            : $"<color={RoleInfo.RoleColorCode}>(宿敵♦)</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        string size = isForHud ? "" : "<size=60%>";
        string c = RoleInfo.RoleColorCode;

        if (!hasChosenArchenemy && isForMeeting)
        {
            if (!inSelectionMode)
            {
                int left = MeetingLimit - UtilsGameLog.day;
                string warn = left <= 0
                    ? "<color=#ff1919>！期限切れ！</color>"
                    : $"残り{left}ターン";
                return $"{size}<color={c}>自投票 → 宿敵選択モードへ ({warn})\n" +
                       $"通常投票 → 今ターンはパス</color>";
            }
            else
            {
                return $"{size}<color={c}>【選択中】誰かに投票して宿敵を指定\n" +
                       $"自投票・スキップでキャンセル</color>";
            }
        }

        if (archenemyPlayerId == byte.MaxValue) return "";

        var ae = Archenemy;
        string name = ae?.Data?.PlayerName ?? "???";
        bool aeDead = ae == null || !ae.IsAlive();
        string status = aeDead ? "<color=#00ff88>†</color>" : "<color=#ff4444>生</color>";
        return $"{size}<color={c}>宿敵: {name} {status}</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(archenemyPlayerId);
        sender.Writer.Write(hasChosenArchenemy);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        archenemyPlayerId = reader.ReadByte();
        hasChosenArchenemy = reader.ReadBoolean();
    }
}