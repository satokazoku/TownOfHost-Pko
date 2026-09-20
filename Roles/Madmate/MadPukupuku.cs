using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Madmate;

public sealed class MadPukupuku : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadPukupuku),
            player => new MadPukupuku(player),
            CustomRoles.MadPukupuku,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            22900,
            SetupOptionItem,
            "mpk",
            OptionSort: (2, 2),
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            from: From.TownOfHost_Pko
        );
    public MadPukupuku(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
    }
    static OptionItem OptionCanVent;
    static OptionItem OptionRevengeImpostor;
    static OptionItem OptionNotify;

    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    public HashSet<byte> VotedPlayerId = new();
    bool IsExild = false;
    enum Op
    {
        MadPukuPukuCanRevengeImpostor,
        MadPukuPukuNotify
    }

    public static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        OptionRevengeImpostor = BooleanOptionItem.Create(RoleInfo, 11, Op.MadPukuPukuCanRevengeImpostor, false, false);
        OptionNotify = BooleanOptionItem.Create(RoleInfo, 12, Op.MadPukuPukuNotify, false, false);
        RoleAddAddons.Create(RoleInfo, 20);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
    public override void Add()
    {
        //テスト用
        _ = new LateTask(() =>
        {
            VotedPlayerId.Add(Player.PlayerId);

            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                VotedPlayerId.Add(p.PlayerId);
            }
        }, 2f, "MPK_Voted_Test", true);
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (Player.PlayerId != exiled.PlayerId) return;
        IsExild = true;
        if (!OptionNotify.GetBool()) return;
        foreach (var p in VotedPlayerId)
        {
            Utils.SendMessage(GetString("MadPukuPukuNotify"),p);
        }
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (IsTaskFinished && !Player.IsAlive() && IsExild)
        {
            foreach (var p in VotedPlayerId)
            {
                var pc = PlayerCatch.GetPlayerById(p);
                if ((!pc.Is(CustomRoleTypes.Impostor)) || OptionRevengeImpostor.GetBool())
                {
                    if (pc.PlayerId != Player.PlayerId)
                    {
                        PlayerState.GetByPlayerId(pc.PlayerId).DeathReason = CustomDeathReason.Poisoned;
                        pc.RpcMurderPlayerV2(pc);
                        VotedPlayerId.Remove(p);
                    }
                }
            }
        }
        return true;
    }
}
