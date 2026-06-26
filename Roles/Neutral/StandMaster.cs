using System.Collections.Generic;
using System.Linq;

using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using HarmonyLib;

namespace TownOfHost.Roles.Neutral;

public sealed class StandMaster : RoleBase, ILNKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(StandMaster),
            player => new StandMaster(player),
            CustomRoles.StandMaster,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            54600,
            SetupOptionItem,
            "stm",
            "#8B4513",
            (6, 4),
            true,
            countType: CountTypes.Crew,
            assignInfo: new RoleAssignInfo(CustomRoles.StandMaster, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            from: From.TownOfHost_Pko
        );

    public StandMaster(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        SummonCooldown = OptionSummonCooldown.GetFloat();
        StandStayTime = OptionStandStayTime.GetFloat();
        standPlayerId = byte.MaxValue;
        standReturnPos = null;
        standSummoned = false;
        standCreated = false;
        standHadDied = false;
        isRevealed = false;
        currentStayTimer = 0f;
    }

    static OptionItem OptionSummonCooldown;
    static OptionItem OptionStandStayTime;
    static OptionItem OptionKillCooldown;
    static OptionItem OptionEnableKillAbility;
    static OptionItem OptionStandImpostorVision;
    static OptionItem OptionEnableTaskAddon;
    static OptionItem OptionAddonGiveToMaster;
    static OptionItem OptionAddonAllowDebuff;
    static OptionItem OptionStandDeathGrantsKill;

    public static float SummonCooldown;
    public static float StandStayTime;
    public static float KillCooldown_;
    public static bool EnableKillAbility;
    public static bool StandImpostorVision;
    public static bool EnableTaskAddon;
    public static bool AddonGiveToMaster;
    public static bool AddonAllowDebuff;
    public static bool StandDeathGrantsKill;

    enum OptionName
    {
        StandMasterSummonCooldown,
        StandMasterStayTime,
        StandMasterKillCooldown,
        StandMasterEnableKillAbility,
        StandMasterStandDeathGrantsKill,
        StandImpostorVision,
        StandEnableTaskAddon,
        StandAddonGiveToMaster,
        StandAddonAllowDebuff,
    }

    public static readonly CustomRoles[] BuffAddons =
    [
        CustomRoles.Autopsy,
        CustomRoles.Lighting,
        CustomRoles.Moon,
        CustomRoles.Guesser,
        CustomRoles.Tiebreaker,
        CustomRoles.Opener,
        CustomRoles.Management,
        CustomRoles.Speeding,
        CustomRoles.MagicHand,
        CustomRoles.Serial,
        CustomRoles.Powerful,
        CustomRoles.PlusVote,
        CustomRoles.Seeing,
        CustomRoles.Sunglasses,
        CustomRoles.Elector,
        CustomRoles.Water,
    ];

    public static readonly CustomRoles[] DebuffAddons =
    [
        CustomRoles.Amnesia,
        CustomRoles.Workhorse,
        CustomRoles.LastImpostor,
        CustomRoles.LastNeutral,
        CustomRoles.Stack,
        CustomRoles.Jumbo,
        CustomRoles.Stamina
    ];

    public byte standPlayerId;
    public Vector2? standReturnPos;
    public bool standSummoned;
    public bool standCreated;
    public bool standHadDied;
    public bool isRevealed;
    public float currentStayTimer;

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionSummonCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.StandMasterSummonCooldown,
            new(2.5f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionStandStayTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.StandMasterStayTime,
            new(2.5f, 60f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);

        OptionEnableKillAbility = BooleanOptionItem.Create(
            RoleInfo, 12, OptionName.StandMasterEnableKillAbility, false, false);
        OptionStandDeathGrantsKill = BooleanOptionItem.Create(
            RoleInfo, 13, OptionName.StandMasterStandDeathGrantsKill, false, false);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 18, OptionName.StandMasterKillCooldown,
            new(0f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);

        OptionStandImpostorVision = BooleanOptionItem.Create(
            RoleInfo, 14, OptionName.StandImpostorVision, false, false);
        OptionEnableTaskAddon = BooleanOptionItem.Create(
            RoleInfo, 15, OptionName.StandEnableTaskAddon, false, false);
        OptionAddonGiveToMaster = BooleanOptionItem.Create(
            RoleInfo, 16, OptionName.StandAddonGiveToMaster, false, false, OptionEnableTaskAddon);
        OptionAddonAllowDebuff = BooleanOptionItem.Create(
            RoleInfo, 17, OptionName.StandAddonAllowDebuff, false, false, OptionEnableTaskAddon);

        HideRoleOptions(CustomRoles.Stand);
    }

    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null && Options.CustomRoleSpawnChances.TryGetValue(role, out var spawnOption))
            spawnOption.SetHidden(true);
        if (Options.CustomRoleCounts != null && Options.CustomRoleCounts.TryGetValue(role, out var countOption))
            countOption.SetHidden(true);
    }

    public override void Add()
    {
        EnableKillAbility = OptionEnableKillAbility.GetBool();
        StandDeathGrantsKill = OptionStandDeathGrantsKill.GetBool();
        KillCooldown_ = OptionKillCooldown.GetFloat();   // ★
        StandImpostorVision = OptionStandImpostorVision.GetBool();
        EnableTaskAddon = OptionEnableTaskAddon.GetBool();
        AddonGiveToMaster = OptionAddonGiveToMaster.GetBool();
        AddonAllowDebuff = OptionAddonAllowDebuff.GetBool();
    }

    public PlayerControl GetStand() =>
        standPlayerId == byte.MaxValue ? null : PlayerCatch.GetPlayerById(standPlayerId);

    public bool IsPhantomRole => true;
    public bool IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = SummonCooldown;
    }

    public void SyncState() { SendRpc(); UtilsNotifyRoles.NotifyRoles(); }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (standPlayerId != byte.MaxValue && seen.PlayerId == standPlayerId) enabled = true;
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (standPlayerId != byte.MaxValue && seer.PlayerId == standPlayerId) enabled = true;
    }

    public override string MeetingAddMessage()
    {
        if (Player.IsAlive() && standCreated && isRevealed)
        {
            var stand = GetStand();
            if (stand != null && stand.IsAlive())
            {
                string title = "<size=90%><color=#8B4513>【====== スタンド情報 ======】</color></size>";
                string msg = $"<color=#8B4513>{UtilsName.GetPlayerColor(stand, true)} はスタンドです。\nスタンドマスターが生きている限り死亡しません。</color>";
                return title + "\n<size=70%>" + msg + "</size>\n";
            }
        }
        return "";
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        (var killer, _) = info.AttemptTuple;
        if (killer.GetRoleClass() is Stand stand && stand.OwnerId == Player.PlayerId)
            return false;
        return true;
    }

    public bool CanUseKillButton()
    {
        if (!Player.IsAlive()) return false;
        if (EnableKillAbility) return true;
        if (!standCreated && !standHadDied) return true;
        if (standHadDied) return StandDeathGrantsKill;
        return false;
    }

    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    public float CalculateKillCooldown()
    {
        if (!standCreated && !standHadDied) return SummonCooldown;
        return KillCooldown_;
    }

    public bool OverrideKillButtonText(out string text)
    {
        bool canCreate = !standCreated && !standHadDied;
        text = canCreate ? "スタンド化" : "キル";
        return true;
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (!standCreated && !standHadDied)
        {
            info.DoKill = false;
            CreateStand(target);
            _ = new LateTask(() => { if (Player.IsAlive()) Player.RpcResetAbilityCooldown(); },
                0.1f, "StandMaster.ResetPhantomCD", true);
            return;
        }

        if (EnableKillAbility || (standHadDied && StandDeathGrantsKill))
        {
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            return;
        }

        info.DoKill = false;
    }

    private void CreateStand(PlayerControl target)
    {
        if (standCreated) return;
        standCreated = true;
        standHadDied = false;
        standPlayerId = target.PlayerId;
        standSummoned = false;
        standReturnPos = null;
        isRevealed = false;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(CustomRoles.Stand, log: null);
        target.RpcSetRole(RoleTypes.Crewmate);

        _ = new LateTask(() =>
        {
            if (target.GetRoleClass() is Stand stand) stand.SetOwner(Player.PlayerId);
        }, 0.2f, "StandMaster.SetOwner", true);

        target.MarkDirtySettings();
        SyncState();
        UtilsGameLog.AddGameLog("StandMaster",
            $"{UtilsName.GetPlayerColor(Player)} が {UtilsName.GetPlayerColor(target)} をスタンドにした");
    }

    private void OnStandSpecialDeath()
    {
        standCreated = false;
        standHadDied = true;
        standPlayerId = byte.MaxValue;
        isRevealed = false;
        SendRpc();
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!Player.IsAlive() || !standCreated) return;
        var stand = GetStand();
        if (stand == null || !stand.IsAlive() || standSummoned) return;

        standReturnPos = stand.transform.position;
        standSummoned = true;
        currentStayTimer = StandStayTime;

        stand.RpcSetRoleDesync(RoleTypes.Phantom, stand.GetClientId());
        foreach (var imp in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.GetCustomRole().IsImpostor()))
            imp.RpcSetRoleDesync(RoleTypes.Scientist, stand.GetClientId());

        stand.MarkDirtySettings();
        stand.RpcSnapToForced(Player.transform.position);
        try { stand.SetKillTimer(0f); } catch { }

        SyncState();
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = StandStayTime;
            Player.RpcResetAbilityCooldown();
            if (stand != null && stand.IsAlive()) stand.RpcResetAbilityCooldown();
        }, 0.1f, "StandMaster.ResetCD", true);
    }

    public void ReturnStand()
    {
        var stand = GetStand();
        if (stand == null) return;

        if (standReturnPos.HasValue) stand.RpcSnapToForced(standReturnPos.Value);

        stand.RpcSetRoleDesync(RoleTypes.Crewmate, stand.GetClientId());
        foreach (var imp in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.GetCustomRole().IsImpostor()))
            imp.RpcSetRoleDesync(RoleTypes.Impostor, stand.GetClientId());

        stand.MarkDirtySettings();
        standSummoned = false;
        standReturnPos = null;
        SyncState();

        _ = new LateTask(() =>
        {
            AURoleOptions.PhantomCooldown = SummonCooldown;
            if (Player.IsAlive()) Player.RpcResetAbilityCooldown();
        }, 0.1f, "StandMaster.ReturnCD", true);
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!standSummoned) return;
        ReturnStand();
    }

    bool skipSwapForThisMeeting;
    public override void OnStartMeeting()
    {
        skipSwapForThisMeeting = SatsumatoImo.IsSpecialMeetingNoSwap();
        if (!skipSwapForThisMeeting) standSummoned = false;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (skipSwapForThisMeeting) { skipSwapForThisMeeting = false; return; }
        skipSwapForThisMeeting = false;
        if (!standCreated) return;

        var stand = GetStand();

        if ((stand == null || !stand.IsAlive()) && Player.IsAlive())
        {
            OnStandSpecialDeath();
            return;
        }

        if (!Player.IsAlive())
        {
            standCreated = false;
            standPlayerId = byte.MaxValue;
            isRevealed = false;
            SendRpc();
            return;
        }

        stand.RpcSetRoleDesync(RoleTypes.Crewmate, stand.GetClientId());
        foreach (var imp in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.GetCustomRole().IsImpostor()))
            imp.RpcSetRoleDesync(RoleTypes.Impostor, stand.GetClientId());

        stand.MarkDirtySettings();
        standSummoned = false;
        SendRpc();

        AURoleOptions.PhantomCooldown = SummonCooldown;
        _ = new LateTask(() => { if (Player.IsAlive()) Player.RpcResetAbilityCooldown(); },
            0.1f, "StandMaster.AfterMeetingCD", true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player != Player || !Player.IsAlive()) return;

        if (standCreated && !GameStates.IsMeeting)
        {
            var stand = GetStand();
            if (stand != null && !stand.IsAlive())
            {
                OnStandSpecialDeath();
                return;
            }
        }

        if (standSummoned && currentStayTimer > 0f)
        {
            currentStayTimer -= Time.fixedDeltaTime;
            if (currentStayTimer <= 0f) ReturnStand();
        }
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(standPlayerId);
        sender.Writer.Write(standCreated);
        sender.Writer.Write(standHadDied);
        sender.Writer.Write(standSummoned);
        sender.Writer.Write(isRevealed);
        sender.Writer.Write(standReturnPos.HasValue);
        if (standReturnPos.HasValue)
        {
            sender.Writer.Write(standReturnPos.Value.x);
            sender.Writer.Write(standReturnPos.Value.y);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        standPlayerId = reader.ReadByte();
        standCreated = reader.ReadBoolean();
        standHadDied = reader.ReadBoolean();
        standSummoned = reader.ReadBoolean();
        isRevealed = reader.ReadBoolean();
        bool hasPos = reader.ReadBoolean();
        standReturnPos = hasPos ? new Vector2(reader.ReadSingle(), reader.ReadSingle()) : null;
    }
}

