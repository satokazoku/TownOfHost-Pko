using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

using Object = UnityEngine.Object;

namespace TownOfHost.Modules;

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
internal static class PresetShareButtonPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameSettingMenu __instance)
    {
        PresetShareMenu.EnsureButton(__instance);
    }
}

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
internal static class PresetShareClosePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        PresetShareMenu.HideAll();
    }
}

// FreeChatInputFieldは入力中に自分で位置/サイズを弄ってくることがあるため、
// 毎フレーム強制的に位置を戻す。GameOptionsMenuUpdatePatchがactiveonlyボタンに
// 対して同じことをしているのと同じ考え方。
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
internal static class PresetShareLayoutTickPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        PresetShareMenu.ReassertLayout();
    }
}

public static class PresetShareMenu
{
    const int RowCount = 5;
    const int SlotMin = 1;
    const int SlotMax = 7;

    // Zは値が小さい(よりマイナス)ほど手前に描画される。設定画面のツールチップ等が
    // -255あたりまで使っているのを確認したので、確実に前面に出るよう大きく離す。
    const float MainPanelZ = -400f;
    const float UploadPanelZ = -450f;
    const float SlotPickerZ = -500f;
    const int MainSortingOrder = 1000;
    const int UploadSortingOrder = 2000;
    const int SlotPickerSortingOrder = 3000;

    const float PanelWidth = 6.4f;
    const float PanelHeight = 5.2f;
    const float UploadPanelWidth = 6.0f;
    const float UploadPanelHeight = 4.6f;
    const float SlotPickerWidth = 5.0f;
    const float SlotPickerHeight = 2.0f;

    const float RowStartY = 1.10f;
    const float RowSpacing = 0.55f;

    static PassiveButton _openButton;
    static SpriteRenderer _panel;
    static bool _built;
    static bool _open;

    // 位置を毎フレーム強制する対象(Transform → 固定したいlocalPosition)
    static readonly Dictionary<Transform, Vector3> _fixedPositions = new();
    // 入力欄 → プレースホルダーのラベル(中身が空の間だけ表示する)
    static readonly Dictionary<FreeChatInputField, TextMeshPro> _placeholders = new();

    static TextMeshPro _titleText;
    static FreeChatInputField _searchInput;
    static FreeChatInputField _tagInput;
    static FreeChatInputField _versionInput;
    static PassiveButton _sortButton;
    static PassiveButton _searchButton;
    static TextMeshPro _statusText;

    static readonly TextMeshPro[] _rowTexts = new TextMeshPro[RowCount];
    static readonly PassiveButton[] _rowCopyButtons = new PassiveButton[RowCount];
    static readonly PassiveButton[] _rowFileButtons = new PassiveButton[RowCount];
    static readonly PassiveButton[] _rowLoadButtons = new PassiveButton[RowCount];

    static TextMeshPro _pageText;
    static PassiveButton _prevButton;
    static PassiveButton _nextButton;
    static PassiveButton _uploadOpenButton;
    static PassiveButton _closeButton;

    // アップロードパネル
    static SpriteRenderer _uploadPanel;
    static FreeChatInputField _uploadDataInput;   // 貼り付け or スロット読込先(1つのテキスト欄を共用)
    static FreeChatInputField _uploadNameInput;
    static FreeChatInputField _uploadTagsInput;
    static PassiveButton _uploadSubmitButton;
    static PassiveButton _uploadCancelButton;
    static TextMeshPro _uploadStatusText;

    // 「どの枠にロードしますか」共通オーバーレイ(ダウンロード後のLoadアクションで使う)
    static SpriteRenderer _slotPickerPanel;
    static TextMeshPro _slotPickerTitle;
    static readonly PassiveButton[] _slotPickerButtons = new PassiveButton[SlotMax - SlotMin + 1];
    static Action<int> _onSlotChosen;

    static List<PresetSummary> _results = new();
    static int _page = 1;
    static int _totalCount;
    static PresetSortOrder _sort = PresetSortOrder.Recent;

