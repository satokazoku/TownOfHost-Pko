/*using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

/// <summary>
/// プレイヤーが死亡した瞬間にスキン（色・帽子・スキン・バイザー）をランダム変更するパッチ。
/// Camouflager.cs の CustomRpcSender パターンを参考に実装。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class RandomSkinOnDeathPatch
{
    // ─── アセット定義 ──────────────────────────────────────────────
    // Among Us 標準カラー数（0〜17）
    private const int ColorCount = 18;

    // ベースゲームの帽子 ID（自由に追加可）
    private static readonly string[] HatIds =
    [
        "hat_None",         "hat_Fedora",       "hat_Baseball",     "hat_Anarchist",
        "hat_Antenna",      "hat_Ushanka",       "hat_Hardhat",      "hat_Bush",
        "hat_Balloon",      "hat_Bear",          "hat_Cheese",       "hat_Chef",
        "hat_Crown",        "hat_Eyebrows",      "hat_Flower",       "hat_Goggles",
        "hat_Headphones",   "hat_Military",      "hat_Mini_Crewmate","hat_PlainHat",
        "hat_Police",       "hat_Sheriff",       "hat_Stickman",     "hat_Toppat",
        "hat_WinterHat",    "hat_Wizard",        "hat_Wolf",         "hat_Visor",
        "hat_Snowman",      "hat_ElfHat",        "hat_Santa",        "hat_Egg",
    ];

    // ベースゲームのスキン ID
    private static readonly string[] SkinIds =
    [
        "skin_None",        "skin_Astronaut",    "skin_Captain",     "skin_Mechanic",
        "skin_Military",    "skin_Police",       "skin_Science",     "skin_Suit",
        "skin_Tarmac",      "skin_Winter",       "skin_Archae",      "skin_Miner",
        "skin_Security",    "skin_Black",        "skin_Prisoner",    "skin_Hazmat",
    ];

    // ベースゲームのバイザー ID
    private static readonly string[] VisorIds =
    [
        "visor_EmptyVisor", "visor_Crack",       "visor_Flames",     "visor_HalfView",
        "visor_LittleOrca", "visor_Monocle",     "visor_BubbleChatVisor",
        "visor_AnimeCatVisor",                   "visor_EyebrowVisor",
    ];

    // ─── パッチ本体 ────────────────────────────────────────────────
    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (__instance == null) return;
        if (!GameStates.IsInGame) return;

        ApplyRandomSkin(__instance);
    }

    // ─── ランダムスキン適用 ───────────────────────────────────────
    private static void ApplyRandomSkin(PlayerControl pc)
    {
        if (pc?.Data == null) return;

        var rng = IRandom.Instance;

        byte color = (byte)rng.Next(ColorCount);
        string hat = HatIds[rng.Next(HatIds.Length)];
        string skin = SkinIds[rng.Next(SkinIds.Length)];
        string visor = VisorIds[rng.Next(VisorIds.Length)];

        // ─ Camouflager.cs と同じ CustomRpcSender パターンで全クライアントに送信 ─
        var sender = CustomRpcSender.Create($"RandomSkinOnDeath({pc.Data.GetLogPlayerName()})");

        // 色
        pc.SetColor(color);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetColor)
            .Write(pc.Data.NetId)
            .Write(color)
            .EndRpc();

        // 帽子
        pc.SetHat(hat, color);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetHatStr)
            .Write(hat)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))
            .EndRpc();

        // スキン
        pc.SetSkin(skin, color);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetSkinStr)
            .Write(skin)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
            .EndRpc();

        // バイザー
        pc.SetVisor(visor, color);
        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetVisorStr)
            .Write(visor)
            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
            .EndRpc();

        sender.SendMessage();

        // ─ Camouflage キャッシュを更新して RpcSetSkin で元に戻されないようにする ─
        UpdateCamouflageCache(pc, color, hat, skin, visor);

        Logger.Info(
            $"[RandomSkinOnDeath] {pc.Data.GetLogPlayerName()} → " +
            $"color={color} hat={hat} skin={skin} visor={visor}",
            "RandomSkinOnDeath");
    }

    // ─── Camouflage キャッシュ更新（PetActionManager と同じパターン） ─────
    private static void UpdateCamouflageCache(
        PlayerControl pc, byte color, string hat, string skin, string visor)
    {
        if (!Camouflage.PlayerSkins.TryGetValue(pc.PlayerId, out var outfit)) return;

        outfit.ColorId = color;
        outfit.HatId = hat;
        outfit.SkinId = skin;
        outfit.VisorId = visor;

        Camouflage.PlayerSkins[pc.PlayerId] = outfit;
    }
}
*/