public sealed class Stand : RoleBase, ILNKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Stand),
            player => new Stand(player),
            CustomRoles.Stand,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            184700,
            SetupOptionItem,
            "st",
            "#8B4513",
            (6, 4),
            countType: CountTypes.OutOfGame,
            from: From.TownOfHost_Pko
        );

    public Stand(PlayerControl player) : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        OwnerId = byte.MaxValue;
        isFollowingDeath = false;
    }

    static void SetupOptionItem() => StandMaster.HideRoleOptions(CustomRoles.Stand);

    public byte OwnerId;
    bool isFollowingDeath;

    public void SetOwner(byte ownerId) { OwnerId = ownerId; SendRPC(); }

    public StandMaster GetOwner()
    {
        if (OwnerId == byte.MaxValue) return null;
        return PlayerCatch.GetPlayerById(OwnerId)?.GetRoleClass() as StandMaster;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = StandMaster.StandStayTime;
        var sm = GetOwner();
        if (StandMaster.StandImpostorVision && sm != null && sm.standSummoned)
            opt.SetVision(true);
    }

    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (OwnerId != byte.MaxValue && seen.PlayerId == OwnerId) enabled = true;
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer,
        ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        addon = false;
        if (OwnerId != byte.MaxValue && seer.PlayerId == OwnerId) enabled = true;
    }

    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        var sm = GetOwner();
        if (sm == null || !sm.Player.IsAlive()) return true;

        (var killer, var target) = info.AttemptTuple;
        if (killer.PlayerId == target.PlayerId) return true;

        killer.RpcProtectedMurderPlayer(target);
        info.GuardPower = 1;

        if (!sm.isRevealed) { sm.isRevealed = true; sm.SyncState(); }
        return true;
    }

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie,
        Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (Exiled == null || Exiled.PlayerId != Player.PlayerId || !Player.IsAlive()) return false;

        var sm = GetOwner();
        if (sm == null || !sm.Player.IsAlive()) return false;

        Exiled = null;
        IsTie = false;
        if (!sm.isRevealed) { sm.isRevealed = true; sm.SyncState(); }
        return true;
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!StandMaster.EnableTaskAddon || !Player.IsAlive()) return true;

        var pool = new List<CustomRoles>(StandMaster.BuffAddons);
        if (StandMaster.AddonAllowDebuff) pool.AddRange(StandMaster.DebuffAddons);
        if (pool.Count == 0) return true;

        var addon = pool[IRandom.Instance.Next(pool.Count)];
        Player.RpcSetCustomRole(addon);
        Logger.Info($"[Stand] {Player.GetNameWithRole()} にアドオン {addon} 付与", "Stand");

        if (StandMaster.AddonGiveToMaster)
        {
            var sm = GetOwner();
            if (sm != null && sm.Player.IsAlive())
            {
                var masterAddon = pool[IRandom.Instance.Next(pool.Count)];
                sm.Player.RpcSetCustomRole(masterAddon);
                Logger.Info($"[Stand] マスター {sm.Player.GetNameWithRole()} にもアドオン {masterAddon} 付与", "Stand");
            }
        }

        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Stand.AddonNotify");
        return true;
    }

    public float CalculateKillCooldown()
    {
        var sm = GetOwner();
        return sm != null && sm.standSummoned ? sm.currentStayTimer : 999f;
    }

    public bool CanUseKillButton()
    {
        var sm = GetOwner();
        return sm != null && sm.standSummoned;
    }

    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player != Player || !Player.IsAlive()) return;
        if (!GameStates.IsInTask) return;
        if (!isFollowingDeath && ShouldFollowOwnerDeath()) FollowOwnerDeath();
    }

    bool ShouldFollowOwnerDeath()
    {
        if (OwnerId == byte.MaxValue) return false;
        var owner = PlayerCatch.GetPlayerById(OwnerId);
        return owner == null || !owner.IsAlive() || owner.GetRoleClass() is not StandMaster;
    }

    void FollowOwnerDeath()
    {
        if (!Player.IsAlive() || isFollowingDeath) return;
        isFollowingDeath = true;
        var state = PlayerState.GetByPlayerId(Player.PlayerId);
        if (state != null) state.DeathReason = CustomDeathReason.FollowingSuicide;
        var owner = OwnerId == byte.MaxValue ? null : PlayerCatch.GetPlayerById(OwnerId);
        Player.SetRealKiller(owner ?? Player);
        Player.RpcMurderPlayerV2(Player);
    }

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(OwnerId);
    }

    public override void ReceiveRPC(MessageReader reader) => OwnerId = reader.ReadByte();
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class StandMasterMurderPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost || __instance == null || target == null) return true;
        if (__instance.PlayerId == target.PlayerId) return true;

        if (__instance.GetRoleClass() is Stand standPlayer)
        {
            var sm = standPlayer.GetOwner();
            if (sm?.Player != null)
            {
                if (target.PlayerId == sm.Player.PlayerId) return false;
                if (sm.standSummoned)
                {
                    _ = new LateTask(() =>
                    {
                        sm.ReturnStand();
                        if (sm.Player?.IsAlive() == true) sm.Player.RpcResetAbilityCooldown();
                    }, 0.3f, "StandMaster.ReturnAfterKill", true);
                }
            }
        }
        return true;
    }
}