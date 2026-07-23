using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilLinker : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilLinker),
            player => new EvilLinker(player),
            CustomRoles.EvilLinker,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            126700,
            SetupOptionItem,
            "el",
            OptionSort: (2, 11)
        );

    public EvilLinker(PlayerControl player)
        : base(RoleInfo, player)
    {
        PlaceCooldown = OptionPlaceCooldown.GetFloat();
        MaxPairs = OptionMaxPairs.GetInt();
        WarpCooldown = OptionWarpCooldown.GetFloat();

        linkPairs = new();
        pendingDummy = null;
        placedCount = 0;
        cooldownTimer = PlaceCooldown;
        warpCooldowns = new();
    }

    static OptionItem OptionPlaceCooldown;
    static OptionItem OptionMaxPairs;
    static OptionItem OptionWarpCooldown;

    static float PlaceCooldown;
    static int MaxPairs;
    static float WarpCooldown;

    enum OptionName
    {
        EvilLinkerPlaceCooldown,
        EvilLinkerMaxPairs,
        EvilLinkerWarpCooldown,
    }

    static void SetupOptionItem()
    {
        OptionPlaceCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.EvilLinkerPlaceCooldown,
            new(2.5f, 60f, 2.5f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionMaxPairs = IntegerOptionItem.Create(RoleInfo, 11, OptionName.EvilLinkerMaxPairs,
            new(1, 10, 1), 3, false).SetValueFormat(OptionFormat.Times);
        OptionWarpCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.EvilLinkerWarpCooldown,
            new(1f, 60f, 1f), 10f, false).SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => Options.DefaultKillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
    }

    class LinkPair
    {
        public EvilLinkerDummy DummyA;
        public EvilLinkerDummy DummyB;
        public int ColorId;
        public bool Activated;
        public HashSet<byte> InRangeA = new();
        public HashSet<byte> InRangeB = new();
    }

    static readonly int[] PairColors = { 1, 11, 10, 2, 5, 4, 3, 14, 17, 8 };
    const int PendingColor = 7;

    readonly List<LinkPair> linkPairs;
    LinkPair pendingDummy;
    int placedCount;
    float cooldownTimer;
    readonly Dictionary<byte, float> warpCooldowns;

    public override void Add()
    {
        linkPairs.Clear();
        pendingDummy = null;
        placedCount = 0;
        cooldownTimer = PlaceCooldown;
        warpCooldowns.Clear();
        PetActionManager.Register(Player.PlayerId, OnPetAction);
    }

    public override void OnSpawn(bool initialState = false)
    {
        cooldownTimer = PlaceCooldown + 1.5f;
        Player.RpcResetAbilityCooldown(Sync: true);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = cooldownTimer > 0f ? cooldownTimer : 0.1f;
    }

    void OnPetAction()
    {
        if (!Player.IsAlive()) return;
        if (cooldownTimer > 0f) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            SendActionRpc();
            return;
        }

        PerformPlacePortal();
    }

    void PerformPlacePortal()
    {
        var pos = Player.transform.position;

        if (pendingDummy == null)
        {
            if (placedCount >= MaxPairs) return;

            int colorId = PairColors[placedCount % PairColors.Length];
            var pair = new LinkPair
            {
                ColorId = colorId,
                DummyA = new EvilLinkerDummy((Vector2)pos, Player, PendingColor, activated: false),
                Activated = false,
            };
            pendingDummy = pair;
            linkPairs.Add(pair);

            cooldownTimer = PlaceCooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
        }
        else
        {
            pendingDummy.DummyB = new EvilLinkerDummy((Vector2)pos, Player, PendingColor, activated: false);
            pendingDummy = null;
            placedCount++;

            cooldownTimer = PlaceCooldown;
            Player.RpcResetAbilityCooldown(Sync: true);
        }

        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
            if (cooldownTimer < 0f) cooldownTimer = 0f;
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask || GameStates.IsMeeting) return;

        if (!Player.IsAlive() && linkPairs.Count > 0)
        {
            foreach (var pair in linkPairs.ToArray())
            {
                try { pair.DummyA?.Despawn(); } catch { }
                try { pair.DummyB?.Despawn(); } catch { }
            }
            linkPairs.Clear();
            pendingDummy = null;
            return;
        }

        foreach (var pid in warpCooldowns.Keys.ToArray())
        {
            warpCooldowns[pid] -= Time.fixedDeltaTime;
            if (warpCooldowns[pid] <= 0f)
                warpCooldowns.Remove(pid);
        }

        const float warpRange = 0.2f;

        foreach (var pair in linkPairs)
        {
            if (!pair.Activated) continue;
            if (pair.DummyA == null || pair.DummyB == null) continue;

            var nowInRangeA = new HashSet<byte>();
            var nowInRangeB = new HashSet<byte>();

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                float distA = Vector2.Distance(pc.transform.position, pair.DummyA.Position);
                float distB = Vector2.Distance(pc.transform.position, pair.DummyB.Position);

                if (distA <= warpRange) nowInRangeA.Add(pc.PlayerId);
                if (distB <= warpRange) nowInRangeB.Add(pc.PlayerId);

                if (distA <= warpRange && !pair.InRangeA.Contains(pc.PlayerId)
                    && !warpCooldowns.ContainsKey(pc.PlayerId))
                {
                    pc.RpcSnapToForced(pair.DummyB.Position);
                    warpCooldowns[pc.PlayerId] = WarpCooldown;
                    UtilsGameLog.AddGameLog("EvilLinker",
                        $"{UtilsName.GetPlayerColor(pc)} がポータルA→Bへワープ");
                }
                else if (distB <= warpRange && !pair.InRangeB.Contains(pc.PlayerId)
                    && !warpCooldowns.ContainsKey(pc.PlayerId))
                {
                    pc.RpcSnapToForced(pair.DummyA.Position);
                    warpCooldowns[pc.PlayerId] = WarpCooldown;
                    UtilsGameLog.AddGameLog("EvilLinker",
                        $"{UtilsName.GetPlayerColor(pc)} がポータルB→Aへワープ");
                }
            }

            pair.InRangeA = nowInRangeA;
            pair.InRangeB = nowInRangeB;
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        for (int i = 0; i < linkPairs.Count; i++)
        {
            var pair = linkPairs[i];
            int idx = i;

            if (pair.DummyA != null && pair.DummyB != null)
            {
                var posA = pair.DummyA.Position;
                var posB = pair.DummyB.Position;
                var colorId = pair.ColorId;
                var owner = Player;
                var oldA = pair.DummyA;
                var oldB = pair.DummyB;

                _ = new LateTask(() =>
                {
                    try { oldA?.Despawn(); } catch { }
                    pair.DummyA = new EvilLinkerDummy(posA, owner, colorId, activated: true);
                }, idx * 0.6f + 1.0f, $"EvilLinker.ActivateA.{idx}", true);

                _ = new LateTask(() =>
                {
                    try { oldB?.Despawn(); } catch { }
                    pair.DummyB = new EvilLinkerDummy(posB, owner, colorId, activated: true);
                    pair.Activated = true;
                    pair.InRangeA.Clear();
                    pair.InRangeB.Clear();
                }, idx * 0.6f + 1.4f, $"EvilLinker.ActivateB.{idx}", true);
            }
            else if (pair.DummyA != null && pair.DummyB == null)
            {
                var posA = pair.DummyA.Position;
                var owner = Player;
                var oldA = pair.DummyA;

                _ = new LateTask(() =>
                {
                    try { oldA?.Despawn(); } catch { }
                    pair.DummyA = new EvilLinkerDummy(posA, owner, PendingColor, activated: false);
                }, idx * 0.6f + 1.0f, $"EvilLinker.ReactivatePending.{idx}", true);
            }
        }

        cooldownTimer = PlaceCooldown;

        Player.MarkDirtySettings();
        Player.RpcResetAbilityCooldown(Sync: true);

        foreach (var pair in linkPairs)
        {
            pair.InRangeA.Clear();
            pair.InRangeB.Clear();
        }

        SendRpc();
    }

    public override void OnStartMeeting()
    {
        warpCooldowns.Clear();
        foreach (var pair in linkPairs)
        {
            pair.InRangeA.Clear();
            pair.InRangeB.Clear();
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        warpCooldowns.Clear();
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        foreach (var pair in linkPairs.ToArray())
        {
            try { pair.DummyA?.Despawn(); } catch { }
            try { pair.DummyB?.Despawn(); } catch { }
        }
        linkPairs.Clear();
        pendingDummy = null;
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        string pend = pendingDummy != null ? " <color=#ffff00>1/2</color>" : "";
        return $"<color={RoleInfo.RoleColorCode}>({placedCount}/{MaxPairs}組){pend}</color>";
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)0);
        sender.Writer.Write(placedCount);
        sender.Writer.Write(cooldownTimer);
        sender.Writer.Write(pendingDummy != null);
    }

    void SendActionRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte rpcType = reader.ReadByte();

        if (rpcType == 0)
        {
            placedCount = reader.ReadInt32();
            cooldownTimer = reader.ReadSingle();
            reader.ReadBoolean();
        }
        else if (rpcType == 1)
        {
            if (AmongUsClient.Instance.AmHost)
                PerformPlacePortal();
        }
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "EvilLinker_Ability";
        return true;
    }
}

