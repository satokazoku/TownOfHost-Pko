using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class BeginnerImpostor : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BeginnerImpostor),
            player => new BeginnerImpostor(player),
            CustomRoles.BeginnerImpostor,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            126800,
            SetupOptionItem,
            "bi",
            "#ff6b35",
            OptionSort: (4, 6),
            assignInfo: new RoleAssignInfo(CustomRoles.BeginnerImpostor, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1)
            },
            from: From.TownOfHost_Pko
        );

    private static OptionItem OptionDummyCount;
    private static OptionItem OptionDummyKillRange;
    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionCooldownReduction;
    private static OptionItem OptionMinimumKillCooldown;
    private static OptionItem OptionRelocateAfterMeeting;
    private static OptionItem OptionResetKillCooldown;
    private static OptionItem OptionRequireDummyKills;
    private static OptionItem OptionRequiredDummyKills;

    private readonly List<BeginnerImpostorDummy> dummies = new();
    private float currentKillCooldown;
    private float syncedAbilityCooldown;
    private int dummyKillCount;
    private bool initialized;

    // スポーン位置は by ChatGpt
    private static readonly Dictionary<MapNames, Vector2[]> DummySpawnPointsByMap = new()
    {
        [MapNames.Skeld] = new[]
        {
            new Vector2(-1.0f, 3.0f),    // Cafeteria
            new Vector2(9.3f, 1.0f),     // Weapons
            new Vector2(6.5f, -3.8f),    // O2
            new Vector2(16.5f, -4.8f),   // Navigation
            new Vector2(9.3f, -12.3f),   // Shields
            new Vector2(4.0f, -15.5f),   // Communications
            new Vector2(-1.5f, -15.5f),  // Storage
            new Vector2(4.5f, -7.9f),    // Admin
            new Vector2(-7.5f, -8.8f),   // Electrical
            new Vector2(-17.0f, -13.5f), // Lower Engine
            new Vector2(-17.0f, -1.3f),  // Upper Engine
            new Vector2(-13.5f, -5.5f),  // Security
            new Vector2(-20.5f, -5.5f),  // Reactor
            new Vector2(-9.0f, -4.0f),   // MedBay
        },
        [MapNames.MiraHQ] = new[]
        {
            new Vector2(25.5f, 2.0f),  // Cafeteria
            new Vector2(24.0f, -2.0f), // Balcony
            new Vector2(19.5f, 4.0f),  // Storage
            new Vector2(17.8f, 11.5f), // Junction
            new Vector2(15.3f, 3.8f),  // Communications
            new Vector2(15.5f, -0.5f), // MedBay
            new Vector2(9.0f, 1.0f),   // Locker Room
            new Vector2(6.1f, 6.0f),   // Decontamination
            new Vector2(9.5f, 12.0f),  // Laboratory
            new Vector2(2.5f, 10.5f),  // Reactor
            new Vector2(-4.5f, 2.0f),  // Launchpad
            new Vector2(21.0f, 17.5f), // Admin
            new Vector2(15.0f, 19.0f), // Office
            new Vector2(17.8f, 23.0f), // Greenhouse
        },
        [MapNames.Polus] = new[]
        {
            new Vector2(19.5f, -18.0f), // Office Left
            new Vector2(26.0f, -17.0f), // Office Right
            new Vector2(24.0f, -22.5f), // Admin
            new Vector2(12.5f, -16.0f), // Communications
            new Vector2(12.0f, -23.5f), // Weapons
            new Vector2(2.3f, -24.0f),  // Boiler Room
            new Vector2(2.0f, -17.5f),  // O2
            new Vector2(9.5f, -12.5f),  // Electrical
            new Vector2(3.0f, -12.0f),  // Security
            new Vector2(16.7f, -3.0f),  // Dropship
            new Vector2(20.5f, -12.0f), // Storage
            new Vector2(26.7f, -8.5f),  // Rocket
            new Vector2(36.5f, -7.5f),  // Laboratory
            new Vector2(34.0f, -10.0f), // Toilet
            new Vector2(36.5f, -22.0f), // Specimen Room
        },
        [MapNames.Airship] = new[]
        {
            new Vector2(-0.7f, 8.5f),   // Brig
            new Vector2(-0.7f, -1.0f),  // Engine Room
            new Vector2(-7.0f, -11.5f), // Kitchen
            new Vector2(33.5f, -1.5f),  // Cargo Bay
            new Vector2(20.0f, 10.5f),  // Records
            new Vector2(15.5f, 0.0f),   // Main Hall
            new Vector2(6.3f, 2.5f),    // Nap Room
            new Vector2(17.1f, 14.9f),  // Meeting Room
            new Vector2(12.0f, 8.5f),   // Gap Room
            new Vector2(-8.9f, 12.2f),  // Vault
            new Vector2(-13.3f, 1.3f),  // Communications
            new Vector2(-23.5f, -1.6f), // Cockpit
            new Vector2(-10.3f, -5.9f), // Armory
            new Vector2(-13.7f, -12.6f),// Viewing Deck
            new Vector2(5.8f, -10.8f),  // Security
            new Vector2(16.3f, -8.8f),  // Electrical
            new Vector2(29.0f, -6.2f),  // Medical
            new Vector2(30.9f, 6.8f),   // Toilet
            new Vector2(21.2f, -0.8f),  // Showers
        },
        [MapNames.Fungle] = new[]
        {
            new Vector2(-17.8f, -7.3f), // Kitchen
            new Vector2(-21.3f, 3.0f),  // Beach
            new Vector2(-16.9f, 5.5f),  // Cafeteria
            new Vector2(-17.7f, 0.0f),  // Rec Room
            new Vector2(-9.7f, 2.7f),   // Bonfire
            new Vector2(-7.6f, 10.4f),  // Dropship
            new Vector2(2.3f, 4.3f),    // Storage
            new Vector2(-4.2f, -2.2f),  // Meeting Room
            new Vector2(1.7f, -1.4f),   // Sleeping Quarters
            new Vector2(-4.2f, -7.9f),  // Laboratory
            new Vector2(9.2f, -11.8f),  // Greenhouse
            new Vector2(21.8f, -7.2f),  // Reactor
            new Vector2(4.2f, -5.3f),   // Jungle Top
            new Vector2(15.9f, -14.8f), // Jungle Bottom
            new Vector2(6.4f, 3.1f),    // Lookout
            new Vector2(12.5f, 9.6f),   // Mining Pit
            new Vector2(15.5f, 3.9f),   // Highlands
            new Vector2(21.9f, 3.2f),   // Upper Engine
            new Vector2(19.8f, 7.3f),   // Precipice
            new Vector2(20.9f, 13.4f),  // Communications
        },
    };

    private enum OptionName
    {
        BeginnerImpostorDummyCount,
        BeginnerImpostorDummyKillRange,
        BeginnerImpostorCooldownReduction,
        BeginnerImpostorMinimumKillCooldown,
        BeginnerImpostorRelocateAfterMeeting,
        BeginnerImpostorResetKillCooldown,
        BeginnerImpostorRequireDummyKills,
        BeginnerImpostorRequiredDummyKills,
    }

    public BeginnerImpostor(PlayerControl player) : base(RoleInfo, player)
    {
        currentKillCooldown = OptionKillCooldown.GetFloat();
        syncedAbilityCooldown = currentKillCooldown;
        dummyKillCount = 0;
    }

    private static void SetupOptionItem()
    {
        OptionDummyCount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.BeginnerImpostorDummyCount,
            new(1, 10, 1), 3, false).SetValueFormat(OptionFormat.Pieces);
        OptionDummyKillRange = StringOptionItem.Create(RoleInfo, 11, OptionName.BeginnerImpostorDummyKillRange,
            new[] { "Short", "Middle", "Long" }, 1, false);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.KillCooldown,
            new(0f, 180f, 0.5f), 35f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCooldownReduction = FloatOptionItem.Create(RoleInfo, 13, OptionName.BeginnerImpostorCooldownReduction,
            new(0f, 180f, 0.5f), 2.5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionMinimumKillCooldown = FloatOptionItem.Create(RoleInfo, 14, OptionName.BeginnerImpostorMinimumKillCooldown,
            new(0f, 180f, 0.5f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionRelocateAfterMeeting = BooleanOptionItem.Create(RoleInfo, 15, OptionName.BeginnerImpostorRelocateAfterMeeting,
            true, false);
        OptionResetKillCooldown = BooleanOptionItem.Create(RoleInfo, 16, OptionName.BeginnerImpostorResetKillCooldown,
            true, false);
        OptionRequireDummyKills = BooleanOptionItem.Create(RoleInfo, 17, OptionName.BeginnerImpostorRequireDummyKills,
            false, false);
        OptionRequiredDummyKills = IntegerOptionItem.Create(RoleInfo, 18, OptionName.BeginnerImpostorRequiredDummyKills,
            new(0, 15, 1), 2, false, OptionRequireDummyKills).SetValueFormat(OptionFormat.Times);
        RoleAddAddons.Create(RoleInfo, 20);
    }

    public float CalculateKillCooldown() => currentKillCooldown;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = syncedAbilityCooldown;
    }

    public override void StartGameTasks()
    {
        // Skeldなどでランダムスポーンが無効の場合は初回OnSpawnが呼ばれないため、
        // 全マップ共通のゲーム開始処理から生成を保証する。
        if (AmongUsClient.Instance.AmHost && !initialized)
            OnSpawn(true);
    }

    public override void OnSpawn(bool initialState = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!initialized || initialState)
        {
            initialized = true;
            currentKillCooldown = OptionKillCooldown.GetFloat();
            syncedAbilityCooldown = currentKillCooldown;
            dummyKillCount = 0;
            (this as IUsePhantomButton).Init(Player);
            IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
            EnsureDummyCount();
            _ = new LateTask(EnsureDummyCount, 0.5f, "BeginnerImpostor.InitialDummyRetry", true);
            SendRPC();
            Logger.Info($"Initial dummy spawn: map={(MapNames)Main.NormalOptions.MapId}, count={OptionDummyCount.GetInt()}",
                "BeginnerImpostor");
            return;
        }

        if (OptionRelocateAfterMeeting.GetBool())
            RelocateDummies();

        syncedAbilityCooldown = currentKillCooldown;
        EnsureDummyCount();
    }

    public override void OnDestroy()
    {
        if (AmongUsClient.Instance?.AmHost == true)
        {
            foreach (var dummy in dummies.ToArray())
                dummy?.Despawn();
        }
        dummies.Clear();
    }

    bool IUsePhantomButton.IsresetAfterKill => true;
    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => true;
    void IUsePhantomButton.SetSyncedAbilityCooldown(float cooldown)
        => syncedAbilityCooldown = cooldown;

    public void OnMurderPlayerAsKiller(MurderInfo info)
        => syncedAbilityCooldown = currentKillCooldown;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;

        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || GameStates.IsMeeting)
        {
            ResetCooldown = false;
            return;
        }

        var target = GetNearestKillableDummy();
        if (target == null)
        {
            ResetCooldown = false;
            return;
        }

        target.OnKilled(Player);

        if (OptionResetKillCooldown.GetBool())
        {
            Main.AllPlayerKillCooldown[Player.PlayerId] = currentKillCooldown;
            IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
            var state = PlayerState.GetByPlayerId(Player.PlayerId);
            if (state != null) state.Is10secKillButton = false;
        }
    }

    public override string GetAbilityButtonText() => GetString("BeginnerImpostorTrainButton");

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        string text = OptionRequireDummyKills.GetBool()
            ? $"({dummyKillCount}/{OptionRequiredDummyKills.GetInt()})"
            : $"({dummyKillCount})";
        return Utils.ColorString(RoleInfo.RoleColor, text);
    }

    public override void CheckWinner(GameOverReason reason)
        => EnforceDummyKillWinRequirement();

    public void EnforceDummyKillWinRequirement()
    {
        if (!OptionRequireDummyKills.GetBool()
            || dummyKillCount >= OptionRequiredDummyKills.GetInt()
            || !CustomWinnerHolder.winners.Contains(CustomWinner.Impostor)
            || !CustomWinnerHolder.WinnerIds.Contains(Player.PlayerId))
            return;

        CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
        CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
        Logger.Info(
            $"勝利条件未達成のため除外: {Player.Data?.GetLogPlayerName()} dummyKills={dummyKillCount}/{OptionRequiredDummyKills.GetInt()}",
            "BeginnerImpostor");
    }

    private BeginnerImpostorDummy GetNearestKillableDummy()
    {
        float range = NormalGameOptionsV10.KillDistances[
            Mathf.Clamp(OptionDummyKillRange.GetValue(), 0, NormalGameOptionsV10.KillDistances.Length - 1)];
        Vector2 origin = Player.GetTruePosition();

        return dummies
            .Where(dummy => dummy?.IsReady == true)
            .Select(dummy => new
            {
                Dummy = dummy,
                Delta = dummy.Position - origin,
                Distance = Vector2.Distance(origin, dummy.Position)
            })
            .Where(entry => entry.Distance <= range
                && (entry.Distance <= 0.01f
                    || !PhysicsHelpers.AnyNonTriggersBetween(origin, entry.Delta.normalized, entry.Distance,
                        Constants.ShipAndObjectsMask)))
            .OrderBy(entry => entry.Distance)
            .Select(entry => entry.Dummy)
            .FirstOrDefault();
    }

    internal bool Owns(PlayerControl killer)
        => killer != null && Player != null && killer.PlayerId == Player.PlayerId;

    internal void OnDummyKilled(BeginnerImpostorDummy dummy)
    {
        if (!AmongUsClient.Instance.AmHost || dummy == null || !dummies.Remove(dummy))
            return;

        dummyKillCount++;
        currentKillCooldown = Mathf.Max(
            OptionMinimumKillCooldown.GetFloat(),
            currentKillCooldown - OptionCooldownReduction.GetFloat());

        SendRPC();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private void EnsureDummyCount()
    {
        if (!AmongUsClient.Instance.AmHost || GameStates.IsEnded || ShipStatus.Instance == null)
            return;

        dummies.RemoveAll(dummy => dummy == null || dummy.IsDestroyed);
        int missing = OptionDummyCount.GetInt() - dummies.Count;
        if (missing <= 0) return;

        var positions = GetRandomSpawnPositions(missing, dummies.Select(dummy => dummy.Position));
        for (int i = 0; i < positions.Count; i++)
            dummies.Add(new BeginnerImpostorDummy(positions[i], this));

        Logger.Info($"Queued {positions.Count} training dummies ({dummies.Count}/{OptionDummyCount.GetInt()})",
            "BeginnerImpostor");
    }

    private void RelocateDummies()
    {
        dummies.RemoveAll(dummy => dummy == null || dummy.IsDestroyed);
        var positions = GetRandomSpawnPositions(dummies.Count);
        for (int i = 0; i < dummies.Count && i < positions.Count; i++)
            dummies[i].Relocate(positions[i]);
    }

    private List<Vector2> GetRandomSpawnPositions(int count, IEnumerable<Vector2> excludedPositions = null)
    {
        var result = new List<Vector2>(count);
        var available = new List<Vector2>();
        MapNames map = (MapNames)(Main.NormalOptions?.MapId ?? 0);
        if (map == MapNames.Dleks)
            map = MapNames.Skeld;

        if (DummySpawnPointsByMap.TryGetValue(map, out var configuredPoints))
            available.AddRange(configuredPoints);

        if (excludedPositions != null)
        {
            var excluded = excludedPositions.ToArray();
            available.RemoveAll(candidate => excluded.Any(position =>
                Vector2.SqrMagnitude(candidate - position) < 0.0001f));
        }

        // 未対応マップだけは従来どおりベント座標へフォールバックする。
        if (available.Count == 0)
        {
            var vents = ShipStatus.Instance?.AllVents;
            if (vents != null)
            {
                for (int i = 0; i < vents.Length; i++)
                {
                    if (vents[i] != null)
                        available.Add((Vector2)vents[i].transform.position + new Vector2(0f, 0.1f));
                }
            }
        }

        while (result.Count < count)
        {
            if (available.Count == 0)
            {
                Vector2 basePosition = Player?.GetTruePosition() ?? Vector2.zero;
                result.Add(basePosition + new Vector2(
                    IRandom.Instance.Next(-10, 11) / 10f,
                    IRandom.Instance.Next(-10, 11) / 10f));
                continue;
            }

            int index = IRandom.Instance.Next(0, available.Count);
            result.Add(available[index]);
            available.RemoveAt(index);
        }

        return result;
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(dummyKillCount);
        sender.Writer.Write(currentKillCooldown);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        dummyKillCount = reader.ReadInt32();
        currentKillCooldown = reader.ReadSingle();
        if (Player != null)
            Main.AllPlayerKillCooldown[Player.PlayerId] = currentKillCooldown;
    }
}

