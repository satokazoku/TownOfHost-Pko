using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static TownOfHost.Translator;

namespace TownOfHost;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
public static class RoleGuideButtonPatch
{
    private static GameObject guidePanel;
    private static readonly List<PassiveButton> blockedVanillaButtons = new();
    private static GameObject hiddenUseButton;
    private static bool hiddenUseButtonWasActive;
    public static bool isPanelOpen = false;
    private static GuideTab currentTab = GuideTab.MyRole;
    private static MyRoleView currentMyRoleView = MyRoleView.Role;
    private static CustomRoles selectedRole = CustomRoles.NotAssigned;
    private static string roleSearchText = "";
    private static TextBoxTMP roleSearchBox;
    private static TextMeshPro roleSearchPlaceholder;
    private static float roleSearchCaretTimer;

    // スクロール
    private static float scrollY = 0f;
    private static float maxScrollY = 0f;
    private static GameObject scrollContent;
    private static readonly List<GameObject> scrollEntries = new();
    private static readonly List<float> scrollSnapTargets = new();
    private static GameObject scrollThumb;       // スクロールバーのつまみ
    private static bool scrollBarDragging = false;
    private static float scrollBarDragStartMouseY = 0f;
    private static float scrollBarDragStartScrollY = 0f;
    private static bool roleListDragging = false;
    private static bool roleListDragMoved = false;
    private static float roleListDragStartMouseY = 0f;
    private static float roleListDragStartScrollY = 0f;

    // ボタンアニメ
    private static SpriteRenderer _btnRenderer;
    private static TextMeshPro _btnText;
    public static bool HasGuideButton => _btnRenderer != null && _btnRenderer.gameObject.activeInHierarchy;
    private static float _btnAnimTimer = 0f;
    private static bool _btnAnimActive = false;
    private const float GuideButtonScale = 0.28f;
    private const float GuideFrameGap = 0.42f;

    // パネル定数
    private const float PanelW = 8.6f;
    private const float PanelH = 5.6f;
    // リスト領域
    private const float ContentLeft = -1.92f;
    private const float ContentTop = 1.78f;
    private const float ContentH = 4.2f;
    private const float ListLeft = -4.08f;
    private const float ListW = 1.65f;
    private const float ListTop = 0.12f;
    private const float ListH = 2.62f;
    private const float RoleListTop = -0.28f;
    private const float RoleListH = 2.22f;
    private const float ItemH = 0.33f;
    private const float CategoryHeaderH = 0.22f;
    // 詳細領域
    private const float DetailX = -1.86f;
    private const float DetailW = 5.72f;
    // スクロールバー
    private const float SbarX = -2.36f;
    private const float SbarW = 0.10f;
    private static float activeListTop = ListTop;
    private static float activeListHeight = ListH;
    private static readonly Dictionary<GuideRoleCategory, float> categoryScrollTargets = new();

    private static Sprite _squareSprite;
    private static Sprite SquareSprite
    {
        get
        {
            if (_squareSprite != null) return _squareSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _squareSprite;
        }
    }

    private enum GuideTab { MyRole, RoleList }
    private enum MyRoleView { Role, Addons }
    private enum GuideRoleCategory
    {
        Impostor,
        Madmate,
        Crewmate,
        Neutral,
        GhostRole,
        Addon,
    }

    private static GuideRoleCategory GetGuideRoleCategory(CustomRoles role)
    {
        if (role.IsGhostRole()) return GuideRoleCategory.GhostRole;
        if (role > CustomRoles.NotAssigned) return GuideRoleCategory.Addon;

        return role.GetCustomRoleTypes() switch
        {
            CustomRoleTypes.Impostor => GuideRoleCategory.Impostor,
            CustomRoleTypes.Madmate => GuideRoleCategory.Madmate,
            CustomRoleTypes.Crewmate => GuideRoleCategory.Crewmate,
            CustomRoleTypes.Neutral => GuideRoleCategory.Neutral,
            _ => GuideRoleCategory.Crewmate,
        };
    }

    private static int GetAddonGuideOrder(CustomRoles role)
    {
        if (role.IsDebuffAddon()) return 0;
        if (role.IsBuffAddon()) return 1;
        return 2;
    }

    public static void Postfix(HudManager __instance)
    {
        _ = new LateTask(() => CreateGuideButton(__instance), 0.5f, "RoleGuide.Create", true);
    }

    private static void CreateGuideButton(HudManager hud)
    {
        try
        {
            if (hud == null) return;
            var old = hud.transform.Find("RoleGuideButton");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);

            var settingsButton = hud.SettingsButton;
            if (settingsButton == null) return;

            var settingsPassiveBtn = settingsButton.GetComponent<PassiveButton>();
            if (settingsPassiveBtn != null)
                settingsPassiveBtn.OnClick.AddListener((UnityEngine.Events.UnityAction)ClosePanel);

            var btnObj = new GameObject("RoleGuideButton");
            btnObj.transform.SetParent(hud.transform);
            btnObj.layer = 5;

            var chatButton = hud.Chat?.chatButton;
            if (chatButton != null && ShouldShowTopRightChatButton())
                chatButton.gameObject.SetActive(true);

            var settingsPos = hud.transform.InverseTransformPoint(settingsButton.transform.position);
            if (chatButton != null && chatButton.gameObject.activeInHierarchy)
            {
                var chatPos = hud.transform.InverseTransformPoint(chatButton.transform.position);
                float buttonSpacing = Mathf.Abs(settingsPos.x - chatPos.x);
                if (buttonSpacing < 0.45f || buttonSpacing > 1.5f) buttonSpacing = 0.78f;
                btnObj.transform.localPosition = new Vector3(
                    chatPos.x - buttonSpacing - GuideFrameGap,
                    settingsPos.y,
                    settingsPos.z - 0.1f);
            }
            else
            {
                btnObj.transform.localPosition = new Vector3(
                    settingsPos.x - 0.78f - GuideFrameGap,
                    settingsPos.y,
                    settingsPos.z - 0.1f);
            }
            btnObj.transform.localScale = new Vector3(GuideButtonScale, GuideButtonScale, 1f);

            CreateVanillaButtonFrame(hud, btnObj.transform);

            var sr = btnObj.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 10;
            sr.sprite = null;
            _btnRenderer = sr;

            var helpText = MakeText(btnObj, "HelpText",
                new Vector3(0f, 0.03f, -0.1f), "HELP", 5f,
                TextAlignmentOptions.Center, new Vector2(2.35f, 1.4f));
            helpText.enableWordWrapping = false;
            helpText.overflowMode = TextOverflowModes.Overflow;
            helpText.characterSpacing = -8f;
            helpText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            _btnText = helpText;

            var col = btnObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.28f, 2.28f);

