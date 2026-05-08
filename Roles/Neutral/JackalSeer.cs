using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static TownOfHost.PlayerCatch;

namespace TownOfHost.Roles.Neutral;

public sealed class JackalSeer : RoleBase, ILNKiller, ISchrodingerCatOwner, IUsePhantomButton, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(JackalSeer),
            player => new JackalSeer(player),
            CustomRoles.JackalSeer,
            () => OptionCanMakeSidekick.GetBool() ? RoleTypes.Phantom : RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            82950,
            SetupOptionItem,
            "js",
            "#00b4eb",
            (1, 0),
            true,
            countType: CountTypes.Jackal,
            assignInfo: new RoleAssignInfo(CustomRoles.JackalSeer, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },
            from: From.SuperNewRoles
        );

    public JackalSeer(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
        SidekickCooldown = OptionSidekickCooldown.GetFloat();
        CanUseSabotage = OptionCanUseSabotage.GetBool();
        CanSideKick = OptionCanMakeSidekick.GetBool();
        ActiveComms = OptionActiveComms.GetBool();
        DelayMode = OptionDelay.GetBool();
        lastMaxdelay = OptionLastMaxdelay.GetFloat();
        lastMindelay = OptionLastMindelay.GetFloat();
        FirstMaxdelay = OptionFirstMaxdelay.GetFloat();
        FirstMindelay = OptionFirstMindelay.GetFloat();
        ShowSoul = OptionShowSoul.GetBool();

        Receivedcount = 0;
        lateTaskdatas = new List<(LateTask, float)>();
        SoulObjects = new();
        PendingDeadBodies = new();
    }

    // ★ ジャッカル系オプション
    static OptionItem OptionKillCooldown;
    static float KillCooldown;
    static OptionItem OptionSidekickCooldown;
    static float SidekickCooldown;
    static OptionItem OptionCanVent;
    static bool CanVent;
    static OptionItem OptionCanUseSabotage;
    static bool CanUseSabotage;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionCanMakeSidekick;
    static OptionItem OptionImpostorCanSidekick;
    bool CanSideKick;

    // ★ シーア系オプション
    static OptionItem OptionActiveComms;
    static bool ActiveComms;
    static OptionItem OptionDelay;
    static bool DelayMode;
    static OptionItem OptionLastMindelay;
    static float lastMindelay;
    static OptionItem OptionLastMaxdelay;
    static float lastMaxdelay;
    static OptionItem OptionFirstMindelay;
    static float FirstMindelay;
    static OptionItem OptionFirstMaxdelay;
    static float FirstMaxdelay;
    static OptionItem OptionShowSoul;
    static bool ShowSoul;

    ICollection<(LateTask latetask, float mintime)> lateTaskdatas;
    int Receivedcount;
    readonly List<SoulObject> SoulObjects;
    readonly List<(byte playerId, Vector2 pos, int colorId, string playerName)> PendingDeadBodies;

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Jackal;

    enum OptionName
    {
        JackalSeerImpostorCanSidekick,
        JackalSeerSidekickCooldown,
        JackalSeerDelayMode,
        JackalSeerLastMindelay,
        JackalSeerLastMaxdelay,
        JackalSeerFirstMindelay,
        JackalSeerFirstMaxdelay,
        JackalSeerShowSoul,
    }

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        OptionCanUseSabotage = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanUseSabotage, false, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.ImpostorVision, true, false);
        OptionCanMakeSidekick = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.CanCreateSideKick, true, false);
        OptionImpostorCanSidekick = BooleanOptionItem.Create(RoleInfo, 15, OptionName.JackalSeerImpostorCanSidekick, false, false, OptionCanMakeSidekick);
        OptionSidekickCooldown = FloatOptionItem.Create(RoleInfo, 16, OptionName.JackalSeerSidekickCooldown, new(0f, 180f, 0.5f), 30f, false, OptionCanMakeSidekick)
            .SetValueFormat(OptionFormat.Seconds);

        // ★ シーア系オプション
        OptionActiveComms = BooleanOptionItem.Create(RoleInfo, 20, GeneralOption.CanUseActiveComms, true, false);
        OptionDelay = BooleanOptionItem.Create(RoleInfo, 21, OptionName.JackalSeerDelayMode, false, false);
        OptionFirstMindelay = FloatOptionItem.Create(RoleInfo, 22, OptionName.JackalSeerFirstMindelay, new(0, 60, 0.5f), 5f, false, OptionDelay)
            .SetValueFormat(OptionFormat.Seconds);
        OptionFirstMaxdelay = FloatOptionItem.Create(RoleInfo, 23, OptionName.JackalSeerFirstMaxdelay, new(0, 60, 0.5f), 7f, false, OptionDelay)
            .SetValueFormat(OptionFormat.Seconds);
        OptionLastMindelay = FloatOptionItem.Create(RoleInfo, 24, OptionName.JackalSeerLastMindelay, new(0, 60, 0.5f), 0f, false, OptionDelay)
            .SetValueFormat(OptionFormat.Seconds);
        OptionLastMaxdelay = FloatOptionItem.Create(RoleInfo, 25, OptionName.JackalSeerLastMaxdelay, new(0, 60, 0.5f), 5f, false, OptionDelay)
            .SetValueFormat(OptionFormat.Seconds);
        OptionShowSoul = BooleanOptionItem.Create(RoleInfo, 26, OptionName.JackalSeerShowSoul, true, false);

        RoleAddAddons.Create(RoleInfo, 30, NeutralKiller: true);
    }

    // ★ ジャッカル系
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => CanUseSabotage;
    public bool CanUseImpostorVentButton() => CanVent;
    public void ApplySchrodingerCatOptions(IGameOptions option) => ApplyGameOptions(option);

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionHasImpostorVision.GetBool());
        AURoleOptions.PhantomCooldown = JackalDoll.GetSideKickCount() <= JackalDoll.NowSideKickCount ? 200f : SidekickCooldown;
    }

    public bool UseOneclickButton => CanSideKick;
    public override bool CanUseAbilityButton() => CanSideKick;
    bool IUsePhantomButton.IsPhantomRole => JackalDoll.GetSideKickCount() > JackalDoll.NowSideKickCount;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        if (!CanSideKick) return;

        if (JackalDoll.GetSideKickCount() <= JackalDoll.NowSideKickCount)
        {
            CanSideKick = false;
            SendRPC();
            return;
        }
        var target = Player.GetKillTarget(true);
        if (target == null) { ResetCooldown = false; return; }

        var targetrole = target.GetCustomRole();
        if (targetrole is CustomRoles.King or CustomRoles.Jackal or CustomRoles.JackalAlien
            or CustomRoles.Jackaldoll or CustomRoles.JackalMafia or CustomRoles.JackalSeer or CustomRoles.Merlin
            || ((targetrole.IsImpostor() || targetrole is CustomRoles.Egoist) && !OptionImpostorCanSidekick.GetBool()))
        {
            ResetCooldown = false;
            return;
        }

        if (SuddenDeathMode.NowSuddenDeathTemeMode)
            target.SideKickChangeTeam(Player);

        CanSideKick = false;
        SendRPC();
        Player.RpcProtectedMurderPlayer(target);
        target.RpcProtectedMurderPlayer(Player);
        target.RpcProtectedMurderPlayer(target);
        UtilsGameLog.AddGameLog("SideKick",
            string.Format(GetString("log.Sidekick"),
            UtilsName.GetPlayerColor(target, true) + $"({UtilsRoleText.GetTrueRoleName(target.PlayerId)})",
            UtilsName.GetPlayerColor(Player, true)));
        target.RpcSetCustomRole(CustomRoles.Jackaldoll, log: null);
        JackalDoll.Sidekick(target, Player);
        if (!Utils.RoleSendList.Contains(target.PlayerId)) Utils.RoleSendList.Add(target.PlayerId);
        UtilsOption.MarkEveryoneDirtySettings();
    }

    public override string GetAbilityButtonText() => GetString("Sidekick");
    public override bool OverrideAbilityButton(out string text) { text = "SideKick"; return true; }

    // ★ シーア系：キルフラッシュ検知
    public bool? CheckKillFlash(MurderInfo info)
    {
        bool canSee = !Utils.IsActive(SystemTypes.Comms) || ActiveComms;

        if (!DelayMode) { Receivedcount++; return canSee; }
        if (!canSee) return false;

        float addDelay = 0f;
        if (lastMaxdelay > 0)
        {
            int chance = IRandom.Instance.Next(0, (int)lastMaxdelay * 10);
            addDelay = chance * 0.1f;
        }
        var lateTask = new LateTask(() =>
        {
            if ((!Utils.IsActive(SystemTypes.Comms) || ActiveComms) is false) return;
            if (GameStates.CalledMeeting || !Player.IsAlive()) return;
            Receivedcount++;
            Player.KillFlash();
        }, addDelay + lastMindelay, "JackalSeerDelayKillFlash", null);
        lateTaskdatas.Add((lateTask, lastMindelay));
        return null;
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        // ★ 遅延タスクを処理
        bool isCalled = (!Utils.IsActive(SystemTypes.Comms) || ActiveComms) is false || !Player.IsAlive();
        foreach (var data in lateTaskdatas)
        {
            if (data.latetask is null) continue;
            if (!isCalled && !data.latetask.Isruned && data.latetask.timer > data.mintime)
            {
                isCalled = true;
                Player.KillFlash();
            }
            data.latetask.CallStop();
        }
        lateTaskdatas.Clear();

        if (!ShowSoul || !Player.IsAlive() || target == null) return;

        foreach (var deadBody in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            var deadPlayer = GetPlayerById(deadBody.ParentId);
            var deadInfo = deadPlayer?.Data;
            if (deadInfo == null) continue;
            AddPendingSoul(deadInfo.PlayerId, deadBody.transform.position, deadInfo.DefaultOutfit.ColorId, deadInfo.PlayerName);
        }
        var fallbackPos = GetPlayerById(target.PlayerId)?.GetTruePosition() ?? Vector2.zero;
        AddPendingSoul(target.PlayerId, fallbackPos, target.DefaultOutfit.ColorId, target.PlayerName);
    }

    public override void AfterMeetingTasks()
    {
        if (!ShowSoul || !AmongUsClient.Instance.AmHost || !Player.IsAlive()) return;

        foreach (var soul in SoulObjects) soul?.Despawn();
        SoulObjects.Clear();

        for (int i = 0; i < PendingDeadBodies.Count; i++)
        {
            var (_, pos, colorId, playerName) = PendingDeadBodies[i];
            int idx = i;
            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                var soul = new SoulObject(pos, colorId, playerName, Player);
                SoulObjects.Add(soul);
            }, idx * 0.6f, $"Jackal.SpawnSoul{idx}", true);
        }
        PendingDeadBodies.Clear();
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        foreach (var soul in SoulObjects) soul?.Despawn();
        SoulObjects.Clear();
    }

    private void AddPendingSoul(byte playerId, Vector2 pos, int colorId, string playerName)
    {
        if (PendingDeadBodies.Exists(d => d.playerId == playerId)) return;
        PendingDeadBodies.Add((playerId, pos, colorId, playerName));
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        string soulText = ShowSoul && SoulObjects.Count > 0
            ? $"<color=#c8a2c8> 霊魂{SoulObjects.Count}</color>"
            : "";
        return $"<color=#00b4eb>(〇)</color>{soulText}";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()
            || JackalDoll.GetSideKickCount() <= JackalDoll.NowSideKickCount || !CanSideKick) return "";
        return isForHud ? GetString("PhantomButtonSideKick") : $"<size=50%>{GetString("PhantomButtonSideKick")}</size>";
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(CanSideKick);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        CanSideKick = reader.ReadBoolean();
    }
}

