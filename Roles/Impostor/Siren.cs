using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Patches.ISystemType;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Roles.Crewmate.AllArounder;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor;

public sealed class Siren : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Siren),
            player => new Siren(player),
            CustomRoles.Siren,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            3800,
            SetupOptionItem,
            "Sir",
            OptionSort: (3, 12)
        );
    public Siren(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Target = null;
        UsedOnThisDay = false;
        Timer = null;
        UseCount = 0;
    }
    public override void Add()
    {
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }
    public override void OnDestroy()
    {
        CustomRoleManager.LowerOthers.Remove(GetLowerTextOthers);
    }
    static OptionItem OptionKillCool;
    static OptionItem OptionCoolDown;
    static OptionItem OptionUseCount;
    static OptionItem OptionTimeLimit;
    public PlayerControl Target;
    public SystemTypes Room;
    float? Timer;
    bool UsedOnThisDay;
    int UseCount;
    public override bool CanUseAbilityButton()
    => Player.Data.Role.Role != RoleTypes.Impostor; // 能力使用後(デスシンクでImpostor化中)は非表示・使用不可

    enum OptionName
    {
        SirenTimeLimit
    }

    static void SetupOptionItem()
    {
        OptionKillCool = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 35f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 35f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTimeLimit = FloatOptionItem.Create(RoleInfo, 12, OptionName.SirenTimeLimit, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 13, GeneralOption.OptionCount, new(1, 14, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = OptionCoolDown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public float CalculateKillCooldown() => OptionKillCool.GetFloat();

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (Timer != null)
        {
            var targetRoom = Target.GetPlainShipRoom().RoomId;
            if (Room == targetRoom)
            {
                Timer = null;
                Target = null;
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    Player.RpcSetRoleDesync(RoleTypes.Impostor, pc.GetClientId());
                }
            }
            Timer -= Time.fixedDeltaTime;
        }
        if (Timer <= 0f)
        {
            CustomRoleManager.OnCheckMurder(Target, Target, Target, Target, true, true, 10, deathReason: CustomDeathReason.Drowning);
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                Player.RpcSetRoleDesync(RoleTypes.Impostor, pc.GetClientId());
            }
            HudManager.Instance.AbilityButton.ToggleVisible(false);
            Player.KillFlash();
            Target = null;
            Timer = null;
        }
    }
    public override void OnStartMeeting()
    {
        Target = null;
        Timer = null;
        UsedOnThisDay = false;
        SendRPC();
        HudManager.Instance.AbilityButton.ToggleVisible(true);
        if (AmongUsClient.Instance.AmHost && UseCount < OptionUseCount.GetInt())
        {
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                Player.RpcSetRoleDesync(RoleTypes.Shapeshifter, pc.GetClientId());
            }
        }
    }
    public override bool CheckShapeshift(PlayerControl target, ref bool animate)
    {
        if (Utils.IsActive(SystemTypes.Electrical) || UsedOnThisDay || OptionUseCount.GetInt() - UseCount <= 0) return false;

        Room = Player.GetPlainShipRoom().RoomId;
        ++UseCount;
        UsedOnThisDay = true;
        Target = target;
        Timer = OptionTimeLimit.GetFloat();
        SendRPC();

        AURoleOptions.ShapeshifterCooldown = OptionTimeLimit.GetFloat();
        Player.RpcResetAbilityCooldown();
        Player.ResetKillCooldown();
        Player.MarkDirtySettings();
        return false;
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (UsedOnThisDay || OptionUseCount.GetInt() - UseCount <= 0) return "";
        return $"<#ff1919>({OptionUseCount.GetInt() - UseCount})</color>";
    }
    public string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";

        // ターゲット本人の画面(自分の名前の下。ホストがターゲットならHUD)だけに出す
        if (Target == null || Timer == null || seer != Target || seen != Target) return "";

        var sec = Mathf.CeilToInt(Timer.Value);
        var roomName = DestroyableSingleton<TranslationController>.Instance.GetString(Room);
        return $"<color=#ff1919>{string.Format(GetString("SirenTargetInfo"), roomName, sec)}</color>";
    }

    public override string GetAbilityButtonText() => Timer == null ? GetString("SirenAbility") : GetString("SirenAbility2");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Siren_Ability";
        return true;
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (Timer is not null) return;
        var killer = info.AttemptKiller;

        AURoleOptions.ShapeshifterCooldown = OptionCoolDown.GetFloat();
        killer.RpcResetAbilityCooldown();
        killer.MarkDirtySettings();
    }
    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(UsedOnThisDay);
        sender.Writer.Write(UseCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        UsedOnThisDay = reader.ReadBoolean();
        UseCount = reader.ReadInt32();
    }
}