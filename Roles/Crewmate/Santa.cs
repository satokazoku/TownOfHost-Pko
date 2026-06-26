using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Santa : RoleBase, IKiller
{
    bool IKiller.IsKiller => true;
    bool IKiller.CanKill => true;

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Santa),
            player => new Santa(player),
            CustomRoles.Santa,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            34600,
            SetupOptionItem,
            "st",
            "#f29c9f",
            (6, 0),
            from: From.SuperNewRoles,
            isDesyncImpostor: true
        );

    public Santa(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptKillCooldown.GetFloat();
        giftMode = false;
        giftCount = 0;
        tasksCompleted = false;
    }

    static OptionItem OptKillCooldown;
    static float KillCooldown;

    static OptionItem OptBalancerRate;
    static OptionItem OptSheriffRate;
    static OptionItem OptLighterRate;
    static OptionItem OptUltraStarRate;
    static OptionItem OptExpressRate;
    static OptionItem OptNiceGuesserRate;
    static OptionItem OptGiftLimit;
    static OptionItem OptCanGiftLovers;
    static OptionItem OptCanGiftMadmate;

    bool giftMode;
    int giftCount;
    bool tasksCompleted;

    private enum OptionName
    {
        SantaGiftRateBalancer,
        SantaGiftRateSheriff,
        SantaGiftRateLighter,
        SantaGiftRateUltraStar,
        SantaGiftRateExpress,
        SantaGiftRateNiceGuesser,
        SantaGiftLimit,
        SantaCanGiftLovers,
        SantaCanGiftMadmate,
    }

    private static readonly Dictionary<byte, int> RememberedColorByPlayerId = new();

    private static void SetupOptionItem()
    {
        OptBalancerRate = IntegerOptionItem.Create(
            RoleInfo, 11, OptionName.SantaGiftRateBalancer,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptSheriffRate = IntegerOptionItem.Create(
            RoleInfo, 12, OptionName.SantaGiftRateSheriff,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptLighterRate = IntegerOptionItem.Create(
            RoleInfo, 13, OptionName.SantaGiftRateLighter,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptUltraStarRate = IntegerOptionItem.Create(
            RoleInfo, 14, OptionName.SantaGiftRateUltraStar,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptExpressRate = IntegerOptionItem.Create(
            RoleInfo, 15, OptionName.SantaGiftRateExpress,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptNiceGuesserRate = IntegerOptionItem.Create(
            RoleInfo, 16, OptionName.SantaGiftRateNiceGuesser,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptKillCooldown = FloatOptionItem.Create(
            RoleInfo, 10, "SantaKillCooldown",
            new(0.5f, 60f, 0.5f), 25f, false
        ).SetValueFormat(OptionFormat.Seconds);

        OptGiftLimit = IntegerOptionItem.Create(
            RoleInfo, 17, OptionName.SantaGiftLimit,
            new(1, 100, 1), 15, false
        ).SetValueFormat(OptionFormat.Times);

        OptCanGiftLovers = BooleanOptionItem.Create(
            RoleInfo, 18, OptionName.SantaCanGiftLovers,
            false, false
        );

        OptCanGiftMadmate = BooleanOptionItem.Create(
            RoleInfo, 19, OptionName.SantaCanGiftMadmate,
            false, false
        );

        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void Add()
    {
        giftMode = false;
        giftCount = 0;
        tasksCompleted = false;
        KillCooldown = OptKillCooldown.GetFloat();
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
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

    public override bool OnCompleteTask(uint taskid)
    {
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
        Utils.SendMessage(
            $"<color={RoleInfo.RoleColorCode}>全タスク完了！ずっとギフトモードになります。</color>",
            Player.PlayerId);

        return true;
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

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(giftMode);
        sender.Writer.Write(giftCount);
        sender.Writer.Write(tasksCompleted);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        giftMode = reader.ReadBoolean();
        giftCount = reader.ReadInt32();
        tasksCompleted = reader.ReadBoolean();
    }

    public float CalculateKillCooldown() => KillCooldown;

    public bool CanUseKillButton()
    {
        if (!Player.IsAlive() || !giftMode) return false;
        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (limit == 0) return true;
        return giftCount < limit;
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    public override RoleTypes? AfterMeetingRole
        => giftMode ? RoleTypes.Impostor : RoleTypes.Crewmate;

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        _ = new LateTask(() =>
        {
            ApplyModeDesync(giftMode);
            Player.RpcResetAbilityCooldown();
        }, Main.LagTime, "Reset-Santa");
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public override void ChengeRoleAdd()
    {
        base.ChengeRoleAdd();
        if (giftMode && Player.IsAlive() && AmongUsClient.Instance.AmHost)
            ApplyModeDesync(true);
    }

    private static int GetGiftRate(CustomRoles role) => role switch
    {
        CustomRoles.Balancer => OptBalancerRate?.GetInt() ?? 0,
        CustomRoles.Sheriff => OptSheriffRate?.GetInt() ?? 0,
        CustomRoles.Lighter => OptLighterRate?.GetInt() ?? 0,
        CustomRoles.UltraStar => OptUltraStarRate?.GetInt() ?? 0,
        CustomRoles.Express => OptExpressRate?.GetInt() ?? 0,
        CustomRoles.NiceGuesser => OptNiceGuesserRate?.GetInt() ?? 0,
        _ => 0
    };

    private static CustomRoles RollGiftRole(CustomRoles[] giftRoles)
    {
        var weightedRoles = giftRoles
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
            return giftRoles[IRandom.Instance.Next(giftRoles.Length)];

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

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        info.DoKill = false;

        if (target.PlayerId == killer.PlayerId) return;

        var targetRoleType = target.GetCustomRole().GetCustomRoleTypes();
        bool isLovers = target.Is(CustomRoles.Lovers) || target.Is(CustomRoles.MadonnaLovers) || target.Is(CustomRoles.OneLove);
        bool isMadmate = targetRoleType == CustomRoleTypes.Madmate;
        bool isCrew = targetRoleType == CustomRoleTypes.Crewmate;
        bool canGift = isCrew;
        if (!canGift && isLovers && (OptCanGiftLovers?.GetBool() ?? false)) canGift = true;
        if (!canGift && isMadmate && (OptCanGiftMadmate?.GetBool() ?? false)) canGift = true;

        if (!canGift)
        {
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
            killer.RpcMurderPlayerV2(killer);
            return;
        }

        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (limit > 0 && giftCount >= limit) return;

        CustomRoles[] giftRoles =
        {
            CustomRoles.Balancer,
            CustomRoles.Sheriff,
            CustomRoles.Lighter,
            CustomRoles.UltraStar,
            CustomRoles.Express,
            CustomRoles.NiceGuesser,
        };

        var role = RollGiftRole(giftRoles);
        var beforeRole = target.GetCustomRole();

        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Crewmate)) return;

        if (role == CustomRoles.UltraStar && beforeRole != CustomRoles.UltraStar)
            RememberedColorByPlayerId[target.PlayerId] = target.Data.DefaultOutfit.ColorId;

        bool resetExpressSpeed = beforeRole == CustomRoles.Express && role != CustomRoles.Express;
        if (resetExpressSpeed)
            Main.AllPlayerSpeed[target.PlayerId] = Main.NormalOptions.PlayerSpeedMod;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(role, log: null);

        if (beforeRole == CustomRoles.UltraStar &&
            role != CustomRoles.UltraStar &&
            RememberedColorByPlayerId.TryGetValue(target.PlayerId, out var originalColorId))
        {
            target.RpcSetColor((byte)originalColorId);
            RememberedColorByPlayerId.Remove(target.PlayerId);
        }

        if (role == CustomRoles.UltraStar)
        {
            var field = typeof(UltraStar).GetField("CanseeAllplayer", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, true);
        }

        if (resetExpressSpeed)
            UtilsOption.MarkEveryoneDirtySettings();

        giftCount++;
        SendRPC();

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        killer.RpcResetAbilityCooldown();

        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(ForceLoop: true), 0.2f, "Santa Gift");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!giftMode) return "";
        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (tasksCompleted)
            return limit == 0
                ? $"<color={RoleInfo.RoleColorCode}>({giftCount}) ∞</color>"
                : $"<color={RoleInfo.RoleColorCode}>({giftCount}/{limit}) ∞</color>";
        if (limit == 0) return $"<color={RoleInfo.RoleColorCode}>({giftCount})</color>";
        return $"<color={RoleInfo.RoleColorCode}>({giftCount}/{limit})</color>";
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = "プレゼント";
        return true;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Santa_Gift";
        return true;
    }
}
