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

public sealed class SmokeMaker : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SmokeMaker),
            player => new SmokeMaker(player),
            CustomRoles.SmokeMaker,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            126400,
            SetupOptionItem,
            "sm",
            OptionSort: (3, 10),
            from: From.TownOfHost_Pko
        );

    public SmokeMaker(PlayerControl player) : base(RoleInfo, player)
    {
        PlaceCooldown = OptionPlaceCooldown.GetFloat();
        SmokeCooldown = OptionSmokeCooldown.GetFloat();
        SmokeDuration = OptionSmokeDuration.GetFloat();
        SmokeSize = OptionSmokeSize.GetInt() * 50;
        MaxDummies = OptionMaxDummies.GetInt();

        placedDummies = new();
        placeCDLeft = 0f;
    }

    static OptionItem OptionPlaceCooldown; static float PlaceCooldown;
    static OptionItem OptionSmokeCooldown; static float SmokeCooldown;
    static OptionItem OptionSmokeDuration; static float SmokeDuration;
    static OptionItem OptionSmokeSize; static int SmokeSize;
    static OptionItem OptionMaxDummies; static int MaxDummies;

    enum OptionName
    {
        SmokeMakerPlaceCooldown,
        SmokeMakerSmokeCooldown,
        SmokeMakerSmokeDuration,
        SmokeMakerSmokeSize,
        SmokeMakerMaxDummies,
    }

    readonly List<SmokeDummy> placedDummies;
    float placeCDLeft;
    int clientDummiesCount;
    int DummiesCount => AmongUsClient.Instance.AmHost ? placedDummies.Count : clientDummiesCount;

    static void SetupOptionItem()
    {
        OptionPlaceCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SmokeMakerPlaceCooldown,
            new(1f, 60f, 1f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSmokeCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.SmokeMakerSmokeCooldown,
            new(1f, 60f, 1f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSmokeDuration = FloatOptionItem.Create(RoleInfo, 12, OptionName.SmokeMakerSmokeDuration,
            new(1f, 60f, 1f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSmokeSize = IntegerOptionItem.Create(RoleInfo, 13, OptionName.SmokeMakerSmokeSize,
            new(100, 650, 50), 150, false).SetValueFormat(OptionFormat.Percent);
        OptionMaxDummies = IntegerOptionItem.Create(RoleInfo, 14, OptionName.SmokeMakerMaxDummies,
            new(1, 20, 1), 5, false).SetValueFormat(OptionFormat.Pieces);
    }

    public float CalculateKillCooldown() => 30f;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void Add()
    {
        placeCDLeft = PlaceCooldown;
        placedDummies.Clear();
        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    public override void OnSpawn(bool initialState = false)
    {
        placeCDLeft = PlaceCooldown + 1.5f;
    }

    public override void OnDestroy()
    {
        DespawnAll();
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = SmokeCooldown;
    }

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (placeCDLeft > 0f) return;
        if (placedDummies.Count >= MaxDummies) return;

        var pos = Player.GetTruePosition();
        var dummy = new SmokeDummy(pos, Player, SmokeSize);
        placedDummies.Add(dummy);

        placeCDLeft = PlaceCooldown;
        Player.MarkDirtySettings();

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        SendSyncRpc();
        Logger.Info($"{Player.Data.GetLogPlayerName()} がスモークダミーを設置: {pos}", "SmokeMaker");
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!Player.IsAlive()) return;

        if (AmongUsClient.Instance.AmHost)
        {
            ActivateSmoke();
        }
        else
        {
            if (DummiesCount > 0)
            {
                SendActivateRequestRpc();
            }
        }
    }

    void ActivateSmoke()
    {
        if (!Player.IsAlive()) return;
        if (placedDummies.Count == 0) return;

        foreach (var dummy in placedDummies.ToArray())
            dummy.Activate(SmokeDuration);

        placedDummies.Clear();

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = SmokeCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.1f, "SmokeMaker.ResetCD", true);

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        SendSyncRpc();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (placeCDLeft > 0f)
        {
            float prev = placeCDLeft;
            placeCDLeft -= Time.fixedDeltaTime;

            if (placeCDLeft <= 0f)
            {
                placeCDLeft = 0f;
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
                SendSyncRpc();
            }
            else if (Mathf.CeilToInt(prev) != Mathf.CeilToInt(placeCDLeft))
            {
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
                SendSyncRpc();
            }
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        DespawnAll();
    }

    public override void OnStartMeeting() => DespawnAll();

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        placeCDLeft = PlaceCooldown;
        SendSyncRpc();
    }

    void DespawnAll()
    {
        foreach (var d in placedDummies.ToArray())
            d.ForceRemove();
        placedDummies.Clear();
        SendSyncRpc();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        if (placeCDLeft > 0f)
            return $" <color=#888888>({Mathf.CeilToInt(placeCDLeft)}s)</color>";
        if (DummiesCount >= MaxDummies)
            return $" <color=#555555>({DummiesCount}/{MaxDummies})</color>";
        return $" <color=#aaaaaa>({DummiesCount}/{MaxDummies})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (DummiesCount == 0)
            return $"{size}<color={color}>ペット → スモーク設置 | ファントム → 起爆</color>";
        return $"{size}<color={color}>設置済: {DummiesCount} | ファントム → 全て起爆！</color>";
    }

    void SendSyncRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)1);
        sender.Writer.Write(placeCDLeft);
        sender.Writer.Write(placedDummies.Count);
    }

    void SendActivateRequestRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)0);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte rpcType = reader.ReadByte();
        if (rpcType == 0)
        {
            if (AmongUsClient.Instance.AmHost)
            {
                ActivateSmoke();
            }
        }
        else if (rpcType == 1)
        {
            placeCDLeft = reader.ReadSingle();
            clientDummiesCount = reader.ReadInt32();
        }
    }
}

