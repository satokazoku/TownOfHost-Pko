using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using AmongUs.GameOptions;
using HarmonyLib;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class NiceGuesser : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceGuesser),
            player => new NiceGuesser(player),
            CustomRoles.NiceGuesser,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            33400,
            SetupOptionItem,
            "ng",
            "#dcf500",
            (1, 6),
            Desc: () => string.Format
            (
                TargetingMode.GetInt() == 0 ? GetString("EvilGuesserDesc") : GetString("EvilGuesserDescSelfVote")
            ),
            from: From.TheOtherRoles
        );

    public NiceGuesser(PlayerControl player)
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
        CanGuessWhiteCrew = BooleanOptionItem.Create(RoleInfo, 14, OptionName.CanWhiteCrew, false, false);

        var targetingModeNames = Enum.GetNames(typeof(TargetingModeOption));
        TargetingMode = StringOptionItem.Create(RoleInfo, 15, OptionName.TargetingMode, targetingModeNames, 1, false);
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
        private static readonly System.Reflection.MethodInfo PcIsCustomRoleMethod =
            AccessTools.Method(typeof(ExtendedPlayerControl), nameof(ExtendedPlayerControl.Is), new[] { typeof(PlayerControl), typeof(CustomRoles) });

        private static readonly System.Reflection.MethodInfo IsGuesserLikeMethod =
            AccessTools.Method(typeof(GuessManagerGuesserMsgPatch), nameof(IsGuesserLike));

        // TargetingMode = ManualId: 従来通り "/cmd /bt <id> <役職>" をそのまま通す
        // TargetingMode = SelfVote : "/cmd /bt <役職>" を受け取り、選択済みターゲットIDを差し込んで
        //                            "/cmd /bt <id> <役職>" に書き換える
        private static bool Prefix(PlayerControl pc, ref string msg)
        {
            if (pc == null || !pc.Is(CustomRoles.NiceGuesser) || !IsBtCommand(msg)) return true;

            var state = PlayerState.GetByPlayerId(pc.PlayerId);
            state?.SetSubRole(CustomRoles.Guesser);

            if (!IsSelfVoteMode) return true; // ManualId方式: idはmsgに既に含まれている前提

            if (pc.GetRoleClass() is not NiceGuesser role || role.targetId is not byte selectedTargetId)
            {
                Utils.SendMessage(GetString("EvilGuesserNoTargetSelected"), pc.PlayerId,
                    Utils.ColorString(Palette.CrewmateBlue, GetString("DefaultSystemMessageTitle")));
                return false; // ターゲット未選択のため元処理は呼ばない
            }

            var tokens = new List<string>(msg.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            // tokens: ["/cmd", "/bt" or "bt", "<役職>", ...]
            tokens.Insert(2, selectedTargetId.ToString());
            msg = string.Join(' ', tokens);

            return true;
        }

        // Guesser role check should treat NiceGuesser as Guesser only during /cmd bt handling.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
            {
                if (code.Calls(PcIsCustomRoleMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, IsGuesserLikeMethod);
                }
                else
                {
                    yield return code;
                }
            }
        }

        private static bool IsGuesserLike(PlayerControl target, CustomRoles role)
        {
            if (target == null) return false;

            if (target.Is(role)) return true;

            return role == CustomRoles.Guesser && target.Is(CustomRoles.NiceGuesser);
        }

        private static void Postfix(PlayerControl pc, string msg, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.NiceGuesser) || !IsBtCommand(msg)) return;

            if (IsSelfVoteMode && pc.GetRoleClass() is NiceGuesser role) role.targetId = null; // 1回使ったら選び直し

            // Mark command as handled so the raw /cmd bt line is not exposed in public chat.
            __result = true;
        }
    }

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GuessCountCrewandMad))]
    private static class GuessCountCrewAndMadPatch
    {
        private static bool Prefix(PlayerControl pc, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.NiceGuesser))
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

    [HarmonyPatch(typeof(GuessManager), nameof(GuessManager.GetCrewmateGuessResult))]
    private static class GetCrewmateGuessResultPatch
    {
        private static bool Prefix(PlayerControl pc, PlayerControl target, CustomRoles guessrole, ref bool __result)
        {
            if (pc == null || !pc.Is(CustomRoles.NiceGuesser))
                return true;

            if (guessrole.IsCrewmate() && !CanGuessNakama.GetBool())
            {
                Utils.SendMessage(GetString("GuessTeamMate"), pc.PlayerId, Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Crewmate), GetString("GuessTeamMateTitle")));
                __result = true;
                return false;
            }

            if (guessrole.IsWhiteCrew() && !CanGuessWhiteCrew.GetBool())
            {
                Utils.SendMessage(string.Format(GetString("GuessWhiteRole"), GetString("Crewmate")), pc.PlayerId, Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.UltraStar), GetString("GuessWhiteRoleTitle")));
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
            if (CustomRoles.NiceGuesser.IsPresent()) __result = true;
        }
    }
}