            var btn = btnObj.AddComponent<PassiveButton>();
            btn.Colliders = new Collider2D[] { col };
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                _btnAnimActive = true;
                _btnAnimTimer = 0f;
                TogglePanel();
            }));
            btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (_btnText != null && !_btnAnimActive)
                    _btnText.color = new Color(0.65f, 0.88f, 1f, 1f);
            }));
            btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (_btnText != null && !_btnAnimActive)
                    _btnText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            }));
        }
        catch (Exception e) { Logger.Error(e.ToString(), "RoleGuideButton"); }
    }

    private static bool ShouldShowTopRightChatButton()
        => GameStates.IsLobby || GameStates.IsMeeting;

    public static void UpdateTopRightButtonLayout()
    {
        if (_btnRenderer == null || !DestroyableSingleton<HudManager>.InstanceExists) return;

        var hud = DestroyableSingleton<HudManager>.Instance;
        var settingsButton = hud.SettingsButton;
        var chatButton = hud.Chat?.chatButton;
        if (settingsButton == null || chatButton == null) return;

        if (ShouldShowTopRightChatButton() && !chatButton.gameObject.activeSelf)
            chatButton.gameObject.SetActive(true);

        var settingsPos = hud.transform.InverseTransformPoint(settingsButton.transform.position);
        var chatPos = hud.transform.InverseTransformPoint(chatButton.transform.position);
        float buttonSpacing = Mathf.Abs(settingsPos.x - chatPos.x);
        if (buttonSpacing < 0.45f || buttonSpacing > 1.5f) buttonSpacing = 0.78f;

        float guideX = chatButton.gameObject.activeInHierarchy
            ? chatPos.x - buttonSpacing - GuideFrameGap
            : settingsPos.x - buttonSpacing - GuideFrameGap;
        _btnRenderer.transform.localPosition = new Vector3(
            guideX,
            settingsPos.y,
            settingsPos.z - 0.1f);
    }

    private static void CreateVanillaButtonFrame(HudManager hud, Transform parent)
    {
        var renderers = hud.transform.root.GetComponentsInChildren<SpriteRenderer>(true);
        var templateRenderer = renderers.FirstOrDefault(renderer =>
                renderer.gameObject.name == "background"
                && renderer.transform.parent != null
                && renderer.transform.parent.name == "Friends List Button")
            ?? renderers.FirstOrDefault(renderer =>
                renderer.gameObject.name == "background"
                && renderer.transform.parent != null
                && renderer.transform.parent.name == "ChatUi");
        if (templateRenderer == null || templateRenderer.sprite == null)
        {
            Logger.Error("バニラのボタン背景スプライトが見つかりませんでした", "RoleGuideButton");
            return;
        }

        var frameObj = new GameObject("VanillaBackground");
        frameObj.transform.SetParent(parent);
        frameObj.name = "VanillaBackground";
        frameObj.layer = 5;
        frameObj.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        frameObj.transform.localRotation = Quaternion.identity;

        var templateScale = templateRenderer.transform.lossyScale;
        var parentScale = parent.lossyScale;
        frameObj.transform.localScale = new Vector3(
            parentScale.x == 0f ? 1f : templateScale.x / parentScale.x,
            parentScale.y == 0f ? 1f : templateScale.y / parentScale.y,
            1f);

        var frameRenderer = frameObj.AddComponent<SpriteRenderer>();
        frameRenderer.sprite = templateRenderer.sprite;
        frameRenderer.color = Color.white;
        frameRenderer.drawMode = templateRenderer.drawMode;
        frameRenderer.size = templateRenderer.size;
        frameRenderer.flipX = templateRenderer.flipX;
        frameRenderer.flipY = templateRenderer.flipY;
        frameRenderer.maskInteraction = SpriteMaskInteraction.None;
        frameRenderer.sortingLayerID = 0;
        frameRenderer.sortingOrder = 9;
        frameRenderer.enabled = true;
    }

    private static void TogglePanel()
    {
        if (isPanelOpen) ClosePanel(); else OpenPanel();
    }

    public static void OpenPanel()
    {
        ClosePanel();
        isPanelOpen = true;
        currentTab = GuideTab.MyRole;
        currentMyRoleView = MyRoleView.Role;
        selectedRole = CustomRoles.NotAssigned;
        roleSearchText = "";
        scrollY = 0f;
        BuildPanel();
        BlockVanillaUi();
    }

    public static void ClosePanel()
    {
        if (roleSearchBox != null)
        {
            if (roleSearchBox.hasFocus) roleSearchBox.LoseFocus();
            roleSearchBox.ForceKeyboardClose();
        }
        Input.imeCompositionMode = IMECompositionMode.Off;

        RestoreVanillaUi();
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);
        guidePanel = null;
        scrollContent = null;
        roleSearchBox = null;
        scrollEntries.Clear();
        scrollSnapTargets.Clear();
        scrollThumb = null;
        activeListTop = ListTop;
        activeListHeight = ListH;
        categoryScrollTargets.Clear();
        isPanelOpen = false;
        scrollBarDragging = false;
        roleListDragging = false;
        roleListDragMoved = false;
    }

    public static void UpdateButtonVisibility()
    {
        if (_btnRenderer == null) return;

        bool shouldShow = !GameSettingMenu.Instance;
        var buttonObject = _btnRenderer.gameObject;
        if (buttonObject != null && buttonObject.activeSelf != shouldShow)
            buttonObject.SetActive(shouldShow);

        if (!shouldShow && isPanelOpen) ClosePanel();
    }

    private static void BlockVanillaUi()
    {
        RestoreVanillaUi();

        foreach (var button in UnityEngine.Object.FindObjectsOfType<PassiveButton>())
        {
            if (button == null || !button.enabled || !button.gameObject.activeInHierarchy) continue;

            var buttonTransform = button.transform;
            bool isGuidePanelButton = guidePanel != null &&
                (buttonTransform == guidePanel.transform || buttonTransform.IsChildOf(guidePanel.transform));
            bool isGuideToggleButton = button.gameObject.name == "RoleGuideButton";
            if (isGuidePanelButton || isGuideToggleButton) continue;

            button.enabled = false;
            blockedVanillaButtons.Add(button);
        }

        HideUseButton();
    }

    private static void RestoreVanillaUi()
    {
        foreach (var button in blockedVanillaButtons)
        {
            if (button != null) button.enabled = true;
        }
        blockedVanillaButtons.Clear();

        if (hiddenUseButton != null)
        {
            hiddenUseButton.SetActive(hiddenUseButtonWasActive);
            hiddenUseButton = null;
        }
    }

    private static void HideUseButton()
    {
        if (!DestroyableSingleton<HudManager>.InstanceExists) return;

        var useButtonObject = DestroyableSingleton<HudManager>.Instance.UseButton?.gameObject;
        if (useButtonObject == null) return;

        if (hiddenUseButton != useButtonObject)
        {
            hiddenUseButton = useButtonObject;
            hiddenUseButtonWasActive = useButtonObject.activeSelf;
        }

        if (useButtonObject.activeSelf) useButtonObject.SetActive(false);
    }

    private static void BuildPanel()
    {
        if (guidePanel != null) UnityEngine.Object.Destroy(guidePanel);
        scrollContent = null;
        roleSearchBox = null;
        scrollEntries.Clear();
        scrollThumb = null;

        guidePanel = new GameObject("RoleGuidePanel");
        guidePanel.transform.SetParent(HudManager.Instance.transform);
        guidePanel.transform.localPosition = new Vector3(0f, 0f, -20f);
        guidePanel.transform.localScale = Vector3.one;
        guidePanel.layer = 5;
        ConfigureGuideSortingGroup();

        // 紙面風の背景と外枠
        MakeCutCornerPanel(guidePanel, "PaperBorder", PanelW + 0.14f, PanelH + 0.14f, 0.66f,
            new Color(0.035f, 0.045f, 0.055f, 0.99f), 4);
        MakeCutCornerPanel(guidePanel, "Paper", PanelW, PanelH, 0.58f,
            Color.white, 5);
        MakeSprite(guidePanel, "TitleBG", new Vector3(0f, 2.45f, -0.5f),
            new Vector3(PanelW, 0.65f, 1f), new Color(0.16f, 0.20f, 0.24f, 1f), 6);
        var title = MakeText(guidePanel, "Title", new Vector3(-3.95f, 2.70f, -1f),
            "ROLE GUIDE / 役職ガイド", 2.25f, TextAlignmentOptions.TopLeft,
            new Vector2(6.8f, 0.5f));
        title.color = Color.white;
        MakeCloseButton();

        // 左タブ・一覧欄
        MakeSprite(guidePanel, "LeftBG", new Vector3(-3.25f, -0.15f, -0.5f),
            new Vector3(2.05f, 4.85f, 1f), Color.white, 6);
        MakeSprite(guidePanel, "LeftDivider", new Vector3(-2.21f, -0.15f, -0.7f),
            new Vector3(0.035f, 4.85f, 1f), new Color(0.13f, 0.15f, 0.16f, 0.95f), 7);

        var tabs = new (string label, GuideTab tab, Color color)[]
        {
            ("自分の役職", GuideTab.MyRole,  new Color(0.31f, 0.77f, 0.97f, 1f)),
            ("配役情報",   GuideTab.RoleList, new Color(1f, 0.4f, 0.4f, 1f)),
        };
        float tabY = 1.72f;
        foreach (var (label, tab, color) in tabs)
        {
            var t = tab;
            MakeTabButton(guidePanel, label, new Vector3(-3.25f, tabY, -1f), color, tab == currentTab,
                () => { currentTab = t; selectedRole = CustomRoles.NotAssigned; scrollY = 0f; BuildPanel(); });
            tabY -= 0.78f;
        }

        // 右下の欠けた部分へ折り返した紙を重ね、手前へ折った角として見せる。
        MakeFoldTriangle(guidePanel, "FoldBorder", PanelW + 0.14f, PanelH + 0.14f, 0.66f,
            new Color(0.035f, 0.045f, 0.055f, 1f), new Color(0.035f, 0.045f, 0.055f, 1f), 7);
        MakeFoldTriangle(guidePanel, "FoldFace", PanelW, PanelH, 0.58f,
            new Color(0.68f, 0.68f, 0.63f, 1f), new Color(0.86f, 0.85f, 0.78f, 1f), 8);

        var foldLine = MakeSpriteChild(guidePanel, "FoldLine",
            new Vector3(PanelW / 2f - 0.29f, -PanelH / 2f + 0.29f, -0.8f),
            new Vector3(0.82f, 0.04f, 1f), new Color(0.10f, 0.12f, 0.13f, 0.95f), 9);
        foldLine.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

        var foldHighlight = MakeSpriteChild(guidePanel, "FoldHighlight",
            new Vector3(PanelW / 2f - 0.27f, -PanelH / 2f + 0.27f, -0.85f),
            new Vector3(0.76f, 0.018f, 1f), new Color(0.96f, 0.94f, 0.85f, 0.72f), 10);
        foldHighlight.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

        BuildContent();
        RaiseGuideSortingOrders();
    }

    private static void MakeCutCornerPanel(GameObject parent, string name, float width, float height,
        float cutSize, Color color, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        float halfW = width / 2f;
        float halfH = height / 2f;
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-halfW, -halfH, 0f),
                new Vector3(halfW - cutSize, -halfH, 0f),
                new Vector3(halfW, -halfH + cutSize, 0f),
                new Vector3(halfW, halfH, 0f),
                new Vector3(-halfW, halfH, 0f),
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3 },
            colors = new[] { color, color, color, color, color },
        };
        mesh.RecalculateBounds();
        obj.AddComponent<MeshFilter>().mesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static void MakeFoldTriangle(GameObject parent, string name, float width, float height,
        float cutSize, Color creaseColor, Color tipColor, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        float halfW = width / 2f;
        float halfH = height / 2f;
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(halfW - cutSize, -halfH, 0f),
                new Vector3(halfW, -halfH + cutSize, 0f),
                new Vector3(halfW, -halfH, 0f),
            },
            triangles = new[] { 0, 1, 2 },
            colors = new[] { creaseColor, creaseColor, tipColor },
        };
        mesh.RecalculateBounds();
        obj.AddComponent<MeshFilter>().mesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.white;
        renderer.sortingOrder = sortingOrder;
    }

    private static void RaiseGuideSortingOrders()
    {
        // HUDの「使用」ボタンなどが紙の上へ描画されないよう、ガイド一式を前面へ固定する。
        foreach (var renderer in guidePanel.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sortingOrder += 100;

        foreach (var text in guidePanel.GetComponentsInChildren<TextMeshPro>(true))
            text.sortingOrder += 100;

        foreach (var renderer in guidePanel.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.GetComponent<TextMeshPro>() == null)
                renderer.sortingOrder += 100;
        }
    }

    private static void ConfigureGuideSortingGroup()
    {
        int targetLayerId = 0;
        int highestLayerValue = int.MinValue;
        int highestOrder = 0;

        var hudRoot = HudManager.Instance.transform.root;
        foreach (var renderer in hudRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(guidePanel.transform)) continue;

            int layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
            if (layerValue > highestLayerValue)
            {
                highestLayerValue = layerValue;
                targetLayerId = renderer.sortingLayerID;
                highestOrder = renderer.sortingOrder;
            }
            else if (layerValue == highestLayerValue)
            {
                highestOrder = Mathf.Max(highestOrder, renderer.sortingOrder);
            }
        }

        var sortingGroup = guidePanel.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerID = targetLayerId;
        sortingGroup.sortingOrder = Mathf.Clamp(highestOrder + 100, -32000, 32000);
    }

    private static void MakeCloseButton()
    {
        var obj = new GameObject("CloseButton");
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = new Vector3(3.95f, 2.45f, -1.1f);
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        var bg = MakeSpriteChild(obj, "BG", Vector3.zero,
            new Vector3(0.48f, 0.48f, 1f), new Color(0.38f, 0.09f, 0.12f, 0.98f), 10);
        var bgRenderer = bg.GetComponent<SpriteRenderer>();
        var closeLabel = MakeText(obj, "Label", Vector3.zero, "×", 2.8f,
            TextAlignmentOptions.Center, new Vector2(0.48f, 0.48f));
        closeLabel.color = Color.white;

        var collider = obj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.52f, 0.52f);
        var button = obj.AddComponent<PassiveButton>();
        button.Colliders = new Collider2D[] { collider };
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((Action)(() =>
        {
            selectedRole = CustomRoles.NotAssigned;
            roleSearchText = "";
            ClosePanel();
        }));
        button.OnMouseOver = new UnityEngine.Events.UnityEvent();
        button.OnMouseOver.AddListener((Action)(() =>
        {
            if (bgRenderer != null) bgRenderer.color = new Color(0.78f, 0.15f, 0.18f, 1f);
        }));
        button.OnMouseOut = new UnityEngine.Events.UnityEvent();
        button.OnMouseOut.AddListener((Action)(() =>
        {
            if (bgRenderer != null) bgRenderer.color = new Color(0.38f, 0.09f, 0.12f, 0.98f);
        }));
    }

    private static void BuildContent()
    {
        switch (currentTab)
        {
            case GuideTab.MyRole: BuildMyRoleContent(); break;
            case GuideTab.RoleList: BuildRoleListContent(); break;
        }
    }

    private static void BuildMyRoleContent()
    {
        BuildMyRoleViewButtons();

        var localPc = PlayerControl.LocalPlayer;
        if (localPc == null) { MakeText(guidePanel, "t", Vector3.zero, "情報なし", 1.9f); return; }

        if (currentMyRoleView == MyRoleView.Addons)
        {
            BuildMyAddonsContent(localPc);
            return;
        }

        var role = localPc.GetCustomRole();
        var roleClass = localPc.GetRoleClass();
        if (localPc.Is(CustomRoles.Amnesia))
            role = localPc.Is(CustomRoleTypes.Crewmate) ? CustomRoles.Crewmate : CustomRoles.Impostor;
        if (localPc.GetMisidentify(out var missrole)) role = missrole;
        if (role is CustomRoles.Amnesiac && roleClass is Amnesiac amnesiac && !amnesiac.Realized)
            role = Amnesiac.IsWolf ? CustomRoles.WolfBoy : CustomRoles.Sheriff;

        BuildPaperRoleDetail(role, localPc.GetRoleDesc(true));
    }

    private static void BuildMyRoleViewButtons()
    {
        MakeMyRoleViewButton("役職", new Vector3(DetailX + 0.40f, 1.96f, -1f),
            new Color(0.31f, 0.77f, 0.97f, 1f), currentMyRoleView == MyRoleView.Role, () =>
            {
                currentMyRoleView = MyRoleView.Role;
                selectedRole = CustomRoles.NotAssigned;
                scrollY = 0f;
                BuildPanel();
            });
        MakeMyRoleViewButton("属性", new Vector3(DetailX + 1.26f, 1.96f, -1f),
            new Color(0.45f, 0.82f, 0.50f, 1f), currentMyRoleView == MyRoleView.Addons, () =>
            {
                currentMyRoleView = MyRoleView.Addons;
                selectedRole = CustomRoles.NotAssigned;
                scrollY = 0f;
                BuildPanel();
            });
    }

    private static void BuildMyAddonsContent(PlayerControl localPc)
    {
        activeListTop = ListTop;
        activeListHeight = ListH;
        var addons = localPc.GetCustomSubRoles()
            .Where(role => role != CustomRoles.NotAssigned && !role.IsGhostRole())
            .Distinct()
            .OrderBy(GetAddonGuideOrder)
            .ThenBy(role => UtilsRoleText.GetRoleName(role))
            .ToList();

        if (addons.Count == 0)
        {
            selectedRole = CustomRoles.NotAssigned;
            var noAddons = MakeText(guidePanel, "NoOwnedAddons",
                new Vector3(0.98f, 0.5f, -1f),
                "<color=#222222>現在付与されている属性はありません</color>",
                1.5f, TextAlignmentOptions.Center, new Vector2(DetailW, 1f));
            noAddons.fontStyle = FontStyles.Bold;
            return;
        }

        if (!addons.Contains(selectedRole)) selectedRole = addons[0];

        var container = new GameObject("MyAddonScrollContent");
        container.transform.SetParent(guidePanel.transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localScale = Vector3.one;
        container.layer = 5;
        scrollContent = container;

        float y = activeListTop;
        foreach (var addon in addons)
        {
            scrollSnapTargets.Add(Mathf.Max(0f, activeListTop - y));
            var currentAddon = addon;
            bool isSelected = selectedRole == currentAddon;
            var itemObj = new GameObject("OwnedAddon_" + currentAddon);
            itemObj.transform.SetParent(container.transform);
            itemObj.transform.localPosition = new Vector3(0f, y, 0f);
            itemObj.transform.localScale = Vector3.one;
            itemObj.layer = 5;
            scrollEntries.Add(itemObj);

            float bgWidth = ListW - 0.12f;
            var normalBgColor = isSelected
                ? new Color(0.68f, 0.84f, 0.70f, 1f)
                : new Color(0.84f, 0.86f, 0.87f, 1f);
            var hoverBgColor = new Color(0.72f, 0.86f, 0.75f, 1f);
            var itemBg = MakeSpriteChild(itemObj, "RowBG",
                new Vector3(ListLeft + bgWidth * 0.5f, -ItemH * 0.5f + 0.03f, 0.1f),
                new Vector3(bgWidth, ItemH * 0.88f, 1f), normalBgColor, 11);
            var itemBgRenderer = itemBg.GetComponent<SpriteRenderer>();

            var text = MakeOutlinedRoleText(itemObj, "Label",
                new Vector3(ListLeft + 0.1f, 0f, 0f),
                UtilsRoleText.GetRoleName(currentAddon), UtilsRoleText.GetRoleColorCode(currentAddon),
                1.08f, TextAlignmentOptions.TopLeft, new Vector2(ListW - 0.15f, ItemH),
                0.012f, 12, 13);

            var collider = itemObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(ListW, ItemH);
            collider.offset = new Vector2(ListLeft + ListW * 0.5f, -ItemH * 0.5f);
            var button = itemObj.AddComponent<PassiveButton>();
            button.Colliders = new Collider2D[] { collider };
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (roleListDragMoved) return;
                selectedRole = currentAddon;
                var savedScroll = scrollY;
                BuildPanel();
                scrollY = savedScroll;
                ApplyScroll();
            }));
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (itemBgRenderer) itemBgRenderer.color = hoverBgColor;
            }));
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (itemBgRenderer) itemBgRenderer.color = normalBgColor;
            }));

            y -= ItemH;
        }

        FinishScrollableList(y);
        BuildPaperRoleDetail(selectedRole);
    }

    private static void BuildRoleListContent()
    {
        activeListTop = RoleListTop;
        activeListHeight = RoleListH;
        CreateRoleSearchBox();

        // ★ スクロールコンテナ（移動する親オブジェクト）
        var container = new GameObject("ScrollContent");
        container.transform.SetParent(guidePanel.transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localScale = Vector3.one;
        container.layer = 5;
        scrollContent = container;

        // 有効な役職を収集
        var roles = Options.CustomRoleCounts.Keys
            .Where(r => r.IsEnable() && r != CustomRoles.GM && r != CustomRoles.NotAssigned)
            .Where(RoleMatchesSearch)
            .OrderBy(r => (int)GetGuideRoleCategory(r))
            .ThenBy(r => GetGuideRoleCategory(r) == GuideRoleCategory.Addon ? GetAddonGuideOrder(r) : 0)
            .ThenBy(r => r.ToString())
            .ToList();

        CreateCategoryJumpButtons(roles.Select(GetGuideRoleCategory).Distinct().ToList());

        float y = activeListTop;
        GuideRoleCategory? lastCategory = null;

        if (roles.Count == 0)
        {
            scrollSnapTargets.Add(0f);
            var noResult = MakeText(container, "NoSearchResults",
            new Vector3(ListLeft + 0.1f, y, 0f),
            "<color=#222222>該当する役職がありません</color>",
            1.0f, TextAlignmentOptions.TopLeft, new Vector2(ListW - 0.2f, ItemH));
            scrollEntries.Add(noResult.gameObject);
            y -= ItemH;
        }

        foreach (var role in roles)
        {
            var category = GetGuideRoleCategory(role);
            // 陣営ヘッダー
            if (category != lastCategory)
            {
                lastCategory = category;
                categoryScrollTargets[category] = Mathf.Max(0f, activeListTop - y);
                scrollSnapTargets.Add(categoryScrollTargets[category]);
                string headerLabel = category switch
                {
                    GuideRoleCategory.Impostor => "<color=#dd2929>☆Impostors</color>",
                    GuideRoleCategory.Madmate => "<color=#bd5429>☆MadMates</color>",
                    GuideRoleCategory.Crewmate => "<color=#087d8c>☆CrewMates</color>",
                    GuideRoleCategory.Neutral => "<color=#222222>☆Neutrals</color>",
                    GuideRoleCategory.GhostRole => "<color=#6250a4>☆Ghost Role</color>",
                    GuideRoleCategory.Addon => "<color=#02724f>☆Addon</color>",
                    _ => category.ToString()
                };
                var categoryCount = roles
                    .Where(r => GetGuideRoleCategory(r) == category)
                    .Sum(r => r.GetCount());
                headerLabel += $" <size=75%><color=#111111>({categoryCount})</color></size>";
                var headerTmp = MakeText(container, "Header_" + category,
                    new Vector3(ListLeft, y, 0f),
                    headerLabel, 1.05f, TextAlignmentOptions.TopLeft, new Vector2(ListW, CategoryHeaderH));
                headerTmp.sortingOrder = 12;
                scrollEntries.Add(headerTmp.gameObject);
                y -= CategoryHeaderH;
            }

            var r = role;
            scrollSnapTargets.Add(Mathf.Max(0f, activeListTop - y));
            var colorCode = UtilsRoleText.GetRoleColorCode(r);
            var roleName = UtilsRoleText.GetRoleName(r);
            bool isSelected = selectedRole == r;

            var itemObj = new GameObject("Item_" + r);
            itemObj.transform.SetParent(container.transform);
            itemObj.transform.localPosition = new Vector3(0f, y, 0f);
            itemObj.layer = 5;
            scrollEntries.Add(itemObj);

            // 選択・ホバー用の行背景
            float bgWidth = ListW - 0.12f;
            var normalBgColor = isSelected
                ? new Color(0.62f, 0.76f, 0.92f, 1f)
                : new Color(0.84f, 0.86f, 0.87f, 1f);
            var hoverBgColor = isSelected
                ? new Color(0.55f, 0.72f, 0.92f, 1f)
                : new Color(0.74f, 0.82f, 0.90f, 1f);
            var itemBg = MakeSpriteChild(itemObj, "RowBG",
                new Vector3(ListLeft + bgWidth * 0.5f, -ItemH * 0.5f + 0.03f, 0.1f),
                new Vector3(bgWidth, ItemH * 0.88f, 1f), normalBgColor, 11);
            var itemBgRenderer = itemBg.GetComponent<SpriteRenderer>();

            var txt = MakeOutlinedRoleText(itemObj, "RoleName",
                new Vector3(ListLeft + 0.1f, 0f, 0f),
                roleName, colorCode, 1.12f, TextAlignmentOptions.TopLeft,
                new Vector2(ListW - 0.47f, ItemH), 0.012f, 12, 13);
            txt.enableWordWrapping = false;
            txt.overflowMode = TextOverflowModes.Ellipsis;

            var countText = MakeText(itemObj, "Count",
                new Vector3(ListLeft + ListW - 0.16f, 0f, 0f),
                $"x{r.GetCount()}", 0.95f, TextAlignmentOptions.TopRight,
                new Vector2(0.30f, ItemH));
            countText.sortingOrder = 13;
            countText.color = new Color(0.07f, 0.07f, 0.07f, 1f);

            var col = itemObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(ListW, ItemH);
            col.offset = new Vector2(ListLeft + ListW * 0.5f, -ItemH * 0.5f);

            var btn = itemObj.AddComponent<PassiveButton>();
            btn.Colliders = new Collider2D[] { col };
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (roleListDragMoved) return;
                selectedRole = r;
                var savedScroll = scrollY;
                BuildPanel();
                scrollY = savedScroll;
                ApplyScroll();
            }));
            btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (itemBgRenderer) itemBgRenderer.color = hoverBgColor;
            }));
            btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
            btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (itemBgRenderer) itemBgRenderer.color = normalBgColor;
            }));

            y -= ItemH;
        }

        FinishScrollableList(y);

        // 詳細エリア
        BuildRoleDetailArea();
    }

    private static void CreateCategoryJumpButtons(List<GuideRoleCategory> categories)
    {
        if (categories.Count == 0) return;

        const float gap = 0.025f;
        const float rowY = 0.04f;
        float usableWidth = ListW - 0.08f;
        float buttonWidth = (usableWidth - gap * (categories.Count - 1)) / categories.Count;
        float startX = ListLeft + 0.04f;

        for (int index = 0; index < categories.Count; index++)
        {
            var category = categories[index];
            var (label, accentColor) = category switch
            {
                GuideRoleCategory.Impostor => ("I", new Color(0.87f, 0.16f, 0.16f, 1f)),
                GuideRoleCategory.Madmate => ("M", new Color(0.74f, 0.33f, 0.16f, 1f)),
                GuideRoleCategory.Crewmate => ("C", new Color(0.03f, 0.49f, 0.55f, 1f)),
                GuideRoleCategory.Neutral => ("N", new Color(0.18f, 0.18f, 0.18f, 1f)),
                GuideRoleCategory.GhostRole => ("G", new Color(0.38f, 0.31f, 0.64f, 1f)),
                GuideRoleCategory.Addon => ("A", new Color(0.01f, 0.45f, 0.31f, 1f)),
                _ => ("?", Color.black),
            };

            var buttonObject = new GameObject("CategoryJump_" + category);
            buttonObject.transform.SetParent(guidePanel.transform);
            buttonObject.transform.localPosition = new Vector3(
                startX + buttonWidth * 0.5f + index * (buttonWidth + gap), rowY, -1f);
            buttonObject.transform.localScale = Vector3.one;
            buttonObject.layer = 5;

            var background = MakeSpriteChild(buttonObject, "Background", Vector3.zero,
                new Vector3(buttonWidth, 0.27f, 1f), new Color(0.88f, 0.89f, 0.90f, 1f), 10);
            var backgroundRenderer = background.GetComponent<SpriteRenderer>();
            MakeSpriteChild(buttonObject, "Accent", new Vector3(0f, -0.115f, -0.1f),
                new Vector3(buttonWidth * 0.82f, 0.035f, 1f), accentColor, 11);
            var icon = MakeText(buttonObject, "Icon", new Vector3(0f, 0.01f, -0.2f), label, 0.9f,
                TextAlignmentOptions.Center, new Vector2(buttonWidth, 0.23f));
            icon.color = accentColor;
            icon.sortingOrder = 12;

            var collider = buttonObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(buttonWidth, 0.27f);
            var button = buttonObject.AddComponent<PassiveButton>();
            button.Colliders = new Collider2D[] { collider };
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (!categoryScrollTargets.TryGetValue(category, out float target)) return;
                scrollY = Mathf.Clamp(target, 0f, maxScrollY);
                ApplyScroll();
            }));
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (backgroundRenderer) backgroundRenderer.color = new Color(0.72f, 0.78f, 0.84f, 1f);
            }));
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (backgroundRenderer) backgroundRenderer.color = new Color(0.88f, 0.89f, 0.90f, 1f);
            }));
        }
    }

    private static void FinishScrollableList(float contentBottomY)
    {
        float totalH = activeListTop - contentBottomY;
        float categoryTargetMax = categoryScrollTargets.Count == 0
            ? 0f
            : categoryScrollTargets.Values.Max();
        maxScrollY = Mathf.Max(Mathf.Max(0f, totalH - activeListHeight), categoryTargetMax);
        float effectiveContentHeight = Mathf.Max(totalH, activeListHeight + maxScrollY);

        float sbarCenterX = SbarX;
        float sbarCenterY = activeListTop - activeListHeight * 0.5f;
        MakeSpriteOnPanel("SbarBG", new Vector3(sbarCenterX, sbarCenterY, -0.5f),
            new Vector3(SbarW, activeListHeight, 1f), new Color(0.1f, 0.1f, 0.2f, 0.9f), 8);

        float thumbH = maxScrollY > 0f
            ? Mathf.Max(0.3f, activeListHeight * (activeListHeight / (effectiveContentHeight + 0.001f)))
            : activeListHeight;

        var thumbObj = new GameObject("SbarThumb");
        thumbObj.transform.SetParent(guidePanel.transform);
        thumbObj.transform.localScale = new Vector3(SbarW * 0.7f, thumbH, 1f);
        thumbObj.layer = 5;
        var thumbRenderer = thumbObj.AddComponent<SpriteRenderer>();
        thumbRenderer.sprite = SquareSprite;
        thumbRenderer.color = new Color(0.5f, 0.6f, 0.9f, 0.95f);
        thumbRenderer.material = new Material(Shader.Find("Sprites/Default"));
        thumbRenderer.sortingOrder = 9;
        scrollThumb = thumbObj;

        var thumbCollider = thumbObj.AddComponent<BoxCollider2D>();
        thumbCollider.size = new Vector2(1f, 1f);
        var thumbButton = thumbObj.AddComponent<PassiveButton>();
        thumbButton.Colliders = new Collider2D[] { thumbCollider };
        thumbButton.OnClick = new Button.ButtonClickedEvent();
        thumbButton.OnMouseOver = new UnityEngine.Events.UnityEvent();
        thumbButton.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (thumbRenderer) thumbRenderer.color = new Color(0.7f, 0.8f, 1f, 1f);
        }));
        thumbButton.OnMouseOut = new UnityEngine.Events.UnityEvent();
        thumbButton.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (thumbRenderer) thumbRenderer.color = new Color(0.5f, 0.6f, 0.9f, 0.95f);
        }));

        const float arrowH = 0.25f;
        MakeScrollArrowButton("SbarUp", new Vector3(sbarCenterX, activeListTop + arrowH * 0.5f, -0.6f),
            new Vector3(0.26f, arrowH, 1f), "▲", -1f);
        MakeScrollArrowButton("SbarDown", new Vector3(sbarCenterX, activeListTop - activeListHeight - arrowH * 0.5f, -0.6f),
            new Vector3(0.26f, arrowH, 1f), "▼", 1f);

        ApplyScroll();
    }

    private static bool RoleMatchesSearch(CustomRoles role)
    {
        var query = roleSearchText?.Trim();
        if (string.IsNullOrEmpty(query)) return true;

        return UtilsRoleText.GetRoleName(role).RemoveHtmlTags()
                   .Contains(query, StringComparison.OrdinalIgnoreCase)
            || role.GetCombinationName(false).RemoveHtmlTags()
                   .Contains(query, StringComparison.OrdinalIgnoreCase)
            || role.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static void CreateRoleSearchBox()
    {
        const float searchY = 0.43f;
        float searchW = ListW - 0.12f;

        var inputObj = new GameObject("RoleSearchBox");
        inputObj.transform.SetParent(guidePanel.transform);
        inputObj.transform.localPosition = new Vector3(ListLeft + searchW * 0.5f, searchY, -1f);
        inputObj.transform.localScale = Vector3.one;
        inputObj.layer = 5;

        MakeSpriteChild(inputObj, "Border", Vector3.zero,
            new Vector3(searchW + 0.04f, 0.38f, 1f), new Color(0.13f, 0.15f, 0.16f, 1f), 9);
        var background = MakeSpriteChild(inputObj, "Background", Vector3.zero,
            new Vector3(searchW, 0.34f, 1f), Color.white, 10);
        var backgroundRenderer = background.GetComponent<SpriteRenderer>();

        var collider = inputObj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(searchW, 0.34f);

        var textBox = inputObj.AddComponent<TextBoxTMP>();
        roleSearchBox = textBox;
        textBox.AllowEmail = false;
        textBox.AllowSymbols = true;
        textBox.AllowPaste = true;
        textBox.allowAllCharacters = true;
        textBox.tempTxt = new();
        textBox.compoText = "";
        textBox.text = roleSearchText ?? "";
        textBox.caretPos = textBox.text.Length;
        textBox.characterLimit = 32;
        textBox.OnChange = new();
        textBox.OnEnter = new();
        textBox.OnFocusLost = new();

        var output = MakeText(inputObj, "InputText",
            new Vector3(-searchW * 0.5f + 0.14f, 0.12f, -0.1f),
            roleSearchText ?? "", 1.2f, TextAlignmentOptions.TopLeft,
            new Vector2(searchW - 0.28f, 0.28f));
        output.fontStyle = FontStyles.Normal;
        output.enableWordWrapping = false;
        output.overflowMode = TextOverflowModes.Truncate;
        textBox.outputText = output;

        var placeholder = MakeText(inputObj, "Placeholder",
            new Vector3(-searchW * 0.5f + 0.14f, 0.12f, -0.05f),
            "<color=#666666>役職を検索...</color>", 1.15f,
            TextAlignmentOptions.TopLeft, new Vector2(searchW - 0.28f, 0.28f));
        roleSearchPlaceholder = placeholder;
        placeholder.fontStyle = FontStyles.Normal;
        placeholder.gameObject.SetActive(string.IsNullOrEmpty(roleSearchText));

        var button = inputObj.AddComponent<PassiveButton>();
        button.Colliders = new Collider2D[] { collider };
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((Action)(() =>
        {
            Input.imeCompositionMode = IMECompositionMode.Auto;
            textBox.GiveFocus();
            textBox.caretPos = (textBox.text ?? "").Length;
            roleSearchCaretTimer = 0f;
            placeholder.gameObject.SetActive(false);
        }));
        button.OnMouseOver = new UnityEngine.Events.UnityEvent();
        button.OnMouseOver.AddListener((Action)(() =>
        {
            if (backgroundRenderer) backgroundRenderer.color = new Color(0.90f, 0.95f, 1f, 1f);
        }));
        button.OnMouseOut = new UnityEngine.Events.UnityEvent();
        button.OnMouseOut.AddListener((Action)(() =>
        {
            if (backgroundRenderer) backgroundRenderer.color = Color.white;
        }));

        textBox.OnChange.AddListener((Action)(() =>
        {
            placeholder.gameObject.SetActive(string.IsNullOrEmpty(textBox.text) && !textBox.hasFocus);
        }));
        textBox.OnFocusLost.AddListener((Action)(() =>
        {
            roleSearchCaretTimer = 0f;
            placeholder.gameObject.SetActive(string.IsNullOrEmpty(textBox.text));
            if (textBox.outputText != null) textBox.outputText.text = textBox.text ?? "";
        }));
        textBox.OnEnter.AddListener((Action)(() =>
        {
            roleSearchText = textBox.text ?? "";
            scrollY = 0f;
            selectedRole = CustomRoles.NotAssigned;
            BuildPanel();
        }));
    }

    private static void UpdateRoleSearchCaret()
    {
        if (roleSearchBox == null || roleSearchBox.outputText == null) return;

        string committedText = roleSearchBox.text ?? "";
        string compositionText = roleSearchBox.compoText ?? "";
        int caretPosition = Mathf.Clamp(roleSearchBox.caretPos, 0, committedText.Length);
        string displayedText = committedText.Insert(caretPosition, compositionText);
        if (!roleSearchBox.hasFocus)
        {
            roleSearchCaretTimer = 0f;
            roleSearchBox.outputText.text = displayedText;
            if (roleSearchPlaceholder != null)
                roleSearchPlaceholder.gameObject.SetActive(string.IsNullOrEmpty(displayedText));
            return;
        }

        roleSearchCaretTimer = (roleSearchCaretTimer + Time.deltaTime) % 1f;
        if (roleSearchCaretTimer < 0.5f)
        {
            int displayedCaretPosition = caretPosition + compositionText.Length;
            displayedText = displayedText.Insert(displayedCaretPosition, "<color=#007f9e>|</color>");
        }
        roleSearchBox.outputText.text = displayedText;
        if (roleSearchPlaceholder != null) roleSearchPlaceholder.gameObject.SetActive(false);
    }

    private static void MakeScrollArrowButton(string name, Vector3 pos, Vector3 scale,
        string label, float scrollDir)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.layer = 5;

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = new Color(0.2f, 0.2f, 0.4f, 0.9f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 9;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        var btn = obj.AddComponent<PassiveButton>();
        btn.Colliders = new Collider2D[] { col };
        btn.OnClick = new Button.ButtonClickedEvent();
        btn.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            StepScroll(scrollDir > 0f ? 1 : -1);
        }));
        btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() => sr.color = new Color(0.4f, 0.4f, 0.7f, 1f)));
        btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() => sr.color = new Color(0.2f, 0.2f, 0.4f, 0.9f)));

        var labelText = MakeText(guidePanel, name + "Label",
            new Vector3(pos.x, pos.y, pos.z - 0.1f), label, 0.85f,
            TextAlignmentOptions.Center, new Vector2(0.3f, scale.y));
        labelText.color = Color.white;
        labelText.sortingOrder = 10;
    }

    private static void ApplyScroll()
    {
        if (scrollContent == null) return;
        scrollY = Mathf.Clamp(scrollY, 0f, Mathf.Max(0f, maxScrollY));

        // ★ コンテナを上下にずらすだけ（SetActiveは使わない）
        scrollContent.transform.localPosition =
            new Vector3(0f, scrollY, 0f);
        UpdateScrollEntryVisibility();

        // ★ スクロールバーつまみ位置更新
        UpdateScrollThumb(false);
    }

    private static List<float> GetOrderedScrollSnapTargets()
        => scrollSnapTargets
            .Select(target => Mathf.Clamp(target, 0f, maxScrollY))
            .Distinct()
            .OrderBy(target => target)
            .ToList();

    private static float SnapScrollPosition(float position)
    {
        var targets = GetOrderedScrollSnapTargets();
        if (targets.Count == 0) return Mathf.Clamp(position, 0f, maxScrollY);

        float closest = targets[0];
        float closestDistance = Mathf.Abs(position - closest);
        foreach (float target in targets)
        {
            float distance = Mathf.Abs(position - target);
            if (distance >= closestDistance) continue;
            closest = target;
            closestDistance = distance;
        }
        return closest;
    }

    private static void StepScroll(int direction)
    {
        var targets = GetOrderedScrollSnapTargets();
        if (targets.Count == 0)
        {
            scrollY += direction * ItemH;
            ApplyScroll();
            return;
        }

        float next = direction > 0 ? targets[^1] : targets[0];
        if (direction > 0)
        {
            foreach (float target in targets)
            {
                if (target <= scrollY + 0.001f) continue;
                next = target;
                break;
            }
        }
        else
        {
            for (int index = targets.Count - 1; index >= 0; index--)
            {
                if (targets[index] >= scrollY - 0.001f) continue;
                next = targets[index];
                break;
            }
        }

        scrollY = next;
        ApplyScroll();
    }

    private static void UpdateScrollEntryVisibility()
    {
        float bottom = activeListTop - activeListHeight;
        foreach (var entry in scrollEntries)
        {
            if (entry == null) continue;
            float entryTop = entry.transform.localPosition.y + scrollY;
            bool visible = entryTop <= activeListTop + 0.01f && entryTop >= bottom + ItemH - 0.01f;
            if (entry.activeSelf != visible) entry.SetActive(visible);
        }
    }

    private static void UpdateScrollThumb(bool handleInput = true)
    {
        if (scrollThumb == null) return;

        float sbarCenterX = SbarX;
        float thumbH = scrollThumb.transform.localScale.y;
        float trackH = activeListHeight - thumbH;
        float t = maxScrollY > 0f ? scrollY / maxScrollY : 0f;
        float thumbY = activeListTop - thumbH * 0.5f - t * trackH;
        scrollThumb.transform.localPosition = new Vector3(sbarCenterX, thumbY, -0.6f);

        if (!handleInput) return;

        // スクロールバードラッグ処理
        if (scrollBarDragging)
        {
            if (!Input.GetMouseButton(0))
            {
                scrollBarDragging = false;
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;
            var worldMouse = cam.ScreenToWorldPoint(Input.mousePosition);
            if (guidePanel == null) return;
            float mouseY = guidePanel.transform.InverseTransformPoint(worldMouse).y;
            float delta = mouseY - scrollBarDragStartMouseY;
            // デルタをスクロール量に変換（トラック長に対する比率）
            float scrollDelta = trackH > 0.001f ? -(delta / trackH) * maxScrollY : 0f;
            scrollY = SnapScrollPosition(scrollBarDragStartScrollY + scrollDelta);
            ApplyScroll();
        }
        else if (Input.GetMouseButtonDown(0) && maxScrollY > 0f)
        {
            // つまみをクリックしたらドラッグ開始
            var cam = Camera.main;
            if (cam == null) return;
            var worldMouse = cam.ScreenToWorldPoint(Input.mousePosition);
            // パネルローカル座標に変換
            if (guidePanel == null) return;
            var localMouse = guidePanel.transform.InverseTransformPoint(worldMouse);
            float tx = scrollThumb.transform.localPosition.x;
            float ty = scrollThumb.transform.localPosition.y;
            float hw = SbarW * 0.5f, hh = thumbH * 0.5f;
            if (localMouse.x >= tx - hw && localMouse.x <= tx + hw &&
                localMouse.y >= ty - hh && localMouse.y <= ty + hh)
            {
                scrollBarDragging = true;
                scrollBarDragStartMouseY = localMouse.y;
                scrollBarDragStartScrollY = scrollY;
            }
        }
    }

    private static void UpdateRoleListDrag()
    {
        if (guidePanel == null || scrollContent == null || scrollBarDragging) return;

        var cam = Camera.main;
        if (cam == null) return;
        var worldMouse = cam.ScreenToWorldPoint(Input.mousePosition);
        var localMouse = guidePanel.transform.InverseTransformPoint(worldMouse);

        if (!roleListDragging && Input.GetMouseButtonDown(0))
        {
            bool isInsideList = localMouse.x >= ListLeft && localMouse.x <= ListLeft + ListW
                && localMouse.y <= activeListTop && localMouse.y >= activeListTop - activeListHeight;
            if (!isInsideList) return;

            roleListDragging = true;
            roleListDragMoved = false;
            roleListDragStartMouseY = localMouse.y;
            roleListDragStartScrollY = scrollY;
            return;
        }

        if (!roleListDragging) return;
        if (!Input.GetMouseButton(0))
        {
            roleListDragging = false;
            return;
        }

        float delta = localMouse.y - roleListDragStartMouseY;
        if (Mathf.Abs(delta) >= 0.05f) roleListDragMoved = true;
        if (!roleListDragMoved) return;

        scrollY = SnapScrollPosition(roleListDragStartScrollY + delta);
        ApplyScroll();
    }

    private static void BuildRoleDetailArea()
    {
        if (selectedRole == CustomRoles.NotAssigned)
        {
            MakeText(guidePanel, "DetailHint",
                new Vector3(0.98f, 0.5f, -1f),
                "<color=#555555>← 役職を選択</color>",
                1.5f, TextAlignmentOptions.Center, new Vector2(DetailW, 1f));
            return;
        }

        BuildPaperRoleDetail(selectedRole);
    }

    private static void BuildPaperRoleDetail(CustomRoles role, string fallbackDescription = null)
    {
        var info = role.GetRoleInfo();
        var description = info?.Description;
        string roleName = UtilsRoleText.GetRoleName(role);
        string colorCode = UtilsRoleText.GetRoleColorCode(role);
        string blurb = description?.Blurb ?? GetString($"{role}Info");
        string body = description?.Description;
        if (string.IsNullOrWhiteSpace(body) || body == $"{role}InfoLong") body = fallbackDescription;
        if (string.IsNullOrWhiteSpace(body)) body = GetString($"{role}InfoLong");
        if (string.IsNullOrWhiteSpace(body) || body == $"{role}InfoLong") body = "説明なし";

        string team = "-";
        string count = "-";
        string basis = "-";
        bool isAddon = GetGuideRoleCategory(role) == GuideRoleCategory.Addon;
        if (isAddon)
        {
            team = "属性";
            count = GetAddonGuideOrder(role) switch
            {
                0 => "デバフ",
                1 => "バフ",
                _ => "コモン",
            };
            basis = "付与済み";
        }
        else if (info != null)
        {
            var roleTeam = info.CustomRoleType == CustomRoleTypes.Madmate
                ? CustomRoleTypes.Impostor
                : info.CustomRoleType;
            team = GetString($"CustomRoleTypes.{roleTeam}");
            count = GetCountTypeDisplayName(info.CountType);
            basis = GetString(info.BaseRoleType.Invoke().ToString());
        }

        var ink = new Color(0.10f, 0.12f, 0.14f, 1f);
        var mutedInk = new Color(0.10f, 0.12f, 0.14f, 1f);
        bool hasMyRoleViewButtons = currentTab == GuideTab.MyRole;
        float roleTitleY = hasMyRoleViewButtons ? 1.56f : 1.86f;
        float roleBlurbY = hasMyRoleViewButtons ? 1.15f : 1.43f;
        float roleMarkY = hasMyRoleViewButtons ? 1.35f : 1.62f;
        float roleMetaY = hasMyRoleViewButtons ? 1.60f : 1.87f;
        float headerRuleY = hasMyRoleViewButtons ? 0.87f : 1.12f;
        float descriptionHeaderY = hasMyRoleViewButtons ? 0.73f : 0.98f;
        float descriptionBodyY = hasMyRoleViewButtons ? 0.41f : 0.66f;
        float descriptionHeight = hasMyRoleViewButtons ? 1.18f : 1.42f;

        var roleTitle = MakeOutlinedRoleText(guidePanel, "PaperRoleName",
            new Vector3(DetailX, roleTitleY, -1f),
            roleName, colorCode, 2.4f,
            TextAlignmentOptions.TopLeft, new Vector2(3.8f, 0.45f),
            0.015f, 10, 11);
        roleTitle.color = ink;

        var roleBlurb = MakeText(guidePanel, "PaperRoleBlurb",
            new Vector3(DetailX, roleBlurbY, -1f), blurb, 1.25f,
            TextAlignmentOptions.TopLeft, new Vector2(3.9f, 0.35f));
        roleBlurb.color = mutedInk;
        roleBlurb.fontStyle = FontStyles.Bold;
        roleBlurb.overflowMode = TextOverflowModes.Ellipsis;

        MakeSprite(guidePanel, "RoleMark", new Vector3(2.35f, roleMarkY, -0.9f),
            new Vector3(0.24f, 0.62f, 1f), UtilsRoleText.GetRoleColor(role), 9);
        var meta = MakeText(guidePanel, "PaperRoleMeta",
            new Vector3(2.55f, roleMetaY, -1f),
            isAddon
                ? $"種別: {team}\n分類: {count}\n状態: {basis}"
                : $"陣営: {team}\nカウント: {count}\nベース: {basis}", 1.05f,
            TextAlignmentOptions.TopLeft, new Vector2(1.45f, 0.9f));
        meta.color = ink;
        meta.fontStyle = FontStyles.Normal;

        MakeSprite(guidePanel, "HeaderRule", new Vector3(0.98f, headerRuleY, -0.8f),
            new Vector3(DetailW, 0.025f, 1f), new Color(0.15f, 0.17f, 0.18f, 0.8f), 8);

        var descHeader = MakeText(guidePanel, "DescriptionHeader",
            new Vector3(DetailX, descriptionHeaderY, -1f), isAddon ? "属性説明" : "役職説明", 1.45f,
            TextAlignmentOptions.TopLeft, new Vector2(2f, 0.3f));
        descHeader.color = ink;
        var descBody = MakeText(guidePanel, "DescriptionBody",
            new Vector3(DetailX, descriptionBodyY, -1f), body, 1.22f,
            TextAlignmentOptions.TopLeft, new Vector2(DetailW, descriptionHeight));
        descBody.color = ink;
        descBody.fontStyle = FontStyles.Normal;
        descBody.overflowMode = TextOverflowModes.Ellipsis;

        MakeSprite(guidePanel, "SettingsRule", new Vector3(0.98f, -0.91f, -0.8f),
            new Vector3(DetailW, 0.025f, 1f), new Color(0.15f, 0.17f, 0.18f, 0.8f), 8);
        var settingsHeader = MakeText(guidePanel, "SettingsHeader",
            new Vector3(DetailX, -1.05f, -1f), "設定", 1.45f,
            TextAlignmentOptions.TopLeft, new Vector2(2f, 0.3f));
        settingsHeader.color = ink;

        string settingsText = GetRoleSettingsText(role, info);
        var (leftSettings, rightSettings, maxLines, maxCharacters) = SplitSettingsIntoColumns(settingsText);
        const float settingsGap = 0.22f;
        float settingsColumnWidth = string.IsNullOrEmpty(rightSettings)
            ? DetailW
            : (DetailW - settingsGap) / 2f;
        float heightScale = 7f / Mathf.Max(maxLines, 7);
        float widthScale = 24f / Mathf.Max(maxCharacters, 24);
        float settingsFontSize = Mathf.Clamp(1.08f * Mathf.Min(1f, Mathf.Min(heightScale, widthScale)), 0.66f, 1.08f);

        var leftSettingsBody = MakeText(guidePanel, "SettingsBodyLeft",
            new Vector3(DetailX, -1.37f, -1f), leftSettings, settingsFontSize,
            TextAlignmentOptions.TopLeft, new Vector2(settingsColumnWidth, 1.22f));
        leftSettingsBody.color = ink;
        leftSettingsBody.fontStyle = FontStyles.Normal;
        leftSettingsBody.enableWordWrapping = false;
        leftSettingsBody.overflowMode = TextOverflowModes.Overflow;

        if (!string.IsNullOrEmpty(rightSettings))
        {
            MakeSprite(guidePanel, "SettingsColumnDivider",
                new Vector3(DetailX + DetailW / 2f, -1.94f, -0.8f),
                new Vector3(0.012f, 1.14f, 1f), new Color(0.55f, 0.55f, 0.55f, 0.55f), 8);

            var rightSettingsBody = MakeText(guidePanel, "SettingsBodyRight",
                new Vector3(DetailX + settingsColumnWidth + settingsGap, -1.37f, -1f),
                rightSettings, settingsFontSize, TextAlignmentOptions.TopLeft,
                new Vector2(settingsColumnWidth, 1.22f));
            rightSettingsBody.color = ink;
            rightSettingsBody.fontStyle = FontStyles.Normal;
            rightSettingsBody.enableWordWrapping = false;
            rightSettingsBody.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static string GetCountTypeDisplayName(CountTypes countType)
    {
        string displayName = countType switch
        {
            CountTypes.OutOfGame => "ゲーム外",
            CountTypes.None => "カウントしない",
            CountTypes.Crew => UtilsRoleText.GetRoleName(CustomRoles.Crewmate),
            CountTypes.Impostor => UtilsRoleText.GetRoleName(CustomRoles.Impostor),
            CountTypes.Jackal => UtilsRoleText.GetRoleName(CustomRoles.Jackal),
            CountTypes.Remotekiller => UtilsRoleText.GetRoleName(CustomRoles.Remotekiller),
            CountTypes.TaskPlayer => UtilsRoleText.GetRoleName(CustomRoles.TaskPlayerB),
            CountTypes.GrimReaper => UtilsRoleText.GetRoleName(CustomRoles.GrimReaper),
            CountTypes.Fox => UtilsRoleText.GetRoleName(CustomRoles.Fox),
            CountTypes.MilkyWay => GetString("MilkyWay"),
            CountTypes.Pavlov => GetString("Pavlov"),
            CountTypes.Eater => UtilsRoleText.GetRoleName(CustomRoles.Eater),
            CountTypes.Monika => UtilsRoleText.GetRoleName(CustomRoles.Monika),
            CountTypes.StandMaster => UtilsRoleText.GetRoleName(CustomRoles.StandMaster),
            CountTypes.Villain => UtilsRoleText.GetRoleName(CustomRoles.Villain),
            _ => countType.ToString(),
        };
        return displayName.RemoveHtmlTags().RemoveColorTags();
    }

    private static (string left, string right, int maxLines, int maxCharacters)
        SplitSettingsIntoColumns(string settingsText)
    {
        var lines = settingsText.Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count <= 1)
        {
            string singleLine = lines.Count == 0 ? "設定なし" : lines[0];
            return (singleLine, "", 1, singleLine.Length);
        }

        int splitIndex = (lines.Count + 1) / 2;
        var leftLines = lines.Take(splitIndex).ToList();
        var rightLines = lines.Skip(splitIndex).ToList();
        int maxLines = Mathf.Max(leftLines.Count, rightLines.Count);
        int maxCharacters = lines.Max(line => line.Length);
        return (string.Join("\n", leftLines), string.Join("\n", rightLines), maxLines, maxCharacters);
    }

    private static string GetRoleSettingsText(CustomRoles role, SimpleRoleInfo info)
    {
        var builder = new StringBuilder();
        if (Options.CustomRoleSpawnChances.TryGetValue(role, out var option))
            UtilsShowOption.ShowChildrenSettings(option, ref builder);
        else if (role is CustomRoles.Braid && Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Driver, out option))
            UtilsShowOption.ShowChildrenSettings(option, ref builder);
        else if (role is CustomRoles.Altair && Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Vega, out option))
            UtilsShowOption.ShowChildrenSettings(option, ref builder);

        if (info?.CustomRoleType == CustomRoleTypes.Madmate && role is not CustomRoles.SatsumatoImoM)
        {
            builder.Append($"{Options.MadMateOption.GetName()}: {Options.MadMateOption.GetString()}\n");
            UtilsShowOption.ShowChildrenSettings(Options.MadMateOption, ref builder);
        }

        string result = builder.ToString().RemoveColorTags().RemoveSizeTags();
        return string.IsNullOrWhiteSpace(result) ? "設定なし" : result;
    }

    // ヘルパー：パネル直下にスプライト
    private static void MakeSpriteOnPanel(string name, Vector3 pos, Vector3 scale, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.layer = 5;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = order;
    }

    private static GameObject MakeSpriteChild(GameObject parent, string name, Vector3 localPos,
        Vector3 scale, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;
        obj.layer = 5;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSprite;
        sr.color = color;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = order;
        return obj;
    }

    private static void MakeSprite(GameObject parent, string name, Vector3 pos, Vector3 scale,
        Color color, int order = 5)
        => MakeSpriteChild(parent, name, pos, scale, color, order);

    private static TextMeshPro MakeText(GameObject parent, string name, Vector3 pos, string text,
        float size, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector2? rectSize = null)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        tmp.sortingOrder = 11;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.fontStyle = FontStyles.Bold;
        if (rectSize.HasValue)
        {
            tmp.rectTransform.sizeDelta = rectSize.Value;
            tmp.rectTransform.pivot = align switch
            {
                TextAlignmentOptions.Center => new Vector2(0.5f, 0.5f),
                TextAlignmentOptions.Top => new Vector2(0.5f, 1f),
                TextAlignmentOptions.TopRight => new Vector2(1f, 1f),
                _ => new Vector2(0f, 1f),
            };
        }
        return tmp;
    }

    private static TextMeshPro MakeOutlinedRoleText(GameObject parent, string name, Vector3 position,
        string roleName, string colorCode, float size, TextAlignmentOptions alignment,
        Vector2 rectSize, float outlineOffset, int outlineOrder, int faceOrder)
    {
        var outlineOffsets = new[]
        {
            new Vector2(-outlineOffset, 0f), new Vector2(outlineOffset, 0f),
            new Vector2(0f, -outlineOffset), new Vector2(0f, outlineOffset),
            new Vector2(-outlineOffset, -outlineOffset), new Vector2(-outlineOffset, outlineOffset),
            new Vector2(outlineOffset, -outlineOffset), new Vector2(outlineOffset, outlineOffset),
        };
        string plainRoleName = roleName.RemoveHtmlTags();
        for (int index = 0; index < outlineOffsets.Length; index++)
        {
            var offset = outlineOffsets[index];
            var outline = MakeText(parent, $"{name}_Outline{index}",
                position + new Vector3(offset.x, offset.y, 0.01f), plainRoleName,
                size, alignment, rectSize);
            outline.color = Color.black;
            outline.sortingOrder = outlineOrder;
            outline.enableWordWrapping = false;
            outline.overflowMode = TextOverflowModes.Ellipsis;
        }

        var face = MakeText(parent, name, position,
            $"<color={colorCode}>{roleName}</color>", size, alignment, rectSize);
        face.sortingOrder = faceOrder;
        face.enableWordWrapping = false;
        face.overflowMode = TextOverflowModes.Ellipsis;
        return face;
    }

    private static void MakeMyRoleViewButton(string label, Vector3 position, Color accentColor,
        bool isSelected, Action onClick)
    {
        var obj = new GameObject("MyRoleView_" + label);
        obj.transform.SetParent(guidePanel.transform);
        obj.transform.localPosition = position;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        var normalColor = isSelected
            ? Color.white
            : new Color(0.92f, 0.92f, 0.92f, 1f);
        MakeSpriteChild(obj, "Border", Vector3.zero, new Vector3(0.80f, 0.35f, 1f),
            new Color(0.13f, 0.15f, 0.16f, 0.96f), 8);
        var background = MakeSpriteChild(obj, "Background", Vector3.zero, new Vector3(0.75f, 0.30f, 1f),
            normalColor, 9);
        var backgroundRenderer = background.GetComponent<SpriteRenderer>();
        MakeSpriteChild(obj, "Underline", new Vector3(0f, -0.132f, -0.1f),
            new Vector3(0.68f, 0.035f, 1f), accentColor, 10);

        var text = MakeText(obj, "Label", new Vector3(0f, 0.015f, -0.2f), label, 1.08f,
            TextAlignmentOptions.Center, new Vector2(0.70f, 0.27f));
        text.color = new Color(0.16f, 0.18f, 0.18f, 1f);

        var collider = obj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.80f, 0.35f);
        var button = obj.AddComponent<PassiveButton>();
        button.Colliders = new Collider2D[] { collider };
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityEngine.Events.UnityAction)onClick);
        button.OnMouseOver = new UnityEngine.Events.UnityEvent();
        button.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && backgroundRenderer)
                backgroundRenderer.color = new Color(0.88f, 0.88f, 0.82f, 1f);
        }));
        button.OnMouseOut = new UnityEngine.Events.UnityEvent();
        button.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && backgroundRenderer) backgroundRenderer.color = normalColor;
        }));
    }

    private static void MakeTabButton(GameObject parent, string label, Vector3 pos,
        Color accentColor, bool isSelected, Action onClick)
    {
        var obj = new GameObject($"Tab_{label}");
        obj.transform.SetParent(parent.transform);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.layer = 5;

        Color bgColor = isSelected
            ? Color.white
            : new Color(0.92f, 0.92f, 0.92f, 1f);

        MakeSpriteChild(obj, "Border", Vector3.zero, new Vector3(1.76f, 0.66f, 1f),
            new Color(0.13f, 0.15f, 0.16f, 0.96f), 6);
        var bg = MakeSpriteChild(obj, "BG", Vector3.zero, new Vector3(1.70f, 0.60f, 1f), bgColor, 7);
        var bgSr = bg.GetComponent<SpriteRenderer>();
        MakeSpriteChild(obj, "Underline", new Vector3(0f, -0.272f, -0.1f),
            new Vector3(1.58f, 0.055f, 1f), accentColor, 8);

        var tmp = MakeText(obj, "Lbl", new Vector3(0f, 0.025f, -0.2f), label, 1.55f,
            TextAlignmentOptions.Center, new Vector2(1.55f, 0.54f));
        tmp.color = isSelected
            ? new Color(0.12f, 0.14f, 0.16f, 1f)
            : new Color(0.28f, 0.29f, 0.28f, 1f);

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.76f, 0.66f);
        var btn = obj.AddComponent<PassiveButton>();
        btn.Colliders = new Collider2D[] { col };
        btn.OnClick = new Button.ButtonClickedEvent();
        btn.OnClick.AddListener((UnityEngine.Events.UnityAction)onClick);
        btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && bgSr) bgSr.color = new Color(0.91f, 0.90f, 0.84f, 1f);
        }));
        btn.OnMouseOut = new UnityEngine.Events.UnityEvent();
        btn.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (!isSelected && bgSr) bgSr.color = bgColor;
        }));
    }

    public static void UpdateTick()
    {
        // H キーで開閉
        if (Input.GetKeyDown(KeyCode.H) && !IsTextInputActive()) TogglePanel();

        if (!isPanelOpen) return;

        // HUD側が毎フレーム再表示しても、ガイドを閉じるまでは前面へ出さない。
        HideUseButton();

        UpdateRoleSearchCaret();

        // マウスホイールでスクロール
        bool hasScrollableList = currentTab == GuideTab.RoleList
            || (currentTab == GuideTab.MyRole && currentMyRoleView == MyRoleView.Addons);
        if (hasScrollableList && scrollContent != null)
        {
            UpdateRoleListDrag();

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
            {
                StepScroll(wheel < 0f ? 1 : -1);
            }

            // スクロールバードラッグの継続処理
            UpdateScrollThumb();
        }

        // クリックアニメ
        if (_btnAnimActive && _btnRenderer != null)
        {
            _btnAnimTimer += Time.deltaTime;
            float t = _btnAnimTimer / 0.3f;
            float scale = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
            var go = _btnRenderer.gameObject;
            if (go != null) go.transform.localScale = new Vector3(GuideButtonScale * scale, GuideButtonScale * scale, 1f);
            if (_btnAnimTimer >= 0.3f)
            {
                _btnAnimActive = false;
                if (go != null) go.transform.localScale = new Vector3(GuideButtonScale, GuideButtonScale, 1f);
                _btnRenderer.color = Color.white;
                if (_btnText != null) _btnText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            }
        }
    }

    private static bool IsTextInputActive()
    {
        if (roleSearchBox?.hasFocus == true) return true;
        if (!DestroyableSingleton<HudManager>.InstanceExists) return false;

        var chat = DestroyableSingleton<HudManager>.Instance.Chat;
        return chat != null && chat.IsOpenOrOpening;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleGuideUpdatePatch
{
    public static void Postfix()
    {
        RoleGuideButtonPatch.UpdateTopRightButtonLayout();
        RoleGuideButtonPatch.UpdateButtonVisibility();
        if (GameSettingMenu.Instance) return;
        if (!GameStates.IsLobby && !GameStates.IsInTask && !GameStates.IsMeeting) return;
        RoleGuideButtonPatch.UpdateTick();
        if (RoleGuideButtonPatch.isPanelOpen && Minigame.Instance != null)
            RoleGuideButtonPatch.ClosePanel();
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
public static class AutoCloseOnChatPatch
{
    public static void Postfix(bool visible)
    {
        if (visible && RoleGuideButtonPatch.isPanelOpen)
            RoleGuideButtonPatch.ClosePanel();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class ClosePanelOnMeetingPatch
{
    public static void Postfix() => RoleGuideButtonPatch.ClosePanel();
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class ClosePanelOnGameEndPatch
{
    public static void Prefix() => RoleGuideButtonPatch.ClosePanel();
}
