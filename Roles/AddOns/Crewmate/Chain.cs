/*using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using UnityEngine;
using static TownOfHost.Options;
using static TownOfHost.PlayerCatch;

namespace TownOfHost;

class Chain
{
    public static Dictionary<byte, byte> ChainList = new();
    public static HashSet<byte> PenalizedPlayers = new();

    public static void Init()
    {
        SubRoleRPCSender.AddHandler(CustomRoles.Chain, ReceiveRPC);
    }

    public static void AssignAndReset()
    {
        ChainList = new();
        PenalizedPlayers = new();

        var sets = CustomRoles.Chain.GetRealCount();
        if (sets <= 0) return;

        List<PlayerControl> candidates = new();
        foreach (var pc in AllPlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role is CustomRoles.GM or CustomRoles.BakeCat) continue;
            candidates.Add(pc);
        }

        if (candidates.Count < 2) return;

        for (int i = 0; i < sets; i++)
        {
            if (candidates.Count < 2) break;

            var list = candidates.OrderBy(_ => Guid.NewGuid()).ToArray();
            var pc1 = list[IRandom.Instance.Next(list.Length)];
            candidates.Remove(pc1);

            var list2 = candidates.OrderBy(_ => Guid.NewGuid()).ToArray();
            var pc2 = list2[IRandom.Instance.Next(list2.Length)];
            candidates.Remove(pc2);

            if (pc1 is null || pc2 is null) break;

            ChainList.Add(pc1.PlayerId, pc2.PlayerId);
            ChainList.Add(pc2.PlayerId, pc1.PlayerId);

            PlayerState.GetByPlayerId(pc1.PlayerId).SetSubRole(CustomRoles.Chain);
            PlayerState.GetByPlayerId(pc2.PlayerId).SetSubRole(CustomRoles.Chain);

            RpcSetChain(pc1.PlayerId, pc2.PlayerId);

            Logger.Info($"{pc1.GetRealName()} ⛓ {pc2.GetRealName()}", "Chain");
        }
    }

    #region Options
    public static OptionItem OptionBreakDistance;
    public static OptionItem OptionSpeedPenalty;
    public static OptionItem OptionVisionPenalty;

    public static float BreakDistance => OptionBreakDistance?.GetFloat() ?? 5f;
    public static float SpeedPenalty => OptionSpeedPenalty?.GetFloat() ?? 0.5f;
    public static float VisionPenalty => OptionVisionPenalty?.GetFloat() ?? 0.3f;

    public static void SetUpChainOptions()
    {
        SetupRoleOptions(19300, TabGroup.Combinations, CustomRoles.Chain, new(1, 7, 1));

        OptionBreakDistance = FloatOptionItem.Create(76200, "ChainBreakDistance",
            new(1f, 20f, 0.5f), 5f, TabGroup.Combinations, false)
            .SetSubRoleOptionItem(CustomRoles.Chain);

        OptionSpeedPenalty = FloatOptionItem.Create(76201, "ChainSpeedPenalty",
            new(0.1f, 2f, 0.1f), 0.5f, TabGroup.Combinations, false)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetSubRoleOptionItem(CustomRoles.Chain);

        OptionVisionPenalty = FloatOptionItem.Create(76202, "ChainVisionPenalty",
            new(0.05f, 2f, 0.05f), 0.3f, TabGroup.Combinations, false)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetSubRoleOptionItem(CustomRoles.Chain);
    }
    #endregion

    public static void CheckDistance(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (player == null || !player.IsAlive()) return;
        if (!ChainList.TryGetValue(player.PlayerId, out byte partnerId)) return;

        var partner = GetPlayerById(partnerId);
        if (partner == null || !partner.IsAlive())
        {
            if (PenalizedPlayers.Remove(player.PlayerId))
                player.MarkDirtySettings();
            return;
        }

        float dist = Vector2.Distance(player.GetTruePosition(), partner.GetTruePosition());
        bool shouldPenalize = dist > BreakDistance;
        bool wasPenalized = PenalizedPlayers.Contains(player.PlayerId);

        if (shouldPenalize == wasPenalized) return;

        if (shouldPenalize)
            PenalizedPlayers.Add(player.PlayerId);
        else
            PenalizedPlayers.Remove(player.PlayerId);

        player.MarkDirtySettings();

        string msg = shouldPenalize
            ? "<color=#ff4444>⛓ 鎖が引っ張られています！速度・視界が低下します。</color>"
            : "<color=#aaaaff>⛓ 鎖の張力が緩みました。</color>";
        Utils.SendMessage(msg, player.PlayerId);
    }

    public static void ApplyOptions(IGameOptions opt, byte playerId)
    {
        if (!PenalizedPlayers.Contains(playerId)) return;

        opt.SetFloat(FloatOptionNames.PlayerSpeedMod, SpeedPenalty);
        opt.SetFloat(FloatOptionNames.CrewLightMod, VisionPenalty);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, VisionPenalty);
    }

    public static void ChainReset(byte leftId)
    {
        if (ChainList.TryGetValue(leftId, out var partnerId))
        {
            ChainList.Remove(leftId);
            ChainList.Remove(partnerId);
            PenalizedPlayers.Remove(leftId);
            PenalizedPlayers.Remove(partnerId);
        }
    }

    public static void RpcSetChain(byte playerId, byte playerId2)
    {
        using var sender = new SubRoleRPCSender(CustomRoles.Chain, playerId);
        sender.Writer.Write(playerId2);
    }

    public static void ReceiveRPC(MessageReader reader, byte playerId)
    {
        var playerId2 = reader.ReadByte();

        if (!ChainList.ContainsKey(playerId)) ChainList.Add(playerId, playerId2);
        if (!ChainList.ContainsKey(playerId2)) ChainList.Add(playerId2, playerId);

        PlayerState.GetByPlayerId(playerId).SetSubRole(CustomRoles.Chain);
        PlayerState.GetByPlayerId(playerId2).SetSubRole(CustomRoles.Chain);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
static class ChainDistancePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!CustomRoles.Chain.IsPresent()) return;
        Chain.CheckDistance(__instance);
    }
}

[HarmonyPatch(typeof(PlayerGameOptionsSender), nameof(PlayerGameOptionsSender.BuildOptions))]
static class ChainOptionsPatch
{
    public static void Postfix(PlayerGameOptionsSender __instance, IGameOptions opt)
    {
        if (!CustomRoles.Chain.IsPresent()) return;
        if (__instance?.player == null) return;
        Chain.ApplyOptions(opt, __instance.player.PlayerId);
    }
}*/