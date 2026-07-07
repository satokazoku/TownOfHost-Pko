using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Ghost;

public static class GhostFloodlight
{
    static GhostRoleAssingData Data;
    private static readonly int Id = 16600;

    public static OptionItem VisionAmount;
    public static OptionItem Duration;
    public static OptionItem CoolDown;
    static OptionItem AssingMadmate;
    public static List<byte> playerIdList = new();

    private static readonly Dictionary<byte, float> Timers = new();

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.GhostRoles, CustomRoles.GhostFloodlight,
            fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));

        Data = GhostRoleAssingData.Create(
            Id + 1, CustomRoles.GhostFloodlight, CustomRoleTypes.Crewmate);

        VisionAmount = FloatOptionItem.Create(
                Id + 2, "GhostFloodlightVision",
                new(0.25f, 5f, 0.25f), 2f, TabGroup.GhostRoles, false)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostFloodlight])
            .SetParentRole(CustomRoles.GhostFloodlight);

        Duration = FloatOptionItem.Create(
                Id + 3, "GhostFloodlightDuration",
                new(1f, 60f, 0.5f), 10f, TabGroup.GhostRoles, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostFloodlight])
            .SetParentRole(CustomRoles.GhostFloodlight);

        CoolDown = FloatOptionItem.Create(Id + 5, "Cooldown", new(0f, 180f, 0.5f), 25f, TabGroup.GhostRoles, false)
            .SetValueFormat(OptionFormat.Seconds).SetParent(CustomRoleSpawnChances[CustomRoles.GhostFloodlight]).SetParentRole(CustomRoles.GhostFloodlight);

        AssingMadmate = BooleanOptionItem.Create(
                Id + 4, "AssgingMadmate", false, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostFloodlight])
            .SetParentRole(CustomRoles.GhostFloodlight);
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public static void Init()
    {
        Timers.Clear();
        playerIdList = new();

        Data.SubRoleType = AssingMadmate.GetBool()
            ? CustomRoleTypes.Madmate
            : CustomRoleTypes.Crewmate;
    }

    public static void UseAbility(PlayerControl ghost, PlayerControl target)
    {
        if (ghost == null || !ghost.Is(CustomRoles.GhostFloodlight)) return;
        if (target == null || !target.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        Timers[target.PlayerId] = Duration.GetFloat();
        target.MarkDirtySettings();

        // start cooldown for ghost ability caller
        if (!playerIdList.Contains(ghost.PlayerId)) playerIdList.Add(ghost.PlayerId);


        Logger.Info(
            $"[GhostFloodlight] {ghost.Data?.GetLogPlayerName()} → " +
            $"{target.Data?.GetLogPlayerName()} に視界 {VisionAmount.GetFloat()}x / {Duration.GetFloat()}s",
            "GhostFloodlight");
    }

    public static void FixedUpdate()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;

        foreach (var id in new List<byte>(Timers.Keys))
        {
            Timers[id] -= Time.fixedDeltaTime;
            if (Timers[id] <= 0f)
            {
                Timers.Remove(id);
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc != null) pc.MarkDirtySettings();
                Logger.Info($"[GhostFloodlight] {id} の視界ブースト終了", "GhostFloodlight");
            }
        }
    }

    public static void Reset()
    {
        foreach (var id in new List<byte>(Timers.Keys))
        {
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc != null) pc.MarkDirtySettings();
        }
        Timers.Clear();
    }

    public static bool IsBoosted(byte playerId) => Timers.ContainsKey(playerId);
    public static float GetVision() => VisionAmount?.GetFloat() ?? 2f;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
internal static class GhostFloodlightTimerPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;
        GhostFloodlight.FixedUpdate();
    }
}

[HarmonyPatch(typeof(RoleBase), nameof(RoleBase.ApplyGameOptions))]
internal static class GhostFloodlightVisionPatch
{
    public static void Postfix(RoleBase __instance, IGameOptions opt)
    {
        if (__instance?.Player == null) return;
        if (!GhostFloodlight.IsBoosted(__instance.Player.PlayerId)) return;

        float vision = GhostFloodlight.GetVision();
        opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class GhostFloodlightResetPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        GhostFloodlight.Reset();
    }
}