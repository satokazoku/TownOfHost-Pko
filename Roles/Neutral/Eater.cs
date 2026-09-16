using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using TownOfHost.Roles.Vanilla;
using UnityEngine;
using static TownOfHost.Roles.Crewmate.AllArounder;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;

public sealed class Eater : RoleBase, IKiller, IUsePhantomButton, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Eater),
            player => new Eater(player),
            CustomRoles.Eater,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            51200,
            SetupOptionItem,
            "Ea",
            "#662B2C",
            (5, 2),
            true,
            countType: CountTypes.Eater,
            from: From.ExtremeRoles
        );

    static OptionItem OptionCanUseVent;
    static OptionItem OptionSwallowCooldown;
    static OptionItem OptionEatCooldown;
    static OptionItem OptionWinCount;
    static OptionItem OptionSwallowTime;
    static OptionItem OptionSwallowRange;
    static OptionItem OptionShowArrow;
    static OptionItem OptionHasImpostorVision;

    float EatCooldown => OptionEatCooldown.GetFloat();

    enum OptionName
    {
        EaterVisionRange,
        EaterLinkCooldowns,
        EaterSharedCooldown,
        EaterSwallowCooldown,
        EaterEatCooldown,
        EaterWinCount,
        EaterSwallowTime,
        EaterEatIncreaseRate,
        EaterSwallowRange,
        EaterSwallowCooldownIncreaseRate,
        EaterMeetingReduceRate
    }

    sealed class PendingSwallowInfo
    {
        public byte TargetId;
        public float Elapsed;
        public float Required;
        public PendingSwallowInfo(byte targetId, float required)
        {
            TargetId = targetId;
            Required = required;
            Elapsed = 0f;
        }
    }

    readonly Dictionary<byte, Vector3> deadBodyPositions = new();
    static readonly HashSet<byte> EatenBodies = new();

    int eatOrSwallowCount;
    bool eatMode;
    float eatCooldownTimer;

    PendingSwallowInfo pendingSwallow;
    public Eater(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        eatOrSwallowCount = 0;
        eatMode = true;
        pendingSwallow = null;
        EatenBodies.Clear();
        Viperkilledplayers = new();
    }

    [Attributes.GameModuleInitializer]
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 10, defo: 1);

        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.ImpostorVision, true, false);
        OptionCanUseVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, false, false);
        OptionSwallowCooldown = FloatOptionItem.Create(RoleInfo, 15, OptionName.EaterSwallowCooldown, new(0f, 60f, 1f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionEatCooldown = FloatOptionItem.Create(RoleInfo, 16, OptionName.EaterEatCooldown, new(0f, 60f, 1f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionWinCount = IntegerOptionItem.Create(RoleInfo, 17, OptionName.EaterWinCount, new(1, 10, 1), 3, false);
        OptionSwallowTime = FloatOptionItem.Create(RoleInfo, 18, OptionName.EaterSwallowTime, new(0.5f, 60f, 0.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSwallowRange = FloatOptionItem.Create(RoleInfo, 19, OptionName.EaterSwallowRange, new(1.25f, 5f, 0.25f), 1.75f, false)
        .SetValueFormat(OptionFormat.Multiplier);
        OptionShowArrow = BooleanOptionItem.Create(RoleInfo, 24, "EaterShowArrow", true, false);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionHasImpostorVision.GetBool());
        AURoleOptions.PhantomCooldown = 0.1f;
    }

    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseImpostorVentButton() => OptionCanUseVent.GetBool();
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => OptionSwallowCooldown.GetFloat();
    Dictionary<byte, float> Viperkilledplayers = new();

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!Player.IsAlive()) return;
        if (pendingSwallow != null) return;
        eatMode = !eatMode;
    }

    public bool UseOneclickButton => true;
    public bool IsPhantomRole => true;
    public bool IsresetAfterKill => false;

    public override void Add()
    {
        eatCooldownTimer = -9f;
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        info.DoKill = false;

        if (target.Is(CustomRoles.Madpsycho))
        {
            if (Madpsycho.CanPsycho)
            {
                PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                target.RpcMurderPlayer(Player);
                return;
            }
        }
        killer.SetKillCooldown(OptionSwallowTime.GetFloat());
        if (target.IsAlive() && pendingSwallow == null)
        {
            pendingSwallow = new(target.PlayerId, 0f);
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: killer);
        }
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        pendingSwallow = null;
        var send = string.Format(GetString("EaterMInfo"), eatOrSwallowCount);
        Utils.SendMessage(send, Player.PlayerId);
        Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
        eatOrSwallowCount++;
        deadBodyPositions.Do(oniku => GetArrow.Remove(Player.PlayerId, oniku.Value));
        deadBodyPositions.Clear();

        eatCooldownTimer = 0f;
        RpcClearDiePlayerPos();
    }

    public bool? CheckKillFlash(MurderInfo info)
    {
        var pos = info.AppearanceTarget.GetTruePosition();
        deadBodyPositions.Add(info.AppearanceTarget.PlayerId, pos);
        GetArrow.Add(Player.PlayerId, pos);
        if (info.AppearanceKiller.GetCustomRole() is CustomRoles.Viper) Viperkilledplayers.Add(info.AppearanceTarget.PlayerId, Viper.ViperDissolveTime);
        RpcAddDiePlayerPos(info.AppearanceTarget.PlayerId, pos);

        return OptionShowArrow.GetBool();
    }

    public void RpcAddDiePlayerPos(byte targetId, Vector2 pos)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.AdddeadBodyPositions);
        sender.Writer.Write(targetId);
        NetHelpers.WriteVector2(pos, sender.Writer);
    }
    public void RpcClearDiePlayerPos()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.CleardeadBodyPositions);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        eatCooldownTimer += Time.fixedDeltaTime;
        if (eatCooldownTimer >= EatCooldown)
        {
            eatCooldownTimer = EatCooldown;
        }
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsInTask && pendingSwallow != null)
        {
            if (!Player.IsAlive())
            {
                pendingSwallow = null;
                UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
            }
            else
            {
                var ar_target = PlayerCatch.GetPlayerById(pendingSwallow.TargetId);
                var ar_time = pendingSwallow.Elapsed;
                if (!ar_target.IsAlive())
                {
                    pendingSwallow = null;
                }
                else if (ar_time >= OptionSwallowTime.GetFloat())
                {
                    Player.SetKillCooldown();
                    var target = PlayerCatch.GetPlayerById(pendingSwallow.TargetId);
                    ++eatOrSwallowCount;
                    PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Swallowed;
                    target.RpcExileV3();
                    if (eatOrSwallowCount >= OptionWinCount.GetInt())
                    {
                        Win();
                    }
                    pendingSwallow = null;
                }
                else
                {
                    float dis;
                    dis = Vector2.Distance(Player.transform.position, ar_target.transform.position);
                    if (dis <= OptionSwallowRange.GetFloat())
                    {
                        pendingSwallow.Elapsed += Time.fixedDeltaTime;
                    }
                    else
                    {
                        pendingSwallow = null;
                        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
                        Player.SetKillCooldown(0.1f, force: true);

                        Logger.Info($"Canceled: {Player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                    }
                }
            }
        }
    }

    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (!eatMode || pendingSwallow != null)
        {
            return false;
        }
        if (eatCooldownTimer < EatCooldown)
        {
            return false;
        }
        if (reporter.PlayerId == Player.PlayerId && target != null && !EatenBodies.Contains(target?.PlayerId ?? (byte)250))
        {
            reason = DontReportreson.Eat;
            EatenBodies.Add(target.PlayerId);
            deadBodyPositions.Where(poss => poss.Key == target.PlayerId).Do(poss => GetArrow.Remove(Player.PlayerId, poss.Value));
            RpcEatPlayer(target.PlayerId);
            Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
            eatOrSwallowCount++;
            if (eatOrSwallowCount >= OptionWinCount.GetInt())
            {
                Win();
            }
            eatCooldownTimer = 0f;
            return true;
        }
        if (reporter?.PlayerId != target?.PlayerId && target != null && EatenBodies.Contains(target?.PlayerId ?? (byte)250))
        {
            reason = DontReportreson.Eat;
            Logger.Info($"{target?.PlayerName ?? "???"}は食事済みだからキャンセル", "Vulture");
            return true;
        }
        return false;
    }
    public void Win()
    {
        ForceSoloWin();
    }
    private void ForceSoloWin()
    {
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Eater, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }


    public void RpcEatPlayer(byte targetId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.EatPlayer);
        sender.Writer.Write(targetId);
    }
    enum RPC_Types
    {
        EatPlayer,
        AdddeadBodyPositions,
        CleardeadBodyPositions
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        switch ((RPC_Types)reader.ReadPackedInt32())
        {
            case RPC_Types.EatPlayer:
                var targetId = reader.ReadByte();
                EatenBodies.Add(targetId);
                deadBodyPositions.Where(poss => poss.Key == targetId).Do(poss => GetArrow.Remove(Player.PlayerId, poss.Value));
                break;
            case RPC_Types.AdddeadBodyPositions:
                var playerPos = NetHelpers.ReadVector2(reader);
                deadBodyPositions.Add(reader.ReadByte(), playerPos);
                GetArrow.Add(Player.PlayerId, playerPos);
                break;
            case RPC_Types.CleardeadBodyPositions:
                deadBodyPositions.Do(oniku => GetArrow.Remove(Player.PlayerId, oniku.Value));
                deadBodyPositions.Clear();
                break;
        }
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting) return "";
        seen ??= seer;
        var text = "食らう";
        if (!eatMode || pendingSwallow != null)
        {
            text = "死体通報";
        }
        return $"<color=#662B2C>現在のモード：{text}モード</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var CoolDownLeft = Math.Ceiling(EatCooldown - eatCooldownTimer);
        var text = Utils.ColorString(Color.yellow, $"({CoolDownLeft})");
        return $"{text}";
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Eater_eat";
        return true;
    }
    public override string GetAbilityButtonText() => GetString("Modechenge");

    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("Swallowed");
        return true;
    }

    public override bool OverrideAbilityButton(out string text)
    {
        text = "Mode_change";
        return true;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        //死亡済み or 会議中 or 他人にマークの場合は空
        if (!Player.IsAlive() || seer.PlayerId != seen.PlayerId) return "";

        var text = "";
        if (OptionShowArrow.GetBool() && !isForMeeting)
        {
            var str = $" <color={RoleInfo.RoleColorCode}>";

            //事前に保存しておいた奴を出す
            foreach (var arrow in deadBodyPositions)
            {
                str += GetArrow.GetArrows(seer, arrow.Value);
            }
            text = $"{str}</color>";
        }
        return text;
    }

    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        achievements.Add(0, n1);
    }
}