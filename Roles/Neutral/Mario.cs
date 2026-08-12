using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.AddOns.Neutral;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static Il2CppSystem.Threading.SemaphoreSlim;

namespace TownOfHost.Roles.Neutral;

public sealed class Mario : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Mario),
            player => new Mario(player),
            CustomRoles.Mario,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Neutral,
            56300,
            SetupOptionItem,
            "mr",
            "#ff6201",
            (1, 4),
            from: From.TownOfHost_Enhanced
        );
    public Mario(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        VentCount = 0;
        CanWin = false;
        Wined = false;
    }
    static OptionItem VentCool;
    static OptionItem WinventCount;

    public int WinCount => WinventCount.GetInt();

    public int VentCount;

    public bool CanWin;

    static OptionItem OptionAddWin;
    enum OptionName
    {
        WinventCount,
        CountKillerAddWin
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 10, show: () => !OptionAddWin.GetBool(), defo: 50);

        VentCool = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, new(2f, 180f, 0.5f), 2f, false).SetValueFormat(OptionFormat.Seconds);
        WinventCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.WinventCount, new(1, 100, 1), 30, false).SetValueFormat(OptionFormat.Times);
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 13, OptionName.CountKillerAddWin, false, false);
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => false;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = VentCool.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        ++VentCount;
        CheckWin();
        SendRPC();
        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        CheckWin();
        SendRPC();
    }

    public void CheckWin()
    {
        if (VentCount >= WinCount)
        {
            Win();
            CanWin = true;
        }
    }

    public void Win()
    {
        if (OptionAddWin.GetBool()) return;

        if (AmongUsClient.Instance.AmHost)
        {
            ForceSoloWin();
        }
    }

    public bool Wined { get; private set; }

    private void ForceSoloWin()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var otherPlayer in PlayerCatch.AllAlivePlayerControls)
        {
            // テロリストは除外(テロリスト勝利防止)
            if (otherPlayer.Is(CustomRoles.Terrorist))
            {
                continue;
            }

            // マリオ本人はスキップ（ここを正しく比較する）
            if (otherPlayer.PlayerId == Player.PlayerId)
            {
                continue;
            }

            otherPlayer.SetRealKiller(Player);
            otherPlayer.RpcMurderPlayer(otherPlayer);
            var playerState = PlayerState.GetByPlayerId(otherPlayer.PlayerId);
            playerState.DeathReason = CustomDeathReason.Bombed;
            playerState.SetDead();
        }

        if (!Wined)
        {
            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Mario, Player.PlayerId))
            {
                Wined = true;
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            }
        }
    }
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        // 追加勝利モードのときは勝者役職を返す（IAdditionalWinner 呼び出し元が使います）
        if (!Player.IsAlive()) return false;
        if (!CanWin) return false;

        if (OptionAddWin.GetBool())
        {
            winnerRole = CustomRoles.Mario;
            return true;
        }

        // 単独勝利モードならホスト側で既に ForceSoloWin を実行しているのでここでは false を返す
        return false;
    }

    public int ventreamCount => WinCount - VentCount;

    private void SendRPC()
    {
        using var sender = CreateSender();
        var remain = WinCount - VentCount;
        if (remain < 0) remain = 0;
        sender.Writer.Write(remain);
    }

    // 受信側で残り回数を同期する
    public override void ReceiveRPC(MessageReader reader)
    {
        var remain = reader.ReadInt32();
        VentCount = WinCount - remain;
    }

    // 名前横に残り回数を表示する
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (WinCount <= 0) return "";
        var disp = WinCount - VentCount;
        if (disp < 0) disp = 0;
        return Utils.ColorString(RoleInfo.RoleColor, $"({disp})");
    }
}
