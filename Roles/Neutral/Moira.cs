/*
using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;
using static TownOfHost.UtilsRoleText;
namespace TownOfHost.Roles.Neutral;

public sealed class Moira : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Moira),
            player => new Moira(player),
            CustomRoles.Moira,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            53100,
            SetupOptionItem,
            "mo",
            "#c084fc",
            (6, 3),
            true,
            from: From.SuperNewRoles,
            assignInfo: new RoleAssignInfo(CustomRoles.Moira, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );

    public Moira(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        MaxSwaps = OptionMaxSwaps.GetInt();
        SwapVotes = OptionSwapVotes.GetBool();
        remainingSwaps = MaxSwaps;
        target1 = byte.MaxValue;
        target2 = byte.MaxValue;
        isRevealed = false;
        usedThisMeeting = false;
        swapHistory = new();
    }

    static OptionItem OptionMaxSwaps;
    static int MaxSwaps;
    static OptionItem OptionSwapVotes;
    static bool SwapVotes;

    int remainingSwaps;
    byte target1;
    byte target2;
    bool isRevealed;
    bool usedThisMeeting;

    List<(byte, byte, CustomRoles, CustomRoles)> swapHistory;

    enum OptionName { MoiraMaxSwaps, MoiraSwapVotes }

    private static void SetupOptionItem()
    {
        OptionMaxSwaps = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MoiraMaxSwaps,
            new(1, 10, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionSwapVotes = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MoiraSwapVotes, false, false);
    }

    bool ISelfVoter.CanUseVoted() => remainingSwaps > 0 && Player.IsAlive() && !usedThisMeeting;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter)) return true;
        if (remainingSwaps <= 0 || usedThisMeeting) return true;

        if (CheckVote.TryGetValue(Player.PlayerId, out var inMode) && inMode
            && target1 != byte.MaxValue && target2 == byte.MaxValue)
        {
            if (votedForId == Player.PlayerId || votedForId == byte.MaxValue)
            {
                target1 = byte.MaxValue;
                target2 = byte.MaxValue;
                SetMode(Player, false);
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                SendRPC();
                return false;
            }
            RegisterTarget(votedForId);
            SendRPC();
            return false;
        }

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            switch (status)
            {
                case VoteStatus.Self:
                    target1 = byte.MaxValue;
                    target2 = byte.MaxValue;
                    Utils.SendMessage(
                        string.Format(GetString("SkillMode"),
                            GetString("Mode.Moira"), GetString("Vote.Moira"))
                        + GetString("VoteSkillMode"),
                        Player.PlayerId);
                    SetMode(Player, true);
                    break;

                case VoteStatus.Skip:
                    target1 = byte.MaxValue;
                    target2 = byte.MaxValue;
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    SetMode(Player, false);
                    break;

                case VoteStatus.Vote:
                    RegisterTarget(votedForId);
                    break;
            }
            SendRPC();
            return false;
        }
        return true;
    }

    void RegisterTarget(byte id)
    {
        var target = GetPlayerById(id);
        if (target == null || !target.IsAlive()) return;

        if (target1 == byte.MaxValue)
        {
            target1 = id;
            Utils.SendMessage(
                $"<color={RoleInfo.RoleColorCode}>【運命改変】</color>\n" +
                $"1人目: {UtilsName.GetPlayerColor(target, true)} を選択しました。\n" +
                $"次に2人目に投票してください。",
                Player.PlayerId);
        }
        else if (id != target1)
        {
            target2 = id;
            usedThisMeeting = true;
            SetMode(Player, false);
            Utils.SendMessage(
                $"<color={RoleInfo.RoleColorCode}>【運命改変】</color>\n" +
                $"✓ {UtilsName.GetPlayerColor(GetPlayerById(target1), true)} と " +
                $"{UtilsName.GetPlayerColor(target, true)} の運命改変を確定しました！",
                Player.PlayerId);
        }
        SendRPC();
    }

    public override void OnStartMeeting()
    {
        usedThisMeeting = false;
        target1 = byte.MaxValue;
        target2 = byte.MaxValue;
        SendRPC();
    }

    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(
        byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        var (votedForId, numVotes, doVote) = base.ModifyVote(voterId, sourceVotedForId, isIntentional);

        if (!SwapVotes || !usedThisMeeting) return (votedForId, numVotes, doVote);
        if (target1 == byte.MaxValue || target2 == byte.MaxValue) return (votedForId, numVotes, doVote);

        if (votedForId == target1) votedForId = target2;
        else if (votedForId == target2) votedForId = target1;

        return (votedForId, numVotes, doVote);
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (target1 == byte.MaxValue || target2 == byte.MaxValue) return;

        ExecuteSwap(target1, target2);
        target1 = byte.MaxValue;
        target2 = byte.MaxValue;
        SendRPC();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        usedThisMeeting = false;
        target1 = byte.MaxValue;
        target2 = byte.MaxValue;
        SendRPC();
    }

    void ExecuteSwap(byte id1, byte id2)
    {
        var p1 = GetPlayerById(id1);
        var p2 = GetPlayerById(id2);
        if (p1 == null || p2 == null) return;
        if (!p1.IsAlive() || !p2.IsAlive()) return;

        var role1 = p1.GetCustomRole();
        var role2 = p2.GetCustomRole();

        swapHistory.Add((id1, id2, role1, role2));

        if (!Utils.RoleSendList.Contains(id1)) Utils.RoleSendList.Add(id1);
        if (!Utils.RoleSendList.Contains(id2)) Utils.RoleSendList.Add(id2);
        p1.RpcSetCustomRole(role2, true, log: null);
        p2.RpcSetCustomRole(role1, true, log: null);

        remainingSwaps--;

        if (remainingSwaps <= 0 && !isRevealed)
        {
            isRevealed = true;
            Utils.SendMessage(
                string.Format(GetString("MoiraRevealed"), UtilsName.GetPlayerColor(Player, true)));
        }

        SwapTaskState(p1, p2);

        var announceId1 = id1;
        var announceId2 = id2;
        _ = new LateTask(() =>
        {
            var ap1 = GetPlayerById(announceId1);
            var ap2 = GetPlayerById(announceId2);
            if (ap1 != null && ap2 != null)
            {
                Utils.SendMessage(string.Format(
                    GetString("MoiraSwapAnnounce"),
                    UtilsName.GetPlayerColor(ap1, true),
                    UtilsName.GetPlayerColor(ap2, true)));
            }
        }, Main.LagTime, "Moira.SwapAnnounce", true);

        UtilsGameLog.AddGameLog("Moira",
            $"{UtilsName.GetPlayerColor(Player)}が" +
            $"{UtilsName.GetPlayerColor(p1)}と{UtilsName.GetPlayerColor(p2)}の役職を入れ替えた");

        SendRPC();
        UtilsNotifyRoles.NotifyRoles();
    }

    static void SwapTaskState(PlayerControl p1, PlayerControl p2)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var tasks1 = p1.Data.Tasks?.ToArray();
        var tasks2 = p2.Data.Tasks?.ToArray();
        if (tasks1 == null || tasks2 == null) return;

        int minLen = Math.Min(tasks1.Length, tasks2.Length);
        for (int i = 0; i < minLen; i++)
            (tasks1[i].Complete, tasks2[i].Complete) = (tasks2[i].Complete, tasks1[i].Complete);

        p1.MarkDirtySettings();
        p2.MarkDirtySettings();
        GameManager.Instance.CheckTaskCompletion();
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!Player.IsAlive()) return;
        if (remainingSwaps > 0) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Moira, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (isRevealed) return $"<color={RoleInfo.RoleColorCode}>【公開済】</color>";
        return $"<color={RoleInfo.RoleColorCode}>({remainingSwaps})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (!isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (remainingSwaps <= 0)
            return $"{size}<color={color}>運命改変使用済み。生存で勝利！</color>";
        if (usedThisMeeting)
            return $"{size}<color={color}>この会議は使用済み</color>";
        if (target1 != byte.MaxValue)
            return $"{size}<color={color}>2人目に投票して入れ替え確定</color>";
        return $"{size}<color={color}>自投票→運命改変モード | 残り{remainingSwaps}回</color>";
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!isForMeeting) return "";
        if (!Is(seer) || !Player.IsAlive()) return "";
        if (seen.PlayerId == Player.PlayerId) return "";

        string color = RoleInfo.RoleColorCode;
        if (seen.PlayerId == target1) return $" <color={color}>①</color>";
        if (seen.PlayerId == target2) return $" <color={color}>②</color>";
        return "";
    }

    public override bool GetTemporaryName(ref string name, ref bool NoMarker, bool isForMeeting,
        PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!isRevealed) return false;
        if (!Is(seen)) return false;
        name = $"<color={RoleInfo.RoleColorCode}>【モイラ】</color>{seen.Data.PlayerName}";
        return true;
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(remainingSwaps);
        sender.Writer.Write(target1);
        sender.Writer.Write(target2);
        sender.Writer.Write(isRevealed);
        sender.Writer.Write(usedThisMeeting);
        sender.Writer.Write(swapHistory.Count);
        foreach (var (id1, id2, r1, r2) in swapHistory)
        {
            sender.Writer.Write(id1);
            sender.Writer.Write(id2);
            sender.Writer.WritePacked((int)r1);
            sender.Writer.WritePacked((int)r2);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainingSwaps = reader.ReadInt32();
        target1 = reader.ReadByte();
        target2 = reader.ReadByte();
        isRevealed = reader.ReadBoolean();
        usedThisMeeting = reader.ReadBoolean();
        int count = reader.ReadInt32();
        swapHistory.Clear();
        for (int i = 0; i < count; i++)
        {
            var id1 = reader.ReadByte();
            var id2 = reader.ReadByte();
            var r1 = (CustomRoles)reader.ReadPackedInt32();
            var r2 = (CustomRoles)reader.ReadPackedInt32();
            swapHistory.Add((id1, id2, r1, r2));
        }
    }
}
*/