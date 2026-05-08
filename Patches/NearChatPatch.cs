/*using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

/// <summary>
/// タスクターン中チャットのフィルタリング。
/// AddChat をフックして距離フィルタを掛ける。
/// ホストからのシスメ（近チャ本体）は常に通す。
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class NearChatAddChatPatch
{
    public static bool Prefix(PlayerControl sourcePlayer, ref string chatText)
    {
        if (!GameStates.IsInTask || MeetingHud.Instance != null) return true;
        if (!Options.OptionGameChatSetting.GetBool()) return true;
        if (sourcePlayer == null) return true;

        var local = PlayerControl.LocalPlayer;
        if (local == null) return true;

        // ★ GM・死亡者は全チャット閲覧可能
        if (local.Is(CustomRoles.GM)) return true;
        if (!local.IsAlive()) return true;

        // ★ ホストからのシスメ（OnReceiveChatのSendMessage経由）は常に通す
        bool isFromHost = sourcePlayer.GetClientId() == AmongUsClient.Instance.HostId;
        if (isFromHost) return true;

        // ★ コマンドは非表示
        if (chatText.TrimStart().StartsWith("/cmd", System.StringComparison.OrdinalIgnoreCase))
            return false;

        // ★ 通常チャット無効 → 全員ブロック
        if (!Options.OptionGameChatNormalChat.GetBool())
            return false;

        // ★ 近チャ有効 → 生のRPCチャットは全員ブロック
        //    （近隣分はホストがシスメで送り直す）
        if (Options.OptionGameChatNormalNearChat.GetBool())
            return false;

        return true;
    }
}

/// <summary>
/// チャット送信時に役職名が被さって読めない問題を修正。
/// RpcSendChat の AddChat 呼び出し前に nameText を一時リセット。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
public static class NearChatNameResetPatch
{
    // ★ Prefix で nameText を一時的にプレイヤー名のみにする
    // 優先度 High にして RpcSendChatPatch より先に実行
    [HarmonyPriority(Priority.High)]
    public static void Prefix(PlayerControl __instance)
    {
        if (!GameStates.IsInTask) return;
        if (!Options.OptionGameChatSetting.GetBool()) return;
        if (__instance == null || !__instance.AmOwner) return;
        if (__instance.cosmetics?.nameText == null) return;

        // ★ Camouflage から実名を取得
        string baseName = __instance.Data?.PlayerName ?? __instance.name;
        if (Camouflage.PlayerSkins.TryGetValue(__instance.PlayerId, out var skin)
            && !string.IsNullOrEmpty(skin.PlayerName))
            baseName = skin.PlayerName;

        if (string.IsNullOrEmpty(baseName)) return;

        // ★ 一時的にプレイヤー名のみにリセット（AddChat がこの名前を表示する）
        __instance.cosmetics.nameText.text = baseName;
    }

    // ★ Postfix で NotifyRoles して名前を復元
    [HarmonyPriority(Priority.High)]
    public static void Postfix(PlayerControl __instance)
    {
        if (!GameStates.IsInTask) return;
        if (__instance == null || !__instance.AmOwner) return;

        _ = new LateTask(() =>
        {
            if (PlayerControl.LocalPlayer == null) return;
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: PlayerControl.LocalPlayer);
        }, 0.15f, "NearChatNameRestore", true);
    }
}*/