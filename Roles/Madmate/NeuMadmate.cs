using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

public sealed class NeuMadmate : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NeuMadmate),
            player => new NeuMadmate(player),
            CustomRoles.NeuMadmate,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            25000,
            SetupOptionItem,
            "mw",
            OptionSort: (4, 3)
        );
    public NeuMadmate(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
        ventCooldown = Options.MadmateVentCooldown.GetFloat();
        inventmaxtime = Options.MadmateVentMaxTime.GetFloat();

        AddReacotr = OptionReactorAddPoint.GetInt();
        AddLight = OptionLightSabotageAddPoint.GetInt();
        AddComms = OptionCommsAddPoint.GetInt();
        RemoveSabotageCooldownPoint = OptionRemoveSabotageCooldownPoint.GetInt();
        KnowImposotrPoint = OptionKnowImposotrPoint.GetInt();
        AddAddonPoint = OptionAddAddon.GetInt();
        RemoveSabotageCooldown = 0;
        IsKnowImposotr = false;
        CanUseAddon = false;
        MyfixAmout = byte.MaxValue;
    }
    static bool canSeeKillFlash, canSeeDeathReason;
    static float ventCooldown, inventmaxtime;
    static OptionItem OptionCanVent;

    static OptionItem OptionReactorAddPoint, OptionLightSabotageAddPoint, OptionCommsAddPoint;
    static int AddReacotr, AddLight, AddComms;

    static OptionItem OptionRemoveSabotageCooldown, OptionRemoveSabotageCooldownPoint; static int RemoveSabotageCooldownPoint;
    static OptionItem OptionKnowImposotrPoint; static int KnowImposotrPoint;
    static OptionItem OptionAddAddon; static int AddAddonPoint;
    bool IsKnowImposotr;
    public bool CanUseAddon;
    enum OptionName
    {
        NeuMadmateReactorAddPoint, NeuMadmateLightAddPoint, NeuMadmateCommsAddPoint,
        NeuMadmateKnowImpostorPoint, NeuMadmateCanUseAddon,
        NeuMadmateRemoveSabotageCooldown, NeuMadmateRemoveSabotagePoint
    }

    byte MyfixAmout;
    int HavePoint;
    public static float RemoveSabotageCooldown;
    private static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        ObjectOptionitem.Create(RoleInfo, 11, "NeuMadmate_AddPotint", true, null).SetOptionName(() => "Sabotage Point");
        OptionReactorAddPoint = IntegerOptionItem.Create(RoleInfo, 12, OptionName.NeuMadmateReactorAddPoint, new(0, 30, 1), 0, false);
        OptionCommsAddPoint = IntegerOptionItem.Create(RoleInfo, 13, OptionName.NeuMadmateCommsAddPoint, new(0, 30, 1), 1, false);
        OptionLightSabotageAddPoint = IntegerOptionItem.Create(RoleInfo, 14, OptionName.NeuMadmateLightAddPoint, new(0, 30, 1), 1, false);
        ObjectOptionitem.Create(RoleInfo, 15, "NeuMadmate_Buff", true, null).SetOptionName(() => "Buff Option");
        OptionKnowImposotrPoint = IntegerOptionItem.Create(RoleInfo, 16, OptionName.NeuMadmateKnowImpostorPoint, new(0, 100, 1), 2, false).SetZeroNotation(OptionZeroNotation.Off);
        OptionRemoveSabotageCooldownPoint = IntegerOptionItem.Create(RoleInfo, 17, OptionName.NeuMadmateRemoveSabotagePoint, new(0, 100, 1), 5, false).SetZeroNotation(OptionZeroNotation.Off);
        OptionRemoveSabotageCooldown = FloatOptionItem.Create(RoleInfo, 18, OptionName.NeuMadmateRemoveSabotageCooldown, new(0.5f, 25f, 0.1f), 3, false, OptionRemoveSabotageCooldownPoint).SetValueFormat(OptionFormat.Seconds);
        OptionAddAddon = IntegerOptionItem.Create(RoleInfo, 19, OptionName.NeuMadmateCanUseAddon, new(0, 100, 1), 3, false).SetZeroNotation(OptionZeroNotation.Off);
        RoleAddAddons.Create(RoleInfo, 30, MadMate: true, DefaaultOn: true);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = ventCooldown;
        AURoleOptions.EngineerInVentMaxTime = inventmaxtime;
    }
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();

    public override void Add() => RemoveSabotageCooldown = 0;
    public override void OnDestroy() => RemoveSabotageCooldown = 0;
    public override void OnFixSabotage(PlayerControl player, SystemTypes systemTypes, byte amount)
    {
        if (Main.SabotageType != systemTypes) return;
        if (GameStates.CalledMeeting) return;
        if (amount is 32 or 33) return;

        if (player.PlayerId == Player.PlayerId)
        {
            MyfixAmout = amount;
            Logger.Info($"{player.PlayerId},Fix: {MyfixAmout}", "NeuMadmate");
        }
        else if (MyfixAmout == amount)
        {
            MyfixAmout = byte.MaxValue;
            Logger.Info($"{player.PlayerId},Reset: {MyfixAmout}", "NeuMadmate");
        }
    }

    public override void AfterSabotage(SystemTypes systemType)
    {
        if (AmongUsClient.Instance.AmHost is false) return;
        // 解除している & 会議による解除でない
        if (MyfixAmout is not byte.MaxValue && !GameStates.CalledMeeting)
        {
            var oldpoint = HavePoint;
            switch (systemType)
            {
                case SystemTypes.Reactor:
                case SystemTypes.LifeSupp:
                case SystemTypes.Laboratory:
                case SystemTypes.HeliSabotage:
                    HavePoint += AddReacotr;
                    break;
                case SystemTypes.Electrical:
                    HavePoint += AddLight;
                    break;
                case SystemTypes.Comms:
                    HavePoint += AddComms;
                    break;
            }
            if (oldpoint != HavePoint)
            {
                CheckAbilityRelese(oldpoint);
                Player.RpcProtectedMurderPlayer();
                UtilsGameLog.AddGameLog("NeuMadmate", string.Format(GetString("NeuMadmateGameLog"), HavePoint - oldpoint, HavePoint));
                SendRpc();
            }
        }
        MyfixAmout = byte.MaxValue;
    }

    void CheckAbilityRelese(int oldpoint)
    {
        Logger.Info($"{Player.PlayerId}-{oldpoint}:{HavePoint}", "NeuMadmate");
        bool IsSendNotifyRole = false;
        // Offではない 旧ポイントでは付与されていない 新ポイントでは超した
        if (AddAddonPoint is not 0 && oldpoint < AddAddonPoint && AddAddonPoint <= HavePoint)
        {
            IsSendNotifyRole = true;
            CanUseAddon = true;
            Logger.Info($"{Player.PlayerId}:AddAddon", "NeuMadmate");
        }
        if (RemoveSabotageCooldownPoint is not 0 && oldpoint < RemoveSabotageCooldownPoint && RemoveSabotageCooldownPoint <= HavePoint)
        {
            IsSendNotifyRole = true;
            RemoveSabotageCooldown -= OptionRemoveSabotageCooldown.GetFloat();
            Logger.Info($"{Player.PlayerId}:SabotageCooldown{RemoveSabotageCooldown}", "NeuMadmate");
        }
        if (KnowImposotrPoint is not 0 && oldpoint < KnowImposotrPoint && KnowImposotrPoint <= HavePoint)
        {
            IsSendNotifyRole = true;
            IsKnowImposotr = true;
            Logger.Info($"{Player.PlayerId}:KnowImpostor", "NeuMadmate");
        }
        if (IsSendNotifyRole)
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player), 1f, "NeuMadNotifyRole", true);
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (IsKnowImposotr)
        {
            var role = seen.GetCustomRole();
            if (role.IsImpostor() || role is CustomRoles.WolfBoy or CustomRoles.Egoist)
                return "<#ff1919>★</color>";
        }
        return "";
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var text = $" ({HavePoint})";
        var addtext = "";

        if (IsKnowImposotr) addtext += "★";
        if (RemoveSabotageCooldown != 0) addtext += "⊖";
        return Utils.ColorString(ModColors.MadMateOrenge, text + $"{addtext}");
    }

    void SendRpc()
    {
        if (AmongUsClient.Instance.AmHost is false) return;
        using var sender = CreateSender();
        sender.Writer.Write(HavePoint);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var oldpoint = HavePoint;
        HavePoint = reader.ReadInt32();
        CheckAbilityRelese(oldpoint);
    }
}
