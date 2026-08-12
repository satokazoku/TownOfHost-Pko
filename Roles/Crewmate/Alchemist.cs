/*
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Modules.ChatManager;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.Vanilla;
using UnityEngine;
using UnityEngine.Internal;
using static Il2CppSystem.Threading.SemaphoreSlim;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class Alchemist : RoleBase, ISelfVoter, IUsePhantomButton, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Alchemist),
            player => new Alchemist(player),
            CustomRoles.Alchemist,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            49900,
            SetupOptionItem,
            "alc",
            "#2a2c50",
            (2, 2),
            from: From.UchuAddon
            );
    enum OptionName
    {
        AlchemistCooldown,
        EvolverEatRange,
    }

    public Alchemist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Max = 0;
        staticEatedPlayers.Clear();
        Usedcount = 0;
        MeetingUsedcount = 0;
        OneMeetingMaximum = Option1MeetingMaximum.GetFloat();
        OneDeadbodyAlchemys = OptionOneDeadbodyAlchemys.GetFloat();
        onemeetingkill = 0;
        Viperkilledplayers = new();
        Taskmode = true;
        EatRange = OptionEatRange.GetFloat();
        AlchemistMode = false;
        Cooldown = OptionCooldown.GetFloat();
        nowcool = Cooldown;
        LastCooltime = (int)Cooldown;

        CooldownTimer = 0f;
        pendingEvolve = null;
        EatenBodies.Clear();
    }

    static OptionItem OptionCooldown;
    static float Cooldown;
    Dictionary<byte, float> Viperkilledplayers = new();//とける予定の死体
    static List<byte> staticEatedPlayers = new();//食べられたおにく
    private static OptionItem Option1MeetingMaximum; //一会議の使用回数
    private static OptionItem OptionOneDeadbodyAlchemys;
    static float Max; //錬金弾の最大回数
    int Usedcount; //錬金弾の使用回数
    static float OneMeetingMaximum; //一会議の使用回数
    static float OneDeadbodyAlchemys;　//1死体の錬金数
    int MeetingUsedcount;
    public bool Taskmode;
    float nowcool;
    int LastCooltime;
    static OptionItem OptionEatRange;
    static float EatRange;

    int onemeetingkill;

    bool AlchemistMode;

    private static void SetupOptionItem()
    {
        Option1MeetingMaximum = IntegerOptionItem.Create(RoleInfo, 10, GeneralOption.MeetingMaxTime, new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionOneDeadbodyAlchemys = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.OneDeadbodyAlchemys, new(0, 14, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.AlchemistCooldown, new(0f, 60f, 1f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionEatRange = FloatOptionItem.Create(RoleInfo, 19, OptionName.EvolverEatRange, new(0.5f, 5f, 0.25f), 1.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);

    }

    float CooldownTimer;
    public override void Add()
    {
        PetActionManager.Register(Player.PlayerId, OnPetUsed);
        AlchemistMode = false;
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        float cd = Mathf.Max(nowcool, 0.1f);
        if (!AlchemistMode)
        {
            AURoleOptions.EngineerCooldown = cd;
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
        else
        {
            AURoleOptions.PhantomCooldown = cd;
        }
    }

    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;

    public bool CanUseKillButton() => false;

    private void OnPetUsed()
    {
        if (!Player.IsAlive())
        {
            Logger.Info("Alchemist.OnPetUsed: not alive", "Alchemist");
            return;
        }
        if (!CanChangeMode())
        {
            Logger.Info("Alchemist.OnPetUsed: cannot change mode", "Alchemist");
            return;
        }

        Logger.Info($"Alchemist.OnPetUsed START AmHost={AmongUsClient.Instance.AmHost} nowcool={nowcool} AlchemistMode={AlchemistMode}", "Alchemist");

        AlchemistMode = !AlchemistMode;

        if (AmongUsClient.Instance.AmHost)
        {
            var cd = Mathf.Max(nowcool, 0.005f);
            Player.SetKillCooldown(cd, delay: true);
            Logger.Info($"Alchemist.OnPetUsed: SetKillCooldown immediate cd={cd}", "Alchemist");
        }

        ApplyModeDesync(AlchemistMode);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);

        if (AmongUsClient.Instance.AmHost)
        {
            // RpcResetAbilityCooldown 等で上書きされる可能性があるため遅延再適用
            _ = new LateTask(() =>
            {
                Logger.Info("Alchemist.ReapplyCooldown LATE start", "Alchemist");
                if (!Player.IsAlive())
                {
                    Logger.Info("Alchemist.ReapplyCooldown: player dead", "Alchemist");
                    return;
                }
                var cd = Mathf.Max(nowcool, 0.005f);
                Player.SetKillCooldown(cd, delay: true);
                Logger.Info($"Alchemist.ReapplyCooldown: SetKillCooldown cd={cd}", "Alchemist");

                // RpcResetAbilityCooldown の呼び出しを削除（Sync:false はクライアントを 0 にしてしまう）
                Player.MarkDirtySettings();
                SendRPC();
                Logger.Info("Alchemist.ReapplyCooldown LATE done", "Alchemist");
            }, 0.25f, "Alchemist.ReapplyCooldown", true);
        }
    }

    public bool CanUsePhantomButton()
        => CanUseAlchemistMode()
        && !Taskmode;

    bool CanChangeMode()
        => Player.IsAlive();

    bool CanUseAlchemistMode()
        => Player.IsAlive();


    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!Player.IsAlive() || !AlchemistMode) return;
        /*if (!AmongUsClient.Instance.AmHost)
        {
            ResetCooldown = false;
            return;
        }*/
