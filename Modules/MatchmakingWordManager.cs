using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using TownOfHost.Modules.ClientOptions;

using Object = UnityEngine.Object;

namespace TownOfHost.Modules;

public static class MatchmakingWordManager
{
    public const int MaxCommentLength = 1000;

    private static readonly object Sync = new();
    private static readonly string WordsFilePath = Path.Combine(Main.BaseDirectory, "words.txt");

    private static string cachedComment = "";
    private static bool loaded;

    private static SpriteRenderer popup;
    private static TextMeshPro titleText;
    private static TextMeshPro counterText;
    private static SpriteRenderer editorBox;
    private static FreeChatInputField editorField;
    private static TextBoxTMP editor;
    private static ToggleButtonBehaviour saveButton;
    private static ToggleButtonBehaviour cancelButton;

    private static readonly List<string> undoStack = new();
    private static readonly List<string> redoStack = new();
    private static bool suppressHistoryEvent;
    private static bool chatUiRefreshEnabled;
    private static float nextChatUiRefreshTime;
    private static float chatUiRefreshExpireTime;
    private const float ChatUiRefreshInterval = 3f;
    private const float ChatUiRefreshDuration = 20f;

    public static string GetCurrentWord()
    {
        EnsureLoaded();
        lock (Sync)
            return cachedComment;
    }

    public static bool TrySetFromCommand(string input, byte requesterId)
    {
        if (!TrySave(input, out var normalized, out var error))
        {
            Utils.SendMessage($"<color=#ff6666>{error}</color>", requesterId);
            return false;
        }

        Utils.SendMessage(
            $"<color=#8cffff>せーぶしたよ！！！ ({normalized.Length}/{MaxCommentLength})</color>",
            requesterId
        );
        return true;
    }

