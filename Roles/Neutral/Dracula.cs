using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

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
            (2, 5),
            true,
            countType: CountTypes.Dracula,
            Desc: () =>
            {
                if (OptionKenzokucount.GetInt() == 0)
                {
                    return string.Format(GetString("DraculaDescNoKenzoku"), OptionSuicideTimer.GetFloat(), OptionDieChance.GetInt(), OptionDieChanceBonus.GetInt());
                }
                return string.Format(GetString("DraculaDesc"), OptionSuicideTimer.GetFloat(), OptionDieChance.GetInt(), OptionDieChanceBonus.GetInt(), OptionKenzokuChance.GetInt());
            }
            //assignInfo: new RoleAssignInfo(CustomRoles.Dracula, CustomRoleTypes.Neutral)
            //{
            //    AssignCountRule = new(1, 1, 1)
            //}
        );
    public Dracula(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CanVent = OptionCanVent.GetBool();
        SuicideTimer = -9f;
        atooi = false;
        staticKenzokuid.Clear();
        Kenzokus.Clear();
        targetDieChanceBonus.Clear();
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
        SerialKillerLimit,
        DraculaKenzokucount,
        DraculaKenzokuChance,
        DraculaDieChance,
        DraculaDieChanceBonus
    }

    public static bool CanVent;
    float SuicideTimer;
    bool atooi;

    // 全クライアントの表示(GetMark)と後追い自殺処理(OnFixedUpdate)で参照されるため、
    // ホストが変更したら必ず SendRPC() で同期する。
    List<byte> Kenzokus = new(14);
    public static List<byte> staticKenzokuid = new(14);

    // ホスト側のキル判定内でのみ書き込み・参照されるため同期不要。
    Dictionary<byte, int> targetDieChanceBonus = new(14);

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 11);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.ImpostorVision, true, false);
        OptionSuicideTimer = FloatOptionItem.Create(RoleInfo, 14, OptionName.SerialKillerLimit, new(0.5f, 999f, 0.5f), 60f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDieChance = IntegerOptionItem.Create(RoleInfo, 15, OptionName.DraculaDieChance, new(0, 100, 5), 10, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionDieChanceBonus = IntegerOptionItem.Create(RoleInfo, 16, OptionName.DraculaDieChanceBonus, new(0, 100, 1), 5, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionKenzokuChance = IntegerOptionItem.Create(RoleInfo, 17, OptionName.DraculaKenzokuChance, new(0, 100, 1), 5, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionKenzokucount = IntegerOptionItem.Create(RoleInfo, 18, OptionName.DraculaKenzokucount, new(0, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Players).SetZeroNotation(OptionZeroNotation.Off);
        RoleAddAddons.Create(RoleInfo, 20);
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => CanVent;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("VampireBiteButtonText");
        return true;
    }
    public bool OverrideKillButton(out string text)
    {
        text = "Vampire_Kill";
        return true;
    }
    public override string GetAbilityButtonText() => GetString("SerialKillerSuicideButtonText");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "BadGirl_Ability";
        return true;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive() && !atooi)
        {
            // 実際のキル処理(CustomRoleManager.OnCheckMurder)はホスト権威で行う。
            // 非ホストは同期済みの Kenzokus をここで直接処理しない(結果はホストからのRPCで反映される)。
            if (AmongUsClient.Instance.AmHost)
            {
                foreach (var targetId in Kenzokus)
                {
                    var target = PlayerCatch.GetPlayerById(targetId);
                    CustomRoleManager.OnCheckMurder(target, target, target, target, true, true, Killpower: 10, deathReason: CustomDeathReason.FollowingSuicide);
                }
                Kenzokus.Clear();
                SendRPC();
            }

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
        if (target.GetCustomRole() is CustomRoles.Dracula) return;
        int diechance = Random.Range(0, 100);
        int Kenzokuchance = Random.Range(0, 100);

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

        if (diechance < OptionDieChance.GetInt() + targetDieChanceBonus.GetValueOrDefault(target.PlayerId, 0) && Player.IsAlive())
        {
            Kenzokuchance = 0;
            targetDieChanceBonus.Remove(target.PlayerId);
            CustomRoleManager.OnCheckMurder(Player, target, Player, target, true, true, Killpower: 1, deathReason: CustomDeathReason.Bloodloss, PlayKillSound: true);
            return;
        }

        if (!targetDieChanceBonus.ContainsKey(target.PlayerId))
        {
            targetDieChanceBonus[target.PlayerId] = OptionDieChanceBonus.GetInt();
        }
        targetDieChanceBonus[target.PlayerId] = targetDieChanceBonus[target.PlayerId] + OptionDieChanceBonus.GetInt();

        if (Kenzokuchance < OptionKenzokuChance.GetInt() && Kenzokus.Count < OptionKenzokucount.GetInt())
        {
            Kenzokus.Add(target.PlayerId);
            staticKenzokuid.Add(target.PlayerId);
            Logger.Info($"プレイヤーId :{target.PlayerId}を眷属にしました！", "Dracula");
            SendRPC();
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

    // --- 追加: Kenzokus の同期 ---
    // 書き込みはホスト側の OnCheckMurderAsKiller(眷属化)と OnFixedUpdate(後追い自殺後のクリア)のみ。
    // 読み取りは全クライアントの GetMark(表示)と OnFixedUpdate(後追い自殺トリガー)にまたがるため、
    // 書いたら都度 SendRPC() で全クライアントへ配る。
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)Kenzokus.Count);
        foreach (var id in Kenzokus)
            sender.Writer.Write(id);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        Kenzokus.Clear();
        byte count = reader.ReadByte();
        for (int i = 0; i < count; i++)
            Kenzokus.Add(reader.ReadByte());
    }
}