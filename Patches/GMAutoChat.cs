using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches;

/// <summary>
/// GM中、会議開始時にチャットを自動で開くPatch。
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class GMAutoChatPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Options.OptionGMAutoChat.GetBool()) return;
        if (!PlayerControl.LocalPlayer.Is(CustomRoles.GM)) return;

        // ★ ChatController.Toggle() でチャットを開く
        var chat = DestroyableSingleton<HudManager>.Instance?.Chat;
        if (chat == null) return;

        // すでに開いている場合は何もしない
        if (chat.IsOpenOrOpening) return;

        chat.Toggle();
    }
}