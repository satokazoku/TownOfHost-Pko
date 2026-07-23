using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Jailer : RoleBase, IUsePhantomButton, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jailer),
            player => new Jailer(player),
            CustomRoles.Jailer,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            260300,
            SetupOptionItem,
            "jlr",
            "#4488cc",
            (3, 5),
            true,
            from: From.TownOfHost_Pko
        );

    public Jailer(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.True)
    {
        mode = JailerMode.Task;
        prisonLocationSet = false;
        prisonLocation = Vector2.zero;
        prisonerPlayerId = byte.MaxValue;
        hasPrisoner = false;
        imprisonSecondsLeft = 0f;
        imprisonTurnsLeft = 0;
        prisonObject = null;
        nowcool = OptionKillCooldown.GetFloat();
        LastCooltime = (int)nowcool;
        spawnCooldownStarted = false;
        remainAbilityCount = OptionAbilityCount.GetInt();
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionImprisonType;
    static OptionItem OptionImprisonTurns;
    static OptionItem OptionImprisonSeconds;
    static OptionItem OptionContinueAfterMeeting;
    static OptionItem OptionAbilityTask;
    static OptionItem OptionAbilityCount;

    enum JailerMode { Task, SetLocation, SelectPrisoner }
    enum ImprisonmentType { Turns, Seconds }

    enum OptionName
    {
        JailerImprisonType,
        JailerImprisonTurns,
        JailerImprisonSeconds,
        JailerContinueAfterMeeting,
        JailerAbilityTask,
        JailerAbilityCount,
    }

    JailerMode mode;
    bool prisonLocationSet;
    Vector2 prisonLocation;
    byte prisonerPlayerId;
    bool hasPrisoner;
    float imprisonSecondsLeft;
    int imprisonTurnsLeft;
    PrisonNetObject prisonObject;
    float nowcool;
    int LastCooltime;
    bool spawnCooldownStarted;
    int remainAbilityCount;

    public bool CanKill { get; private set; } = false;
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public bool CanUseKillButton() => mode == JailerMode.SelectPrisoner && prisonLocationSet && !hasPrisoner && Player.IsAlive();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    static float KillCooldown => OptionKillCooldown.GetFloat();
    static ImprisonmentType ImpType => (ImprisonmentType)OptionImprisonType.GetValue();
    static int ImprisonTurns => OptionImprisonTurns.GetInt();
    static float ImprisonSeconds => OptionImprisonSeconds.GetFloat();
    static bool ContinueThroughMeeting => OptionContinueAfterMeeting.GetBool();
    static int AbilityTask => OptionAbilityTask.GetInt();
    static int AbilityCount => OptionAbilityCount.GetInt();

    bool IsAbilityUnlocked
        => AbilityTask <= 0
           || (MyTaskState != null && MyTaskState.CompletedTasksCount >= AbilityTask);

    bool HasAbilityUse
        => AbilityCount == 0 || remainAbilityCount > 0;

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
                new(2.5f, 60f, 2.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);

        OptionImprisonType = StringOptionItem.Create(RoleInfo, 11, OptionName.JailerImprisonType,
            new string[] { "Turns", "Seconds" }, 0, false);

        OptionImprisonTurns = IntegerOptionItem.Create(RoleInfo, 12, OptionName.JailerImprisonTurns,
                new(1, 10, 1), 2, false)
            .SetValueFormat(OptionFormat.Times)
            .SetParent(OptionImprisonType);

        OptionImprisonSeconds = FloatOptionItem.Create(RoleInfo, 13, OptionName.JailerImprisonSeconds,
                new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(OptionImprisonType);

        OptionContinueAfterMeeting = BooleanOptionItem.Create(RoleInfo, 14,
                OptionName.JailerContinueAfterMeeting, false, false)
            .SetParent(OptionImprisonSeconds);

        OptionAbilityTask = IntegerOptionItem.Create(RoleInfo, 15, OptionName.JailerAbilityTask,
                new(0, 20, 1), 0, false)
            .SetValueFormat(OptionFormat.Pieces);

        OptionAbilityCount = IntegerOptionItem.Create(RoleInfo, 16, OptionName.JailerAbilityCount,
                new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times)
            .SetZeroNotation(OptionZeroNotation.Infinity);
    }

    public override void Add()
    {
        nowcool = KillCooldown;
        LastCooltime = (int)nowcool;
        spawnCooldownStarted = false;
        remainAbilityCount = AbilityCount;
        PetActionManager.Register(Player.PlayerId, OnPetAction);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        prisonObject?.Despawn();
        prisonObject = null;
    }

    bool IUsePhantomButton.IsPhantomRole => mode == JailerMode.SetLocation;
    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (mode != JailerMode.SetLocation || hasPrisoner) return;

        prisonLocation = (Vector2)Player.transform.position;
        prisonLocationSet = true;

        prisonObject?.Despawn();
        prisonObject = new PrisonNetObject(prisonLocation, "?");

        SwitchMode(JailerMode.SelectPrisoner);
        SendRpc();
        Utils.SendMessage(GetString("JailerLocationSet"), Player.PlayerId);
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        switch (mode)
        {
            case JailerMode.Task:
                AURoleOptions.EngineerCooldown = Mathf.Max(nowcool, 0.1f);
                AURoleOptions.EngineerInVentMaxTime = 0f;
                break;
            case JailerMode.SetLocation:
                AURoleOptions.PhantomCooldown = 0.1f;
                break;
        }
    }

    public override bool CanClickUseVentButton => mode == JailerMode.Task;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
        => mode == JailerMode.Task && !hasPrisoner;
    public override RoleTypes? AfterMeetingRole
        => mode == JailerMode.SetLocation ? RoleTypes.Phantom : RoleTypes.Engineer;

    void OnPetAction()
    {
        if (!Player.IsAlive()) return;
        if (hasPrisoner) return;

        if (mode == JailerMode.Task)
        {
            if (nowcool > 0f) return;

            if (!IsAbilityUnlocked)
            {
                int need = AbilityTask;
                int done = MyTaskState?.CompletedTasksCount ?? 0;
                Utils.SendMessage(
                    string.Format(GetString("JailerAbilityLocked"), done, need),
                    Player.PlayerId);
                return;
            }
            if (!HasAbilityUse)
            {
                Utils.SendMessage(GetString("JailerAbilityExhausted"), Player.PlayerId);
                return;
            }

            SwitchMode(JailerMode.SetLocation);
        }
        else
        {
            prisonLocationSet = false;
            prisonObject?.Despawn();
            prisonObject = null;
            SwitchMode(JailerMode.Task);
        }

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    void SwitchMode(JailerMode newMode)
    {
        mode = newMode;
        ApplyModeDesync(newMode);
    }

    void ApplyModeDesync(JailerMode newMode)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        RoleTypes self = newMode switch
        {
            JailerMode.Task => RoleTypes.Engineer,
            JailerMode.SetLocation => RoleTypes.Phantom,
            JailerMode.SelectPrisoner => RoleTypes.Impostor,
            _ => RoleTypes.Engineer
        };
        Player.RpcSetRoleDesync(self, Player.GetClientId());

        if (newMode == JailerMode.SelectPrisoner)
        {
            foreach (var pc in AllAlivePlayerControls)
            {
                if (pc.GetCustomRole().IsImpostor())
                    pc.RpcSetRoleDesync(RoleTypes.Scientist, Player.GetClientId());
            }
        }

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            if (newMode != JailerMode.SelectPrisoner)
                Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "Jailer.Desync", true);
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (mode != JailerMode.SelectPrisoner) return;
        if (!prisonLocationSet || hasPrisoner) return;
        if (AbilityCount > 0 && remainAbilityCount <= 0) return;

        (_, var target) = info.AttemptTuple;
        info.DoKill = false;

        prisonerPlayerId = target.PlayerId;
        hasPrisoner = true;
        imprisonSecondsLeft = ImprisonSeconds;
        imprisonTurnsLeft = ImprisonTurns;
        if (AbilityCount > 0) remainAbilityCount--;

        target.RpcSnapToForced(prisonLocation);
        prisonObject?.UpdateName(target.Data.PlayerName);

        SwitchMode(JailerMode.Task);

        nowcool = KillCooldown;
        LastCooltime = (int)nowcool;

        Utils.SendMessage(
            string.Format(GetString("JailerImprisoned"), target.Data.PlayerName),
            Player.PlayerId);
        Utils.SendMessage(GetString("JailerYouImprisoned"), target.PlayerId);

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!Player.IsAlive())
        {
            FreePrisoner();
            return;
        }

        if (!spawnCooldownStarted && Player.IsAlive()
            && !GameStates.Intro && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            spawnCooldownStarted = true;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        if (hasPrisoner && GameStates.IsInTask)
        {
            var prisoner = GetPlayerById(prisonerPlayerId);
            if (prisoner == null || !prisoner.IsAlive()) { FreePrisoner(); return; }

            if (prisoner.inVent)
            {
                _ = new LateTask(() =>
                {
                    if (prisoner != null && prisoner.IsAlive())
                        prisoner.RpcSnapToForced(prisonLocation);
                }, 0.1f, "Jailer.VentEscape", true);
            }
            else if (Vector2.Distance(prisoner.GetTruePosition(), prisonLocation) > 0.25f)
                prisoner.RpcSnapToForced(prisonLocation);
        }

        if (hasPrisoner && ImpType == ImprisonmentType.Seconds && GameStates.IsInTask)
        {
            if (!GameStates.IsMeeting || ContinueThroughMeeting)
            {
                imprisonSecondsLeft -= Time.fixedDeltaTime;
                if (imprisonSecondsLeft <= 0f) { FreePrisoner(); return; }
            }
        }

        if (mode == JailerMode.Task && !hasPrisoner && Player.IsAlive() && GameStates.IsInTask)
        {
            if (nowcool > 0) nowcool -= Time.fixedDeltaTime;
            else nowcool = 0;

            int now = (int)nowcool;
            if (now != LastCooltime)
            {
                LastCooltime = now;
                Player.MarkDirtySettings();
                _ = new LateTask(() =>
                {
                    if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
                }, 0.1f, "Jailer.CDSync", true);
                if (player != PlayerControl.LocalPlayer)
                    UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
            }
        }
    }

    void FreePrisoner()
    {
        if (!hasPrisoner) return;

        var prisoner = GetPlayerById(prisonerPlayerId);
        if (prisoner != null && prisoner.IsAlive())
            Utils.SendMessage(GetString("JailerFreed"), prisoner.PlayerId);

        prisonObject?.Despawn();
        prisonObject = null;
        hasPrisoner = false;
        prisonerPlayerId = byte.MaxValue;
        prisonLocationSet = false;
        imprisonSecondsLeft = 0f;
        imprisonTurnsLeft = 0;
        nowcool = 0f;
        LastCooltime = 0;
        Player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "Jailer.FreeReset", true);

        Utils.SendMessage(GetString("JailerPrisonerFreed"), Player.PlayerId);
        SendRpc();
        UtilsNotifyRoles.NotifyRoles();
    }

    public override void OnStartMeeting()
    {
        if (!hasPrisoner || ImpType != ImprisonmentType.Turns) return;
        imprisonTurnsLeft--;
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        if (hasPrisoner)
        {
            if (ImpType == ImprisonmentType.Turns && imprisonTurnsLeft <= 0)
            {
                FreePrisoner(); return;
            }

            _ = new LateTask(() =>
            {
                var prisoner = GetPlayerById(prisonerPlayerId);
                if (prisoner != null && prisoner.IsAlive())
                    prisoner.RpcSnapToForced(prisonLocation);
            }, 1.0f, "Jailer.PostMeetingSnap", true);
        }

        _ = new LateTask(() => ApplyModeDesync(mode), 0.5f, "Jailer.AfterMeetDesync", true);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";

        if (hasPrisoner)
        {
            var prisoner = GetPlayerById(prisonerPlayerId);
            string pn = prisoner?.Data?.PlayerName ?? "???";
            string t = ImpType == ImprisonmentType.Seconds
                ? $"{Mathf.CeilToInt(imprisonSecondsLeft)}s"
                : $"{imprisonTurnsLeft}T";
            return $"<color={RoleInfo.RoleColorCode}>[JAIL] {pn}({t})</color>";
        }

        if (!IsAbilityUnlocked)
        {
            int done = MyTaskState?.CompletedTasksCount ?? 0;
            return $"<color=#aaaaaa>({done}/{AbilityTask})</color>";
        }
        if (!HasAbilityUse)
            return $"<color=#888888>(使用済)</color>";

        string modeStr = mode switch
        {
            JailerMode.Task => "<color=#f8cd46>[タスク]</color>",
            JailerMode.SetLocation => "<color=#ff8800>[位置設定]</color>",
            JailerMode.SelectPrisoner => "<color=#ff4444>[拘禁選択]</color>",
            _ => "?"
        };
        string countStr = AbilityCount > 0 ? $"/{remainAbilityCount}" : "";
        return $"<color={RoleInfo.RoleColorCode}>({LastCooltime}{countStr})</color>{modeStr}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        string sz = isForHud ? "" : "<size=60%>";
        string c = RoleInfo.RoleColorCode;

        if (hasPrisoner)
        {
            var prisoner = GetPlayerById(prisonerPlayerId);
            string pn = prisoner?.Data?.PlayerName ?? "???";
            string t = ImpType == ImprisonmentType.Seconds
                ? $"{Mathf.CeilToInt(imprisonSecondsLeft)}秒"
                : $"残り{imprisonTurnsLeft}ターン";
            return $"{sz}<color={c}>[JAIL] {pn} を拘禁中 ({t})</color>";
        }

        if (!IsAbilityUnlocked)
        {
            int done = MyTaskState?.CompletedTasksCount ?? 0;
            return $"{sz}<color=#aaaaaa>能力解放まで: タスク {done}/{AbilityTask}</color>";
        }
        if (!HasAbilityUse)
            return $"{sz}<color=#888888>能力使用回数が切れました</color>";

        return mode switch
        {
            JailerMode.Task =>
                $"{sz}<color={c}>ペット → 能力モードへ" +
                (nowcool > 0 ? $" (CD: {LastCooltime}s)" : " <color=#00ff88>(準備完了)</color>") +
                "</color>",
            JailerMode.SetLocation =>
                $"{sz}<color=#ff8800>ファントムボタン → 牢屋の位置を設定\nペット → キャンセル</color>",
            JailerMode.SelectPrisoner =>
                $"{sz}<color=#ff4444>キルボタン → 閉じ込める相手を選択\nペット → キャンセル</color>",
            _ => ""
        };
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null,
    bool isForMeeting = false)
    {
        seen ??= seer;
        if (!hasPrisoner) return "";
        if (seen.PlayerId != prisonerPlayerId || seer.PlayerId != prisonerPlayerId) return "";
        return "<color=#4488cc>[JAIL] 拘禁中</color>";
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)mode);
        sender.Writer.Write(hasPrisoner);
        sender.Writer.Write(prisonerPlayerId);
        sender.Writer.Write(prisonLocationSet);
        sender.Writer.Write(prisonLocation.x);
        sender.Writer.Write(prisonLocation.y);
        sender.Writer.Write(imprisonSecondsLeft);
        sender.Writer.Write(imprisonTurnsLeft);
        sender.Writer.Write(remainAbilityCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        mode = (JailerMode)reader.ReadByte();
        hasPrisoner = reader.ReadBoolean();
        prisonerPlayerId = reader.ReadByte();
        prisonLocationSet = reader.ReadBoolean();
        prisonLocation = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        imprisonSecondsLeft = reader.ReadSingle();
        imprisonTurnsLeft = reader.ReadInt32();
        remainAbilityCount = reader.ReadInt32();
    }
}

