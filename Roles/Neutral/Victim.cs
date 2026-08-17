using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Victim : RoleBase, IKiller, IAdditionalWinner
{
    const float AbuserForcedKillGraceTime = 3f;

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Victim),
            player => new Victim(player),
            CustomRoles.Victim,
            () => Abuser.CanVictimVentBeforeAwakening ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            78400,
            null,
            "vic",
            "#6f8fa8",
            (3, 3),
            false,
            tab: TabGroup.Combinations,
            countType: CountTypes.Crew,
            combination: CombinationRoles.AbuserandVictim,
            from: From.TownOfHost_Pko
        );

    enum VictimMode : byte
    {
        Dormant,
        Awakened,
        Crewmate
    }

    VictimMode mode;
    bool forcedKillArmed;
    bool performingForcedKill;
    float forcedKillTimer;
    byte abuserId;
    Abuser abuser;

    public bool IsAwakened => mode == VictimMode.Awakened;

    public override RoleTypes? AfterMeetingRole => mode switch
    {
        VictimMode.Awakened => RoleTypes.Impostor,
        VictimMode.Crewmate => RoleTypes.Crewmate,
        _ => Abuser.CanVictimVentBeforeAwakening ? RoleTypes.Engineer : RoleTypes.Crewmate
    };

    public Victim(PlayerControl player) : base(RoleInfo, player, () => HasTask.False)
    {
        mode = VictimMode.Dormant;
        abuserId = byte.MaxValue;
    }

    public override void Add()
    {
        MyState.SetCountType(CountTypes.Crew);
        BindAbuser();
        SendRPC();
    }

    public override void StartGameTasks() => BindAbuser();

    internal void BindAbuser(Abuser role)
    {
        abuser = role;
        abuserId = role?.Player?.PlayerId ?? byte.MaxValue;
    }

    void BindAbuser()
    {
        abuser ??= CustomRoleManager.AllActiveRoles.Values.OfType<Abuser>().FirstOrDefault();
        if (abuser?.Player != null)
            abuserId = abuser.Player.PlayerId;
    }

    // 覚醒前でも、加虐者から押し付けられた自動キルの実行中だけは
    // 共通キル検証を通過できるようにする。
    public bool CanKill => mode == VictimMode.Awakened || performingForcedKill;
    public bool CanUseKillButton() => (mode == VictimMode.Awakened || performingForcedKill) && Player.IsAlive();
    public float CalculateKillCooldown() => Abuser.VictimKillCooldown?.GetFloat() ?? 30f;
    public bool CanUseImpostorVentButton() => mode switch
    {
        VictimMode.Dormant => Abuser.CanVictimVentBeforeAwakening,
        VictimMode.Awakened => Abuser.CanVictimVentAfterAwakening,
        _ => false
    };
    public bool CanUseSabotageButton() => mode == VictimMode.Awakened && Abuser.CanVictimSabotageAfterAwakening;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(mode == VictimMode.Awakened);

    internal void ArmForcedKill()
    {
        if (!Player.IsAlive() || mode == VictimMode.Crewmate) return;
        forcedKillArmed = true;
        forcedKillTimer = 0f;
        Logger.Info($"{Player.Data.GetLogPlayerName()}を強制キル待機状態にしました", "Abuser");
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    internal void Awaken()
    {
        if (mode != VictimMode.Dormant) return;
        mode = VictimMode.Awakened;
        MyState.SetCountType(CountTypes.Victim);
        SetAwakenedBaseRole();
        Player.SyncSettings();
        Player.SetKillCooldown(force: true);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    void BecomeCrewmate()
    {
        if (mode == VictimMode.Crewmate) return;
        mode = VictimMode.Crewmate;
        forcedKillArmed = false;
        forcedKillTimer = 0f;
        MyState.SetCountType(CountTypes.Crew);
        MyState.NowRoleType = RoleTypes.Crewmate;
        if (AmongUsClient.Instance.AmHost)
            Player.RpcSetRoleDesync(RoleTypes.Crewmate, Player.GetClientId());
        Player.SyncSettings();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!performingForcedKill && mode != VictimMode.Awakened)
            info.DoKill = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !forcedKillArmed || !GameStates.IsInTask || GameStates.IsMeeting) return;
        if (!Player.IsAlive() || mode == VictimMode.Crewmate)
        {
            CancelForcedKill();
            return;
        }

        forcedKillTimer += Time.fixedDeltaTime;
        if (forcedKillTimer < (Abuser.ForcedKillDelay?.GetFloat() ?? 5f)) return;

        BindAbuser();
        var protectAbuser = forcedKillTimer < AbuserForcedKillGraceTime;
        var target = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc != null
                && pc.PlayerId != Player.PlayerId
                && (!protectAbuser || pc.PlayerId != abuserId)
                && !pc.inVent)
            .OrderBy(pc => Vector2.Distance(Player.GetTruePosition(), pc.GetTruePosition()))
            .FirstOrDefault();
        if (target == null) return;

        var range = NormalGameOptionsV10.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
        if (Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition()) > range) return;

        forcedKillArmed = false;
        forcedKillTimer = 0f;
        performingForcedKill = true;
        Logger.Info($"{Player.Data.GetLogPlayerName()} => {target.Data.GetLogPlayerName()} の強制キルを実行します", "Abuser");
        SendRPC();
        try
        {
            CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, false);
        }
        finally
        {
            performingForcedKill = false;
        }
    }

    void CancelForcedKill()
    {
        if (!forcedKillArmed && forcedKillTimer <= 0f) return;
        forcedKillArmed = false;
        forcedKillTimer = 0f;
        SendRPC();
    }

    public override void OnStartMeeting() => CancelForcedKill();

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;
        if (mode == VictimMode.Awakened)
            SetAwakenedBaseRole();
        else if (mode == VictimMode.Crewmate)
            Player.RpcSetRoleDesync(RoleTypes.Crewmate, Player.GetClientId());
        SendRPC();
    }

    void SetAwakenedBaseRole()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MyState.NowRoleType = RoleTypes.Impostor;
        Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
        foreach (var impostor in PlayerCatch.AllPlayerControls.Where(pc => pc.PlayerId != Player.PlayerId && pc.Is(CustomRoleTypes.Impostor)))
            impostor.RpcSetRoleDesync(RoleTypes.Scientist, Player.GetClientId());
        Player.MarkDirtySettings();
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        BindAbuser();
        abuser?.OnVictimKilledBy(info.AttemptKiller);
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        BindAbuser();
        if (exiled?.PlayerId == Player.PlayerId)
            abuser?.OnVictimExiled();
        else if (exiled?.PlayerId == abuserId)
            BecomeCrewmate();
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (mode == VictimMode.Crewmate && CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate)
            return true;

        return mode == VictimMode.Dormant
            && Abuser.CanVictimWinWithImpostors
            && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor;
    }

    public void EnforceFactionWin()
    {
        var wins = (mode == VictimMode.Crewmate && CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate)
            || (mode == VictimMode.Dormant
                && Abuser.CanVictimWinWithImpostors
                && CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor);
        if (!wins) return;

        CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Victim);
    }

    public bool TryWinNow()
    {
        if (mode != VictimMode.Awakened || !Player.IsAlive()) return false;
        BindAbuser();
        if (abuserId == byte.MaxValue || PlayerCatch.GetPlayerById(abuserId)?.IsAlive() != false) return false;
        if (PlayerCatch.AllAlivePlayerControls.Count() != 2) return false;
        if (PlayerCatch.AllAlivePlayerControls.Any(pc => pc.PlayerId != Player.PlayerId && pc.Is(CustomRoleTypes.Impostor))) return false;
        if (PlayerCatch.AllPlayerControls.Any(pc => pc.PlayerId != abuserId && pc.IsAlive() && pc.Is(CustomRoleTypes.Impostor))) return false;

        if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Victim, Player.PlayerId, hantrole: CustomRoles.Victim))
            return false;

        CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        return true;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId) return "";
        var key = forcedKillArmed ? "VictimForcedKillArmed" : mode switch
        {
            VictimMode.Awakened => "VictimAwakened",
            VictimMode.Crewmate => "VictimCrewmateMode",
            _ => "VictimDormant"
        };
        var text = GetString(key);
        return isForHud ? text : $"<size=50%>{text}</size>";
    }

    void SendRPC()
    {
        // 覚醒状態はキルボタンの生成に必要なため、欠落しないようReliableで同期する。
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write((byte)mode);
        sender.Writer.Write(forcedKillArmed);
        sender.Writer.Write(forcedKillTimer);
        sender.Writer.Write(abuserId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        mode = (VictimMode)reader.ReadByte();
        forcedKillArmed = reader.ReadBoolean();
        forcedKillTimer = reader.ReadSingle();
        abuserId = reader.ReadByte();
        MyState.SetCountType(mode == VictimMode.Awakened ? CountTypes.Victim : CountTypes.Crew);
    }
}
