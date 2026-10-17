using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Locksmith : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Locksmith),
            player => new Locksmith(player),
            CustomRoles.Locksmith,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            12933,
            SetupOptionItem,
            "ls",
            "#DAA520",
            (9, 7),
            introSound: () => GetIntroSound(RoleTypes.Engineer),
            from: From.TownOfHost_Temp,
            isNewRole: true
        );

    public Locksmith(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        count = OptionCount.GetInt();
        cooldown = OptionCooldown.GetFloat();
        canOpenDoor = OptionCanOpenDoor.GetBool();
        IsInfinity = count is 0;

        sealedVents = new List<int>();
        banishedPlayers = new Dictionary<byte, float>();
        isBooting = false;
        cooldownTimer = 0f;
    }

    private static OptionItem OptionCount;
    private static OptionItem OptionCooldown;
    private static OptionItem OptionCanOpenDoor;

    private float cooldown;
    private float cooldownTimer;
    static bool canOpenDoor;
    static bool IsInfinity;
    int count;

    public static List<int> SealedVentsStatic = new List<int>();
    private List<int> sealedVents;
    private Dictionary<byte, float> banishedPlayers;
    private bool isBooting;

    enum OptionName
    {
        OptionCount,
        Cooldown,
        LocksmithCanOpenDoor
    }

    private static void SetupOptionItem()
    {
        OptionCount = IntegerOptionItem.Create(
            RoleInfo, 1310, OptionName.OptionCount,
            new(0, 5, 1), 2, false)
            .SetZeroNotation(OptionZeroNotation.Infinity);

        OptionCooldown = IntegerOptionItem.Create(
            RoleInfo, 1311, OptionName.Cooldown,
            new(0, 180, 1), 30, false);

        OptionCanOpenDoor = BooleanOptionItem.Create(
            RoleInfo, 1312, OptionName.LocksmithCanOpenDoor,
            true, false);
    }

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!CanUseAbility || cooldownTimer > 0f) return false;
        if (sealedVents.Contains(ventId)) return false;

        if (AmongUsClient.Instance.AmHost && !GameStates.CalledMeeting)
        {
            isBooting = true;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.PlayerId == Player.PlayerId ||
                    p.Data.IsDead) continue;

                if (p.inVent && p.MyPhysics != null)
                {
                    p.MyPhysics.RpcBootFromVent(ventId);
                    p.inVent = false;

                    var allVents = ShipStatus.Instance?.AllVents;
                    if (allVents != null)
                    {
                        var ventObj = allVents.FirstOrDefault(
                            v => v.Id == ventId);
                        if (ventObj != null && p.NetTransform != null)
                        {
                            Vector2 escapePos =
                                (Vector2)ventObj.transform.position +
                                new Vector2(0f, -0.1f);
                            p.NetTransform.SnapTo(escapePos);
                        }
                    }
                    if (!banishedPlayers.ContainsKey(p.PlayerId))
                    {
                        banishedPlayers.Add(p.PlayerId, 1.5f);
                    }
                }
            }
            isBooting = false;
        }

        sealedVents.Add(ventId);
        if (!SealedVentsStatic.Contains(ventId))
        {
            SealedVentsStatic.Add(ventId);
        }

        if (!IsInfinity)
            count--;

        cooldownTimer = cooldown;
        SendRPC();

        Player.KillFlash(false);

        if (physics != null)
        {
            Player.inVent = false;
            physics.RpcExitVent(ventId);
        }

        return false;
    }

    public static bool OnEnterVentOthersHook(
        PlayerPhysics physics, int ventId)
    {
        if (SealedVentsStatic.Contains(ventId))
        {
            if (physics != null && physics.myPlayer != null)
            {
                physics.RpcBootFromVent(ventId);
                physics.myPlayer.inVent = false;

                if (physics.myPlayer.NetTransform != null)
                {
                    var allVents = ShipStatus.Instance?.AllVents;
                    var vent = allVents?.FirstOrDefault(
                        v => v.Id == ventId);
                    if (vent != null)
                    {
                        Vector2 targetPos =
                            (Vector2)vent.transform.position +
                            new Vector2(0f, -0.1f);
                        physics.myPlayer.NetTransform.SnapTo(targetPos);
                    }
                }
            }
            return true;
        }
        return false;
    }

    public override void OnVentilationSystemUpdate(
        PlayerControl user, VentilationSystem.Operation Operation,
        int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || isBooting) return;
        if (user == null || Is(user)) return;

        if (sealedVents.Contains(ventId))
        {
            if (banishedPlayers.ContainsKey(user.PlayerId) &&
                banishedPlayers[user.PlayerId] > 0f)
            {
                user.inVent = false;
                return;
            }

            if (user.MyPhysics != null)
            {
                isBooting = true;
                user.MyPhysics.RpcBootFromVent(ventId);
                user.inVent = false;

                if (!banishedPlayers.ContainsKey(user.PlayerId))
                {
                    banishedPlayers.Add(user.PlayerId, 1.5f);
                }
                else
                {
                    banishedPlayers[user.PlayerId] = 1.5f;
                }

                var allVents = ShipStatus.Instance?.AllVents;
                if (allVents != null)
                {
                    var vent = allVents.FirstOrDefault(v => v.Id == ventId);
                    if (vent != null && user.NetTransform != null)
                    {
                        Vector2 targetPos =
                            (Vector2)vent.transform.position +
                            new Vector2(0f, -0.1f);
                        user.NetTransform.SnapTo(targetPos);
                    }
                }
                isBooting = false;
            }
        }
    }
    public override void OnReportDeadBody(
    PlayerControl reporter, NetworkedPlayerInfo target)
    {
        banishedPlayers.Clear();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (cooldownTimer > 0f)
            cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.fixedDeltaTime);

        if (GameStates.IsInTask && AmongUsClient.Instance.AmHost &&
            banishedPlayers.Count > 0)
        {
            var keys = banishedPlayers.Keys.ToList();
            foreach (var key in keys)
            {
                if (banishedPlayers[key] > 0f)
                {
                    banishedPlayers[key] -= Time.fixedDeltaTime;
                }
            }
        }

        if (!canOpenDoor || !AmongUsClient.Instance.AmHost ||
            !GameStates.IsInTask || Player == null) return;

        var shipStatus = ShipStatus.Instance;
        DoorsSystemType doorsSystem = null;
        if (shipStatus?.Systems.TryGetValue(
            SystemTypes.Doors, out var system) == true)
        {
            doorsSystem = system.TryCast<DoorsSystemType>();
        }

        // Keep door opening independent of the system lookup. Some maps or
        // game states expose AllDoors before the doors system is available.
        if (shipStatus != null && shipStatus.AllDoors != null)
        {
            bool openedDoor = false;

            foreach (var door in shipStatus.AllDoors)
            {
                if (door == null) continue;

                if (!door.IsOpen && Vector2.Distance(
                    Player.transform.position,
                    door.transform.position) < 2.0f)
                {
                    door.SetDoorway(true);
                    openedDoor = true;
                    Logger.Info(
                        $"ドアを自動解錠: {door.Room}",
                        "Locksmith");
                }
            }

            // SetDoorway changes the local door state. Marking the doors
            // system dirty makes the host replicate that state to clients.
            if (openedDoor && doorsSystem != null)
                doorsSystem.IsDirty = true;
        }
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(count);
        sender.Writer.Write(cooldownTimer);
        sender.Writer.Write(sealedVents.Count);
        foreach (var ventId in sealedVents)
        {
            sender.Writer.Write(ventId);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        count = reader.ReadInt32();
        cooldownTimer = Mathf.Max(0f, reader.ReadSingle());
        int sealedCount = reader.ReadInt32();
        sealedVents.Clear();
        SealedVentsStatic.Clear();
        for (int i = 0; i < sealedCount; i++)
        {
            int id = reader.ReadInt32();
            sealedVents.Add(id);
            SealedVentsStatic.Add(id);
        }
    }

    public override bool CanVentMoving(
        PlayerPhysics physics, int ventId) => false;

    public override string GetProgressText(
        bool comms = false, bool gamelog = false) =>
        IsInfinity ? "" : Utils.ColorString(
            CanUseAbility ? RoleInfo.RoleColor : Color.gray,
            $"({count})"
        );

    public bool CanUseAbility => IsInfinity || count > 0;
    public override bool CanClickUseVentButton =>
        CanUseAbility && cooldownTimer <= 0f;

    public override string GetAbilityButtonText() =>
        CanUseAbility ? GetString("LocksmithAbility") : "";

    public override bool OverrideAbilityButton(out string text)
    {
        if (CanUseAbility && cooldownTimer <= 0f)
        {
            text = "Locksmith_Ability";
            return true;
        }
        text = "";
        return false;
    }

    [HarmonyPatch(typeof(CustomRoleManager), nameof(CustomRoleManager.Initialize))]
    private class ResetLocksmithDataPatch
    {
        static void Prefix()
        {
            SealedVentsStatic.Clear();
        }
    }

    public static Dictionary<int, Achievement> achievements =
        new Dictionary<int, Achievement>();

    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 0, 0, 0);
        achievements.Add(0, n1);

        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthersHook);
    }
}
