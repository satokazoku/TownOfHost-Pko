// ペットの自動破棄機能はK対応来るまで一時的にこうしてます。対応が来たらTOHK処理に戻します。

using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
internal static class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;
        if (__instance.petting) return true;

        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId))
            LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[__instance.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return true;
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        __instance.petting = false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if ((RpcCalls)callID != RpcCalls.Pet) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby) return;

        var pc = __instance.myPlayer;
        if (pc == null || !pc.IsAlive()) return;

        if (!LastProcess.ContainsKey(pc.PlayerId))
            LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[pc.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

        LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Logger.Info($"{pc.Data?.GetLogPlayerName()} がペットを撫でた", "PetActionPatch");

        OnPetUse(pc);
    }

    private static void OnPetUse(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby || GameStates.IsMeeting) return;

        if (pc.inVent || pc.inMovingPlat || pc.onLadder || pc.walkingToVent) return;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;

        if (PetActionManager.Handlers.TryGetValue(pc.PlayerId, out var handler))
        {
            handler.Invoke();
            Logger.Info($"{pc.Data?.GetLogPlayerName()} のOnPet実行", "PetActionPatch");
        }
    }
}

public static class PetsHelper
{
    public static void SetPet(PlayerControl pc, string petId)
    {
        if (pc == null) return;
        pc.RpcSetPet(petId);
    }

    public static void RemovePet(PlayerControl pc)
    {
        // Data.IsDeadは会議中にアンチブラックアウトで偽装されるため、PlayerState由来のIsAliveで判定する
        if (pc?.Data == null || pc.IsAlive()) return;
        if (pc.CurrentOutfit.PetId == "") return;

        SetPet(pc, "");

        if (Camouflage.PlayerSkins.TryGetValue(pc.PlayerId, out var outfit))
        {
            outfit.PetId = "";
            Camouflage.PlayerSkins[pc.PlayerId] = outfit;
        }

        Logger.Info($"{pc.Data.GetLogPlayerName()} が死亡したのでペットを外しました", "PetActionPatch");
    }
}

#region ペットを自動破棄
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class RemoveDeadPlayersPetsOnMeetingStartPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var pc in PlayerCatch.AllPlayerControls)
            PetsHelper.RemovePet(pc);
    }
}
#endregion

public static class PetActionManager
{
    private const string DefaultPetIdForPetAction = "pet_test";

    public static readonly Dictionary<byte, System.Action> Handlers = new();

    public static void Register(byte playerId, System.Action action)
    {
        Handlers[playerId] = action;
        EnsureDefaultPet(playerId);
    }

    public static void Unregister(byte playerId)
    {
        Handlers.Remove(playerId);
    }

    public static void Reset()
    {
        Handlers.Clear();
    }

    public static void EnsureDefaultPet(byte playerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInGame || GameStates.IsLobby) return;

        var pc = PlayerCatch.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || HasPet(pc)) return;

        PetsHelper.SetPet(pc, DefaultPetIdForPetAction);

        if (Camouflage.PlayerSkins.TryGetValue(playerId, out var outfit))
        {
            outfit.PetId = DefaultPetIdForPetAction;
            Camouflage.PlayerSkins[playerId] = outfit;
        }

        Logger.Info($"{pc.Data?.GetLogPlayerName()} にペットを自動付与: {DefaultPetIdForPetAction}", "PetActionPatch");
    }

    private static bool HasPet(PlayerControl pc)
    {
        string petId = pc.Data?.DefaultOutfit?.PetId ?? pc.CurrentOutfit?.PetId ?? "";
        if (string.IsNullOrEmpty(petId)) return false;

        petId = petId.ToLowerInvariant();
        return petId != "none"
            && petId != "pet_none"
            && petId != "pet_emptypet"
            && petId != "pet_enmptypet";
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
internal static class AutoPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() => AssignPets(), 0.6f, "AutoPetAssignAfterIntro", true);
        _ = new LateTask(() => AssignPets(), 2f, "AutoPetAssignAfterIntroRetry", true);
    }

    private static void AssignPets()
    {
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            PetActionManager.EnsureDefaultPet(pc.PlayerId);
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class AfterMeetingPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
                PetActionManager.EnsureDefaultPet(pc.PlayerId);
        }, 1.5f, "AfterMeetingPetAssign", true);
    }
}