    public static void ShowEditor(byte requesterId)
    {
        EnsureLoaded();

        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud?.Chat != null)
        {
            try
            {
                // Close the chat panel, but keep ChatUI itself visible.
                hud.Chat.ForceClosed();
                ScheduleChatUiRefresh(ChatUiRefreshDuration, 0.1f);
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(MatchmakingWordManager));
            }
        }

        var options = ResolveOptionsMenu();
        if (options?.Background != null && options.Background.gameObject.activeInHierarchy)
        {
            try
            {
                options.Close();
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(MatchmakingWordManager));
            }
        }

        if (!TryEnsurePopup(options))
        {
            Utils.SendMessage("<color=#ff6666>開くのに失敗したよ</color>", requesterId);
            return;
        }

        popup.gameObject.SetActive(true);
        suppressHistoryEvent = true;
        try
        {
            editor.SetText(GetCurrentWord());
        }
        finally
        {
            suppressHistoryEvent = false;
        }
        ResetHistory(editor.text ?? "");
        UpdateCounterText();
        editor.GiveFocus();
    }

    public static void HideEditor()
    {
        if (popup != null)
            popup.gameObject.SetActive(false);

        TryRestoreChatUiNow();
        ScheduleChatUiRefresh(8f, 0f);
    }

    public static void TickChatUiRefresh()
    {
        if (!chatUiRefreshEnabled) return;
        if (Time.time < nextChatUiRefreshTime) return;

        TryRestoreChatUiNow();
        nextChatUiRefreshTime = Time.time + ChatUiRefreshInterval;

        var popupOpen = popup != null && popup.gameObject.activeInHierarchy;
        if (!popupOpen && Time.time >= chatUiRefreshExpireTime)
            chatUiRefreshEnabled = false;
    }

    public static bool TryHandleEditorHotkeys()
    {
        if (popup == null || !popup.gameObject.activeInHierarchy || editor == null || !editor.hasFocus)
            return false;

        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl) return false;

        if (Input.GetKeyDown(KeyCode.C))
        {
            GUIUtility.systemCopyBuffer = editor.text ?? "";
            return true;
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            ApplyEditorText((editor.text ?? "") + GUIUtility.systemCopyBuffer, true);
            return true;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (!TrySelectAll())
                GUIUtility.systemCopyBuffer = editor.text ?? "";
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            UndoEditorText();
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            RedoEditorText();
            return true;
        }

        return false;
    }

    private static bool TrySave(string input, out string normalized, out string error)
    {
        normalized = Normalize(input);
        if (normalized.Length > MaxCommentLength)
        {
            error = $"メッセージ量がおおすぎる {MaxCommentLength} ";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Main.BaseDirectory);
            File.WriteAllText(WordsFilePath, normalized);

            lock (Sync)
            {
                cachedComment = normalized;
                loaded = true;
            }

            error = "";
            return true;
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(MatchmakingWordManager));
            error = "ほぞんにしっぱいしたよ！！";
            return false;
        }
    }

    private static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (loaded) return;
            loaded = true;
        }

        try
        {
            if (!File.Exists(WordsFilePath))
            {
                lock (Sync) cachedComment = "";
                return;
            }

            var text = File.ReadAllText(WordsFilePath);
            lock (Sync) cachedComment = Normalize(text);
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(MatchmakingWordManager));
            lock (Sync) cachedComment = "";
        }
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\r\n", "\n").Trim();
    }

    private static OptionsMenuBehaviour ResolveOptionsMenu()
    {
        var options = OptionsMenuBehaviourStartPatch.Instance;
        if (options == null)
            options = HudManager.Instance?.GameMenu;
        if (options == null)
            options = Object.FindObjectOfType<OptionsMenuBehaviour>();

        if (options != null && OptionsMenuBehaviourStartPatch.Instance == null)
            OptionsMenuBehaviourStartPatch.Instance = options;

        return options;
    }

    private static bool TryEnsurePopup(OptionsMenuBehaviour options)
    {
        if (popup != null && editor != null)
            return true;

        if (options == null || options.DisableMouseMovement == null || options.Background == null)
            return false;

        var parent = ClientActionItem.CustomBackground?.transform?.parent;
        if (parent == null || !parent.gameObject.activeInHierarchy)
            parent = HudManager.Instance != null ? HudManager.Instance.transform : options.transform;

        popup = Object.Instantiate(options.Background, parent);
        popup.name = "MatchmakingWordPopup";
        popup.transform.localPosition = new(0f, 0f, -20f);
        popup.transform.localScale = new(1.65f, 0.92f, 1f);
        popup.gameObject.SetActive(false);

        titleText = Object.Instantiate(options.DisableMouseMovement.Text, popup.transform);
        titleText.name = "MatchmakingWordTitle";
        titleText.transform.localPosition = new(0f, 2.30f, -1f);
        titleText.transform.localScale = new(0.72f, 1f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.text = $"マッチメイキングのてきすと\n(Max {MaxCommentLength} 数字まで)";

        counterText = Object.Instantiate(options.DisableMouseMovement.Text, popup.transform);
        counterText.name = "MatchmakingWordCounter";
        counterText.transform.localPosition = new(0f, -1.78f, -1f);
        counterText.transform.localScale = new(0.62f, 0.82f, 1f);
        counterText.alignment = TextAlignmentOptions.Center;

        editor = CreateEditorTextBox(new(0f, 0.28f, -1.1f), popup.transform, options.DisableMouseMovement.Text, options);
        if (editor == null)
            return false;

        saveButton = Object.Instantiate(options.DisableMouseMovement, popup.transform);
        saveButton.name = "MatchmakingWordSave";
        saveButton.transform.localPosition = new(0.85f, 1.98f, -2f);
        saveButton.transform.localScale = new(0.34f, 0.74f, 1f);
        saveButton.Text.text = "保存";
        var savePb = saveButton.GetComponent<PassiveButton>();
        savePb.OnClick = new();
        savePb.OnClick.AddListener((Action)(() =>
        {
            if (TrySave(editor?.text ?? "", out var normalized, out var error))
            {
                suppressHistoryEvent = true;
                try
                {
                    editor.SetText(normalized);
                }
                finally
                {
                    suppressHistoryEvent = false;
                }
                ResetHistory(normalized);
                UpdateCounterText();
                Utils.SendMessage($"<color=#8cffff>せーぶしたぜ ({normalized.Length}/{MaxCommentLength})</color>", PlayerControl.LocalPlayer.PlayerId);
                HideEditor();
            }
            else
            {
                Utils.SendMessage($"<color=#ff6666>{error}</color>", PlayerControl.LocalPlayer.PlayerId);
                UpdateCounterText();
            }
        }));

        cancelButton = Object.Instantiate(options.DisableMouseMovement, popup.transform);
        cancelButton.name = "MatchmakingWordCancel";
        cancelButton.transform.localPosition = new(-0.85f, 1.98f, -2f);
        cancelButton.transform.localScale = new(0.34f, 0.74f, 1f);
        cancelButton.Text.text = "閉じる";
        var cancelPb = cancelButton.GetComponent<PassiveButton>();
        cancelPb.OnClick = new();
        cancelPb.OnClick.AddListener((Action)HideEditor);

        return true;
    }

    private static TextBoxTMP CreateEditorTextBox(
        Vector3 position,
        Transform parent,
        TextMeshPro textTemplate,
        OptionsMenuBehaviour options
    )
    {
        var template = DestroyableSingleton<HudManager>.Instance?.Chat?.freeChatField;
        if (template != null)
        {
            editorField = Object.Instantiate(template, parent);
            editorField.name = "MatchmakingWordInputField";
            editorField.transform.localPosition = new(0.16f, 2.45f, -3.6f);
            editorField.transform.localScale = new(0.72f, 1.65f, 1f);
            editorField.gameObject.SetActive(true);

            if (editorField.submitButton != null)
                editorField.submitButton.gameObject.SetActive(false);
            if (editorField.charCountText != null)
                editorField.charCountText.gameObject.SetActive(false);

            var textArea = editorField.textArea;
            if (textArea != null)
            {
                ConfigureTextBox(textArea);
                if (textArea.outputText != null)
                {
                    textArea.outputText.color = Color.white;
                    textArea.outputText.outlineColor = Color.black;
                    textArea.outputText.outlineWidth = 0.12f;
                    textArea.outputText.enableWordWrapping = true;
                    textArea.outputText.overflowMode = TextOverflowModes.Overflow;
                    textArea.outputText.alignment = TextAlignmentOptions.TopLeft;
                    textArea.outputText.enableAutoSizing = false;
                    textArea.outputText.fontSize = 1.25f;
                    textArea.outputText.transform.localScale = Vector3.one;
                    textArea.outputText.gameObject.SetActive(true);
                }
                return textArea;
            }
        }

        editorBox = Object.Instantiate(options.DisableMouseMovement.Background, popup.transform);
        editorBox.name = "MatchmakingWordEditorBox";
        editorBox.transform.localPosition = new(0.16f, 0.28f, -3.6f);
        editorBox.transform.localScale = new(1.42f, 0.45f, 1f);
        editorBox.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
        return CreateFallbackTextBox(position, parent, textTemplate);
    }

    private static TextBoxTMP CreateFallbackTextBox(Vector3 position, Transform parent, TextMeshPro textTemplate)
    {
        var collider = new GameObject("MatchmakingWordInput").AddComponent<BoxCollider2D>();
        var textBox = collider.gameObject.AddComponent<TextBoxTMP>();
        var button = textBox.gameObject.AddComponent<PassiveButton>();
        var text = textTemplate != null
            ? Object.Instantiate(textTemplate, textBox.transform)
            : new GameObject("Text").AddComponent<TextMeshPro>();

        ConfigureTextBox(textBox);

        textBox.transform.SetParent(parent);
        textBox.transform.localPosition = position;
        textBox.transform.localScale = new(0.92f, 0.92f, 1f);

        button.OnMouseOut = new();
        button.OnMouseOver = new();
        button.OnClick = new();
        button.OnClick.AddListener((Action)(() => textBox.GiveFocus()));

        collider.offset = new Vector2(0f, 0f);
        collider.size = new Vector2(5.0f, 1.7f);

        text.name = "Text";
        text.text = "";
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax = text.fontSizeMin = 1.45f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.outlineWidth = 0.18f;
        text.outlineColor = Color.black;
        if (text.transform.parent != textBox.transform)
            text.transform.SetParent(textBox.transform);
        text.transform.localScale = Vector3.one;
        text.transform.localPosition = new(-2.35f, 0.58f, 0f);
        text.rectTransform.sizeDelta = new(4.72f, 1.26f);

        textBox.gameObject.layer = LayerMask.NameToLayer("UI");
        text.gameObject.layer = LayerMask.NameToLayer("UI");
        return textBox;
    }

    private static void ConfigureTextBox(TextBoxTMP textBox)
    {
        if (textBox == null) return;

        textBox.AllowEmail = false;
        textBox.AllowSymbols = true;
        textBox.AllowPaste = true;
        textBox.allowAllCharacters = true;
        textBox.tempTxt = new();
        textBox.compoText = "";
        textBox.characterLimit = MaxCommentLength;
        textBox.OnChange = new();
        textBox.OnEnter = new();
        textBox.OnFocusLost = new();
        textBox.OnChange.AddListener((Action)(() =>
        {
            if (!suppressHistoryEvent)
                PushUndoSnapshot(textBox.text ?? "");
            UpdateCounterText();
        }));
        textBox.OnEnter.AddListener((Action)(() =>
        {
            if (!suppressHistoryEvent)
                PushUndoSnapshot(textBox.text ?? "");
            UpdateCounterText();
        }));
        textBox.OnFocusLost.AddListener((Action)UpdateCounterText);
    }

    private static void UpdateCounterText()
    {
        if (counterText == null) return;
        var len = (editor?.text ?? "").Length;
        var color = len > MaxCommentLength ? "#ff6666" : "#8cffff";
        counterText.text = $"<color={color}>{len}/{MaxCommentLength}</color>";
    }

    private static void ResetHistory(string current)
    {
        undoStack.Clear();
        redoStack.Clear();
        undoStack.Add(current ?? "");
    }

    private static void PushUndoSnapshot(string text)
    {
        var value = text ?? "";
        if (undoStack.Count > 0 && undoStack[^1] == value)
            return;

        undoStack.Add(value);
        if (undoStack.Count > 200)
            undoStack.RemoveAt(0);
        redoStack.Clear();
    }

    private static void UndoEditorText()
    {
        if (undoStack.Count <= 1 || editor == null)
            return;

        var current = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        redoStack.Add(current);
        ApplyEditorText(undoStack[^1], false);
    }

    private static void RedoEditorText()
    {
        if (redoStack.Count == 0 || editor == null)
            return;

        var value = redoStack[^1];
        redoStack.RemoveAt(redoStack.Count - 1);
        undoStack.Add(value);
        ApplyEditorText(value, false);
    }

    private static void ApplyEditorText(string value, bool pushUndoSnapshot)
    {
        if (editor == null) return;

        var normalized = value ?? "";
        if (normalized.Length > MaxCommentLength)
            normalized = normalized[..MaxCommentLength];

        suppressHistoryEvent = true;
        try
        {
            editor.SetText(normalized);
        }
        finally
        {
            suppressHistoryEvent = false;
        }

        if (pushUndoSnapshot)
            PushUndoSnapshot(normalized);

        UpdateCounterText();
        editor.GiveFocus();
    }

    private static bool TrySelectAll()
    {
        if (editor == null) return false;
        try
        {
            var method = editor.GetType().GetMethod(
                "SelectAll",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            if (method == null) return false;
            method.Invoke(editor, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ScheduleChatUiRefresh(float duration, float startDelay)
    {
        chatUiRefreshEnabled = true;
        nextChatUiRefreshTime = Time.time + Mathf.Max(0f, startDelay);
        chatUiRefreshExpireTime = Time.time + Mathf.Max(duration, ChatUiRefreshInterval);
    }

    private static void TryRestoreChatUiNow()
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        var chat = hud?.Chat;
        if (chat == null) return;

        try
        {
            var chatButtonObject = chat.chatButton?.gameObject;
            if (chatButtonObject != null && chatButtonObject.activeSelf && chatButtonObject.activeInHierarchy)
                return;

            chat.SetVisible(true);
            if (chatButtonObject != null)
                chatButtonObject.SetActive(true);
            chat.HideBanButton();
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(MatchmakingWordManager));
        }
    }
}