    public static void EnsureButton(GameSettingMenu instance)
    {
        if (instance == null) return;

        var settingsButton = HudManager.Instance?.SettingsButton?.GetComponent<PassiveButton>();
        if (settingsButton == null) return;

        _openButton = Object.Instantiate(settingsButton, instance.transform);
        _openButton.name = "PresetShareOpen";
        _openButton.transform.localScale -= new Vector3(0.25f, 0.25f);

        var aspect = _openButton.GetComponent<AspectPosition>();
        if (aspect != null)
        {
            aspect.DistanceFromEdge = new Vector3(-1.95f, 2.49f, -200f);
            aspect.Alignment = AspectPosition.EdgeAlignments.Center;
        }

        var label = Object.Instantiate(HudManager.Instance.TaskPanel.taskText, _openButton.transform);
        label.text = "共有";
        label.transform.localPosition = new Vector3(0f, -0.9f, -1f);
        label.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        label.alignment = TextAlignmentOptions.Center;

        _openButton.OnClick = new();
        _openButton.OnClick.AddListener((Action)(() =>
        {
            if (!_built) Build(instance);
            Toggle();
        }));
    }

    public static void HideAll()
    {
        _open = false;
        _panel?.gameObject?.SetActive(false);
        _uploadPanel?.gameObject?.SetActive(false);
        _slotPickerPanel?.gameObject?.SetActive(false);
    }

    static void Toggle()
    {
        _open = !_open;
        _panel.gameObject.SetActive(_open);
        _uploadPanel.gameObject.SetActive(false);
        _slotPickerPanel.gameObject.SetActive(false);
        if (_open) RefreshResults();
    }

    // 毎フレーム、位置がズレていないか強制的に戻す + プレースホルダーの表示切替
    public static void ReassertLayout()
    {
        if (_panel == null || !_panel.gameObject.activeSelf) return;

        foreach (var kv in _fixedPositions)
        {
            if (kv.Key == null) continue;
            if (kv.Key.localPosition != kv.Value)
                kv.Key.localPosition = kv.Value;
        }

        foreach (var kv in _placeholders)
        {
            var input = kv.Key;
            var placeholderLabel = kv.Value;
            if (input == null || placeholderLabel == null) continue;
            var empty = string.IsNullOrEmpty(input.textArea?.text);
            if (placeholderLabel.gameObject.activeSelf != empty)
                placeholderLabel.gameObject.SetActive(empty);
        }
    }

    static void SetFixedPosition(Transform t, Vector3 pos)
    {
        t.localPosition = pos;
        _fixedPositions[t] = pos;
    }

