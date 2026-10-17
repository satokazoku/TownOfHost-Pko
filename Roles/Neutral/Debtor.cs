using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Modules;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

/// <summary>
/// 代償者。能力を得るほど強くなるが、キルクールも増加する単独勝利ニュートラルキラー。
/// </summary>
public sealed class Debtor : RoleBase, ILNKiller, IUsePhantomButton
{
    // 一時的に無効化。元に戻すときは元のDebtor.csへ差し替える。
    // RoleInfoを登録しないため、代償者本体と設定項目が生成されない。
    public static readonly SimpleRoleInfo RoleInfo = null;

    private enum OptionName
    {
        DebtorRequiredKills,
        DebtorRequiredAcquisitions,
        DebtorInitialKillCooldown,
        DebtorKillCooldownIncrease,
        DebtorMaximumKillCooldown,
        DebtorAbilityCooldown,
        DebtorAcquisitionMethod,
        DebtorAllowDuplicates,
        DebtorMaximumAcquisitions,
        DebtorCountActivations,
        DebtorRequiredActivations,
        DebtorTrackerDuration,
        DebtorTrackerDistance,
        DebtorAcceleration,
        DebtorEnhancedVision,
        DebtorLongReach,
        DebtorNightVision,
        DebtorTracker
    }

    private enum Ability
    {
        Acceleration,
        EnhancedVision,
        LongReach,
        NightVision,
        Tracker
    }

    private static OptionItem RequiredKills;
    private static OptionItem RequiredAcquisitions;
    private static OptionItem InitialKillCooldown;
    private static OptionItem KillCooldownIncrease;
    private static OptionItem MaximumKillCooldown;
    private static OptionItem AbilityCooldown;
    private static OptionItem AllowDuplicates;
    private static OptionItem MaximumAcquisitions;
    private static OptionItem CountActivations;
    private static OptionItem RequiredActivations;
    private static OptionItem TrackerDuration;
    private static OptionItem TrackerDistance;
    private static OptionItem Acceleration;
    private static OptionItem EnhancedVision;
    private static OptionItem LongReach;
    private static OptionItem NightVision;
    private static OptionItem Tracker;

    private readonly List<Ability> abilities = new();
    private readonly Dictionary<byte, Vector2> trackerBodies = new();
    private int accelerationCount;
    private int activationCount;
    private bool hasEnhancedVision;
    private bool hasLongReach;
    private bool hasNightVision;
    private bool hasWon;
    private float abilityCooldown;
    private float currentKillCooldown;

