using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Madmate;

public sealed class BlackSanta : RoleBase, IKiller, IKillFlashSeeable, IDeathReasonSeeable
{
    bool IKiller.IsKiller => true;
    bool IKiller.CanKill => true;

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BlackSanta),
            player => new BlackSanta(player),
            CustomRoles.BlackSanta,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            22600,
            SetupOptionItem,
            "bst",
            OptionSort: (2, 3),
            from: From.SuperNewRoles,
            isDesyncImpostor: true
        );

    public BlackSanta(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute)
    {
        Cooldown = OptCooldown.GetFloat();
        giftCount = 0;
        giftMode = false;
        tasksCompleted = false;
        MyTaskState.NeedTaskCount = OptNeedsTaskForKnowImpostors.GetBool() ? OptNeededTaskCountForKnowImpostors.GetInt() : 0;
    }

    static OptionItem OptCooldown;
    static OptionItem OptGiftLimit;
    static OptionItem OptTryLoverToDeath;

    static OptionItem OptEvilGuesserRate;
    static OptionItem OptSelfBomberRate;
    static OptionItem OptPenguinRate;
    static OptionItem OptHadouHoRate;
    static OptionItem OptTimeSleeperRate;
    static OptionItem OptSmokeMakerRate;
    static OptionItem OptReloaderRate;
    static OptionItem OptEvilMakerRate;
    static OptionItem OptBorderKillerRate;

    static OptionItem OptCanUseVent;
    static OptionItem OptHasImpostorVision;
    static OptionItem OptCanKnowImpostors;
    static OptionItem OptNeedsTaskForKnowImpostors;
    static OptionItem OptNeededTaskCountForKnowImpostors;

    static float Cooldown;
    int giftCount;
    bool giftMode;
    bool tasksCompleted;

    private enum OptionName
    {
        SantaAbilityCooldown,
        SantaCanUseAbilityCount,
        SantaTryLoverToDeath,
        BlackSantaEvilGuesserPercentage,
        BlackSantaSelfBomberPercentage,
        BlackSantaPenguinPercentage,
        BlackSantaWaveCannonPercentage,
        BlackSantaTimeSleeperPercentage,
        BlackSantaSmokeMakerPercentage,
        BlackSantaReloaderPercentage,
        BlackSantaEvilMakerPercentage,
        BlackSantaBorderKillerPercentage,
        BlackSantaCanKnowImpostors,
        BlackSantaNeedsTaskForKnowImpostors,
        BlackSantaNeededTaskCountForKnowImpostors,
    }

    private static void SetupOptionItem()
    {
        // サンタ機能
        OptCooldown = FloatOptionItem.Create(
            RoleInfo, 10, OptionName.SantaAbilityCooldown,
            new(0f, 180f, 2.5f), 25f, false
        ).SetValueFormat(OptionFormat.Seconds);

        OptGiftLimit = IntegerOptionItem.Create(
            RoleInfo, 11, OptionName.SantaCanUseAbilityCount,
            new(1, 100, 1), 15, false
        ).SetValueFormat(OptionFormat.Times);

        OptTryLoverToDeath = BooleanOptionItem.Create(
            RoleInfo, 12, OptionName.SantaTryLoverToDeath,
            false, false
        );

        // 役職配布設定
        OptEvilGuesserRate = IntegerOptionItem.Create(
            RoleInfo, 20, OptionName.BlackSantaEvilGuesserPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptSelfBomberRate = IntegerOptionItem.Create(
            RoleInfo, 21, OptionName.BlackSantaSelfBomberPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptPenguinRate = IntegerOptionItem.Create(
            RoleInfo, 22, OptionName.BlackSantaPenguinPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptHadouHoRate = IntegerOptionItem.Create(
            RoleInfo, 23, OptionName.BlackSantaWaveCannonPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptTimeSleeperRate = IntegerOptionItem.Create(
            RoleInfo, 24, OptionName.BlackSantaTimeSleeperPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptSmokeMakerRate = IntegerOptionItem.Create(
            RoleInfo, 25, OptionName.BlackSantaSmokeMakerPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptReloaderRate = IntegerOptionItem.Create(
            RoleInfo, 26, OptionName.BlackSantaReloaderPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptEvilMakerRate = IntegerOptionItem.Create(
            RoleInfo, 27, OptionName.BlackSantaEvilMakerPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        OptBorderKillerRate = IntegerOptionItem.Create(
            RoleInfo, 28, OptionName.BlackSantaBorderKillerPercentage,
            new(0, 100, 5), 0, false
        ).SetValueFormat(OptionFormat.Percent);

        // マッドメイト機能
        OptCanUseVent = BooleanOptionItem.Create(
            RoleInfo, 30, GeneralOption.CanVent, true, false
        );

        OptHasImpostorVision = BooleanOptionItem.Create(
            RoleInfo, 31, GeneralOption.ImpostorVision, true, false
        );

        OptCanKnowImpostors = BooleanOptionItem.Create(
            RoleInfo, 33, OptionName.BlackSantaCanKnowImpostors, true, false
        );

        OptNeedsTaskForKnowImpostors = BooleanOptionItem.Create(
            RoleInfo, 34, OptionName.BlackSantaNeedsTaskForKnowImpostors, false, false, OptCanKnowImpostors
        );

        OptNeededTaskCountForKnowImpostors = IntegerOptionItem.Create(
            RoleInfo, 35, OptionName.BlackSantaNeededTaskCountForKnowImpostors,
            new(1, 100, 1), 3, false, OptNeedsTaskForKnowImpostors
        ).SetValueFormat(OptionFormat.Pieces);

        OverrideTasksData.Create(RoleInfo, 32);
    }

    public override void Add()
    {
        giftCount = 0;
        giftMode = false;
        tasksCompleted = false;
        Cooldown = OptCooldown.GetFloat();
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
        CheckAndAddNameColorToImpostors();
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    private void OnPetUsed()
    {
        if (!Player.IsAlive()) return;
        if (tasksCompleted) return;

        giftMode = !giftMode;
        ApplyModeDesync(giftMode);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private void ApplyModeDesync(bool toGiftMode)
    {
        if (Is(PlayerControl.LocalPlayer)) return;
        if (!Player.IsAlive()) return;

        var roleType = toGiftMode ? RoleTypes.Impostor : RoleTypes.Crewmate;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(toGiftMode ? RoleTypes.Scientist : role.GetRoleTypes(), Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(roleType, Player.GetClientId());
        }
    }

    public override bool OnCompleteTask(uint taskid)
    {
        CheckAndAddNameColorToImpostors();

        if (!AmongUsClient.Instance.AmHost) return true;
        if (tasksCompleted) return true;
        if (!MyTaskState.IsTaskFinished) return true;

        tasksCompleted = true;
        if (!giftMode)
        {
            giftMode = true;
            ApplyModeDesync(true);
        }

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return true;
    }

    // インポスターがわかるようになったか(タスク不要ならOptCanKnowImpostorsのみで判定)
    private bool KnowsImpostors()
    {
        if (!OptCanKnowImpostors.GetBool()) return false;
        if (!OptNeedsTaskForKnowImpostors.GetBool()) return true;
        return MyTaskState.HasCompletedEnoughCountOfTasks(OptNeededTaskCountForKnowImpostors.GetInt());
    }

    // マッドスニッチのCheckAndAddNameColorToImpostorsと同じ要領で、わかった時点のインポスターを自分の色でマーキングする
    private void CheckAndAddNameColorToImpostors()
    {
        if (!KnowsImpostors()) return;

        foreach (var impostor in PlayerCatch.AllPlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)))
        {
            NameColorManager.Add(Player.PlayerId, impostor.PlayerId, Player.GetRoleColorCode());
        }
    }

    public float CalculateKillCooldown() => Cooldown;

    public bool CanUseKillButton()
    {
        if (!Player.IsAlive() || !giftMode) return false;
        var limit = OptGiftLimit?.GetInt() ?? 1;
        if (limit == 0) return true;
        return giftCount < limit;
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => OptCanUseVent?.GetBool() ?? true;

    public bool? CheckKillFlash(MurderInfo info) => Options.MadmateCanSeeKillFlash.GetBool();
    public bool? CheckSeeDeathReason(PlayerControl seen) => Options.MadmateCanSeeDeathReason.GetBool();

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptHasImpostorVision?.GetBool() ?? true);
    }

    public override RoleTypes? AfterMeetingRole
        => giftMode ? RoleTypes.Impostor : RoleTypes.Crewmate;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        _ = new LateTask(() =>
        {
            ApplyModeDesync(giftMode);
            Player.RpcResetAbilityCooldown();
        }, Main.LagTime, "Reset-BlackSanta");
    }

    public override void ChengeRoleAdd()
    {
        base.ChengeRoleAdd();
        if (giftMode && Player.IsAlive() && AmongUsClient.Instance.AmHost)
            ApplyModeDesync(true);
    }

    // 配布候補と設定確率
    private static int GetGiftRate(CustomRoles role) => role switch
    {
        CustomRoles.EvilGuesser => OptEvilGuesserRate?.GetInt() ?? 0,
        CustomRoles.SelfBomber => OptSelfBomberRate?.GetInt() ?? 0,
        CustomRoles.Penguin => OptPenguinRate?.GetInt() ?? 0,
        CustomRoles.HadouHo => OptHadouHoRate?.GetInt() ?? 0,
        CustomRoles.TimeSleeper => OptTimeSleeperRate?.GetInt() ?? 0,
        CustomRoles.SmokeMaker => OptSmokeMakerRate?.GetInt() ?? 0,
        CustomRoles.Reloader => OptReloaderRate?.GetInt() ?? 0,
        CustomRoles.EvilMaker => OptEvilMakerRate?.GetInt() ?? 0,
        CustomRoles.BorderKiller => OptBorderKillerRate?.GetInt() ?? 0,
        _ => 0
    };

    private static readonly CustomRoles[] GiftRoles =
    {
        CustomRoles.EvilGuesser,
        CustomRoles.SelfBomber,
        CustomRoles.Penguin,
        CustomRoles.HadouHo,
        CustomRoles.TimeSleeper,
        CustomRoles.SmokeMaker,
        CustomRoles.Reloader,
        CustomRoles.EvilMaker,
        CustomRoles.BorderKiller,
    };

    private static CustomRoles RollGiftRole()
    {
        var weightedRoles = GiftRoles
            .Select(role =>
            {
                var weight = GetGiftRate(role);
                if (weight < 0) weight = 0;
                if (weight > 100) weight = 100;
                return (Role: role, Weight: weight);
            })
            .Where(x => x.Weight > 0)
            .ToArray();

        if (weightedRoles.Length == 0)
            return GiftRoles[IRandom.Instance.Next(GiftRoles.Length)];

        var totalWeight = weightedRoles.Sum(x => x.Weight);
        var roll = IRandom.Instance.Next(totalWeight);
        var acc = 0;
        foreach (var entry in weightedRoles)
        {
            acc += entry.Weight;
            if (roll < acc) return entry.Role;
        }
        return weightedRoles[weightedRoles.Length - 1].Role;
    }

    // キルボタン → プレゼント処理
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        info.DoKill = false;

        if (target.PlayerId == killer.PlayerId) return;

        bool tryLoverToDeath = OptTryLoverToDeath?.GetBool() ?? false;
        bool isLovers = target.Is(CustomRoles.Lovers) || target.Is(CustomRoles.MadonnaLovers) || target.Is(CustomRoles.OneLove);

        // 恋人へプレゼント → 自爆させる設定なら自爆
        if (isLovers && !tryLoverToDeath)
        {
            SantaSuicide();
            return;
        }

        // インポスター陣営以外へプレゼント → 自爆(SNR: ForImpostor)
        if (target.GetCustomRole().GetCustomRoleTypes() != CustomRoleTypes.Impostor)
        {
            SantaSuicide();
            return;
        }

        var limit = OptGiftLimit?.GetInt() ?? 1;
        if (limit > 0 && giftCount >= limit) return;

        var role = RollGiftRole();
        var beforeRole = target.GetCustomRole();

        // 既に同じ役職なら消費せず何もしない(無駄撃ち防止)
        if (beforeRole == role)
        {
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcResetAbilityCooldown();
            return;
        }

        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Impostor)) return;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(role, log: null);

        giftCount++;
        SendRPC();

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        killer.RpcResetAbilityCooldown();

        Logger.Info($"{Player.Data?.GetLogPlayerName()} が {target.Data?.GetLogPlayerName()} に {role} をプレゼント", "BlackSanta");
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(ForceLoop: true), 0.2f, "BlackSanta Gift");
    }

    private void SantaSuicide()
    {
        PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
        Player.RpcMurderPlayerV2(Player);
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(giftMode);
        sender.Writer.Write(tasksCompleted);
        sender.Writer.Write(giftCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        giftMode = reader.ReadBoolean();
        tasksCompleted = reader.ReadBoolean();
        giftCount = reader.ReadInt32();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!giftMode) return "";
        var limit = OptGiftLimit?.GetInt() ?? 1;
        if (tasksCompleted)
            return limit == 0
                ? $"<color={RoleInfo.RoleColorCode}>({giftCount}) ∞</color>"
                : $"<color={RoleInfo.RoleColorCode}>({giftCount}/{limit}) ∞</color>";
        if (limit == 0) return $"<color={RoleInfo.RoleColorCode}>({giftCount})</color>";
        return $"<color={RoleInfo.RoleColorCode}>({giftCount}/{limit})</color>";
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("SantaButtonText");
        return true;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "BlackSanta_Kill";
        return true;
    }
}