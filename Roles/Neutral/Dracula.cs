/*using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using TMPro;
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
        SuicideTimer = -9f;
        Kenzokucount = 0;
        atooi = false;
    }

    public static OptionItem OptionKillCooldown;
    public static OptionItem OptionCanVent;
    public static OptionItem OptionSuicideTimer;
    public static OptionItem OptionHasImpostorVision;
    public static OptionItem OptionKenzokucount;
    public static OptionItem OptionKenzokuChance;
    public static OptionItem OptionDieChance;
    public static OptionItem OptionDieChanceBonus;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionHasImpostorVision.GetBool());
        AURoleOptions.PhantomCooldown = OptionSuicideTimer.GetFloat();
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
    int Kenzokucount;
    bool atooi;

    List<byte> Kenzokus = new(14);
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
        OptionKenzokuChance = IntegerOptionItem.Create(RoleInfo, 17, OptionName.DraculaKenzokuChance, new(0, 100, 1), 5, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionKenzokucount = IntegerOptionItem.Create(RoleInfo, 18, OptionName.DraculaKenzokucount, new(0, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Players);
        RoleAddAddons.Create(RoleInfo, 20);
        HideRoleOptions(CustomRoles.Dracula);
    }
    internal static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null &&
            Options.CustomRoleSpawnChances.TryGetValue(role, out var spawnOption))
        {
            spawnOption.SetHidden(true);
        }

        if (Options.CustomRoleCounts != null &&
            Options.CustomRoleCounts.TryGetValue(role, out var countOption))
        {
            countOption.SetHidden(true);
        }
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
            foreach (var targetId in Kenzokus)
            {
                var target = PlayerCatch.GetPlayerById(targetId);
                CustomRoleManager.OnCheckMurder(target, target, target, target, true, true, Killpower: 10, deathReason: CustomDeathReason.FollowingSuicide);
            }
            Kenzokus.Clear();

            atooi = true;
        }

        if (AmongUsClient.Instance.AmHost && !ExileController.Instance && Player.IsAlive())
        {
            if (SuicideTimer >= OptionSuicideTimer.GetFloat())
            {
                MyState.DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayer(Player);
            }
            else
            {
                SuicideTimer += Time.fixedDeltaTime;
            }
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;

        var target = info.AppearanceTarget;

        int diechance = Random.Range(0, 100);
        int Kenzokuchance = Random.Range(0, 100);
        int bonus = targetDieChanceBonus.GetValueOrDefault(target.PlayerId, 0);

        if (Kenzokus.Contains(target.PlayerId))
        {
            Logger.Info($"{target}は眷属です", "Dracula");
            return;
        }
        Player.MarkDirtySettings();
        SuicideTimer = 0f;
        Player.RpcResetAbilityCooldown();
        Player.SyncSettings();
        Main.AllPlayerKillCooldown[Player.PlayerId] = OptionKillCooldown.GetFloat();
        Player.SetKillCooldown(delay: true);

        if (diechance < OptionDieChance.GetInt() + bonus && Player.IsAlive())
        {
            Kenzokuchance = 101;
            targetDieChanceBonus.Remove(target.PlayerId);
            CustomRoleManager.OnCheckMurder(target, target, Player, target, true, true, Killpower: 1, deathReason: CustomDeathReason.Bloodloss);
            Main.AllPlayerKillCooldown[Player.PlayerId] = OptionKillCooldown.GetFloat();
            Player.SetKillCooldown(delay: true);
            return;
        }

        targetDieChanceBonus[target.PlayerId] = Mathf.Min(bonus + OptionDieChanceBonus.GetInt(), 100);

        if (Kenzokuchance < OptionKenzokuChance.GetInt() && Kenzokucount < OptionKenzokucount.GetInt() && Kenzokuchance != 101)
        {
            ++Kenzokucount;
            Kenzokus.Add(target.PlayerId);
            Logger.Info($"プレイヤーId :{target.PlayerId}を眷属にしました！", "Dracula");
            target.RpcSetCustomRole(CustomRoles.Kenzoku);
            return;
        }
    }

    public override void AfterMeetingTasks()
    {
        targetDieChanceBonus.Clear();
        if (Player.IsAlive())
        {
            SuicideTimer = 0f;
        }
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;

        if (Kenzokus.Contains(seen.PlayerId))
            return Utils.ColorString(RoleInfo.RoleColor, "▲");

        return "";
    }
    public override void OnSpawn(bool initialState = false)
    {
        SuicideTimer = 0f;
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
    }
}*/