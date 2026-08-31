using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public enum JesterTransformTiming
{
    OnAssign,
    OnKilled,
    AfterMeetingStart,
    OnTaskComplete,
}

public sealed class HappyJester : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(HappyJester),
            player => new HappyJester(player),
            CustomRoles.HappyJester,
            () => CanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            652700,
            SetupOptionItem,
            "hj",
            "#ffb6c1",
            (4, 0),
            true,
            assignInfo: new RoleAssignInfo(CustomRoles.HappyJester, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(0, 15, 1)
            },
            from: From.TownOfHost_Pko
        );

    public HappyJester(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        requireTask = OptionRequireTask.GetBool();
        canTransform = OptionCanTransform.GetBool();
        transformTiming = (JesterTransformTiming)OptionTransformTiming.GetValue();
        transformChance = OptionTransformChance.GetInt();
        secretlyUnhappy = false;
    }

    static OptionItem CanUseVent;
    static OptionItem CanVentMove;
    static OptionItem OptionRequireTask;
    static bool requireTask;
    static OptionItem OptionCanTransform;
    static OptionItem OptionTransformTiming;
    static OptionItem OptionTransformChance;
    static bool canTransform;
    static JesterTransformTiming transformTiming;
    static int transformChance;

    bool secretlyUnhappy;

    enum Option
    {
        MadmateCanMovedByVent,
        HappyJesterRequireTask,
        HappyJesterCanTransform,
        HappyJesterTransformTiming,
        HappyJesterTransformChance,
    }

    private static void SetupOptionItem()
    {
        CanUseVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        CanVentMove = BooleanOptionItem.Create(RoleInfo, 11, Option.MadmateCanMovedByVent, false, false, CanUseVent);
        OptionRequireTask = BooleanOptionItem.Create(RoleInfo, 20, Option.HappyJesterRequireTask, false, false);
        OptionCanTransform = BooleanOptionItem.Create(RoleInfo, 25, Option.HappyJesterCanTransform, false, false);
        OptionTransformTiming = StringOptionItem.Create(RoleInfo, 30, Option.HappyJesterTransformTiming,
            EnumHelper.GetAllNames<JesterTransformTiming>(), 0, false, OptionCanTransform);
        OptionTransformChance = IntegerOptionItem.Create(RoleInfo, 35, Option.HappyJesterTransformChance,
            new(1, 100, 1), 50, false, OptionCanTransform).SetValueFormat(OptionFormat.Percent);
        OverrideTasksData.Create(RoleInfo, 21);
    }

    public bool CanUseImpostorVentButton() => false;
    public override bool CanClickUseVentButton => CanUseVent.GetBool();
    public override bool CanUseAbilityButton() => false;
    public bool CanUseSabotageButton() => false;
    public override bool OnInvokeSabotage(SystemTypes systemType) => false;
    public bool CanKill { get; private set; } = false;
    public bool CanUseKillButton() => false;
    float IKiller.CalculateKillCooldown() => 0f;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
        opt.SetVision(false);
    }

    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVentMove.GetBool();

    public override void StartGameTasks()
    {
        TryTransform(JesterTransformTiming.OnAssign);
    }

    public override void OnStartMeeting()
    {
        TryTransform(JesterTransformTiming.AfterMeetingStart);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished) Player.MarkDirtySettings();
        TryTransform(JesterTransformTiming.OnTaskComplete);
        return true;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (!info.DoKill) return true;
        if (!canTransform || transformTiming != JesterTransformTiming.OnKilled) return true;

        var (killer, target) = info.AppearanceTuple;
        if (!RollTransform()) return true;

        info.CanKill = false;
        killer.RpcProtectedMurderPlayer(target);
        DoTransform();
        return false;
    }

    private bool RollTransform() => IRandom.Instance.Next(100) < transformChance;

    private void TryTransform(JesterTransformTiming timing)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (!canTransform || transformTiming != timing) return;
        if (!RollTransform()) return;

        DoTransform();
    }

    private void DoTransform()
    {
        if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
        Player.RpcSetCustomRole(CustomRoles.UnHappyJester, log: null);
        if (Player.GetRoleClass() is UnHappyJester uj) uj.SetSecretlyHappy();
        UtilsNotifyRoles.NotifyRoles();
        Logger.Info($"{Player.Data.GetLogPlayerName()} がアンハッピージェスターに変化", "HappyJester");
    }

    public void SetSecretlyUnhappy()
    {
        secretlyUnhappy = true;
        SendRpc();
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (!secretlyUnhappy) return;
        if (!Is(seer)) return;

        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.UnHappyJester);
        roleText = GetString("UnHappyJester");
    }

    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (!secretlyUnhappy) return;
        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.UnHappyJester);
        roleText = GetString("UnHappyJester");
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost || Player.PlayerId != exiled.PlayerId) return;

        if (requireTask && !IsTaskFinished) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jester, Player.PlayerId))
        {
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);

            foreach (var pc in PlayerCatch.AllPlayerControls)
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }

        DecidedWinner = true;
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole)
        => Player.IsAlive() && (!requireTask || IsTaskFinished);

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(secretlyUnhappy);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        secretlyUnhappy = reader.ReadBoolean();
    }
}

