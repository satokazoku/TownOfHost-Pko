/*using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Neutral;

public sealed class Dracula : RoleBase, ILNKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Dracula),
            player => new Dracula(player),
            CustomRoles.Dracula,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            56500,
            SetupOptionItem,
            "drc",
            "#4d4398",
            (1, 6),
            true,
            countType: CountTypes.Dracula,
            assignInfo: new RoleAssignInfo(CustomRoles.Dracula, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );
    public Dracula(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CanVent = OptionCanVent.GetBool();
        SuicideTimer = OptionSuicideTimer.GetFloat();
        kenzokucount = 0;
        atooi = false;
    }

    public static OptionItem OptionKillCooldown;
    public static OptionItem OptionCanVent;
    public static OptionItem OptionSuicideTimer;
    public static OptionItem OptionHasImpostorVision;
    public static OptionItem OptionKenzokucount;
    public static OptionItem OptionkenzokuChance;
    public static OptionItem OptionDieChance;
    public static OptionItem OptionDieChanceBonus;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionHasImpostorVision.GetBool());
        AURoleOptions.PhantomCooldown = SuicideTimer;
    }

    enum OptionName
    {
        DraculaSuicideTimer,
        DraculaKenzokucount,
        DraculaKenzokuChance,
        DraculaDieChance,
        DraculaDieChanceBonus
    }

    public static bool CanVent;
    float SuicideTimer;
    int kenzokucount;
    bool atooi;

    Dictionary<byte, string> kenzokus = new(14);
    Dictionary<byte, int> targetDieChanceBonus = new(14);

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 11);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.ImpostorVision, true, false);
        OptionSuicideTimer = FloatOptionItem.Create(RoleInfo, 14, OptionName.DraculaSuicideTimer, new(0.5f, 999f, 0.5f), 60f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDieChance = IntegerOptionItem.Create(RoleInfo, 15, OptionName.DraculaDieChance, new(0, 100, 5), 10, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionDieChanceBonus = IntegerOptionItem.Create(RoleInfo, 16, OptionName.DraculaDieChanceBonus, new(0, 100, 1), 5, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionkenzokuChance = IntegerOptionItem.Create(RoleInfo, 17, OptionName.DraculaKenzokuChance, new(0, 100, 1), 5, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionKenzokucount = IntegerOptionItem.Create(RoleInfo, 18, OptionName.DraculaKenzokucount, new(0, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Players);
        RoleAddAddons.Create(RoleInfo, 20);
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => CanVent;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive() && !atooi)
        {
            foreach (var targetId in kenzokus.Keys)
            {
                var target = PlayerCatch.GetPlayerById(targetId);
                CustomRoleManager.OnCheckMurder(target, target, target, target, true, true, Killpower: 10, deathReason: CustomDeathReason.FollowingSuicide);
            }
            kenzokus.Clear();

            atooi = true;
        }

        if (AmongUsClient.Instance.AmHost && !ExileController.Instance && Player.IsAlive())
        {
            if (SuicideTimer <= 0f)
            {
                MyState.DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayer(Player);
            }
            else
            {
                SuicideTimer -= Time.fixedDeltaTime;
            }
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;

        var target = info.AppearanceTarget;

        int diechance = Random.Range(0, 100);
        int kenzokuchance = Random.Range(0, 100);
        int bonus = targetDieChanceBonus.GetValueOrDefault(target.PlayerId, 0);
        foreach (var targetId in kenzokus.Keys)
        {
            var kenzoku = PlayerCatch.GetPlayerById(targetId);
            if (kenzoku.PlayerId == target.PlayerId)
            {
                Logger.Info($"{target}は眷属です", "Dracula");
                return;
            }
        }
        SuicideTimer = OptionSuicideTimer.GetFloat();
        Main.AllPlayerKillCooldown[Player.PlayerId] = OptionKillCooldown.GetFloat();

        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown();
        Player.SyncSettings();
        Player.SetKillCooldown(delay: true);
        if (diechance < OptionDieChance.GetInt() + bonus && Player.IsAlive())
        {
            kenzokuchance = 101;
            RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, true, Killpower: 1, deathReason: CustomDeathReason.Bite);
            targetDieChanceBonus.Remove(target.PlayerId);

            return; //キルできた時は眷属作成処理に移行しない
        }
        targetDieChanceBonus[target.PlayerId] = Mathf.Min(bonus + OptionDieChanceBonus.GetInt(), 100);

        if (kenzokuchance < OptionkenzokuChance.GetInt() && kenzokucount < OptionKenzokucount.GetInt() && kenzokuchance != 101)
        {
            ++kenzokucount;
            kenzokus.Add(target.PlayerId, "");
            Logger.Info($"プレイヤーId :{target.PlayerId}を眷属にしました！", "Dracula");
            target.RpcSetCustomRole(CustomRoles.kenzoku);
            return;
        }
    }

    public override void AfterMeetingTasks()
    {
        if (Player.IsAlive())
        {
            SuicideTimer = 0f;
        }
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;
    }
}*/