/*using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Vanilla;
using UnityEngine;
using static Sentry.MeasurementUnit;

namespace TownOfHost.Roles.Crewmate;

public sealed class Gatekeeper : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gatekeeper),
            player => new Gatekeeper(player),
            CustomRoles.Gatekeeper,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            39100,
            SetupOptionItem,
            "gt",
            "#dc143c",
            (6, 4),
            introSound: () => GetIntroSound(RoleTypes.Tracker)
        );
    public Gatekeeper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Cooldown = OptionCooldown.GetFloat();
        NoiseRoom = SystemTypes.Dropship;
        IsNoised = false;
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Cooldown;
        AURoleOptions.EngineerInVentMaxTime = 1;
    }
    private static OptionItem OptionCooldown;
    private static OptionItem OptionCanseeNoiseAllPlayer;
    private static OptionItem OptionTeleport;
    SystemTypes NoiseRoom;
    bool IsNoised;
    static HashSet<Gatekeeper> Gatekeepers = new();
    Vector2 pos;
    enum OptionName
    {
        GatekeeperCanseeNoiseAllPlayer,
        GateKeepeeCanTeleport,
    }
    private static float Cooldown;
    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0.5f, 180f, 0.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanseeNoiseAllPlayer = BooleanOptionItem.Create(RoleInfo, 11, OptionName.GatekeeperCanseeNoiseAllPlayer, false, false);
        OptionTeleport = BooleanOptionItem.Create(RoleInfo, 12, OptionName.GateKeepeeCanTeleport, false, false);
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!IsNoised)
        {
            NoiseRoom = Player.GetPlainShipRoom().RoomId;
            pos = Player.transform.position;
        }
        else
        {
            Player.RpcSnapToForced(pos);
        }
        return false;
    }
    public override string GetAbilityButtonText() => GetString("GatekeeperAbility");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Gatekeeper_Ability";
        return true;
    }
    public override void AfterMeetingTasks()
    {
        NoiseRoom = SystemTypes.Dropship;
        IsNoised = false;
    }
    public static void CanAbility(PlayerControl target, PlainShipRoom room)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (OptionCanseeNoiseAllPlayer.GetBool())
        {
            bool CanNoise = false;
            foreach (var gt in Gatekeepers)
            {
                if (gt.Player.IsAlive() && gt.NoiseRoom == room.RoomId)
                {
                    CanNoise = true;
                    if (OptionTeleport.GetBool())
                    {
                        gt.Player.RpcSnapToForced(gt.pos);
                    }
                    gt.IsNoised = true;
                    _ = new LateTask(() =>
                    {
                        gt.IsNoised = false;
                    }, Noisemaker.NoisemakerAlertDuration.GetFloat(), "GateKeeper_Noise", true);
                    break;
                }
                else if (gt.NoiseRoom != room.RoomId)
                {
                    Logger.Info($"部屋が違います　NoiseRoom:{gt.NoiseRoom}room.RoomId:{room.RoomId}", "GateKeeper");
                }
            }
            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                if (p.IsAlive() && CanNoise)
                {
                    if (p == PlayerControl.LocalPlayer)
                        target.StartCoroutine(target.CoSetRole(RoleTypes.Noisemaker, true));
                    else
                        target.RpcSetRoleDesync(RoleTypes.Noisemaker, p.GetClientId());
                    target.SyncSettings();
                }
            }
        }
        else
        {
            foreach (var gt in Gatekeepers)
            {
                if (gt.Player.IsAlive() && gt.NoiseRoom == room.RoomId)
                {
                    if (gt.Player == PlayerControl.LocalPlayer)
                        target.StartCoroutine(target.CoSetRole(RoleTypes.Noisemaker, true));
                    else
                        target.RpcSetRoleDesync(RoleTypes.Noisemaker, gt.Player.GetClientId());
                    target.SyncSettings();
                }
                else if (gt.NoiseRoom != room.RoomId)
                {
                    Logger.Info($"部屋が違います　NoiseRoom:{gt.NoiseRoom}room.RoomId:{room.RoomId}", "GateKeeper");
                }
            }
        }
    }

}*/