public sealed class UnHappyJester : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(UnHappyJester),
            player => new UnHappyJester(player),
            CustomRoles.UnHappyJester,
            () => CanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            652800,
            SetupOptionItem,
            "uhj",
            "#6f6f8f",
            (4, 0),
            true,
            assignInfo: new RoleAssignInfo(CustomRoles.UnHappyJester, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(0, 15, 1)
            },
            from: From.TownOfHost_Pko
        );

    public UnHappyJester(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        canTransform = OptionCanTransform.GetBool();
        transformTiming = (JesterTransformTiming)OptionTransformTiming.GetValue();
        transformChance = OptionTransformChance.GetInt();
        secretlyHappy = false;
    }

    static OptionItem CanUseVent;
    static OptionItem CanVentMove;
    static OptionItem OptionCanTransform;
    static OptionItem OptionTransformTiming;
    static OptionItem OptionTransformChance;
    static bool canTransform;
    static JesterTransformTiming transformTiming;
    static int transformChance;

    bool secretlyHappy;

    enum Option
    {
        MadmateCanMovedByVent,
        UnHappyJesterCanTransform,
        UnHappyJesterTransformTiming,
        UnHappyJesterTransformChance,
    }

    private static void SetupOptionItem()
    {
        CanUseVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        CanVentMove = BooleanOptionItem.Create(RoleInfo, 11, Option.MadmateCanMovedByVent, false, false, CanUseVent);
        OverrideTasksData.Create(RoleInfo, 20);

        OptionCanTransform = BooleanOptionItem.Create(RoleInfo, 25, Option.UnHappyJesterCanTransform, false, false);
        OptionTransformTiming = StringOptionItem.Create(RoleInfo, 30, Option.UnHappyJesterTransformTiming,
            EnumHelper.GetAllNames<JesterTransformTiming>(), 0, false, OptionCanTransform);
        OptionTransformChance = IntegerOptionItem.Create(RoleInfo, 35, Option.UnHappyJesterTransformChance,
            new(1, 100, 1), 50, false, OptionCanTransform).SetValueFormat(OptionFormat.Percent);
    }

    public bool CanUseImpostorVentButton() => false;
    public override bool CanClickUseVentButton => CanUseVent.GetBool();
    public override bool CanUseAbilityButton() => false;
    public bool CanUseSabotageButton() => false;
    public override bool OnInvokeSabotage(SystemTypes systemType) => false;
    public bool CanKill { get; private set; } = false;
    public bool CanUseKillButton() => false;
    float IKiller.CalculateKillCooldown() => 0f;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
        opt.SetVision(false);
    }

    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVentMove.GetBool();

    public override void StartGameTasks()
    {
        TryTransform(JesterTransformTiming.OnAssign);
    }

    public override void OnStartMeeting()
    {
        TryTransform(JesterTransformTiming.AfterMeetingStart);
    }

    public override bool OnCompleteTask(uint taskid)
    {
        TryTransform(JesterTransformTiming.OnTaskComplete);
        return true;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (!info.DoKill) return true;
        if (!canTransform || transformTiming != JesterTransformTiming.OnKilled) return true;

        var (killer, target) = info.AppearanceTuple;
        if (!RollTransform()) return true;

        info.CanKill = false;
        killer.RpcProtectedMurderPlayer(target);
        DoTransform();
        return false;
    }

    private bool RollTransform() => IRandom.Instance.Next(100) < transformChance;

    private void TryTransform(JesterTransformTiming timing)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (!canTransform || transformTiming != timing) return;
        if (!RollTransform()) return;

        DoTransform();
    }

    private void DoTransform()
    {
        if (!Utils.RoleSendList.Contains(Player.PlayerId)) Utils.RoleSendList.Add(Player.PlayerId);
        Player.RpcSetCustomRole(CustomRoles.HappyJester, log: null);
        if (Player.GetRoleClass() is HappyJester hj) hj.SetSecretlyUnhappy();
        UtilsNotifyRoles.NotifyRoles();
        Logger.Info($"{Player.Data.GetLogPlayerName()} がハッピージェスターに変化", "UnHappyJester");
    }

    public void SetSecretlyHappy()
    {
        secretlyHappy = true;
        SendRpc();
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (!secretlyHappy) return;
        if (!Is(seer)) return;

        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.HappyJester);
        roleText = GetString("HappyJester");
    }

    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        if (!secretlyHappy) return;
        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.HappyJester);
        roleText = GetString("HappyJester");
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost || Player.PlayerId != exiled.PlayerId) return;

        var others = PlayerCatch.AllPlayerControls.Where(pc => pc.PlayerId != Player.PlayerId).ToArray();
        if (others.Length > 0 && CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.UnHappyJester, others[0].PlayerId, true))
        {
            foreach (var pc in others)
            {
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                CustomWinnerHolder.CantWinPlayerIds.Remove(pc.PlayerId);
            }
            CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
        }

        DecidedWinner = true;

        _ = new LateTask(() =>
        {
            GameManager.Instance.enabled = false;
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
        }, 0.5f, "UnHappyJester.EndGame", true);
    }

    bool IAdditionalWinner.CheckWin(ref CustomRoles winnerRole) => Player.IsAlive();

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(secretlyHappy);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        secretlyHappy = reader.ReadBoolean();
    }
}