public sealed class PrisonNetObject : CustomNetObject
{
    readonly Vector2 _pos;
    string _prisonerName;

    public PrisonNetObject(Vector2 position, string prisonerName)
    {
        _pos = position;
        _prisonerName = prisonerName;
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        if (PlayerControl == null) return;

        var hostPC = PlayerControl.LocalPlayer;
        byte hColor = (byte)(hostPC?.Data?.DefaultOutfit.ColorId ?? 0);

        PlayerControl.RpcSetColor(6);
        if (hostPC != null) hostPC.RpcSetColor(hColor);
        PlayerControl.RawSetColor(6);

        try { PlayerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear; } catch { }
        PlayerControl.cosmetics.colorBlindText.color = Color.clear;

        SetName(BuildPrisonLabel(_prisonerName));
        SnapToPosition(_pos);

        var capturedPC = PlayerControl;
        _ = new LateTask(() =>
        {
            if (capturedPC != null) capturedPC.RawSetColor(6);
        }, 0.15f, "PrisonCNO.Color", true);
    }

    public void UpdateName(string prisonerName)
    {
        _prisonerName = prisonerName;
        SetName(BuildPrisonLabel(prisonerName));
    }

    static string BuildPrisonLabel(string prisoner)
    {
        int count = 10;

        string roof = new('■', count);
        string bottom = new('■', count);
        string bars = new('│', count);

        string cRoof = $"<color=#777777>{roof}</color>";
        string cBars = $"<color=#777777>{bars}</color>";
        string cBottom = $"<color=#777777>{bottom}</color>";

        return $"<voffset=-7.0em><line-height=55%>{cRoof}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBars}\n" +
               $"{cBottom}</line-height></voffset>";
    }

    public override void OnMeeting() { }
}