using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using Steamworks;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static Il2CppSystem.Threading.SemaphoreSlim;
using static TownOfHost.PlayerCatch;
using static UnityEngine.GraphicsBuffer;


namespace TownOfHost.Roles.Neutral;

public sealed class Mermaid : RoleBase, ILNKiller, ISchrodingerCatOwner, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Mermaid),
            player => new Mermaid(player),
            CustomRoles.Mermaid,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            56600,
            SetupOptionItem,
            "mer",
            "#1d7fad",
            (2, 2),
            true,
            countType: CountTypes.Crew
        );
    public Mermaid(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        chatcount = 0;
        IsKilledImpostor = false;
        Currentmode = 1;
        cancangemode = false;
        MeetingCount = 0;
    }
    public static OptionItem OptionKillCooldown;
    public static OptionItem OptionAddWin;
    public static OptionItem OptionChangingChats;
    public static OptionItem OptionNotifyChange;
    public static OptionItem OptionLockMode;
    public static OptionItem OptionNotify;

    int chatcount;
    int Currentmode; //1=人魚(インポスター) 0=人間(クルーメイト)
    bool IsKilledImpostor;
    bool cancangemode;
    int MeetingCount;

    enum OptionName
    {
        CountKillerAddWin,
        MermaidChangingChats,
        MermaidNotifyChange,
        MermaidLockMode,
        MermaidNotify
    }
    private static float KillCooldown;
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);

        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 40f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionChangingChats = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MermaidChangingChats, new(1, 999, 1), 35, false)
            .SetValueFormat(OptionFormat.Times);
        OptionNotifyChange = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MermaidNotifyChange, false, false);
        OptionLockMode = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MermaidLockMode, true, false);
        OptionNotify = BooleanOptionItem.Create(RoleInfo, 14, OptionName.MermaidNotify, true, false);
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 15, OptionName.CountKillerAddWin, true, false);

        RoleAddAddons.Create(RoleInfo, 16);
    }
    //にゃんこはその時の陣営にする。
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => Currentmode == 1 ? ISchrodingerCatOwner.TeamType.Mad : ISchrodingerCatOwner.TeamType.Crew;
    public float CalculateKillCooldown() => KillCooldown;
    public override void Add()
    {
        KillCooldown = OptionKillCooldown.GetFloat();
    }
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        var target = info.AppearanceTarget;
        if (target.Is(CustomRoleTypes.Impostor) && OptionLockMode.GetBool())
        {
            IsKilledImpostor = true;
        }
        else
        {
            IsKilledImpostor = false;
        }
        SendRPC();
        return;
    }

    public void ChangeMode(PlayerControl Player)
    {
        if (!Player.IsAlive())
        {
            return;
        }
        if (IsKilledImpostor)
        {
            Currentmode = 1;
            SendRPC();
            return;
        }
        if (Currentmode == 1)
        {
            Currentmode = 0;
            SendRPC();
            if (!OptionNotifyChange.GetBool())
            {
                return;
            }
            foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null))
            {
                Utils.SendMessage(string.Format(GetString("MermaidChangeNotifyCrew")), go.PlayerId);
            }
            return;
        }
        if (Currentmode == 0)
        {
            Currentmode = 1;
            SendRPC();
            if (!OptionNotifyChange.GetBool())
            {
                return;
            }
            foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null))
            {
                Utils.SendMessage(string.Format(GetString("MermaidChangeNotifyImp")), go.PlayerId);
            }
            return;
        }
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(Color.white, Currentmode == 1
            ? "<color=#ff1919>[人魚の姿]</color>"
            : "<color=#8cffff>[人間の姿]</color>");
        if (GameStates.IsMeeting)
            progress += Utils.ColorString(Color.white, $"{OptionChangingChats.GetInt() - chatcount})");
        return progress;
    }

    public static void SCheckCanwin(ref GameOverReason reason)
    {
        if (OptionAddWin.GetBool())
        {
            return;
        }
        foreach (var p in AllAlivePlayerControls)
        {
            if (p.GetRoleClass() is Mermaid mermaid)
            {
                mermaid.MermaidCheckWin(ref reason);
            }
        }      
    }
    public bool MermaidCheckWin(ref GameOverReason reason)
    {
        var currentWinner = CustomWinnerHolder.WinnerTeam;
        if (currentWinner == CustomWinner.Crewmate && Currentmode == 0)
        {
            foreach (var pc in AllPlayerControls)
            {
                if (pc == null || !pc.IsAlive()) continue;
                if (pc.GetRoleClass() is not Mermaid Mermaid) continue;
                if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Mermaid, pc.PlayerId, true)) continue;

                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Mermaid);
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

                reason = GameOverReason.CrewmatesByVote;
            }
            return true;
        }
        else if (currentWinner == CustomWinner.Impostor && Currentmode == 1)
        {
            foreach (var pc in AllPlayerControls)
            {
                if (pc == null || !pc.IsAlive()) continue;
                if (pc.GetRoleClass() is not Mermaid Mermaid) continue;
                if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Mermaid, pc.PlayerId, true)) continue;

                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Mermaid);
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);

                reason = GameOverReason.CrewmatesByVote;
            }
            return true;
        }
        else
        {
            return false;
        }
    }
    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(cancangemode);
        sender.Writer.Write(IsKilledImpostor);
        sender.Writer.Write(Currentmode);
        sender.Writer.Write(chatcount);
        sender.Writer.Write(MeetingCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        cancangemode = reader.ReadBoolean();
        IsKilledImpostor = reader.ReadBoolean();
        Currentmode = reader.ReadInt32();
        chatcount = reader.ReadInt32();
        MeetingCount = reader.ReadInt32();
    }
    public bool CheckWin(ref CustomRoles winnerRole)
        => Currentmode == 1 && OptionAddWin.GetBool() ? CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor : CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate;

    public override void AfterMeetingTasks()
    {
        if (cancangemode && !IsKilledImpostor)
        {
            ChangeMode(Player);
        }
        cancangemode = false;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ++MeetingCount;
        if (MeetingCount == 1 && OptionNotify.GetBool())
        {
            foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null))
            {
                Utils.SendMessage(string.Format(GetString("MermaidNotifyText")), go.PlayerId);
            }
        }
        if (OptionChangingChats.GetInt() - chatcount < 0)
        {
            chatcount = OptionChangingChats.GetInt();
        }
    }
    public void Notify()
    {
        if (IsKilledImpostor)
        {
            Currentmode = 1;
            SendRPC();
            return;
        }
        SendRPC();
        var Player = PlayerControl.LocalPlayer;

        if (Currentmode == 1)
        {
            Utils.SendMessage(string.Format(GetString("MermaidChangeNotifyForMermaidCrew")), Player.PlayerId);
        }
        if (Currentmode == 0)
        {
            Utils.SendMessage(string.Format(GetString("MermaidChangeNotifyForMermaidImp")), Player.PlayerId);
        }
    }
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    public static class MermaidChatPatch
    {
        public static void Postfix(PlayerControl sourcePlayer, string chatText)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (sourcePlayer == null || !sourcePlayer) return;
            if (!sourcePlayer.IsAlive()) return;
            if (sourcePlayer.GetRoleClass() is not Mermaid Mermaid) return;
            foreach (var p in AllAlivePlayerControls)
            {
                if (p.GetRoleClass() is Mermaid mermaid)
                {
                    if (mermaid.IsKilledImpostor) //インポスターをキルしていて設定が有効な場合は何もしない
                    {
                        mermaid.Currentmode = 1;
                        return;
                    }

                    // ★ /cmd を含むメッセージはコマンドなのでリセットしない
                    if (chatText != null && chatText.TrimStart().StartsWith("/cmd"))
                    {
                        return;
                    }
                    ++mermaid.chatcount;
                    if (mermaid.chatcount >= OptionChangingChats.GetInt() && !mermaid.cancangemode && !mermaid.IsKilledImpostor)
                    {
                        mermaid.cancangemode = true;
                        mermaid.chatcount = 0;
                    }
                    mermaid.Notify();
                }
            }
        }
    }
}