    static Sprite MakeFlatSprite(Color color, float worldWidth, float worldHeight)
    {
        const int ppu = 100;
        var w = Math.Max(1, (int)(worldWidth * ppu));
        var h = Math.Max(1, (int)(worldHeight * ppu));
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    static SpriteRenderer CreateSolidPanel(string name, Transform parent, float z, float width, float height, int sortingOrder, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.transform.localScale = Vector3.one;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeFlatSprite(color, width, height);
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    static void Build(GameSettingMenu instance)
    {
        _built = true;
        var parent = HudManager.Instance != null ? HudManager.Instance.transform : instance.transform;

        // ===== メインパネル(完全不透明) =====
        // 修正箇所：アルファ値を1.0fにして完全に不透明にし、役職一覧の背景画像のように真っ黒にする。
        _panel = CreateSolidPanel("PresetSharePanel", parent, MainPanelZ, PanelWidth, PanelHeight,
            MainSortingOrder, new Color(0f, 0f, 0f, 1.0f));

        // 修正箇所：背後のUIへのクリック判定を遮断するためのColliderを追加。
        var panelCollider = _panel.gameObject.AddComponent<BoxCollider2D>();
        panelCollider.size = new Vector2(PanelWidth, PanelHeight);
        panelCollider.isTrigger = true; // 他の物理オブジェクトとの衝突を防ぐためにトリガーに設定

        _panel.gameObject.SetActive(false);

        // 修正箇所：アップロードパネルもアルファ値を1.0fにして完全に不透明にする。
        _uploadPanel = CreateSolidPanel("PresetShareUploadPanel", parent, UploadPanelZ, UploadPanelWidth, UploadPanelHeight,
            UploadSortingOrder, new Color(0.05f, 0.07f, 0.05f, 1.0f));

        // 修正箇所：アップロードパネルにも Collider を追加して入力を遮断。
        var uploadCollider = _uploadPanel.gameObject.AddComponent<BoxCollider2D>();
        uploadCollider.size = new Vector2(UploadPanelWidth, UploadPanelHeight);
        uploadCollider.isTrigger = true;

        _uploadPanel.gameObject.SetActive(false);

        _slotPickerPanel = CreateSolidPanel("PresetShareSlotPicker", parent, SlotPickerZ, SlotPickerWidth, SlotPickerHeight,
            SlotPickerSortingOrder, new Color(0.07f, 0.05f, 0.05f, 1f));
        _slotPickerPanel.gameObject.SetActive(false);

        BuildMainPanel(instance);
        BuildUploadPanel(instance);
        BuildSlotPicker(instance);
    }

    static void BuildMainPanel(GameSettingMenu instance)
    {
        var t = _panel.transform;

        _titleText = MakeLabel(t, "<size=140%><b>プリセット共有</b></size>", new Vector3(0f, 2.30f, -1f), 0.6f, TextAlignmentOptions.Center);

        _searchInput = CreateInput(t, new Vector3(-2.20f, 1.85f, -1f), "検索(名前)");
        _tagInput = CreateInput(t, new Vector3(-0.75f, 1.85f, -1f), "タグ(,区切り)");
        _versionInput = CreateInput(t, new Vector3(0.55f, 1.85f, -1f), "バージョン");

        _sortButton = CreateSmallButton(instance, t, new Vector3(1.65f, 1.85f, -1f), "新着順", () =>
        {
            _sort = _sort == PresetSortOrder.Recent ? PresetSortOrder.Popular : PresetSortOrder.Recent;
            _sortButton.buttonText.text = _sort == PresetSortOrder.Recent ? "新着順" : "人気順";
            _page = 1;
            RefreshResults();
        });

        _searchButton = CreateSmallButton(instance, t, new Vector3(2.55f, 1.85f, -1f), "検索", () =>
        {
            _page = 1;
            RefreshResults();
        });

        _statusText = MakeLabel(t, "", new Vector3(0f, 1.50f, -1f), 0.38f, TextAlignmentOptions.Center);

        for (var i = 0; i < RowCount; i++)
        {
            var y = RowStartY - i * RowSpacing;

            var rowText = MakeLabel(t, "", new Vector3(-2.20f, y, -1f), 0.34f, TextAlignmentOptions.MidlineLeft);
            _rowTexts[i] = rowText;

            var idx = i;
            _rowCopyButtons[i] = CreateSmallButton(instance, t, new Vector3(1.50f, y, -1f), "Copy", () => OnCopyClicked(idx));
            _rowFileButtons[i] = CreateSmallButton(instance, t, new Vector3(2.20f, y, -1f), "File", () => OnFileClicked(idx));
            _rowLoadButtons[i] = CreateSmallButton(instance, t, new Vector3(2.90f, y, -1f), "Load", () => OnLoadClicked(idx));
        }

        var pagingY = RowStartY - RowCount * RowSpacing - 0.25f;

        _prevButton = CreateSmallButton(instance, t, new Vector3(-0.55f, pagingY, -1f), "< 前", () =>
        {
            if (_page <= 1) return;
            _page--;
            RefreshResults();
        });

        _pageText = MakeLabel(t, "1/1", new Vector3(0f, pagingY, -1f), 0.32f, TextAlignmentOptions.Center);

        _nextButton = CreateSmallButton(instance, t, new Vector3(0.55f, pagingY, -1f), "次 >", () =>
        {
            var maxPage = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)RowCount));
            if (_page >= maxPage) return;
            _page++;
            RefreshResults();
        });

        var bottomY = pagingY - 0.45f;
        _uploadOpenButton = CreateSmallButton(instance, t, new Vector3(-1.55f, bottomY, -1f), "アップロード", () =>
        {
            _uploadPanel.gameObject.SetActive(true);
        });

