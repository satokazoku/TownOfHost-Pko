/*
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

public sealed class Spider : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Spider),
            player => new Spider(player),
            CustomRoles.Spider,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            185000,
            SetupOptionItem,
            "sdr",
            OptionSort: (2, 12),
            from: From.SuperNewRoles
        );

    public Spider(PlayerControl player)
        : base(RoleInfo, player)
    {
        TrapCooldown = OptionTrapCooldown.GetFloat();
        ArmTime = OptionArmTime.GetFloat();
        CatchDuration = OptionCatchDuration.GetFloat();

        traps = new();
        knownCaught = new();
        cooldownTimer = TrapCooldown;
    }

    static OptionItem OptionTrapCooldown;
    static OptionItem OptionArmTime;
    static OptionItem OptionCatchDuration;

    static float TrapCooldown;
    static float ArmTime;
    static float CatchDuration;

    enum OptionName
    {
        SpiderTrapCooldown,
        SpiderTrapArmTime,
        SpiderTrapCatchDuration,
    }

    static void SetupOptionItem()
    {
        OptionTrapCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SpiderTrapCooldown,
            new(2.5f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionArmTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.SpiderTrapArmTime,
            new(0f, 60f, 2.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCatchDuration = FloatOptionItem.Create(RoleInfo, 12, OptionName.SpiderTrapCatchDuration,
            new(2.5f, 60f, 2.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
    }

    const float CatchDistance = 0.9f;

    class TrapInfo
    {
        public Vector2 Position;
        public float ArmTimer;
        public bool Armed => ArmTimer <= 0f;
        public byte CaughtId = byte.MaxValue;
        public float HoldTimer;
    }

    readonly List<TrapInfo> traps;
    readonly HashSet<byte> knownCaught;
    float cooldownTimer;

    static readonly Dictionary<byte, float> CaughtRemaining = new();
    static readonly Dictionary<byte, float> SavedSpeed = new();

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

    public override void Add()
    {
        traps.Clear();
        knownCaught.Clear();
        cooldownTimer = TrapCooldown;
        PetActionManager.Register(Player.PlayerId, OnPetAction);
    }

    public override void OnSpawn(bool initialState = false)
    {
        if (initialState)
        {
            traps.Clear();
            knownCaught.Clear();
        }
        cooldownTimer = TrapCooldown + 1.5f;
        Player.RpcResetAbilityCooldown(Sync: true);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        foreach (var id in knownCaught.ToArray())
            ApplyRelease(id, restoreSpeed: true);
        traps.Clear();
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

        PlaceTrap();
    }

    void PlaceTrap()
    {
        traps.Add(new TrapInfo
        {
            Position = Player.transform.position,
            ArmTimer = ArmTime,
        });

        cooldownTimer = TrapCooldown;
        Player.RpcResetAbilityCooldown(Sync: true);
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

        if (!Player.IsAlive())
        {
            if (traps.Count > 0)
            {
                traps.Clear();
                SendRpc();
            }
            return;
        }
        if (!GameStates.IsInTask) return;

        bool changed = false;

        foreach (var trap in traps.ToArray())
        {
            if (trap.ArmTimer > 0f)
                trap.ArmTimer = Mathf.Max(0f, trap.ArmTimer - Time.fixedDeltaTime);

            if (trap.CaughtId == byte.MaxValue)
            {
                if (!trap.Armed) continue;

                foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                {
                    if (pc.Is(CustomRoleTypes.Impostor)) continue;
                    if (Vector2.Distance(pc.transform.position, trap.Position) > CatchDistance) continue;

                    trap.CaughtId = pc.PlayerId;
                    trap.HoldTimer = CatchDuration;
                    ApplyCatch(pc.PlayerId, CatchDuration);
                    changed = true;
                    break;
                }
            }
            else
            {
                var caught = PlayerCatch.GetPlayerById(trap.CaughtId);
                trap.HoldTimer -= Time.fixedDeltaTime;

                if (caught == null || !caught.IsAlive() || trap.HoldTimer <= 0f)
                {
                    ApplyRelease(trap.CaughtId, restoreSpeed: caught != null && caught.IsAlive());
                    traps.Remove(trap);
                    changed = true;
                }
                else
                {
                    CaughtRemaining[trap.CaughtId] = trap.HoldTimer;
                }
            }
        }

        if (changed) SendRpc();
    }


    void ApplyCatch(byte targetId, float holdTime)
    {
        var target = PlayerCatch.GetPlayerById(targetId);
        if (target == null) return;

        if (!SavedSpeed.ContainsKey(targetId))
            SavedSpeed[targetId] = Main.AllPlayerSpeed.GetValueOrDefault(targetId, Main.NormalOptions.PlayerSpeedMod);
        Main.AllPlayerSpeed[targetId] = Main.MinSpeed;
        target.MarkDirtySettings();

        CaughtRemaining[targetId] = holdTime;
        TargetArrow.Add(Player.PlayerId, targetId);
        knownCaught.Add(targetId);

        if (Is(PlayerControl.LocalPlayer))
        {
            Player.KillFlash(true);
            UtilsGameLog.AddGameLog("Spider", $"{UtilsName.GetPlayerColor(target)} が巣にかかった");
        }
    }

    void ApplyRelease(byte targetId, bool restoreSpeed)
    {
        if (restoreSpeed && SavedSpeed.TryGetValue(targetId, out var speed))
        {
            Main.AllPlayerSpeed[targetId] = speed;
            PlayerCatch.GetPlayerById(targetId)?.MarkDirtySettings();
        }
        SavedSpeed.Remove(targetId);
        CaughtRemaining.Remove(targetId);
        TargetArrow.Remove(Player.PlayerId, targetId);
        knownCaught.Remove(targetId);
    }

    public override void OnStartMeeting()
    {
        traps.RemoveAll(t => t.CaughtId == byte.MaxValue);
    }

    public override void OnLeftPlayer(PlayerControl player)
    {
        traps.RemoveAll(t => t.CaughtId == player.PlayerId);
        if (knownCaught.Contains(player.PlayerId))
            ApplyRelease(player.PlayerId, restoreSpeed: false);
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer.PlayerId != seen.PlayerId || !Is(seer) || !Player.IsAlive()) return "";
        var cd = Mathf.CeilToInt(Mathf.Max(0f, cooldownTimer));
        return isForHud ? "" : $"<size=60%>Pet:トラップ設置 ({cd}s)</size>";
    }

    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        CustomRoleManager.LowerOthers.Add(GetCaughtLowerText);
    }

    // クモの巣に捕まっている本人視点のロワーテキスト。役職に関係なく全プレイヤーに効く(CustomRoleManager.LowerOthers経由)。
    static string GetCaughtLowerText(PlayerControl seer, PlayerControl seen, bool isForMeeting, bool isForHud)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (!CaughtRemaining.TryGetValue(seen.PlayerId, out var remaining) || remaining <= 0f) return "";
        return $"<size=60%><color=#8B4513>クモの巣に引っかかった ({Mathf.CeilToInt(remaining)}s)</color></size>";
    }

    void SendRpc()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)0);
        sender.Writer.Write(cooldownTimer);

        var caughtList = traps.Where(t => t.CaughtId != byte.MaxValue).ToArray();
        sender.Writer.Write(caughtList.Length);
        foreach (var trap in caughtList)
        {
            sender.Writer.Write(trap.CaughtId);
            sender.Writer.Write(trap.HoldTimer);
        }
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
            cooldownTimer = reader.ReadSingle();
            int count = reader.ReadInt32();

            var nowCaught = new HashSet<byte>();
            for (int i = 0; i < count; i++)
            {
                byte caughtId = reader.ReadByte();
                float timer = reader.ReadSingle();
                nowCaught.Add(caughtId);

                if (!knownCaught.Contains(caughtId))
                    ApplyCatch(caughtId, timer);
                else
                    CaughtRemaining[caughtId] = timer;
            }

            foreach (var id in knownCaught.ToArray())
            {
                if (!nowCaught.Contains(id))
                    ApplyRelease(id, restoreSpeed: true);
            }
        }
        else if (rpcType == 1)
        {
            if (AmongUsClient.Instance.AmHost)
                PlaceTrap();
        }
    }
}
*/