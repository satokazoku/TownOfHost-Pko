using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Neutral;

public sealed class BatGirl : RoleBase, ISelfVoter, IUsePhantomButton, IAdditionalWinner
{
    static readonly HashSet<BatGirl> Instances = new();

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BatGirl),
            player => new BatGirl(player),
            CustomRoles.BatGirl,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            50400,
            SetupOptionItem,
            "bg",
            "#ff4f8f",
            (8, 1),
            true,
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            Desc: () => GetString((OptionAddWin?.GetBool() ?? true) ? "BatGirlInfoLongAddWin" : "BatGirlInfoLongSoloOnly")
        );

    static OptionItem OptionSuicideCooldown;
    static OptionItem OptionPrinceDetectRange;
    static OptionItem OptionPrinceDetectSeconds;
    static OptionItem OptionAddWin;

    enum OptionName
    {
        BatGirlSuicideCooldown,
        BatGirlPrinceDetectRange,
        BatGirlPrinceDetectSeconds,
        BatGirlAddWin
    }

    byte princeId;
    bool inLoveMode;
    bool soloDeathSatisfied;
    bool followQueued;

    bool princeDiedThisTurn;
    bool batGirlDiedThisTurn;

    bool prevBatGirlAlive;
    bool prevPrinceAlive;

    readonly Dictionary<byte, float> nearPrinceTimers;
    readonly HashSet<byte> notifyPlayerIdsAtMeeting;

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionSuicideCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.BatGirlSuicideCooldown, new(0f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionPrinceDetectRange = FloatOptionItem.Create(RoleInfo, 11, OptionName.BatGirlPrinceDetectRange, new(1.25f, 5f, 0.25f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionPrinceDetectSeconds = FloatOptionItem.Create(RoleInfo, 12, OptionName.BatGirlPrinceDetectSeconds, new(0.5f, 10f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 13, OptionName.BatGirlAddWin, true, false);
    }

    public BatGirl(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        Instances.Add(this);
        princeId = byte.MaxValue;
        inLoveMode = false;
        soloDeathSatisfied = false;
        followQueued = false;
        princeDiedThisTurn = false;
        batGirlDiedThisTurn = false;
        prevBatGirlAlive = Player.IsAlive();
        prevPrinceAlive = false;
        nearPrinceTimers = new();
        notifyPlayerIdsAtMeeting = new();
    }

    public override void Add()
    {
        CustomRoleManager.OnMurderPlayerOthers.Add(OnAnyMurdered);
        princeId = byte.MaxValue;
        inLoveMode = false;
        soloDeathSatisfied = false;
        followQueued = false;
        princeDiedThisTurn = false;
        batGirlDiedThisTurn = false;
        prevBatGirlAlive = Player.IsAlive();
        prevPrinceAlive = false;
        nearPrinceTimers.Clear();
        notifyPlayerIdsAtMeeting.Clear();
        SendRPC();
    }

    public override void OnDestroy()
    {
        CustomRoleManager.OnMurderPlayerOthers.Remove(OnAnyMurdered);
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = OptionSuicideCooldown.GetFloat();
    }

    bool ISelfVoter.CanUseVoted() => Canuseability() && Player.IsAlive() && princeId == byte.MaxValue;

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter)) return true;
        if (!Player.IsAlive()) return true;
        if (princeId != byte.MaxValue) return true;

        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            switch (status)
            {
                case VoteStatus.Self:
                    inLoveMode = true;
                    SetMode(Player, true);
                    Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.BatGirl"), GetString("Vote.BatGirl")) + GetString("VoteSkillMode"), Player.PlayerId);
                    SendRPC();
                    return false;
                case VoteStatus.Skip:
                    inLoveMode = false;
                    SetMode(Player, false);
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    SendRPC();
                    return false;
                case VoteStatus.Vote:
                    if (!inLoveMode) return true;
                    SelectPrince(votedForId);
                    return false;
            }
        }
        return true;
    }

    void SelectPrince(byte targetId)
    {
        var target = PlayerCatch.GetPlayerById(targetId);
        if (target == null || !target.IsAlive() || target.PlayerId == Player.PlayerId)
        {
            Utils.SendMessage(GetString("BatGirlPrinceInvalid"), Player.PlayerId);
            return;
        }

        princeId = target.PlayerId;
        inLoveMode = false;
        SetMode(Player, false);
        prevPrinceAlive = target.IsAlive();
        nearPrinceTimers.Clear();
        notifyPlayerIdsAtMeeting.Clear();
        followQueued = false;
        SendRPC();
        Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} prince set => {target.GetNameWithRole().RemoveHtmlTags()}", "BatGirl");

        Utils.SendMessage(string.Format(GetString("BatGirlPrinceSet"), UtilsName.GetPlayerColor(target, true)) + GetString("VoteSkillFin"), Player.PlayerId);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (princeId != byte.MaxValue && Main.AfterMeetingDeathPlayers.ContainsKey(princeId))
            MarkPrinceDeathThisTurn();
        if (Main.AfterMeetingDeathPlayers.ContainsKey(Player.PlayerId))
            MarkBatGirlDeathThisTurn();

        var prince = GetPrince();
        bool nowBatGirlAlive = Player.IsAlive();
        bool nowPrinceAlive = prince?.IsAlive() ?? false;

        if (prevBatGirlAlive && !nowBatGirlAlive)
            MarkBatGirlDeathThisTurn();
        if (prevPrinceAlive && !nowPrinceAlive)
            MarkPrinceDeathThisTurn();

        prevBatGirlAlive = nowBatGirlAlive;
        prevPrinceAlive = nowPrinceAlive;

        if (!nowBatGirlAlive || !nowPrinceAlive) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;

        UpdatePrinceProximity(prince);
    }

    void UpdatePrinceProximity(PlayerControl prince)
    {
        float range = OptionPrinceDetectRange.GetFloat();
        float required = OptionPrinceDetectSeconds.GetFloat();
        Vector2 princePos = prince.GetTruePosition();

        var targets = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != Player.PlayerId && pc.PlayerId != prince.PlayerId)
            .ToArray();

        var validIds = new HashSet<byte>(targets.Select(t => t.PlayerId));
        foreach (var oldId in nearPrinceTimers.Keys.ToArray())
        {
            if (!validIds.Contains(oldId)) nearPrinceTimers.Remove(oldId);
        }

        foreach (var target in targets)
        {
            float dist = Vector2.Distance(princePos, target.GetTruePosition());
            if (!nearPrinceTimers.TryGetValue(target.PlayerId, out float timer))
                timer = 0f;

            if (dist <= range)
            {
                timer += Time.fixedDeltaTime;
                if (timer >= required)
                {
                    notifyPlayerIdsAtMeeting.Add(target.PlayerId);
                    timer = required;
                }
            }
            else
            {
                timer = 0f;
            }

            nearPrinceTimers[target.PlayerId] = timer;
        }
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (princeDiedThisTurn && batGirlDiedThisTurn)
            soloDeathSatisfied = true;

        princeDiedThisTurn = false;
        batGirlDiedThisTurn = false;
        inLoveMode = false;

        if (notifyPlayerIdsAtMeeting.Count > 0)
        {
            var msg = GetString("BatGirlStareNotice");
            var title = GetString("BatGirlStareTitle");
            foreach (var targetId in notifyPlayerIdsAtMeeting.ToArray())
            {
                if (PlayerCatch.GetPlayerById(targetId) == null) continue;
                MeetingHudPatch.EnqueuePostDayMeetingMessage(targetId, msg, title);
            }
            notifyPlayerIdsAtMeeting.Clear();
        }
        nearPrinceTimers.Clear();

        prevBatGirlAlive = Player.IsAlive();
        prevPrinceAlive = GetPrince()?.IsAlive() ?? false;
        SendRPC();
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled == null) return;

        if (exiled.PlayerId == princeId)
        {
            princeDiedThisTurn = true;
            TrySetSoloDeathSatisfied();
            QueueFollowSuicideIfNeeded();
        }
        if (exiled.PlayerId == Player.PlayerId)
        {
            batGirlDiedThisTurn = true;
            TrySetSoloDeathSatisfied();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        inLoveMode = false;

        QueueFollowSuicideIfNeeded();

        prevBatGirlAlive = Player.IsAlive();
        prevPrinceAlive = GetPrince()?.IsAlive() ?? false;
        SendRPC();
    }

    void QueueFollowSuicideIfNeeded()
    {
        if (!Player.IsAlive()) return;
        if (followQueued) return;
        if (princeId == byte.MaxValue) return;
        if (Main.AfterMeetingDeathPlayers.ContainsKey(Player.PlayerId)) return;

        var prince = GetPrince();
        if (prince?.IsAlive() == true) return;

        MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.FollowingSuicide, Player.PlayerId);
        followQueued = true;

        if (princeDiedThisTurn)
        {
            batGirlDiedThisTurn = true;
            TrySetSoloDeathSatisfied();
        }
    }

    void MarkPrinceDeathThisTurn()
    {
        princeDiedThisTurn = true;
        TrySetSoloDeathSatisfied();
    }

    void MarkBatGirlDeathThisTurn()
    {
        batGirlDiedThisTurn = true;
        TrySetSoloDeathSatisfied();
    }

    void TrySetSoloDeathSatisfied()
    {
        if (princeDiedThisTurn && batGirlDiedThisTurn)
        {
            soloDeathSatisfied = true;
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} soloDeathSatisfied=true (prince:{princeId})", "BatGirl");
        }
    }

    void OnAnyMurdered(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (info?.AttemptTarget == null) return;

        var targetId = info.AttemptTarget.PlayerId;
        if (targetId == Player.PlayerId) MarkBatGirlDeathThisTurn();
        if (targetId == princeId) MarkPrinceDeathThisTurn();
    }

    bool CanSoloWinNow()
    {
        if (princeId == byte.MaxValue) return false;
        return soloDeathSatisfied || (princeDiedThisTurn && batGirlDiedThisTurn);
    }

    public static bool TryTakeOverSoloWin(ref GameOverReason reason)
    {
        if (OptionAddWin.GetBool())
        {
            return false;
        }

        foreach (var role in Instances.ToArray())
        {
            if (role == null) continue;
            if (!role.CanSoloWinNow()) continue;

            Logger.Info($"TryTakeOverSoloWin candidate: {role.Player.GetNameWithRole().RemoveHtmlTags()} winner={CustomWinnerHolder.WinnerTeam}", "BatGirl");

            if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.BatGirl, role.Player.PlayerId, true, CustomRoles.BatGirl))
            {
                Logger.Info("TryTakeOverSoloWin rejected by priority/options", "BatGirl");
                continue;
            }

            CustomWinnerHolder.NeutralWinnerIds.Add(role.Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(role.Player.PlayerId);
            reason = GameOverReason.ImpostorsByKill;
            Logger.Info($"TryTakeOverSoloWin success: {role.Player.GetNameWithRole().RemoveHtmlTags()}", "BatGirl");
            return true;
        }

        return false;
    }

    public static string NormalizeWinnerText(string text)
    {
        return text;
    }

    PlayerControl GetPrince() => princeId == byte.MaxValue ? null : PlayerCatch.GetPlayerById(princeId);

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        bool hasSelfWinner = CustomWinnerHolder.WinnerIds.Contains(Player.PlayerId);
        bool hasBatGirlAdditional = CustomWinnerHolder.AdditionalWinnerRoles.Contains(CustomRoles.BatGirl);
        Logger.Info(
            $"CheckWinner state: {Player.GetNameWithRole().RemoveHtmlTags()} prince={princeId} pDead={princeDiedThisTurn} bgDead={batGirlDiedThisTurn} sat={soloDeathSatisfied} winner={CustomWinnerHolder.WinnerTeam} hasSelfWinner={hasSelfWinner} hasBatGirlAdditional={hasBatGirlAdditional}",
            "BatGirl"
        );
        if (!CanSoloWinNow()) return;

        if (OptionAddWin.GetBool())
        {
            Logger.Info("CheckWinner skip takeover: AddWin enabled", "BatGirl");
            return;
        }

        if (CustomWinnerHolder.WinnerTeam == CustomWinner.BatGirl)
        {
            if (!hasSelfWinner)
                CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            Logger.Info("CheckWinner skip duplicate add-win: WinnerTeam already BatGirl", "BatGirl");
            return;
        }

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.BatGirl, Player.PlayerId, false, CustomRoles.BatGirl))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
    {
        if (!OptionAddWin.GetBool()) return false;

        if (CanSoloWinNow())
        {
            winnerRole = CustomRoles.BatGirl;
            return true;
        }

        if (!Player.IsAlive()) return false;
        var prince = GetPrince();
        if (prince == null || !prince.IsAlive()) return false;

        winnerRole = CustomRoles.BatGirl;
        return true;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!Player.IsAlive()) return;

        AdjustKillCooldown = false;
        ResetCooldown = false;
        MyState.DeathReason = CustomDeathReason.Suicide;
        Player.SetRealKiller(Player);
        Player.RpcMurderPlayer(Player, true);
    }

    bool IUsePhantomButton.IsPhantomRole => Player.IsAlive();
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override string GetAbilityButtonText() => GetString("BatGirlAbilityButton");

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.PlayerId != Player.PlayerId) return "";

        var prince = GetPrince();
        if (seen.PlayerId == Player.PlayerId)
        {
            if (OptionAddWin.GetBool() && Player.IsAlive() && prince?.IsAlive() == true)
                return Utils.AdditionalWinnerMark;
            return "";
        }

        if (prince != null && seen.PlayerId == prince.PlayerId && prince.IsAlive())
            return $" <color=#ff4f8f>{'\u2661'}</color>";

        return "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId || !Player.IsAlive()) return "";

        if (isForMeeting && princeId == byte.MaxValue && Canuseability())
        {
            var text = inLoveMode ? GetString("BatGirlVoteModeOn") : GetString("SelfVoteRoleInfoMeg");
            var msg = $"<color={RoleInfo.RoleColorCode}>{text}</color>";
            return isForHud ? msg.RemoveSizeTags() : $"<size=45%>{msg}</size>";
        }
        return "";
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(princeId);
        sender.Writer.Write(inLoveMode);
        sender.Writer.Write(soloDeathSatisfied);
        sender.Writer.Write(followQueued);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        princeId = reader.ReadByte();
        inLoveMode = reader.ReadBoolean();
        soloDeathSatisfied = reader.ReadBoolean();
        followQueued = reader.ReadBoolean();
    }
}