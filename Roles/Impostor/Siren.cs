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
        lastShownSec = -1;
        MyIsTarget = false;
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
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
    int lastShownSec;

    // 非ホストのMOD導入者(ターゲット本人のクライアント)用。Cakeshop の MyAddAddons と同じ役割
    static bool MyIsTarget;
    static byte MySirenId;      // 複数 Siren がいても二重表示にならないよう、送信元を区別する
    static SystemTypes MyRoom;
    static float MyDeadline;    // 受信時刻 + 制限時間 (Time.time 基準)

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
        // 非ホストのターゲット本人: 自分のクライアントで秒が変わったときだけ再描画
        if (!AmongUsClient.Instance.AmHost && MyIsTarget && MySirenId == Player.PlayerId)
        {
            var left = MyDeadline - Time.time;
            var s = Mathf.CeilToInt(left);
            if (left > 0f && s != lastShownSec)
            {
                lastShownSec = s;
                UtilsNotifyRoles.NotifyRoles(SpecifySeer: PlayerControl.LocalPlayer);
            }
            return;
        }

        if (Timer is not float timer) return;
        if (Target == null || !Target.IsAlive()) { ClearTarget(); return; }

        var targetRoom = Target.GetPlainShipRoom()?.RoomId;
        if (targetRoom != null && targetRoom == Room) { ClearTarget(); return; }

        timer -= Time.fixedDeltaTime;
        Timer = timer;

        if (timer <= 0f)
        {
            var target = Target;
            CustomRoleManager.OnCheckMurder(target, target, target, target, true, true, 10, deathReason: CustomDeathReason.Drowning);
            if (AmongUsClient.Instance.AmHost)
                Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
            Player.KillFlash();
            ClearTarget();
            return;
        }

        var sec = Mathf.CeilToInt(timer);
        if (sec != lastShownSec && AmongUsClient.Instance.AmHost)
        {
            lastShownSec = sec;
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Target);
        }
    }

    void ClearTarget(bool notify = true)
    {
        var old = Target;
        Target = null;
        Timer = null;
        lastShownSec = -1;
        if (old == null || !AmongUsClient.Instance.AmHost) return;

        SendTargetRpc(old.PlayerId, false); // 導入者のターゲットの表示を止める
        if (notify) UtilsNotifyRoles.NotifyRoles(SpecifySeer: old);
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
        SendTargetRpc(Target.PlayerId, true);
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Target);
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
        if (seen != seer) return "";
        if (isForMeeting) return "";

        // 非ホストのMOD導入者: 受信した情報から自分のクライアントで組み立てる
        if (seer.IsModClient() && MyIsTarget)
        {
            if (MySirenId != Player.PlayerId) return "";
            var left = MyDeadline - Time.time;
            return left <= 0f ? "" : BuildText(MyRoom, left);
        }

        // ホスト(と非導入者向けの名前生成): ホストが持つ状態を使う
        if (Target == null || Timer == null || seer != Target) return "";
        return BuildText(Room, Timer.Value);
    }

    static string BuildText(SystemTypes room, float left)
    {
        var roomName = DestroyableSingleton<TranslationController>.Instance.GetString(room);
        var text = string.Format(GetString("SirenTargetInfo"), roomName, Mathf.CeilToInt(left));
        return $"<size=50%><color=#ff1919>{text}</color></size>";
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
    void SendTargetRpc(byte targetId, bool active)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var target = PlayerCatch.GetPlayerById(targetId);
        if (target == null || !target.IsModClient() || target.AmOwner) return; // 導入者かつホスト以外にだけ送る

        var sender = RPC.RpcPublicRoleSync(targetId, RoleInfo.RoleName);
        sender.Write(active);
        sender.Write(Player.PlayerId);
        sender.WritePacked((int)Room);
        sender.Write(Timer ?? 0f);
        AmongUsClient.Instance.FinishRpcImmediately(sender);
    }

    public static void ReceivePublickRPC(MessageReader reader)
    {
        MyIsTarget = reader.ReadBoolean();
        MySirenId = reader.ReadByte();
        MyRoom = (SystemTypes)reader.ReadPackedInt32();
        MyDeadline = Time.time + reader.ReadSingle();
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        UsedOnThisDay = reader.ReadBoolean();
        UseCount = reader.ReadInt32();
    }
}