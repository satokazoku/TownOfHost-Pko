/*using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class AfterMeetingPetShapeshiftPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(
            PetShapeshiftAssigner.Run,
            1.5f,
            "PetShapeshiftAssign",
            true);
    }
}

public static class PetShapeshiftAssigner
{
    public const string DefaultPetId = "pet_test";

    private const float StepDelay = 0.25f;

    public static void Run()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInGame || GameStates.IsLobby || GameStates.IsMeeting) return;

        var host = PlayerControl.LocalPlayer;
        if (host == null) return;

        // ホストの元スキンを保存
        var saved = SaveOutfit(host);

        // ホスト以外の生存プレイヤー
        var targets = PlayerCatch.AllAlivePlayerControls
            .Where(pc => pc.PlayerId != host.PlayerId)
            .ToList();

        float delay = 0f;

        foreach (var pc in targets)
        {
            var cap = pc;
            var capD = delay;

            _ = new LateTask(() =>
            {
                if (!AmongUsClient.Instance.AmHost) return;
                if (cap == null || !cap.IsAlive()) return;
                if (GameStates.IsMeeting) return;

                // 1. ホスト → プレイヤーXのスキンに変更
                ChangeHostSkinTo(host, cap);

                // 2. ホスト自身にペットをつける
                PetsHelper.SetPet(host, DefaultPetId);

                // 3. プレイヤーXがホストに変身
                cap.RpcShapeshift(host, false);

                Logger.Info(
                    $"[PetShapeshift] {cap.Data?.GetLogPlayerName()} → ペット付与完了",
                    "PetShapeshift");

            }, capD, $"PetShift_{pc.PlayerId}", true);

            delay += StepDelay;
        }

        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (GameStates.IsMeeting) return;

            RestoreHostSkin(host, saved);
            PetsHelper.SetPet(host, DefaultPetId);

            Logger.Info("[PetShapeshift] ホストスキン復元完了", "PetShapeshift");

        }, delay + 0.1f, "PetShapeshift.Restore", true);
    }

    //ホストのスキンをプレイヤーXに合わせる
    private static void ChangeHostSkinTo(PlayerControl host, PlayerControl source)
    {
        if (host?.Data == null || source?.Data == null) return;

        var o = source.Data.DefaultOutfit;
        byte col = (byte)o.ColorId;
        string hat = o.HatId ?? "";
        string skin = o.SkinId ?? "";
        string visor = o.VisorId ?? "";

        var sender = CustomRpcSender.Create(
            $"PetShapeshift.ChangeSkin({source.Data.GetLogPlayerName()})");

        host.SetColor(col);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetColor)
            .Write(host.Data.NetId)
            .Write(col)
            .EndRpc();

        host.SetHat(hat, col);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetHatStr)
            .Write(hat)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        host.SetSkin(skin, col);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(skin)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        host.SetVisor(visor, col);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(visor)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        sender.SendMessage();

        // Camouflage キャッシュも更新（RpcSetSkin で元に戻されないよう）
        SetCamouflageOutfit(host.PlayerId, col, hat, skin, visor);
    }

    //ホストのスキンを元に戻す
    private static void RestoreHostSkin(PlayerControl host, SavedOutfit s)
    {
        if (host?.Data == null) return;

        var sender = CustomRpcSender.Create("PetShapeshift.RestoreSkin");

        host.SetColor(s.Color);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetColor)
            .Write(host.Data.NetId)
            .Write(s.Color)
            .EndRpc();

        host.SetHat(s.Hat, s.Color);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetHatStr)
            .Write(s.Hat)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        host.SetSkin(s.Skin, s.Color);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(s.Skin)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        host.SetVisor(s.Visor, s.Color);
        sender.AutoStartRpc(host.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(s.Visor)
            .Write(host.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        sender.SendMessage();
        SetCamouflageOutfit(host.PlayerId, s.Color, s.Hat, s.Skin, s.Visor);
    }

    private static void SetCamouflageOutfit(
        byte playerId, byte color, string hat, string skin, string visor)
    {
        if (!Camouflage.PlayerSkins.TryGetValue(playerId, out var outfit)) return;
        outfit.ColorId = color;
        outfit.HatId = hat;
        outfit.SkinId = skin;
        outfit.VisorId = visor;
        Camouflage.PlayerSkins[playerId] = outfit;
    }

    //ホストの元スキン保存
    private static SavedOutfit SaveOutfit(PlayerControl pc)
    {
        if (Camouflage.PlayerSkins.TryGetValue(pc.PlayerId, out var c))
            return new SavedOutfit
            {
                Color = (byte)c.ColorId,
                Hat = c.HatId ?? "",
                Skin = c.SkinId ?? "",
                Visor = c.VisorId ?? "",
                Pet = c.PetId ?? ""
            };

        var d = pc.Data?.DefaultOutfit;
        if (d == null) return default;
        return new SavedOutfit
        {
            Color = (byte)d.ColorId,
            Hat = d.HatId ?? "",
            Skin = d.SkinId ?? "",
            Visor = d.VisorId ?? "",
            Pet = d.PetId ?? ""
        };
    }

    private struct SavedOutfit
    {
        public byte Color;
        public string Hat, Skin, Visor, Pet;
    }
}*/