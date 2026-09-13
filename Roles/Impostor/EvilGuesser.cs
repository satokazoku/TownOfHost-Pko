using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilGuesser : RoleBase, IImpostor, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilGuesser),
            player => new EvilGuesser(player),
            CustomRoles.EvilGuesser,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4000,
            SetupOptionItem,
            "eg",
            "#ff1919",
            (2, 1),
            Desc: () => string.Format
            (
                TargetingMode.GetInt() == 0 ? GetString("EvilGuesserDesc") : GetString("EvilGuesserDescSelfVote")
            ),
            from: From.TheOtherRoles
        );

    public EvilGuesser(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
    {
    }

    // 会議中に自投票→対象投票で選ばれたターゲット。/bt実行後や会議開始時にリセットされる
    private byte? targetId;

    private static OptionItem CanGuessTime;
    private static OptionItem OwnCanGuessTime;
    private static OptionItem CanGuessVanilla;
    private static OptionItem CanGuessNakama;
    private static OptionItem CanGuessTaskDoneSnitch;
    private static OptionItem CanGuessWhiteCrew;
    private static OptionItem TargetingMode;

    // ターゲット指定方式。文字列選択肢としてオプション画面に表示される
    public enum TargetingModeOption
    {
        ManualId,   // 従来:「/cmd /bt id 役職」を直接打つ
        CmdAndSelfVote    // 新方式:自投票→対象投票→「/cmd /bt 役職」
    }

    private enum OptionName
    {
        CanGuessTime,
        OwnCanGuessTime,
        CanGuessVanilla,
        CanGuessNakama,
        CanGuessTaskDoneSnitch,
        CanWhiteCrew,
        TargetingMode
    }

    private static void SetupOptionItem()
    {
        CanGuessTime = IntegerOptionItem.Create(RoleInfo, 10, OptionName.CanGuessTime, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OwnCanGuessTime = IntegerOptionItem.Create(RoleInfo, 11, OptionName.OwnCanGuessTime, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        CanGuessVanilla = BooleanOptionItem.Create(RoleInfo, 12, OptionName.CanGuessVanilla, true, false);
        CanGuessNakama = BooleanOptionItem.Create(RoleInfo, 13, OptionName.CanGuessNakama, true, false);
        CanGuessTaskDoneSnitch = BooleanOptionItem.Create(RoleInfo, 14, OptionName.CanGuessTaskDoneSnitch, false, false);
        CanGuessWhiteCrew = BooleanOptionItem.Create(RoleInfo, 15, OptionName.CanWhiteCrew, false, false);

        var targetingModeNames = Enum.GetNames(typeof(TargetingModeOption));
        TargetingMode = StringOptionItem.Create(RoleInfo, 16, OptionName.TargetingMode, targetingModeNames, 1, false);
    }

    private static bool IsSelfVoteMode => TargetingMode.GetValue() == (int)TargetingModeOption.CmdAndSelfVote;

    private static bool IsBtCommand(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return false;

        var args = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2) return false;
        if (!args[0].Equals("/cmd", StringComparison.OrdinalIgnoreCase)) return false;

        var cmd = args[1].StartsWith("/") ? args[1] : $"/{args[1]}";
        return cmd.Equals("/bt", StringComparison.OrdinalIgnoreCase);
    }

    // ===== 自投票によるターゲット選択(TargetingMode = SelfVoteの時のみ有効) =====

    bool ISelfVoter.CanUseVoted() =>
        IsSelfVoteMode && GameStates.IsMeeting && Player.IsAlive() && targetId == null;

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (IsSelfVoteMode && isForMeeting && Player.IsAlive() && seer.PlayerId == seen.PlayerId && targetId == null)
        {
            var mes = $"<color={RoleInfo.RoleColorCode}>{GetString("EvilGuesserSelfVoteInfo")}</color>";
            return isForHud ? mes : $"<size=40%>{mes}</size>";
        }
        return "";
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!IsSelfVoteMode) return true;
        if (!Is(voter)) return true;
        if (targetId != null) return true; // 既に対象確定済みなら通常投票として扱う

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
                Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.EvilGuesser"), GetString("Vote.EvilGuesser")) + GetString("VoteSkillMode"), Player.PlayerId);
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
            {
                targetId = votedForId;
                var target = PlayerCatch.GetPlayerById(votedForId);
                Utils.SendMessage(string.Format(GetString("EvilGuesserTargetSelected"), UtilsName.GetPlayerColor(target, true)), Player.PlayerId);
            }
            SetMode(Player, status is VoteStatus.Self);
            return false;
        }
        return true;
    }

    public override void OnStartMeeting()
    {
        targetId = null;
    }

    // ===== ここまで自投票によるターゲット選択 =====

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GuesserMsg))]
    private static class GuessManagerGuesserMsgPatch
    {
        // TargetingMode = ManualId: 従来通り "/cmd /bt <id> <役職>" をそのまま通す
        // TargetingMode = SelfVote : "/cmd /bt <役職>" を受け取り、選択済みターゲットIDを差し込んで
        //                            "/cmd /bt <id> <役職>" に書き換える
        private static bool Prefix(PlayerControl pc, ref string msg)
        {
            if (pc == null || !pc.Is(CustomRoles.EvilGuesser) || !IsBtCommand(msg)) return true;

            var state = PlayerState.GetByPlayerId(pc.PlayerId);
            state?.SetSubRole(CustomRoles.Guesser);

            if (!IsSelfVoteMode) return true; // ManualId方式: idはmsgに既に含まれている前提

            if (pc.GetRoleClass() is not EvilGuesser role || role.targetId is not byte selectedTargetId)
            {
                Utils.SendMessage(GetString("EvilGuesserNoTargetSelected"), pc.PlayerId,
                    Utils.ColorString(Palette.ImpostorRed, GetString("DefaultSystemMessageTitle")));
                return false; // ターゲット未選択のため元処理は呼ばない
            }

            var tokens = new List<string>(msg.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            // tokens: ["/cmd", "/bt" or "bt", "<役職>", ...]
            tokens.Insert(2, selectedTargetId.ToString());
            msg = string.Join(' ', tokens);

            return true;
        }

        private static void Postfix(PlayerControl pc, string msg, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.EvilGuesser) || !IsBtCommand(msg)) return;

            if (IsSelfVoteMode && pc.GetRoleClass() is EvilGuesser role) role.targetId = null; // 1回使ったら選び直し

            __result = true;
        }
    }

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GuessCountImp))]
    private static class GuessCountImpPatch
    {
        private static bool Prefix(PlayerControl pc, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.EvilGuesser))
                return true;

            if (!GuessManager.GuesserGuessed.ContainsKey(pc.PlayerId)) GuessManager.GuesserGuessed[pc.PlayerId] = 0;
            if (!GuessManager.OneMeetingGuessed.ContainsKey(pc.PlayerId)) GuessManager.OneMeetingGuessed[pc.PlayerId] = 0;

            var shotLimit = CanGuessTime.GetInt();
            var oneMeetingShotLimit = OwnCanGuessTime.GetInt();

            if (GuessManager.GuesserGuessed[pc.PlayerId] >= shotLimit)
            {
                Utils.SendMessage(GetString("GuessercountError"), pc.PlayerId, Utils.ColorString(Palette.AcceptedGreen, GetString("GuessercountErrorT")));
                __result = true;
                return false;
            }

            if (GuessManager.OneMeetingGuessed[pc.PlayerId] >= oneMeetingShotLimit)
            {
                Utils.SendMessage(GetString("GuesserMTGcountError"), pc.PlayerId, Utils.ColorString(Palette.AcceptedGreen, GetString("GuesserMTGcountErrorT")));
                __result = true;
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GetImpostorGuessResult))]
    private static class GetImpostorGuessResultPatch
    {
        private static bool Prefix(PlayerControl pc, PlayerControl target, CustomRoles guessrole, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.EvilGuesser))
                return true;

            if (guessrole is CustomRoles.Snitch && target.AllTasksCompleted() && !CanGuessTaskDoneSnitch.GetBool())
            {
                Utils.SendMessage(string.Format(GetString("GuessSnitch"), GetString("Impostor")), pc.PlayerId, Utils.ColorString(Palette.ImpostorRed, GetString("GuessSnitchTitle")));
                __result = true;
                return false;
            }

            if (guessrole.IsImpostor() && target.Is(CustomRoleTypes.Impostor) && !CanGuessNakama.GetBool())
            {
                Utils.SendMessage(GetString("GuessTeamMate"), pc.PlayerId, Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Impostor), GetString("GuessTeamMateTitle")));
                __result = true;
                return false;
            }

            if (guessrole.IsWhiteCrew() && !CanGuessWhiteCrew.GetBool())
            {
                Utils.SendMessage(string.Format(GetString("GuessWhiteRole"), GetString("Impostor")), pc.PlayerId, Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.UltraStar), GetString("GuessWhiteRoleTitle")));
                __result = true;
                return false;
            }

            if (guessrole.IsVanilla() && !CanGuessVanilla.GetBool())
            {
                Utils.SendMessage(GetString("GuessVanillaRoleTitle"), pc.PlayerId, Utils.ColorString(UtilsRoleText.GetRoleColor(guessrole), GetString("GuessVanillaRole")));
                __result = true;
                return false;
            }
            if (!GameStates.IsMeeting)
            {
                __result = true;
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CustomRolesHelper), "CheckGuesser")]
    private static class CheckGuesserPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (__result) return;
            if (CustomRoles.EvilGuesser.IsPresent()) __result = true;
        }
    }
}