public class SmokeDummy : CustomNetObject
{
    readonly PlayerControl _owner;
    readonly int _smokeSize;
    readonly Vector2 _spawnPos;
    readonly int _colorId;
    bool _activated = false;

    public static HashSet<byte> PlayersInSmoke = new();
    public static readonly List<SmokeCloud> ActiveSmokes = new();

    public SmokeDummy(Vector2 position, PlayerControl owner, int smokeSize)
    {
        _owner = owner;
        _smokeSize = smokeSize;
        _spawnPos = position;
        _colorId = IRandom.Instance.Next(0, 18);
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        SetAppearance(_colorId, "", "", "", "");
        SetName("");
        SnapToPosition(_spawnPos);

        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.notRealPlayer) continue;
            if (_owner == null || pc.PlayerId != _owner.PlayerId)
                Hide(pc);
        }
    }

    public void Activate(float duration)
    {
        if (_activated) return;
        _activated = true;

        new SmokeCloud(_spawnPos, _smokeSize, duration);
        ForceRemove();
    }

    public static bool IsPlayerInSmoke(PlayerControl target)
    {
        return target != null && PlayersInSmoke.Contains(target.PlayerId);
    }

    public static void UpdateAll()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        bool smokeRemoved = false;
        foreach (var smoke in ActiveSmokes.ToArray())
        {
            smoke._durationLeft -= Time.fixedDeltaTime;
            if (smoke._durationLeft <= 0f)
            {
                smoke.ForceRemove();
                smokeRemoved = true;
            }
        }

        var currentInSmoke = new HashSet<byte>();
        if (ActiveSmokes.Count > 0)
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                var pos = pc.GetTruePosition();
                foreach (var smoke in ActiveSmokes)
                {
                    float radius = smoke._smokeSize / 2500f;
                    if (Vector2.Distance(pos, smoke._spawnPos) <= radius)
                    {
                        currentInSmoke.Add(pc.PlayerId);
                        break;
                    }
                }
            }
        }

        if (smokeRemoved || !currentInSmoke.SetEquals(PlayersInSmoke))
        {
            PlayersInSmoke = currentInSmoke;
            UtilsNotifyRoles.NotifyRoles();
        }
    }

    public void ForceRemove()
    {
        try { Despawn(); } catch { }
    }

    public override void OnMeeting()
    {
        ForceRemove();
        PlayersInSmoke.Clear();
    }
}

public class SmokeCloud : CustomNetObject
{
    public readonly int _smokeSize;
    public readonly Vector2 _spawnPos;
    public float _durationLeft;

    public SmokeCloud(Vector2 pos, int smokeSize, float duration)
    {
        _spawnPos = pos;
        _smokeSize = smokeSize;
        _durationLeft = duration;
        CreateNetObject(pos);
    }

    protected override void OnCreated()
    {
        SetAppearance(0, "", "", "", "");
        SetName($"<color=#aaaaaa><size={_smokeSize}%>・</size></color>");
        SnapToPosition(_spawnPos);
        SmokeDummy.ActiveSmokes.Add(this);
    }

    public void ForceRemove()
    {
        SmokeDummy.ActiveSmokes.Remove(this);
        try { Despawn(); } catch { }
    }

    public override void OnMeeting() => ForceRemove();
}

[HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class SmokeDummyUpdatePatch
{
    public static void Postfix()
    {
        if (!GameStates.IsInTask) return;
        SmokeDummy.UpdateAll();
    }
}