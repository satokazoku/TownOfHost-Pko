using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace TownOfHost.Modules;

/// <summary>
/// 試合中にチャットバブルを表示するクラス。
/// EHRのChatBubbleShowerを参考にTOH-P向けに移植。
/// </summary>
public static class ChatBubbleShower
{
    private static long LastShowTs = 0L;
    private static readonly HashSet<(string Message, string Title)> Queue = new();

    public static void Update()
    {
        try
        {
            if (Queue.Count == 0) return;
            if (ExileController.Instance != null) return;
            if (!HudManager.InstanceExists || HudManager.Instance?.Chat == null) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int wait = GameStates.IsInTask ? 8 : 4;
            if (LastShowTs + wait > now) return;
            LastShowTs = now;

            var (message, title) = Queue.First();
            Queue.Remove((message, title));

            var chat = HudManager.Instance.Chat;

            if (GameStates.IsMeeting || chat.IsOpenOrOpening)
            {
                ShowBubble(chat, message, title);
            }
            else if (GameStates.IsLobby)
            {
                Utils.SendMessage(message, PlayerControl.LocalPlayer.PlayerId, title);
            }
            else
            {
                // ★ 試合中・チャット非表示時はチャットを開いてから表示
                chat.SetVisible(true);
                ShowBubble(chat, message, title);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "ChatBubbleShower");
        }
    }

    private static void ShowBubble(ChatController chat, string message, string title)
    {
        try
        {
            var data = PlayerControl.LocalPlayer.Data;
            var bubble = chat.GetPooledBubble();

            bubble.transform.SetParent(chat.scroller.Inner);
            bubble.transform.localScale = Vector3.one;
            bubble.SetCosmetics(data);

            // ★ プレイヤーアイコンを非表示
            var poolablePlayer = bubble.gameObject.transform.Find("PoolablePlayer");
            if (poolablePlayer != null) poolablePlayer.gameObject.SetActive(false);

            if (bubble.ColorBlindName != null)
                bubble.ColorBlindName.gameObject.SetActive(false);

            bubble.SetLeft();

            // ★ テキスト・名前の位置補正
            var nameTextObj = bubble.gameObject.transform.Find("NameText (TMP)");
            if (nameTextObj != null)
                nameTextObj.transform.localPosition += new Vector3(-0.7f, 0f);

            var chatTextObj = bubble.gameObject.transform.Find("ChatText (TMP)");
            if (chatTextObj != null)
                chatTextObj.transform.localPosition += new Vector3(-0.7f, 0f);

            chat.SetChatBubbleName(bubble, data, data.IsDead, false,
                Palette.PlayerColors[data.DefaultOutfit.ColorId]);

            bubble.SetText(message);
            bubble.AlignChildren();

            // ★ タイトル（役職名など）を上書き
            if (bubble.NameText != null)
                bubble.NameText.text = title;

            // ★ テキスト色・背景色
            if (chatTextObj != null)
                chatTextObj.GetComponent<TextMeshPro>().color = Color.white;

            var bg = bubble.transform.Find("Background")?.GetComponent<SpriteRenderer>();
            if (bg != null)
                bg.color = new Color(0.05f, 0.05f, 0.05f, 1f);

            chat.AlignAllBubbles();
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "ChatBubbleShower.ShowBubble");
        }
    }

    /// <summary>
    /// 試合中にチャットバブルでメッセージを表示する
    /// </summary>
    public static void Show(string message, string title = "")
    {
        if (string.IsNullOrEmpty(title))
            title = $"<{Main.ModColor}>System</color>";

        Queue.Add((message, title));
    }

    public static void Reset()
    {
        Queue.Clear();
        LastShowTs = 0L;
    }
}