public sealed class BeginnerImpostorDummy : CustomNetObject, IKillableDummy
{
    private readonly BeginnerImpostor owner;
    private Vector2 spawnPosition;
    private readonly int colorId;

    public bool IsReady => PlayerControl != null;
    public bool IsDestroyed { get; private set; }

    public BeginnerImpostorDummy(Vector2 position, BeginnerImpostor owner)
    {
        this.owner = owner;
        spawnPosition = position;
        colorId = IRandom.Instance.Next(0, 18);
        CreateNetObject(position);
    }

    protected override void OnCreated()
    {
        if (IsDestroyed || PlayerControl == null) return;
        var hostPlayer = PlayerControl.LocalPlayer;
        byte hostColor = (byte)(hostPlayer?.Data?.DefaultOutfit.ColorId ?? 0);

        PlayerControl.RpcSetColor((byte)colorId);
        // CNOはホストのPlayerDataを一時参照するため、ホスト本人の色データは即座に復元する。
        if (hostPlayer != null)
            hostPlayer.RpcSetColor(hostColor);
        PlayerControl.RawSetColor((byte)colorId);
        SetName(GetString("BeginnerImpostorDummyName"));
        SnapToPosition(spawnPosition);

        // CNO生成直後の外見初期化でホスト色へ戻される場合があるため、
        // ホスト側の描画色を生成完了後にもう一度固定する。
        var capturedDummy = PlayerControl;
        _ = new LateTask(() =>
        {
            if (!IsDestroyed && capturedDummy != null)
                capturedDummy.RawSetColor((byte)colorId);
        }, 0.15f, "BeginnerImpostor.ApplyDummyColor", true);
    }

    public void Relocate(Vector2 position)
    {
        if (IsDestroyed) return;
        spawnPosition = position;
        Position = position;
        if (PlayerControl != null)
            SnapToPosition(position);
    }

    public void OnKilled(PlayerControl killer)
    {
        if (IsDestroyed || owner == null || !owner.Owns(killer))
            return;

        IsDestroyed = true;
        owner.OnDummyKilled(this);
        Despawn();
    }

    public override void OnMeeting() { }
}