/*
        if (pendingEvolve != null || !CanEvolveNow())
        {
            ResetCooldown = false;
            return;
        }
        var body = GetNearestEatableBody();
        if (body == null)
        {
            ResetCooldown = false;
            return;
        }

        BeginEvolve(body.ParentId, body.TruePosition);
        Max = Max + OneDeadbodyAlchemys;
        Player.SyncSettings();
        ResetCooldown = true;
    }

    sealed class PendingEvolveInfo
    {
        public byte BodyId;
        public Vector2 BodyPos;
        public float Elapsed;
        public float Required;
        public PendingEvolveInfo(byte id, Vector2 pos, float req)
        { BodyId = id; BodyPos = pos; Required = req; Elapsed = 0f; }
    }
    PendingEvolveInfo pendingEvolve;

    static readonly HashSet<byte> EatenBodies = new();

    [Attributes.GameModuleInitializer]
    public static void Init() => EatenBodies.Clear();

    bool CanEvolveNow()
        => Player.IsAlive()
        && pendingEvolve == null
        && CooldownTimer <= 0f;
    DeadBodyInfo GetNearestEatableBody()
    {
        DeadBodyInfo nearest = null;
        var myPos = Player.GetTruePosition();
        foreach (var db in Object.FindObjectsOfType<DeadBody>())
        {
            var id = db.ParentId;
            if (EatenBodies.Contains(id)) continue;
            var pos = (Vector2)db.TruePosition;
            var dist = Vector2.Distance(myPos, pos);
            if (dist > EatRange) continue;
            if (nearest == null || dist < nearest.Distance)
                nearest = new DeadBodyInfo(id, pos, dist);
        }
        return nearest;
    }
    sealed class DeadBodyInfo
    {
        public byte ParentId; public Vector2 TruePosition; public float Distance;
        public DeadBodyInfo(byte id, Vector2 pos, float d) { ParentId = id; TruePosition = pos; Distance = d; }
    }

    void BeginEvolve(byte bodyId, Vector2 bodyPos)
    {
        pendingEvolve = new PendingEvolveInfo(bodyId, bodyPos, Mathf.Max(0f));
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    bool IsWithinBodyRange()
        => pendingEvolve != null
        && Vector2.Distance(Player.GetTruePosition(), pendingEvolve.BodyPos) <= EatRange;

    bool BodyStillExists(byte bodyId)
        => Object.FindObjectsOfType<DeadBody>().Any(b => b.ParentId == bodyId)
        && !EatenBodies.Contains(bodyId);

    void CancelEvolve(bool syncButton = true)
    {
        if (pendingEvolve == null) return;
        pendingEvolve = null;
        if (syncButton)
            SyncPhantomCooldown();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }
    void SyncPhantomCooldown()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        Player.RpcResetAbilityCooldown(Sync: true);
    }
    void CompleteEvolve()
    {
        var bodyId = pendingEvolve.BodyId;
        pendingEvolve = null;

        CooldownTimer = Cooldown;

        EatenBodies.Add(bodyId);

        SyncPhantomCooldown();

        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }


    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => false;
    public override bool CanClickUseVentButton => false;

    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    bool IUsePhantomButton.IsresetAfterKill => false;

    void ApplyModeDesync(bool toAlchemistMode)
    {
        if (!Player.IsAlive())
        {
            Logger.Info("ApplyModeDesync: player not alive", "Alchemist");
            return;
        }

        Logger.Info($"ApplyModeDesync START toAlchemistMode={toAlchemistMode} nowcool={nowcool}", "Alchemist");

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(
                    toAlchemistMode ? RoleTypes.Scientist : role.GetRoleTypes(),
                    Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(
                    toAlchemistMode ? RoleTypes.Phantom : RoleTypes.Engineer,
                    Player.GetClientId());
        }

        // RpcResetAbilityCooldown を呼ばず、遅延してクールを再適用（Reset による上書きを避ける）
        _ = new LateTask(() =>
        {
            Logger.Info("ApplyModeDesync.LateTask start", "Alchemist");
            if (!Player.IsAlive())
            {
                Logger.Info("ApplyModeDesync.LateTask: player dead", "Alchemist");
                return;
            }

            var cd = Mathf.Max(nowcool, 0.005f);
            Player.SetKillCooldown(cd, delay: true);
            Logger.Info($"ApplyModeDesync.LateTask: SetKillCooldown cd={cd}", "Alchemist");

            Player.MarkDirtySettings();
            SendRPC();
            Logger.Info("ApplyModeDesync.LateTask done", "Alchemist");
        }, 0.50f, "ALC.ModeDesync", true);
    }

    public override void OnSpawn(bool initialState = false)
    {
        if (initialState)
        {
            // ★ ゲーム開始時は EvolveCooldown でタイマーを初期化（即捕食防止）
            CooldownTimer = Cooldown;
            pendingEvolve = null;
            EatenBodies.Clear();

            (this as IUsePhantomButton).Init(Player);
            IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
            Player.RpcResetAbilityCooldown(Sync: true);

            Player.SyncSettings();
        }
        else
        {
            SyncPhantomCooldown();
        }
    }



    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (target == null) return false;
        if (EatenBodies.Contains(target.PlayerId))
        {
            reason = DontReportreson.Alchemy;
            return true;
        }
        return false;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    => CancelEvolve();

    public override void AfterMeetingTasks()
    {
        pendingEvolve = null;
        EatenBodies.Clear();
        if (!Player.IsAlive()) return;

        CooldownTimer = Cooldown;
        SyncPhantomCooldown();
        nowcool = Cooldown;
        LastCooltime = (int)Cooldown;
        ApplyModeDesync(AlchemistMode);
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "DeadBodyEat_Ability";
        return true;
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        // tag 0 = state sync
        sender.Writer.Write((byte)0);
        sender.Writer.Write(Usedcount);
        sender.Writer.Write(Taskmode);
        sender.Writer.Write(nowcool);
        sender.Writer.Write(AlchemistMode);
    }
    private void SendForceCooldown()
    {
        // tag 2 = 強制クール同期
        using var sender = CreateSender();
        sender.Writer.Write((byte)2);
        sender.Writer.Write(nowcool);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        byte tag = reader.ReadByte();
        if (tag == 0)
        {
            Usedcount = reader.ReadInt32();
            Taskmode = reader.ReadBoolean();
            nowcool = reader.ReadSingle();
            AlchemistMode = reader.ReadBoolean();

            // クライアント側で自分の表示を更新する（ホストから送られた nowcool を反映）
            try
            {
                if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == Player.PlayerId)
                {
                    var cd = Mathf.Max(nowcool, 0.005f);

                    // 即時にセット
                    Player.SetKillCooldown(cd, delay: true);
                    Player.MarkDirtySettings();
                    Logger.Info($"Alchemist.ReceiveRPC: ApplyCooldown immediate cd={cd}", "Alchemist");

                    // 競合する Reset を上書きするため、遅延して再適用を行う（複数回）
                    _ = new LateTask(() =>
                    {
                        if (!Player.IsAlive()) return;
                        Player.SetKillCooldown(cd, delay: true);
                        Player.MarkDirtySettings();
                        Logger.Info($"Alchemist.ReceiveRPC: ReapplyCooldown @0.12 cd={cd}", "Alchemist");
                    }, 0.12f, "Alchemist.ReceiveRPC.Reapply1", true);

                    _ = new LateTask(() =>
                    {
                        if (!Player.IsAlive()) return;
                        Player.SetKillCooldown(cd, delay: true);
                        Player.MarkDirtySettings();
                        Logger.Info($"Alchemist.ReceiveRPC: ReapplyCooldown @0.45 cd={cd}", "Alchemist");
                    }, 0.45f, "Alchemist.ReceiveRPC.Reapply2", true);
                    }
            }
            catch
            {
                Logger.Info("Alchemist.ReceiveRPC: failed to apply local cooldown", "Alchemist");
            }

            return;
        }

        // other tags: EatPlayer etc.
        if (tag == 1)
        {
            var targetId = reader.ReadByte();
            staticEatedPlayers.Add(targetId);
            return;
        }

        // 保険: 既存 enum を使って拡張する場合はここに追加
    }
    enum RPC_Types
    {
        EatPlayer,
        AddDiePlayerPos,
        ClearDiePlayerPos,
    }
    public override void OnStartMeeting()
    {
        onemeetingkill = 0;
        MeetingUsedcount = 0;
        if (AlchemistMode) { AlchemistMode = false; SendRPC(); }
    }


    public override string GetProgressText(bool comms = false, bool gamelog = false) => Utils.ColorString(Max <= Usedcount ? Color.gray : Color.cyan, $"({Max - Usedcount})");
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting && Player.IsAlive() && seer.PlayerId == seen.PlayerId && Canuseability() && Max > Usedcount)
        {
            var mes = $"<color={RoleInfo.RoleColorCode}>{GetString("SelfVoteRoleInfoMeg")}</color>";
            return isForHud ? mes : $"<size=40%>{mes}</size>";
        }
        return "";
    }
    bool ISelfVoter.CanUseVoted() => Canuseability() && Max > Usedcount && (MeetingUsedcount < OneMeetingMaximum || OneMeetingMaximum == 0);

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (Max > Usedcount && Is(voter) && (MeetingUsedcount < OneMeetingMaximum || OneMeetingMaximum == 0))
        {
            if (CheckSelfVoteMode(Player, votedForId, out var status))
            {
                if (status is VoteStatus.Self)
                    Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.Alchemist"), GetString("Vote.Alchemist")) + GetString("VoteSkillMode"), Player.PlayerId);
                if (status is VoteStatus.Skip)
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                if (status is VoteStatus.Vote)
                    AlchemistreBullet(votedForId);
                SetMode(Player, status is VoteStatus.Self);
                return false;
            }
        }
        return true;
    }
    public void AlchemistreBullet(byte votedForId)
    {
        PlayerState state;
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (!target.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (target.Is(CustomRoles.Stand))
        {
            var sm = target.GetRoleClass() as TownOfHost.Roles.Neutral.Stand;
            var owner = sm?.GetOwner();
            if (owner != null && owner.Player.IsAlive())
            {
                Utils.SendMessage(
                    "<color=#8B4513>残念だったな！スタンドは撃ち抜けないんだぜ！</color>",
                    Player.PlayerId);
                return;
            }
        }
        var meetingHud = MeetingHud.Instance;
        var hudManager = DestroyableSingleton<HudManager>.Instance.KillOverlay;
        Usedcount++;
        MeetingUsedcount++;//1会議のカウント
        SendRPC();

        //ゲッサーがいるなら～
        if ((PlayerCatch.AllPlayerControls.Any(pc => pc.Is(CustomRoles.Guesser)) || CustomRolesHelper.CheckGuesser()) && !Options.ExHideChatCommand.GetBool())
            ChatManager.SendPreviousMessagesToAll();

        var AlienTairo = false;
        var targetroleclass = target.GetRoleClass();
        if ((targetroleclass as Alien)?.CheckSheriffKill(target) == true) AlienTairo = true;
        if ((targetroleclass as JackalAlien)?.CheckSheriffKill(target) == true) AlienTairo = true;
        if ((targetroleclass as AlienHijack)?.CheckSheriffKill(target) == true) AlienTairo = true;

        if (!AlienTairo)
        {
            state = PlayerState.GetByPlayerId(target.PlayerId);
            target.RpcExileV3();
            state.DeathReason = CustomDeathReason.Kill;
            state.SetDead();

            UtilsGameLog.AddGameLog($"Alchemist", $"{UtilsName.GetPlayerColor(target, true)}(<b>{UtilsRoleText.GetTrueRoleName(target.PlayerId, false)}</b>) [{Utils.GetVitalText(target.PlayerId, true)}]");
            UtilsGameLog.AddGameLogsub($"\n\t⇐ {UtilsName.GetPlayerColor(Player, true)}(<b>{UtilsRoleText.GetTrueRoleName(Player.PlayerId, false)}</b>)");

            if (Options.ExHideChatCommand.GetBool())
            {
                ChatManager.OnDisconnectOrDeadPlayer(target.PlayerId);
            }
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()}がシェリフ成功({target.GetNameWithRole().RemoveHtmlTags()}) 残り{Max - Usedcount}", "Alchemist");
            Utils.SendMessage(UtilsName.GetPlayerColor(target, true) + GetString("Meetingkill"), title: GetString("MSKillTitle"));
            foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null && !pc.IsAlive()))
            {
                Utils.SendMessage(string.Format(GetString("MMeetingKill"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(target, true)), go.PlayerId, GetString("RMSKillTitle"));
            }

            MeetingVoteManager.ResetVoteManager(target.PlayerId);
            if (target != PlayerControl.LocalPlayer) Player.RpcMeetingKill(target);
            onemeetingkill++;
            return;
        }
        Player.RpcExileV3();
        MyState.DeathReason = target.Is(CustomRoles.Tairou) && Tairou.TairoDeathReason ? CustomDeathReason.Counter :
                            target.Is(CustomRoles.Alien) && Alien.TairoDeathReason ? CustomDeathReason.Counter :
                            (target.Is(CustomRoles.JackalAlien) && JackalAlien.TairoDeathReason ? CustomDeathReason.Counter :
                            (target.Is(CustomRoles.AlienHijack) && Alien.TairoDeathReason ? CustomDeathReason.Counter : CustomDeathReason.Misfire));
        MyState.SetDead();

        if (Options.ExHideChatCommand.GetBool())
        {
            ChatManager.OnDisconnectOrDeadPlayer(Player.PlayerId);
        }
        Utils.SendMessage(UtilsName.GetPlayerColor(Player, true) + GetString("Meetingkill"), title: GetString("MSKillTitle"));
        foreach (var go in PlayerCatch.AllPlayerControls.Where(pc => pc != null && !pc.IsAlive()))
        {
            Utils.SendMessage(string.Format(GetString("MMeetingKillfall"), UtilsName.GetPlayerColor(Player, true), UtilsName.GetPlayerColor(target, true)), go.PlayerId, GetString("RMSKillTitle"));
        }

        MeetingVoteManager.ResetVoteManager(Player.PlayerId);
        if (Player != PlayerControl.LocalPlayer) Player.RpcMeetingKill(Player);
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled == null || exiled?.Object == null)
        {
            return;
        }
        if (exiled.Object.GetCustomRole().IsCrewmate() is false) onemeetingkill++;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        // CooldownTimer は常時減らす
        if (CooldownTimer > 0f)
            CooldownTimer = Mathf.Max(0f, CooldownTimer - Time.fixedDeltaTime);

        // pendingEvolve がある場合はその処理（存在確認・進行）を行う
        if (pendingEvolve != null)
        {
            if (!Player.IsAlive()) { CancelEvolve(); return; }
            if (!BodyStillExists(pendingEvolve.BodyId)) { CancelEvolve(); return; }
            if (!IsWithinBodyRange()) { CancelEvolve(); return; }

            pendingEvolve.Elapsed += Time.fixedDeltaTime;
            if (pendingEvolve.Elapsed >= pendingEvolve.Required)
                CompleteEvolve();
        }

        if (Player.IsAlive() && GameStates.IsInTask)
        {

            var now = (int)nowcool;
            if (now != LastCooltime)
            {
                LastCooltime = now;
                Player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
                }, 0.1f, "SHH.CDSync", true);
                if (player != PlayerControl.LocalPlayer)
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
            }
        }
    }

    public void RpcEatPlayer(byte targetId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.EatPlayer);
        sender.Writer.Write(targetId);
    }
}
*/