public sealed class EvilLinkerDummy : CustomNetObject
{
    readonly PlayerControl _owner;
    readonly int _colorId;
    readonly Vector2 _pos;
    public bool Activated { get; private set; }

    public EvilLinkerDummy(Vector2 position, PlayerControl owner, int colorId, bool activated)
    {
        _owner = owner;
        _colorId = colorId;
        _pos = position;
        Activated = activated;
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        if (PlayerControl == null) return;

        var hostPlayer = PlayerControl.LocalPlayer;
        byte hostColor = (byte)(hostPlayer?.Data?.DefaultOutfit.ColorId ?? 0);

        PlayerControl.RpcSetColor((byte)_colorId);
        if (hostPlayer != null)
            hostPlayer.RpcSetColor(hostColor);
        PlayerControl.RawSetColor((byte)_colorId);

        SetName("ポータル");
        SnapToPosition(_pos);

        if (!Activated)
        {
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.notRealPlayer) continue;
                if (pc.PlayerId != _owner.PlayerId)
                    Hide(pc);
            }
        }

        var capturedDummy = PlayerControl;
        _ = new LateTask(() =>
        {
            if (capturedDummy != null)
                capturedDummy.RawSetColor((byte)_colorId);
        }, 0.15f, "EvilLinker.ApplyDummyColor", true);
    }

    public override void OnMeeting() { }
}
