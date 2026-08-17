using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Neutral;

namespace TownOfHost.Roles.Impostor;

public sealed class Abuser : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Abuser),
            player => new Abuser(player),
            CustomRoles.Abuser,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            78300,
            SetupOptionItem,
            "abu",
            "#8f1d2c",
            (3, 2),
            tab: TabGroup.Combinations,
            assignInfo: new RoleAssignInfo(CustomRoles.Abuser, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = [CustomRoles.Abuser, CustomRoles.Victim]
            },
            combination: CombinationRoles.AbuserandVictim,
            from: From.TownOfHost_Pko
        );

    static OptionItem VictimWinPriority;
    static OptionItem AbilityUseCount;
    static OptionItem VictimCanVentBeforeAwakening;
    internal static OptionItem VictimKillCooldown;
    static OptionItem VictimCanVentAfterAwakening;
    static OptionItem VictimCanSabotageAfterAwakening;
    static OptionItem VictimCanWinWithImpostorsBeforeAwakening;
    static OptionItem AbuserKillCooldown;
    static OptionItem AbilityCooldown;
    internal static OptionItem ForcedKillDelay;

    enum OptionName
    {
        AbuserAbilityUseCount,
        VictimCanVentBeforeAwakening,
        VictimKillCooldown,
        VictimCanVentAfterAwakening,
        VictimCanSabotageAfterAwakening,
        VictimCanWinWithImpostorsBeforeAwakening,
        AbuserKillCooldown,
        AbuserAbilityCooldown,
        AbuserForcedKillDelay
    }

    int remainingUses;
    bool victimResolvedByAbuser;
    bool pendingRetaliation;
    bool suicideScheduled;
    byte retaliationKillerId;
    Victim victim;

    public Abuser(PlayerControl player) : base(RoleInfo, player)
    {
        remainingUses = AbilityUseCount?.GetInt() ?? 2;
        retaliationKillerId = byte.MaxValue;
    }

    static void SetupOptionItem()
    {
        VictimWinPriority = SoloWinOption.Create(RoleInfo, 10, CustomRoles.Victim, defo: 1).OptionWin;
        AbilityUseCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AbuserAbilityUseCount, new(0, 99, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
        VictimCanVentBeforeAwakening = BooleanOptionItem.Create(RoleInfo, 12, OptionName.VictimCanVentBeforeAwakening, false, false);
        VictimKillCooldown = FloatOptionItem.Create(RoleInfo, 13, OptionName.VictimKillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        VictimCanVentAfterAwakening = BooleanOptionItem.Create(RoleInfo, 14, OptionName.VictimCanVentAfterAwakening, true, false);
        VictimCanSabotageAfterAwakening = BooleanOptionItem.Create(RoleInfo, 15, OptionName.VictimCanSabotageAfterAwakening, true, false);
        VictimCanWinWithImpostorsBeforeAwakening = BooleanOptionItem.Create(RoleInfo, 16, OptionName.VictimCanWinWithImpostorsBeforeAwakening, true, false);
        AbuserKillCooldown = FloatOptionItem.Create(RoleInfo, 17, OptionName.AbuserKillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        AbilityCooldown = FloatOptionItem.Create(RoleInfo, 18, OptionName.AbuserAbilityCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        ForcedKillDelay = FloatOptionItem.Create(RoleInfo, 19, OptionName.AbuserForcedKillDelay, new(0f, 180f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add()
    {
        BindVictim();
        SendRPC();
    }

    public override void StartGameTasks()
    {
        BindVictim();
        RevealVictim();
    }

    void BindVictim()
    {
        victim ??= CustomRoleManager.AllActiveRoles.Values.OfType<Victim>().FirstOrDefault();
        victim?.BindAbuser(this);
    }

    void RevealVictim()
    {
        if (victim?.Player == null) return;
        NameColorManager.Add(Player.PlayerId, victim.Player.PlayerId, Victim.RoleInfo.RoleColorCode);
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public float CalculateKillCooldown() => AbuserKillCooldown?.GetFloat() ?? 30f;
    public override void ApplyGameOptions(IGameOptions opt)
        => AURoleOptions.PhantomCooldown = AbilityCooldown?.GetFloat() ?? 30f;

    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.IsPhantomRole => remainingUses > 0;
    public bool UseOneclickButton => remainingUses > 0;
    public override bool CanUseAbilityButton()
    {
        BindVictim();
        return remainingUses > 0 && Player.IsAlive() && victim?.Player?.IsAlive() == true;
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        // 能力を使用しても、進行中のキルクールは変化させない。
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (!CanUseAbilityButton()) return;

        var target = Player.GetKillTarget(true);
        if (target == null || target.PlayerId != victim.Player.PlayerId) return;

        remainingUses--;
        victim.ArmForcedKill();
        ResetCooldown = true;
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        BindVictim();
        if (victim?.Player == null || !victim.Player.IsAlive()) return true;

        victim.Awaken();
        pendingRetaliation = true;
        retaliationKillerId = info.AttemptKiller?.PlayerId ?? byte.MaxValue;
        info.CanKill = false;
        info.GuardPower = 9;
        info.AppearanceKiller?.RpcProtectedMurderPlayer(Player);
        SendRPC();
        return false;
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        BindVictim();
        if (victim?.Player != null && info.AttemptTarget.PlayerId == victim.Player.PlayerId)
        {
            victimResolvedByAbuser = true;
            SendRPC();
        }
    }

    internal void OnVictimExiled()
    {
        victimResolvedByAbuser = true;
        SendRPC();
    }

    internal void OnVictimKilledBy(PlayerControl killer)
    {
        if (killer?.PlayerId == Player.PlayerId)
        {
            victimResolvedByAbuser = true;
            SendRPC();
            return;
        }

        victimResolvedByAbuser = false;
        ScheduleSuicide();
        SendRPC();
    }

    void ScheduleSuicide()
    {
        if (suicideScheduled || !Player.IsAlive()) return;
        suicideScheduled = true;
        if (!AmongUsClient.Instance.AmHost) return;
        MyState.DeathReason = CustomDeathReason.Suicide;
        Player.RpcMurderPlayer(Player);
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost || !pendingRetaliation || !Player.IsAlive()) return;

        pendingRetaliation = false;
        MyState.DeathReason = CustomDeathReason.Retaliation;
        var retaliationKiller = PlayerCatch.GetPlayerById(retaliationKillerId);
        if (retaliationKiller != null)
            Player.SetRealKiller(retaliationKiller, true);
        Player.RpcExileV3();
        MyState.SetDead();
        PlayerCatch.CountAlivePlayers(true);
        SendRPC();
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled?.PlayerId == victim?.Player?.PlayerId)
            OnVictimExiled();
    }

    public override void CheckWinner(GameOverReason reason)
        => EnforceWinRequirement();

    public void EnforceWinRequirement()
    {
        if (!victimResolvedByAbuser)
            CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
    }

    public override void OverrideDisplayRoleNameAsSeer(
        PlayerControl seen,
        ref bool enabled,
        ref UnityEngine.Color roleColor,
        ref string roleText,
        ref bool addon)
    {
        BindVictim();
        if (seen?.PlayerId != victim?.Player?.PlayerId) return;

        enabled = true;
        roleColor = Victim.RoleInfo.RoleColor;
        roleText = UtilsRoleText.GetRoleName(CustomRoles.Victim);
        addon = false;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
        => Utils.ColorString(remainingUses > 0 ? RoleInfo.RoleColor : Palette.DisabledGrey, $"({remainingUses})");

    public override string GetAbilityButtonText() => GetString("AbuserForceKillButtonText");

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(remainingUses);
        sender.Writer.Write(victimResolvedByAbuser);
        sender.Writer.Write(pendingRetaliation);
        sender.Writer.Write(retaliationKillerId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainingUses = reader.ReadInt32();
        victimResolvedByAbuser = reader.ReadBoolean();
        pendingRetaliation = reader.ReadBoolean();
        retaliationKillerId = reader.ReadByte();
    }

    internal static bool CanVictimVentBeforeAwakening => VictimCanVentBeforeAwakening?.GetBool() ?? false;
    internal static bool CanVictimVentAfterAwakening => VictimCanVentAfterAwakening?.GetBool() ?? true;
    internal static bool CanVictimSabotageAfterAwakening => VictimCanSabotageAfterAwakening?.GetBool() ?? true;
    internal static bool CanVictimWinWithImpostors => VictimCanWinWithImpostorsBeforeAwakening?.GetBool() ?? true;
}
