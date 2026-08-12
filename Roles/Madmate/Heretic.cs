using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Modules.ChatManager;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Madmate;

public sealed class Heretic : RoleBase, IKiller, IKillFlashSeeable, IDeathReasonSeeable, INekomata, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Heretic),
            player => new Heretic(player),
            CustomRoles.Heretic,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Madmate,
            21500,
            SetupOptionItem,
            "he",
            OptionSort: (2, 1),
            isDesyncImpostor: true,
            introSound: () => GetIntroSound(RoleTypes.Phantom),
            from: From.ExtremeRoles
        );

    public Heretic(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        Mode = (ModeOption)OptionMode.GetValue();
        CanVent = OptionCanVent.GetBool();
        CanSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        CanSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();

        impostorsGetRevenged = optionImpostorsGetRevenged.GetBool();
        madmatesGetRevenged = optionMadmatesGetRevenged.GetBool();
        neutralsGetRevenged = optionNeutralsGetRevenged.GetBool();
        meetingtarg = false;

        targetPlayerId = 255;

        checkSelfVote();
    }


    private static bool impostorsGetRevenged;
    private static bool madmatesGetRevenged;
    private static bool neutralsGetRevenged;

    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionMode;
    private static OptionItem OptionCanVent;

    private static float KillCooldown;
    private static bool CanVent;
    private static bool CanSeeKillFlash;
    private static bool CanSeeDeathReason;
    public byte targetPlayerId;

    /// <summary>インポスターを道連れ候補に含む</summary>
    private static BooleanOptionItem optionImpostorsGetRevenged;

    /// <summary>マッドメイトを道連れ候補に含む</summary>
    private static BooleanOptionItem optionMadmatesGetRevenged;

    /// <summary>ニュートラルを道連れ候補に含む</summary>
    private static BooleanOptionItem optionNeutralsGetRevenged;
    private ModeOption Mode;

    private enum SuicideMotionOption
    {
        Default,
        MotionKilled
    }

    private enum ModeOption
    {
        Task,
        TaskTarget,
        Meeting,
        MeetingTarget,
        Eject
    }

    private enum OptionName
    {
        SillySheriffSuicideMotion,
        Mode,
        BlackCatImpostorsGetRevenged,
        BlackCatMadmatesGetRevenged,
        BlackCatNeutralsGetRevenged,
    }

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionMode = StringOptionItem.Create(RoleInfo, 12, OptionName.Mode, EnumHelper.GetAllNames<ModeOption>(), 0, false);
        optionImpostorsGetRevenged =
        BooleanOptionItem.Create(RoleInfo, 13,
        OptionName.BlackCatImpostorsGetRevenged,
        false, false);

        optionMadmatesGetRevenged =
            BooleanOptionItem.Create(RoleInfo, 14,
                OptionName.BlackCatMadmatesGetRevenged,
                false, false);

        optionNeutralsGetRevenged =
            BooleanOptionItem.Create(RoleInfo, 15,
                OptionName.BlackCatNeutralsGetRevenged,
                false, false);
        RoleAddAddons.Create(RoleInfo, 16, MadMate: true);
    }

    public bool CanUseKillButton() => Player.IsAlive();
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseImpostorVentButton() => CanVent;
    public bool CanUseSabotageButton() => false;
    public bool? CheckKillFlash(MurderInfo info) => CanSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => CanSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
    bool IKiller.CanKill
    {
        get
        {
            return Mode switch
            {
                ModeOption.Task => true,
                ModeOption.TaskTarget => true,
                ModeOption.Meeting => false,
                ModeOption.MeetingTarget => false,
                ModeOption.Eject => false,
                _ => false,
            };
        }
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        switch (Mode)
        {
            case ModeOption.Task:
                if (!Is(info.AttemptKiller) || info.IsSuicide) return;

                PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = CustomDeathReason.Spell;
                PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Spell;
                        killer.RpcMurderPlayer(killer);
                        break;
            case ModeOption.Eject:
                info.DoKill = false;
                break;
            case ModeOption.Meeting:
                info.DoKill = false;
                break;
            case ModeOption.MeetingTarget:
                info.DoKill = false;
                break;
            case ModeOption.TaskTarget:
                info.DoKill = false;
                if (!Is(info.AttemptKiller) || info.IsSuicide)
                {
                    return;
                }
                if (targetPlayerId == 255)
                {
                    targetPlayerId = target.PlayerId;
                }
                break;
        }
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("DeathReason.Spell");
        return true;
    }
    public bool OverrideKillButton(out string text)
    {
        text = "Witch_Ability";
        return true;
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        // seen が省略されたら seer を使う
        seen ??= seer;

        // target フラグを最新化
        checkNekomata();

        if (!target) return "";

        // PlayerId と targetPlayerId を比較して一致する場合のみマークを付ける
        if (seen.PlayerId == targetPlayerId)
            return Utils.ColorString(RoleInfo.RoleColor, "×");

        return "";
    }
    public bool DoRevenge(CustomDeathReason deathReason)
    => deathReason == CustomDeathReason.Vote;

    private bool Nekomata;
    private bool target;
    private void checkNekomata()
    {
        switch (Mode)
        {
            case ModeOption.Task:
                Nekomata = false;
                break;
            case ModeOption.Meeting:
                Nekomata = false;
                break;
            case ModeOption.TaskTarget:
                Nekomata = false;
                target = true;
                break;
            case ModeOption.MeetingTarget:
                Nekomata = false;
                target = true;
                break;
            case ModeOption.Eject:
                Nekomata = true;
                break;
        }
    }
    private bool selfvote;
    private bool meetingtarg;
    private void checkSelfVote()
    {
        switch (Mode)
        {
            case ModeOption.Task:
                selfvote = false;
                break;
            case ModeOption.Meeting:
                selfvote = true;
                break;
            case ModeOption.MeetingTarget:
                selfvote = true;
                meetingtarg = true;
                break;
            case ModeOption.TaskTarget:
                selfvote = false;
                break;
            case ModeOption.Eject:
                selfvote = false;
                break;
        }
    }



    public bool IsCandidate(PlayerControl player)
    {
        checkNekomata();
        if (Nekomata)
        {
            return player.GetCustomRole().GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => impostorsGetRevenged,
                CustomRoleTypes.Madmate => madmatesGetRevenged,
                CustomRoleTypes.Neutral => neutralsGetRevenged,
                _ => true,
            };
        }
        else if (target)
        {
            return player.PlayerId == targetPlayerId;
        }
        else
        {
            return player.GetCustomRole().GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => false,
                CustomRoleTypes.Madmate => false,
                CustomRoleTypes.Neutral => false,
                _ => false,
            };
        }
    }
    bool ISelfVoter.CanUseVoted() => selfvote;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        // 修正点: 他者の投票を誤って処理しないようにガードを追加し、selfvoteが無効な場合は何もしない
        if (!selfvote) return true;                 // selfvoteモードでないなら通常の投票を許可
        if (!Canuseability()) return true;          // 能力使用ができないなら通常投票
        if (!Is(voter)) return true;                // 自分以外の投票には反応しない

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            if (status is VoteStatus.Self)
                Utils.SendMessage(string.Format(GetString("Mode.Heretic"), Player.PlayerId));
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
                HereticSelfvote(votedForId);
            SetMode(Player, status is VoteStatus.Self);
            return false;
        }
        return true;
    }
    public void HereticSelfvote(byte votedForId)
    {
        if (!selfvote) return;
        if (meetingtarg)
        {
            var target = PlayerCatch.GetPlayerById(votedForId);
            targetPlayerId = target.PlayerId;
            selfvote = false;
        }
        else
        {
            var target = PlayerCatch.GetPlayerById(votedForId);
            if (!target.IsAlive()) return;
            if (!AmongUsClient.Instance.AmHost) return;
            if (target.Is(CustomRoles.Stand))
            {
                var sm = target.GetRoleClass() as TownOfHost.Roles.Neutral.Stand;
                var owner = sm?.GetOwner();
                if (owner != null && owner.Player.IsAlive())
                {
                    Utils.SendMessage(
                        "<color=#8B4513>残念だったな！スタンドは撃ち抜けないんだぜ！</color>",
                        Player.PlayerId);
                    return;
                }
            }
            var meetingHud = MeetingHud.Instance;
            var hudManager = DestroyableSingleton<HudManager>.Instance.KillOverlay;

            if ((PlayerCatch.AllPlayerControls.Any(pc => pc.Is(CustomRoles.Guesser)) || CustomRolesHelper.CheckGuesser()) && !Options.ExHideChatCommand.GetBool())
                ChatManager.SendPreviousMessagesToAll();

            var AlienTairo = false;
            var targetroleclass = target.GetRoleClass();
            if ((targetroleclass as Alien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            if ((targetroleclass as JackalAlien)?.CheckSheriffKill(target) == true) AlienTairo = true;
            if ((targetroleclass as AlienHijack)?.CheckSheriffKill(target) == true) AlienTairo = true;

            if (!AlienTairo)
            {
                // target 側の PlayerState を明示的に宣言して使う
                var targetState = PlayerState.GetByPlayerId(target.PlayerId);
                target.RpcExileV3();
                targetState.DeathReason = CustomDeathReason.Spell;
                targetState.SetDead();

                // 自分（発動者）の PlayerState も別変数で扱う
                var selfState = PlayerState.GetByPlayerId(Player.PlayerId);
                Player.RpcExileV3();
                selfState.DeathReason = CustomDeathReason.Spell;
                selfState.SetDead();

                UtilsGameLog.AddGameLog($"Alchemist", $"{UtilsName.GetPlayerColor(target, true)}(<b>{UtilsRoleText.GetTrueRoleName(target.PlayerId, false)}</b>) [{Utils.GetVitalText(target.PlayerId, true)}]");
                UtilsGameLog.AddGameLogsub($"\n\t⇐ {UtilsName.GetPlayerColor(Player, true)}(<b>{UtilsRoleText.GetTrueRoleName(Player.PlayerId, false)}</b>)");

                if (Options.ExHideChatCommand.GetBool())
                {
                    ChatManager.OnDisconnectOrDeadPlayer(target.PlayerId);
                }
                Utils.SendMessage(UtilsName.GetPlayerColor(target, true) + GetString("Meetingkill"), title: GetString("MSKillTitle"));
                foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null && !pc.IsAlive()))
                {
                    Utils.SendMessage(string.Format(GetString("MMeetingKill"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(target, true)), go.PlayerId, GetString("RMSKillTitle"));
                }

                MeetingVoteManager.ResetVoteManager(target.PlayerId);
                if (target != PlayerControl.LocalPlayer) Player.RpcMeetingKill(target);
                return;
            }
            Player.RpcExileV3();
            MyState.DeathReason = target.Is(CustomRoles.Tairou) && Tairou.TairoDeathReason ? CustomDeathReason.Counter :
                                target.Is(CustomRoles.Alien) && Alien.TairoDeathReason ? CustomDeathReason.Counter :
                                (target.Is(CustomRoles.JackalAlien) && JackalAlien.TairoDeathReason ? CustomDeathReason.Counter :
                                (target.Is(CustomRoles.AlienHijack) && Alien.TairoDeathReason ? CustomDeathReason.Counter : CustomDeathReason.Misfire));
            MyState.SetDead();

            if (Options.ExHideChatCommand.GetBool())
            {
                ChatManager.OnDisconnectOrDeadPlayer(Player.PlayerId);
            }
            Utils.SendMessage(UtilsName.GetPlayerColor(Player, true) + GetString("Meetingkill"), title: GetString("MSKillTitle"));
            foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null && !pc.IsAlive()))
            {
                Utils.SendMessage(string.Format(GetString("MMeetingKillfall"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(target, true)), go.PlayerId, GetString("RMSKillTitle"));
            }

            MeetingVoteManager.ResetVoteManager(Player.PlayerId);
            if (Player != PlayerControl.LocalPlayer) Player.RpcMeetingKill(Player);
        }
    }
}