    public Debtor(PlayerControl player) : base(RoleInfo, player, () => HasTask.False)
    {
        currentKillCooldown = InitialKillCooldown.GetFloat();
        abilityCooldown = 0f;
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 3);
        RequiredKills = IntegerOptionItem.Create(RoleInfo, 10, OptionName.DebtorRequiredKills, new(1, 10, 1), 3, false).SetValueFormat(OptionFormat.Players);
        RequiredAcquisitions = IntegerOptionItem.Create(RoleInfo, 11, OptionName.DebtorRequiredAcquisitions, new(1, 10, 1), 2, false).SetValueFormat(OptionFormat.Times);
        InitialKillCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.DebtorInitialKillCooldown, new(10f, 60f, 5f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        KillCooldownIncrease = FloatOptionItem.Create(RoleInfo, 13, OptionName.DebtorKillCooldownIncrease, new(0f, 15f, 1f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        MaximumKillCooldown = FloatOptionItem.Create(RoleInfo, 14, OptionName.DebtorMaximumKillCooldown, new(10f, 120f, 5f), 45f, false).SetValueFormat(OptionFormat.Seconds);
        AbilityCooldown = FloatOptionItem.Create(RoleInfo, 15, OptionName.DebtorAbilityCooldown, new(5f, 60f, 5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        // 初期実装はランダム方式。将来の3択・全選択用に設定キーだけ保持する。
        StringOptionItem.Create(RoleInfo, 16, OptionName.DebtorAcquisitionMethod, new[] { "Random", "ThreeChoices", "All" }, 0, false);
        AllowDuplicates = BooleanOptionItem.Create(RoleInfo, 17, OptionName.DebtorAllowDuplicates, false, false);
        MaximumAcquisitions = IntegerOptionItem.Create(RoleInfo, 18, OptionName.DebtorMaximumAcquisitions, new(1, 10, 1), 5, false).SetValueFormat(OptionFormat.Times);
        CountActivations = BooleanOptionItem.Create(RoleInfo, 19, OptionName.DebtorCountActivations, false, false);
        RequiredActivations = IntegerOptionItem.Create(RoleInfo, 20, OptionName.DebtorRequiredActivations, new(0, 10, 1), 0, false, CountActivations).SetValueFormat(OptionFormat.Times);
        TrackerDuration = FloatOptionItem.Create(RoleInfo, 21, OptionName.DebtorTrackerDuration, new(1f, 60f, 1f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        TrackerDistance = FloatOptionItem.Create(RoleInfo, 22, OptionName.DebtorTrackerDistance, new(1f, 20f, 0.5f), 8f, false);
        Acceleration = FloatOptionItem.Create(RoleInfo, 23, OptionName.DebtorAcceleration, new(0.05f, 0.5f, 0.05f), 0.15f, false);
        EnhancedVision = BooleanOptionItem.Create(RoleInfo, 24, OptionName.DebtorEnhancedVision, true, false);
        LongReach = BooleanOptionItem.Create(RoleInfo, 25, OptionName.DebtorLongReach, true, false);
        NightVision = BooleanOptionItem.Create(RoleInfo, 26, OptionName.DebtorNightVision, true, false);
        Tracker = BooleanOptionItem.Create(RoleInfo, 27, OptionName.DebtorTracker, true, false);
        RoleAddAddons.Create(RoleInfo, 40, NeutralKiller: true);
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public float CalculateKillCooldown() => currentKillCooldown;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = abilityCooldown;
        opt.SetVision(hasNightVision);
        if (hasEnhancedVision)
            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision * 1.25f);
        if (hasLongReach)
            AURoleOptions.KillDistance = 2;
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (!Player.IsAlive() || abilityCooldown > 0f || abilities.Count >= MaximumAcquisitions.GetInt()) return;

        var candidates = Enum.GetValues<Ability>().Where(IsEnabled).Where(a => AllowDuplicates.GetBool() || !abilities.Contains(a)).Where(a => CountOf(a) < MaxCount(a)).ToList();
        if (candidates.Count == 0) return;
        var acquired = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        abilities.Add(acquired);
        ApplyAbility(acquired);
        abilityCooldown = AbilityCooldown.GetFloat();
        currentKillCooldown = Math.Min(MaximumKillCooldown.GetFloat(), InitialKillCooldown.GetFloat() + abilities.Count * KillCooldownIncrease.GetFloat());
        CheckVictory();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    private bool IsEnabled(Ability ability) => ability switch
    {
        Ability.Acceleration => true,
        Ability.EnhancedVision => EnhancedVision.GetBool(),
        Ability.LongReach => LongReach.GetBool(),
        Ability.NightVision => NightVision.GetBool(),
        Ability.Tracker => Tracker.GetBool(),
        _ => false
    };

    private int MaxCount(Ability ability) => ability == Ability.Acceleration ? 2 : 1;
    private int CountOf(Ability ability) => abilities.Count(a => a == ability);

    private void ApplyAbility(Ability ability)
    {
        switch (ability)
        {
            case Ability.Acceleration:
                accelerationCount++;
                Main.AllPlayerSpeed[Player.PlayerId] += Acceleration.GetFloat();
                break;
            case Ability.EnhancedVision: hasEnhancedVision = true; break;
            case Ability.LongReach: hasLongReach = true; break;
            case Ability.NightVision: hasNightVision = true; break;
            case Ability.Tracker: break;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (abilityCooldown > 0f) abilityCooldown = Math.Max(0f, abilityCooldown - Time.fixedDeltaTime);
        if (AmongUsClient.Instance.AmHost && trackerBodies.Count > 0)
        {
            foreach (var body in trackerBodies.ToArray())
            {
                if (Vector2.Distance(Player.GetTruePosition(), body.Value) > TrackerDistance.GetFloat())
                {
                    RemoveTrackerBody(body.Key, body.Value);
                }
            }
        }
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!info.DoKill || !info.CanKill || info.AppearanceKiller.PlayerId != Player.PlayerId) return;
        CheckVictory();
        if (!abilities.Contains(Ability.Tracker)) return;
        var target = info.AppearanceTarget;
        if (target == null) return;
        var position = target.GetTruePosition();
        if (Vector2.Distance(Player.GetTruePosition(), position) > TrackerDistance.GetFloat()) return;
        AddTrackerBody(target.PlayerId, position);
    }

    private void AddTrackerBody(byte targetId, Vector2 position)
    {
        trackerBodies[targetId] = position;
        GetArrow.Add(Player.PlayerId, position);
        if (AmongUsClient.Instance.AmHost)
        {
            using var sender = CreateSender();
            sender.Writer.WritePacked((int)RpcType.AddBody);
            sender.Writer.Write(targetId);
            NetHelpers.WriteVector2(position, sender.Writer);
            _ = new LateTask(() => RemoveTrackerBody(targetId, position), TrackerDuration.GetFloat(), "DebtorTrackerExpire", true);
        }
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    private void RemoveTrackerBody(byte targetId, Vector2 position)
    {
        if (!trackerBodies.Remove(targetId)) return;
        GetArrow.Remove(Player.PlayerId, position);
        if (AmongUsClient.Instance.AmHost)
        {
            using var sender = CreateSender();
            sender.Writer.WritePacked((int)RpcType.RemoveBody);
            sender.Writer.Write(targetId);
        }
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    private void ClearTrackerBodies()
    {
        GetArrow.RemoveAllTarget(Player.PlayerId);
        trackerBodies.Clear();
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RpcType.ClearBodies);
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => ClearTrackerBodies();
    public override void OnStartMeeting() => ClearTrackerBodies();
    public override void OnDestroy()
    {
        GetArrow.RemoveAllTarget(Player.PlayerId);
        if (accelerationCount > 0) Main.AllPlayerSpeed[Player.PlayerId] -= Acceleration.GetFloat() * accelerationCount;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId || isForMeeting || !Player.IsAlive() || !abilities.Contains(Ability.Tracker)) return "";
        var arrows = trackerBodies.Values.Select(pos => GetArrow.GetArrows(seer, pos)).ToArray();
        return string.Concat(arrows);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => $"<color={RoleInfo.RoleColorCode}>({MyState.GetKillCount(false)}/{RequiredKills.GetInt()} | {abilities.Count}/{RequiredAcquisitions.GetInt()})</color>";

    private void CheckVictory()
    {
        if (hasWon || MyState.GetKillCount(false) < RequiredKills.GetInt() || abilities.Count < RequiredAcquisitions.GetInt()) return;
        if (CountActivations.GetBool() && activationCount < RequiredActivations.GetInt()) return;
        hasWon = true;
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Debtor, Player.PlayerId, true))
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (hasWon) return;
        if (MyState.GetKillCount(false) >= RequiredKills.GetInt() && abilities.Count >= RequiredAcquisitions.GetInt() && (!CountActivations.GetBool() || activationCount >= RequiredActivations.GetInt()))
        {
            hasWon = true;
            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Debtor, Player.PlayerId, true))
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RpcType.State);
        sender.Writer.Write(abilities.Count);
        foreach (var ability in abilities) sender.Writer.Write((byte)ability);
        sender.Writer.Write(abilityCooldown);
        sender.Writer.Write(hasWon);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        switch ((RpcType)reader.ReadPackedInt32())
        {
            case RpcType.State:
                if (accelerationCount > 0 && Main.AllPlayerSpeed.ContainsKey(Player.PlayerId))
                    Main.AllPlayerSpeed[Player.PlayerId] -= Acceleration.GetFloat() * accelerationCount;
                abilities.Clear();
                accelerationCount = 0;
                hasEnhancedVision = false;
                hasLongReach = false;
                hasNightVision = false;
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var ability = (Ability)reader.ReadByte();
                    abilities.Add(ability);
                    if (ability == Ability.Acceleration) accelerationCount++;
                    ApplyAbility(ability);
                }
                abilityCooldown = reader.ReadSingle();
                hasWon = reader.ReadBoolean();
                currentKillCooldown = Math.Min(MaximumKillCooldown.GetFloat(), InitialKillCooldown.GetFloat() + abilities.Count * KillCooldownIncrease.GetFloat());
                break;
            case RpcType.AddBody:
                var id = reader.ReadByte();
                var position = NetHelpers.ReadVector2(reader);
                trackerBodies[id] = position;
                GetArrow.Add(Player.PlayerId, position);
                break;
            case RpcType.RemoveBody:
                var removeId = reader.ReadByte();
                if (trackerBodies.Remove(removeId, out var removePosition)) GetArrow.Remove(Player.PlayerId, removePosition);
                break;
            case RpcType.ClearBodies:
                ClearTrackerBodies();
                break;
        }
    }

    private enum RpcType
    {
        State,
        AddBody,
        RemoveBody,
        ClearBodies
    }
}