/// <summary>ジャッカルシーア専用の霊魂オブジェクト</summary>
public sealed class JackalSeerSoulObject : CustomNetObject
{
    private static readonly string[] ColorCodes =
    {
        "#c51111", "#132ed1", "#117f2d", "#ed54ba",
        "#ef7d0d", "#f5f557", "#3f474e", "#d6e0f0",
        "#6b2fbb", "#71491e", "#38fedc", "#50ef39",
        "#ff0000", "#ffff00", "#fffebe", "#c8a2c8",
        "#4d2b6e", "#00c3fc",
    };

    readonly int _colorId;
    readonly string _playerName;
    readonly PlayerControl _seer;
    readonly Vector2 _spawnPos;

    public JackalSeerSoulObject(Vector2 position, int colorId, string playerName, PlayerControl seer)
    {
        _colorId = colorId;
        _playerName = playerName;
        _seer = seer;
        _spawnPos = position;
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        string color = _colorId >= 0 && _colorId < ColorCodes.Length
            ? ColorCodes[_colorId]
            : "#ffffff";

        if (PlayerControl != null)
            PlayerControl.RpcSetColor((byte)_colorId);

        SetName($"<color={color}>霊魂\n<size=70%>({_playerName})</size></color>");
        SnapToPosition(_spawnPos);

        foreach (var pc in AllPlayerControls)
        {
            if (_seer == null || pc.PlayerId != _seer.PlayerId)
                Hide(pc);
        }
    }

    public override void OnMeeting() { }
}