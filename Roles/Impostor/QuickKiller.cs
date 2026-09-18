using System;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class QuickKiller : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(QuickKiller),
            player => new QuickKiller(player),
            CustomRoles.QuickKiller,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            6400,
            SetupOptionItem,
            "qk",
            OptionSort: (7, 5),
            Desc: () => OptionCanKill.GetBool() ? string.Format(GetString("QuickKillerDescOneClick"), OptionAbiltyCanUsePlayercount.GetInt(), OptionQuickKillTimer.GetInt()) : string.Format(GetString("QuickKillerDesc"), OptionAbiltyCanUsePlayercount.GetInt(), OptionQuickKillTimer.GetInt()),
            from: From.TownOfHost_K
        );
    public QuickKiller(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        timer = null;
        KillCool = OptionKillCoolDown.GetFloat();
        Cool = OptionAbilityCoolDown.GetFloat();
        UseCount = OptionUseCount.GetInt();
        CanSubkill = true;
        IsPhantom = true;
        IsQuick = false;
        KillCoolTimer = KillCool;
    }
    static OptionItem OptionKillCoolDown;
    static OptionItem OptionAbiltyCanUsePlayercount;
    static OptionItem OptionQuickKillTimer;
    static OptionItem OptionUseCount;
    static OptionItem OptionCanKill;
    static OptionItem OptionAbilityCoolDown;
    static OptionItem OptionPenaltyKillCool;
    static OptionItem OptionPenaltyCool;
    static OptionItem OptionCanSeeNameColor;
    float KillCool;
    float Cool;
    int UseCount;
    //クイック可能の時間。null → 未キル
    float? timer;
    public bool CanSubkill;
    bool IsQuick;
    bool isCheckKillScheduled;
    float KillCoolTimer;
    enum OptionName
    {
        QuickKillerCanuseplayercount,
        QuickKillerTimer,
        QuickKillerCanKill,
        QuickKillerpenaltyKillCool,
        QuickKillerpenaltyCool,
        QuickKillerChangeNameColor,
    }
    private static void SetupOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionQuickKillTimer = FloatOptionItem.Create(RoleInfo, 11, OptionName.QuickKillerTimer, new(0.1f, 10f, 0.1f), 3f, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionAbiltyCanUsePlayercount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.QuickKillerCanuseplayercount, new(0, 15, 1), 6, false)
            .SetValueFormat(OptionFormat.Players).SetZeroNotation(OptionZeroNotation.Off);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 13, GeneralOption.OptionCount, new(1, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Off);
        OptionCanSeeNameColor = BooleanOptionItem.Create(RoleInfo, 14, OptionName.QuickKillerChangeNameColor, true, false);
        OptionCanKill = BooleanOptionItem.Create(RoleInfo, 15, OptionName.QuickKillerCanKill, true, false);
        OptionAbilityCoolDown = FloatOptionItem.Create(RoleInfo, 16, GeneralOption.Cooldown, OptionBaseCoolTime, 45f, false, OptionCanKill)
            .SetValueFormat(OptionFormat.Seconds);
        OptionPenaltyKillCool = FloatOptionItem.Create(RoleInfo, 17, OptionName.QuickKillerpenaltyKillCool, new(0f, 180f, 0.5f), 2.5f, false, OptionCanKill)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Off);
        OptionPenaltyCool = FloatOptionItem.Create(RoleInfo, 18, OptionName.QuickKillerpenaltyCool, new(0f, 180f, 0.5f), 2.5f, false, OptionCanKill)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Off);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Cool;
    }
    bool IsPhantom;
    bool IUsePhantomButton.IsPhantomRole => OptionCanKill.GetBool() && IsPhantom;
    bool IUsePhantomButton.IsresetAfterKill => true;
    public static bool KnowTargetRoleColor(PlayerControl target, bool isMeeting)
        => OptionCanSeeNameColor.GetBool() && target.Is(CustomRoles.QuickKiller) && !isMeeting && target.GetRoleClass() is QuickKiller quick && quick.IsQuick;

    public override void OnFixedUpdate(PlayerControl player)
    {
        CheckKill();
        if (!player.IsAlive()) return;
        if (GameStates.IsMeeting) return;
        if (timer != null && AmongUsClient.Instance.AmHost)
        {
            timer -= Time.fixedDeltaTime;
        }      
        if (timer < 0 && timer != null && AmongUsClient.Instance.AmHost)
        {
            timer = null;
            quickmodekillcount = 0;
            if (OptionCanKill.GetBool())
            {
                KillCool += OptionPenaltyKillCool.GetFloat();
                Cool += OptionPenaltyCool.GetFloat();
            }
            KillCoolTimer = KillCool;
            Main.AllPlayerKillCooldown[Player.PlayerId] = KillCool;
            player.ResetKillCooldown();
            player.SetKillCooldown(force: true);
            player.RpcResetAbilityCooldown();
            IsQuick = false;
            if (UseCount <= 0)
            {
                IsPhantom = false;
            }
        }
        if (timer != null && timer > 0f)
        {
            KillCoolTimer = 0.0001f;
            Main.AllPlayerKillCooldown[player.PlayerId] = KillCoolTimer;
        }
        else
        {
            KillCoolTimer -= Time.fixedDeltaTime;
        }
        Main.AllPlayerKillCooldown[player.PlayerId] = KillCoolTimer;
    }
    public void CheckKill()
    {
        if (OptionAbilityCoolDown.GetFloat() < 1f && !isCheckKillScheduled && !CanSubkill) //キルク1未満でも一秒待たない。
        {
            isCheckKillScheduled = true;
            _ = new LateTask(() =>
            {
                if (!CanSubkill)
                {
                    CanSubkill = true;
                    isCheckKillScheduled = false;
                    SendRPC();
                }
            }, OptionAbilityCoolDown.GetFloat(), "", true);
        }
        else if (!isCheckKillScheduled && !CanSubkill)
        {
            _ = new LateTask(() =>
            {
                if (!CanSubkill)
                {
                    CanSubkill = true;
                    isCheckKillScheduled = false;
                    SendRPC();
                }
            }, 1f, "", true);
        }
    }
    void IKiller.OnMurderPlayerAsKiller(MurderInfo info)
    {
        //必要人数未満だったらさいなら
        if (OptionAbiltyCanUsePlayercount.GetInt() > PlayerCatch.AllAlivePlayersCount) return;
        if (!IsQuick && UseCount <= 0) return;
        if (OptionCanKill.GetBool() && !IsQuick)
        {
            Main.AllPlayerKillCooldown[Player.PlayerId] = KillCool;
            KillCoolTimer = KillCool;
            Player.ResetKillCooldown();
            Player.SetKillCooldown(force: true);
            return;
        }
        var (killer, target) = info.AttemptTuple;
        if (OptionCanKill.GetBool() && IsQuick)
        {
            quickmodekillcount++;
            switch (quickmodekillcount)
            {
                case 1: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]); break;
                case 2: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]); break;
                case 4: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]); break;
            }
            killer.RpcResetAbilityCooldown();
            Main.AllPlayerKillCooldown[killer.PlayerId] = 0.0001f;
            return;
        }
        --UseCount;
        if (!info.IsCanKilling || info.IsFakeSuicide || info.IsSuicide) return;
        Main.AllPlayerKillCooldown[Player.PlayerId] = KillCool;
        Player.ResetKillCooldown();
        Player.SetKillCooldown(force: true);
        killer.RpcResetAbilityCooldown();
        //タイマー進行中なら止める
        Main.AllPlayerKillCooldown[killer.PlayerId] = 0.0001f;
        KillCoolTimer = 0f;

        if (timer.HasValue)
        {
            quickmodekillcount++;
            switch (quickmodekillcount)
            {
                case 1: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]); break;
                case 2: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]); break;
                case 4: Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]); break;
            }
            killer.SyncSettings();
            return;
        }
        quickmodekillcount = 0;
        timer = OptionQuickKillTimer.GetFloat();
        killer.SyncSettings();
    }
    public float CalculateKillCooldown() => KillCool;
    public override void OnStartMeeting()
    {
        timer = null;
        CanSubkill = true;
        IsQuick = false;
        KillCoolTimer = KillCool;
        SendRPC();
    }
    public override string GetAbilityButtonText() => GetString("QuickKiller_Timer");
    public override bool CanUseAbilityButton() => true;
    public override bool OverrideAbilityButton(out string text)
    {
        text = "QuickKiller_Ability";
        return true;
    }
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        var target = Player.GetKillTarget(true);

        if (!Player.IsAlive() || target == null || !CanSubkill || UseCount <= 0 || IsQuick) { Main.AllPlayerKillCooldown[Player.PlayerId] = KillCoolTimer; SendRPC(); return; }

        var targetrole = target.GetCustomRole();
        if (targetrole.IsImpostor()) { Main.AllPlayerKillCooldown[Player.PlayerId] = KillCoolTimer; return; }

        else if (target.GetRoleClass() is MadPsycho ms)
        {
            ms.Psycho(Player, 1);
        }
        else
        {
            --UseCount;
            var killer = PlayerControl.LocalPlayer;
            Player.RpcResetAbilityCooldown(Sync: true);
            float savedKillTimer = Player.killTimer;
            Vector2 targetPos = target.transform.position;
            CanSubkill = false;
            CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, true, 1, CustomDeathReason.Kill);
            if (Player.IsAlive()) RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
            Player.RpcSnapToForced(targetPos);
            IsQuick = true;
            KillCoolTimer = 0.0001f;
            quickmodekillcount = 0;
            timer = OptionQuickKillTimer.GetFloat();
            SendRPC();
        }

    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsQuick);
        sender.Writer.Write(timer.HasValue);
        if (timer.HasValue) sender.Writer.Write(timer.Value);
        sender.Writer.Write(UseCount);
        sender.Writer.Write(KillCool);
        sender.Writer.Write(Cool);
        sender.Writer.Write(CanSubkill);
        sender.Writer.Write(KillCoolTimer);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        IsQuick = reader.ReadBoolean();
        bool hasTimer = reader.ReadBoolean();
        timer = hasTimer ? reader.ReadSingle() : null;
        UseCount = reader.ReadInt32();
        KillCool = reader.ReadSingle();
        Cool = reader.ReadSingle();
        CanSubkill = reader.ReadBoolean();
        KillCoolTimer = reader.ReadSingle();
    }
    int quickmodekillcount;
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
    bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;

        if (!OptionCanKill.GetBool())
        {
            return "";
        }
        var text = $"<color=#ff1919>キルクール:{KillCool}秒　クイックキルクール: {Cool}秒</color>";
        if (UseCount <= 0)
        {
            text = $"<color=#ff1919>キルクール:{KillCool}秒</color>";
        }
        if (IsQuick)
        {
            var time = Math.Truncate((double)timer * 10) / 10;
            text = $"<color=#ff1919>残り時間:{time}秒</color>";
        }
        return text;
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {   
        return UseCount > 0 ? $"<#ff0000>({UseCount})</color>" : "";
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        var sp1 = new Achievement(RoleInfo, 2, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
        achievements.Add(2, sp1);
    }
}