        _closeButton = CreateSmallButton(instance, t, new Vector3(1.55f, bottomY, -1f), "閉じる", () =>
        {
            _open = false;
            _panel.gameObject.SetActive(false);
        });
    }

    static void BuildUploadPanel(GameSettingMenu instance)
    {
        var t = _uploadPanel.transform;

        MakeLabel(t, "<size=130%><b>プリセットをアップロード</b></size>", new Vector3(0f, 2.00f, -1f), 0.7f, TextAlignmentOptions.Center);

        _uploadDataInput = CreateInput(t, new Vector3(0f, 1.15f, -1f), "ここに貼り付け、またはスロット番号を押して読込", wide: true, tall: true);

        MakeLabel(t, "スロットから読込:", new Vector3(-2.55f, 0.35f, -1f), 0.34f, TextAlignmentOptions.MidlineLeft);
        for (var slot = SlotMin; slot <= SlotMax; slot++)
        {
            var s = slot;
            var x = -2.4f + (slot - SlotMin) * 0.8f;
            CreateSmallButton(instance, t, new Vector3(x, 0.05f, -1f), s.ToString(), () =>
            {
                _uploadDataInput.textArea.text = GetPresetSlotValue(s);
            });
        }

        _uploadNameInput = CreateInput(t, new Vector3(0f, -0.55f, -1f), "名前(必須)", wide: true);
        _uploadTagsInput = CreateInput(t, new Vector3(0f, -0.95f, -1f), "タグ(,区切り 例:闇鍋,人外)", wide: true);

        _uploadStatusText = MakeLabel(t, "", new Vector3(0f, -1.35f, -1f), 0.40f, TextAlignmentOptions.Center);

        _uploadSubmitButton = CreateSmallButton(instance, t, new Vector3(-0.75f, -1.80f, -1f), "送信", OnUploadSubmit);
        _uploadCancelButton = CreateSmallButton(instance, t, new Vector3(0.75f, -1.80f, -1f), "キャンセル", () =>
        {
            _uploadPanel.gameObject.SetActive(false);
        });
    }

    static void BuildSlotPicker(GameSettingMenu instance)
    {
        var t = _slotPickerPanel.transform;

        _slotPickerTitle = MakeLabel(t, "", new Vector3(0f, 0.65f, -1f), 0.42f, TextAlignmentOptions.Center);

        for (var slot = SlotMin; slot <= SlotMax; slot++)
        {
            var s = slot;
            var x = -2.1f + (slot - SlotMin) * 0.7f;
            _slotPickerButtons[slot - SlotMin] = CreateSmallButton(instance, t, new Vector3(x, -0.15f, -1f), s.ToString(), () =>
            {
                _slotPickerPanel.gameObject.SetActive(false);
                _onSlotChosen?.Invoke(s);
                _onSlotChosen = null;
            });
        }

        CreateSmallButton(instance, t, new Vector3(0f, -0.75f, -1f), "キャンセル", () =>
        {
            _slotPickerPanel.gameObject.SetActive(false);
            _onSlotChosen = null;
        });
    }

    static void ShowSlotPicker(string title, Action<int> onChosen)
    {
        _slotPickerTitle.text = title;
        _onSlotChosen = onChosen;
        _slotPickerPanel.gameObject.SetActive(true);
    }

    static TextMeshPro MakeLabel(Transform parent, string text, Vector3 localPos, float scale, TextAlignmentOptions align)
    {
        var label = Object.Instantiate(HudManager.Instance.TaskPanel.taskText, parent);
        label.text = text;
        SetFixedPosition(label.transform, localPos);
        label.transform.localScale = new Vector3(scale, scale, 1f);
        label.alignment = align;
        return label;
    }

    static FreeChatInputField CreateInput(Transform parent, Vector3 localPos, string placeholder, bool wide = false, bool tall = false)
    {
        var input = Object.Instantiate(HudManager.Instance.Chat.freeChatField, parent);
        input.name = $"PresetShareInput_{placeholder}";
        SetFixedPosition(input.transform, localPos);
        var scaleY = tall ? 3.2f : 1.1f;
        input.transform.localScale = wide ? new Vector3(0.85f, scaleY, 1f) : new Vector3(0.5f, scaleY, 1f);
        input.gameObject.SetActive(true);
        if (input.submitButton != null) input.submitButton.gameObject.SetActive(false);
        if (input.charCountText != null) input.charCountText.gameObject.SetActive(false);

        // プレースホルダー(薄いヒント文字)。inputの子ではなく同じ親に置いて
        // inputのスケールに巻き込まれないようにする。
        var placeholderLabel = Object.Instantiate(HudManager.Instance.TaskPanel.taskText, parent);
        placeholderLabel.text = $"<color=#888888>{placeholder}</color>";
        SetFixedPosition(placeholderLabel.transform, localPos + new Vector3(0f, 0f, 0.02f));
        placeholderLabel.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
        placeholderLabel.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderLabel.raycastTarget = false;
        _placeholders[input] = placeholderLabel;

        return input;
    }

    static PassiveButton CreateSmallButton(GameSettingMenu instance, Transform parent, Vector3 localPos, string text, Action onClick)
    {
        var template = instance.GamePresetsButton;
        var btn = Object.Instantiate(template, parent);
        btn.name = $"PresetShareBtn_{text}";

        var aspect = btn.GetComponent<AspectPosition>();
        if (aspect != null) Object.Destroy(aspect);

        SetFixedPosition(btn.transform, localPos);
        btn.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
        if (btn.buttonText != null)
        {
            btn.buttonText.text = text;
            btn.buttonText.DestroyTranslator();
        }
        btn.OnClick = new();
        btn.OnClick.AddListener((Action)onClick);
        return btn;
    }

    static string GetPresetSlotValue(int slot) => slot switch
    {
        1 => Main.Preset1.Value,
        2 => Main.Preset2.Value,
        3 => Main.Preset3.Value,
        4 => Main.Preset4.Value,
        5 => Main.Preset5.Value,
        6 => Main.Preset6.Value,
        7 => Main.Preset7.Value,
        _ => "",
    };

    static void SetPresetSlotValue(int slot, string value)
    {
        switch (slot)
        {
            case 1: Main.Preset1.Value = value; break;
            case 2: Main.Preset2.Value = value; break;
            case 3: Main.Preset3.Value = value; break;
            case 4: Main.Preset4.Value = value; break;
            case 5: Main.Preset5.Value = value; break;
            case 6: Main.Preset6.Value = value; break;
            case 7: Main.Preset7.Value = value; break;
        }
    }

    // ===== データ取得(バックグラウンド)→ 反映(メインスレッド) =====
    static void RefreshResults()
    {
        _statusText.text = "検索中...";

        var search = _searchInput.textArea.text;
        var version = _versionInput.textArea.text;
        var tags = (_tagInput.textArea.text ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        var sort = _sort;
        var page = _page;

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var result = await PresetShareClient.FetchListAsync(search, version, tags, sort, page, RowCount).ConfigureAwait(false);
            MainThreadDispatcher.Enqueue(() => ApplyResults(result));
        });
    }

    static void ApplyResults(PresetListResult result)
    {
        _results = result.Presets ?? new List<PresetSummary>();
        _totalCount = result.TotalCount;

        for (var i = 0; i < RowCount; i++)
        {
            if (i < _results.Count)
            {
                var p = _results[i];
                var tagsText = p.Tags is { Count: > 0 } ? $"[{string.Join(",", p.Tags)}]" : "";
                _rowTexts[i].text =
                    $"<b>{Truncate(p.Name, 18)}</b> v{p.Version}\n" +
                    $"<size=80%>by {Truncate(p.UploaderName, 12)} DL:{p.DownloadCount} {tagsText}</size>";
                SetRowButtonsActive(i, true);
            }
            else
            {
                _rowTexts[i].text = "";
                SetRowButtonsActive(i, false);
            }
        }

        var maxPage = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)RowCount));
        _pageText.text = $"{_page}/{maxPage}  (計{_totalCount}件)";
        _statusText.text = _results.Count == 0 ? "該当するプリセットが見つかりません" : "";
    }

    static void SetRowButtonsActive(int i, bool active)
    {
        _rowCopyButtons[i].gameObject.SetActive(active);
        _rowFileButtons[i].gameObject.SetActive(active);
        _rowLoadButtons[i].gameObject.SetActive(active);
    }

    static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    // ===== 3種のダウンロード動作 =====
    static void OnCopyClicked(int index)
    {
        if (index >= _results.Count) return;
        var id = _results[index].Id;
        _statusText.text = "取得中...";

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var detail = await PresetShareClient.DownloadAsync(id).ConfigureAwait(false);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (detail == null) { _statusText.text = "取得に失敗しました"; return; }
                GUIUtility.systemCopyBuffer = detail.Data;
                _statusText.text = $"「{detail.Name}」をクリップボードにコピーしました";
            });
        });
    }

    static void OnFileClicked(int index)
    {
        if (index >= _results.Count) return;
        var id = _results[index].Id;
        _statusText.text = "取得中...";

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var detail = await PresetShareClient.DownloadAsync(id).ConfigureAwait(false);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (detail == null) { _statusText.text = "取得に失敗しました"; return; }
                try
                {
                    var dir = Path.Combine(Main.BaseDirectory, "SharedPresets");
                    Directory.CreateDirectory(dir);
                    var safeName = string.Concat(detail.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                    var path = Path.Combine(dir, $"{safeName}_{detail.Id[..Math.Min(8, detail.Id.Length)]}.txt");
                    File.WriteAllText(path, detail.Data, Encoding.UTF8);
                    _statusText.text = $"保存しました: SharedPresets/{Path.GetFileName(path)}";
                }
                catch (Exception e)
                {
                    Logger.Exception(e, nameof(PresetShareMenu));
                    _statusText.text = "ファイル保存に失敗しました";
                }
            });
        });
    }

    static void OnLoadClicked(int index)
    {
        if (index >= _results.Count) return;
        var id = _results[index].Id;
        _statusText.text = "取得中...";

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var detail = await PresetShareClient.DownloadAsync(id).ConfigureAwait(false);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (detail == null) { _statusText.text = "取得に失敗しました"; return; }
                ShowSlotPicker($"「{Truncate(detail.Name, 20)}」をどの枠にロードしますか？", (slot) =>
                {
                    SetPresetSlotValue(slot, detail.Data);
                    _statusText.text = $"「{detail.Name}」をプリセット枠{slot}にロードしました";
                });
            });
        });
    }

    // ===== アップロード =====
    static void OnUploadSubmit()
    {
        var name = _uploadNameInput.textArea.text?.Trim() ?? "";
        if (name.Length == 0)
        {
            _uploadStatusText.text = "名前を入力してください";
            return;
        }

        var data = _uploadDataInput.textArea.text ?? "";
        if (string.IsNullOrWhiteSpace(data))
        {
            _uploadStatusText.text = "データを貼り付けるか、スロット番号を押してください";
            return;
        }

        var tags = (_uploadTagsInput.textArea.text ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        var uploaderName = PlayerControl.LocalPlayer?.Data?.PlayerName ?? "Unknown";
        // TODO: Main.Versionが実際のバージョン文字列を持つ静的プロパティ/フィールドである前提です。
        // 名前が違う場合はここを実際のフィールド名に合わせてください。
        var version = Main.version?.ToString() ?? "";

        _uploadStatusText.text = "アップロード中...";

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var (success, error) = await PresetShareClient.UploadAsync(name, uploaderName, "", version, tags, data).ConfigureAwait(false);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (success)
                {
                    _uploadStatusText.text = "アップロードしました！";
                    _uploadNameInput.textArea.Clear();
                    _uploadTagsInput.textArea.Clear();
                    _uploadDataInput.textArea.Clear();
                    _page = 1;
                    RefreshResults();
                }
                else
                {
                    _uploadStatusText.text = $"失敗: {error}";
                }
            });
        });
    }
}