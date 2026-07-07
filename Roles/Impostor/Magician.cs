using AmongUs.GameOptions;
using System.Collections.Generic;
using UnityEngine;
using Hazel;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Magician : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Magician),
            player => new Magician(player),
            CustomRoles.Magician,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            5400,
            SetupOptionItem,
            "mc",
            OptionSort: (3, 2),
            from: From.TownOfHost_K
        );
    public Magician(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        DefaultCooldown = OptionMagicCooldown.GetFloat();
        Maximum = OptionMaximum.GetFloat();
        Radius = OptionRadius.GetFloat();
        ShowDeadbody = OptionShowDeadbody.GetBool();
        MagicUseKillCount = OptionMagicUseKillCount.GetInt();
        ResetKillCount = OptionResetKillCount.GetBool();
        ResetMagicTarget = OptionResetMagicTarget.GetBool();
        MagicCooldown = DefaultCooldown;
        MagicCount = 0;
        HaveKillCount = 0;
        MagicTarget = new();
        magiccount = 0;

        if (OptionMettingMark.GetBool())
        {
            CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        }
    }

    static OptionItem OptionMagicCooldown;
    static OptionItem OptionMaximum;
    static OptionItem OptionRadius;
    static OptionItem OptionShowDeadbody;
    static OptionItem OptionMagicUseKillCount;
    static OptionItem OptionResetKillCount;
    static OptionItem OptionResetMagicTarget;
    static OptionItem OptionMettingMark;

    static float DefaultCooldown;
    static float Maximum;
    static float Radius;
    static bool ShowDeadbody; //会議で死亡してるかを表示するか
    static int MagicUseKillCount; //必要なキル数
    static bool ResetKillCount; //←↓会議後リセットするかの設定 | キル数

    float MagicCooldown;
    bool ResetMagicTarget;// マジックで消す予定の人

    int MagicCount;
    int HaveKillCount;
    List<byte> MagicTarget;

    enum Option
    {
        MagicianMaximum,
        MagicianRadius,
        MagicianShowDeadbody,
        MagicianMagicUseKillCount,
        MagicianResetKillCount,
        MagicianResetMagicTarget,
        MagicianMeetingMark,
    }

    public static void SetupOptionItem()
    {
        OptionMagicCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new FloatValueRule(0, 120, 1), 15, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMaximum = IntegerOptionItem.Create(RoleInfo, 11, Option.MagicianMaximum, new(0, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionRadius = FloatOptionItem.Create(RoleInfo, 12, Option.MagicianRadius, new(0.5f, 3f, 0.5f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionMagicUseKillCount = IntegerOptionItem.Create(RoleInfo, 13, Option.MagicianMagicUseKillCount, new(0, 99, 1), 1, false)
        .SetValueFormat(OptionFormat.Players);
        OptionShowDeadbody = BooleanOptionItem.Create(RoleInfo, 14, Option.MagicianShowDeadbody, false, false);
        OptionResetKillCount = BooleanOptionItem.Create(RoleInfo, 15, Option.MagicianResetKillCount, true, false);
        OptionResetMagicTarget = BooleanOptionItem.Create(RoleInfo, 16, Option.MagicianResetMagicTarget, false, false);
        OptionMettingMark = BooleanOptionItem.Create(RoleInfo, 17, Option.MagicianMeetingMark, false, false);
    }

    public override void OnDestroy()
    {
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
    }

    public override void AfterMeetingTasks()
    {
        if (ResetKillCount) HaveKillCount = 0;
        if (ResetMagicTarget) MagicTarget.Clear(); //↓死亡している対象を削除
        else MagicTarget.RemoveAll(id => PlayerState.GetByPlayerId(id).IsDead);

        //クールダウンリセット
        if (Maximum <= MagicCount && Maximum > 0)
            MagicCooldown = 999;
        else
            MagicCooldown = DefaultCooldown;
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        HaveKillCount++;
        SendRPC_SetKillCount();
    }

    bool IUsePhantomButton.IsPhantomRole => MagicCount < Maximum || Maximum is 0;
    bool IUsePhantomButton.IsresetAfterKill => false;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (Maximum <= MagicCount && Maximum > 0) return;
        ResetCooldown = true;

        bool check = false;
        float minDis = Radius * Radius;
        PlayerControl target = null;

        var myPos = Player.transform.position;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            //自分と相方以外で、近くにいて既にターゲットにされていないか
            if (pc.Is(CustomRoles.King)) continue;
            if (Is(pc) || MagicTarget.Contains(pc.PlayerId)) continue;
            if (pc.IsTeammate(Player)) continue;

            var dis = Vector2.SqrMagnitude(myPos - pc.transform.position);
            if (dis < minDis)
            {
                target = pc;
                minDis = dis;
            }
        }
        if (target != null)
        {
            MagicCount++;
            SendRPC_AddTarget(target.PlayerId);
            check = (Maximum <= MagicCount && Maximum > 0) || MagicCooldown != DefaultCooldown;
            MagicTarget.Add(target.PlayerId);
            Player.SetKillCooldown(target: target);
            MagicCooldown = (Maximum <= MagicCount && Maximum > 0) ? 999 : DefaultCooldown;
            _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.1f);
        }
        else
        {
            check = MagicCooldown != 0;
            MagicCooldown = 0;
        }
        if (check)
        {
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown();
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (MagicTarget.Count == 0 || HaveKillCount < MagicUseKillCount) return;
        foreach (var id in MagicTarget)
        {
            var target = PlayerCatch.GetPlayerById(id);
            if (!target.IsAlive()) continue;
            var state = PlayerState.GetByPlayerId(target.PlayerId);
            Player.SetKillCooldown(target: target);
            if (ShowDeadbody)
            {
                var position = target.transform.position;
                target.RpcSnapToForced(new Vector2(9999f, 9999f));
                target.RpcMurderPlayer(target, true);
                _ = new LateTask(() => target.RpcSnapToForced(position), 0.5f);
            }
            else
            {
                target.RpcExileV3();
            }
            if (Eraser.Erasedtargets.Contains(target.PlayerId))
            {
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
            }
            magiccount++;
            state.DeathReason = CustomDeathReason.Magic;
            state.SetDead();
            var roomName = target.GetShipRoomName();
            target.GetPlayerState().KillRoom = roomName;
            UtilsGameLog.AddGameLog($"Magic", $"{UtilsName.GetPlayerColor(target.PlayerId, true)}({UtilsRoleText.GetTrueRoleName(target.PlayerId, false).RemoveSizeTags()}) [{Utils.GetVitalText(target.PlayerId, true)}]〔{roomName}〕");
            UtilsGameLog.AddGameLogsub($"\n\t⇐ {UtilsName.GetPlayerColor(Player.PlayerId, true)}({UtilsRoleText.GetTrueRoleName(Player.PlayerId, false)})");
        }
        MagicTarget.Clear();
        HaveKillCount -= MagicUseKillCount;
        SendRPC_MagicKill();
        Player.SetKillCooldown();
        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.1f);
    }

    public override string GetAbilityButtonText() => GetString("MagicButtonText");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Magician_Ability";
        return true;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        if (Maximum == 0 && MagicUseKillCount == 0) return "";
        var text = "(";
        if (Maximum != 0) text += Maximum - MagicCount;
        if (MagicUseKillCount > 0) text += (Maximum > 0 ? " | " : "") + $"{HaveKillCount}/{MagicUseKillCount}";
        var color = HaveKillCount < MagicUseKillCount ? Color.yellow : MagicCount < Maximum && Maximum > 0 ? Color.red : Color.gray;
        return Utils.ColorString(color, text + ")");
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = MagicCooldown;
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        if (isForHud) return GetString("PhantomButtonLowertext");
        return $"<size=50%>{GetString("PhantomButtonLowertext")}</size>";
    }

    public string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting & MagicTarget.Contains(seen.PlayerId))
        {
            return Utils.ColorString(Palette.ImpostorRed, "♢");
        }
        return "";
    }

    public void SendRPC_AddTarget(byte targetId)
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.AddMagicTarget);
        sender.Writer.Write(targetId);
        sender.Writer.Write(MagicCount);
    }

    public void SendRPC_SetKillCount()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.SetKillCount);
        sender.Writer.Write(HaveKillCount);
    }

    public void SendRPC_MagicKill()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.MagicKill);
        sender.Writer.Write(HaveKillCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        switch ((RPC_Types)reader.ReadPackedInt32())
        {
            case RPC_Types.AddMagicTarget:
                byte targetId = reader.ReadByte();
                if (!MagicTarget.Contains(targetId))
                    MagicTarget.Add(targetId);
                MagicCount = reader.ReadInt32();
                break;
            case RPC_Types.SetKillCount:
                HaveKillCount = reader.ReadInt32();
                break;
            case RPC_Types.MagicKill:
                MagicTarget.Clear();
                HaveKillCount = reader.ReadInt32();
                break;
        }
    }

    enum RPC_Types
    {
        AddMagicTarget,
        SetKillCount,
        MagicKill
    }

    public override void CheckWinner(GameOverReason reason)
    {
        Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0], magiccount);
        Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[1], magiccount);
    }
    int magiccount;
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 3, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 15, 0, 1);
        var sp1 = new Achievement(RoleInfo, 2, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
        achievements.Add(2, sp1);
    }
}