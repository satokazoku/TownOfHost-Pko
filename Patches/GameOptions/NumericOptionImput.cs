using System;
using System.Globalization;
using TMPro;
using TownOfHost.Modules;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TownOfHost
{
    public static class NumericOptionInput
    {
        private static TextBoxTMP activeInput;
        private static TextMeshPro activeOutput;
        private static TextMeshPro activeValueText;
        private static OptionItem activeOption;
        private static StringOption activeStringOption;
        private static NumberOption activeNumberOption;
        private static GameObject activeTargetObject;
        private static Collider2D activeInputCollider;
        private static int activeStartFrame;
        private static float activeCaretTimer;
        private static bool committing;
        private static bool initializing;
        private static bool sanitizing;
        private static bool dirty;

        public static void Attach(OptionItem option, StringOption stringOption)
        {
            if (!CanEdit(option) || stringOption == null) return;

            var valueBox = stringOption.transform.FindChild("ValueBox");
            if (valueBox == null) return;

            var collider = valueBox.GetComponent<BoxCollider2D>() ?? valueBox.gameObject.AddComponent<BoxCollider2D>();
            collider.autoTiling = true;
            collider.size = new Vector2(1.65f, 0.5f);

            var passive = valueBox.GetComponent<PassiveButton>() ?? valueBox.gameObject.AddComponent<PassiveButton>();
            passive.Colliders = new Collider2D[] { collider };
            passive.OnClick = new Button.ButtonClickedEvent();
            passive.OnClick.AddListener((Action)(() => BeginEdit(option, stringOption)));
            passive.OnMouseOut = new();
            passive.OnMouseOver = new();

            valueBox.gameObject.layer = LayerMask.NameToLayer("UI");
        }

        public static void Attach(NumberOption numberOption)
        {
            if (numberOption == null) return;

            var valueBox = numberOption.transform.FindChild("ValueBox");
            if (valueBox == null) return;

            var collider = valueBox.GetComponent<BoxCollider2D>() ?? valueBox.gameObject.AddComponent<BoxCollider2D>();
            collider.autoTiling = true;
            collider.size = new Vector2(1.65f, 0.5f);

            var passive = valueBox.GetComponent<PassiveButton>() ?? valueBox.gameObject.AddComponent<PassiveButton>();
            passive.Colliders = new Collider2D[] { collider };
            passive.OnClick = new Button.ButtonClickedEvent();
            passive.OnClick.AddListener((Action)(() => BeginEdit(numberOption)));
            passive.OnMouseOut = new();
            passive.OnMouseOver = new();

            valueBox.gameObject.layer = LayerMask.NameToLayer("UI");
        }

        public static void Tick()
        {
            if (activeInput == null) return;

            if (activeTargetObject == null || !activeTargetObject.activeInHierarchy)
            {
                Close(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close(false);
                return;
            }
            if (Input.GetMouseButtonDown(0) && Time.frameCount > activeStartFrame && !IsPointerInsideInput())
            {
                Close(false);
                return;
            }

            SanitizeActiveText();
            UpdateCaret();
        }

        public static void Close(bool commit = true)
        {
            if (activeInput == null) return;
            if (committing && !commit) return;
            if (commit) CommitActive();
            else CleanupActive();
        }

        private static bool CanEdit(OptionItem option)
        {
            if (option == null || option.HideValue) return false;
            return option is IntegerOptionItem or FloatOptionItem;
        }

        private static void BeginEdit(OptionItem option, StringOption stringOption)
        {
            if (!CanEdit(option) || stringOption == null) return;
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return;

            if (activeInput != null) Close(false);
            if (activeInput != null) return;

            activeOption = option;
            activeStringOption = stringOption;
            activeTargetObject = stringOption.gameObject;
            activeValueText = stringOption.ValueText;
            activeInput = CreateInput(stringOption.transform, stringOption.ValueText, GetCharacterLimit(option));
            if (activeInput == null)
            {
                CleanupActive();
                return;
            }

            activeOutput = activeInput.outputText;
            activeInputCollider = activeInput.GetComponent<Collider2D>();
            activeStartFrame = Time.frameCount;
            activeValueText?.gameObject.SetActive(false);

            initializing = true;
            activeInput.SetText("");
            activeInput.caretPos = 0;
            initializing = false;
            dirty = false;

            activeInput.gameObject.SetActive(true);
            activeOutput?.gameObject.SetActive(true);
            activeCaretTimer = 0f;
            Input.imeCompositionMode = IMECompositionMode.Auto;
            activeInput.GiveFocus();
            UpdateCaret();
        }

        private static void BeginEdit(NumberOption numberOption)
        {
            if (numberOption == null) return;
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return;

            if (activeInput != null) Close(false);
            if (activeInput != null) return;

            activeNumberOption = numberOption;
            activeTargetObject = numberOption.gameObject;
            activeValueText = numberOption.ValueText;
            activeInput = CreateInput(numberOption.transform, numberOption.ValueText, GetCharacterLimit(numberOption));
            if (activeInput == null)
            {
                CleanupActive();
                return;
            }

            activeOutput = activeInput.outputText;
            activeInputCollider = activeInput.GetComponent<Collider2D>();
            activeStartFrame = Time.frameCount;
            activeValueText?.gameObject.SetActive(false);

            initializing = true;
            activeInput.SetText("");
            activeInput.caretPos = 0;
            initializing = false;
            dirty = false;

            activeInput.gameObject.SetActive(true);
            activeOutput?.gameObject.SetActive(true);
            activeCaretTimer = 0f;
            Input.imeCompositionMode = IMECompositionMode.Auto;
            activeInput.GiveFocus();
            UpdateCaret();
        }

        private static TextBoxTMP CreateInput(Transform parent, TextMeshPro valueText, int characterLimit)
        {
            var inputObj = new GameObject("NumericOptionInput");
            inputObj.transform.SetParent(parent);
            inputObj.transform.localPosition = valueText.transform.localPosition;
            inputObj.transform.localScale = valueText.transform.localScale;
            inputObj.layer = LayerMask.NameToLayer("UI");

            TextBoxTMP textBox = null;
            var collider = inputObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.65f, 0.5f);

            var button = inputObj.AddComponent<PassiveButton>();
            button.Colliders = new Collider2D[] { collider };
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((Action)(() => textBox?.GiveFocus()));
            button.OnMouseOut = new();
            button.OnMouseOver = new();

            textBox = inputObj.AddComponent<TextBoxTMP>();
            textBox.AllowEmail = false;
            textBox.AllowSymbols = true;
            textBox.AllowPaste = true;
            textBox.allowAllCharacters = true;
            textBox.tempTxt = new();
            textBox.compoText = "";
            textBox.text = "";
            textBox.characterLimit = characterLimit;
            textBox.OnChange = new();
            textBox.OnEnter = new();
            textBox.OnFocusLost = new();

            var output = Object.Instantiate(valueText, parent);
            output.name = "NumericOptionInputText";
            output.text = "";
            output.alignment = TextAlignmentOptions.Center;
            output.enableWordWrapping = false;
            output.overflowMode = TextOverflowModes.Truncate;
            output.transform.localPosition = valueText.transform.localPosition;
            output.transform.localScale = valueText.transform.localScale;
            output.rectTransform.sizeDelta = new Vector2(1.8f, 0.45f);
            output.gameObject.layer = LayerMask.NameToLayer("UI");
            textBox.outputText = output;

            textBox.OnChange.AddListener((Action)(() =>
            {
                if (!initializing) dirty = true;
                SanitizeActiveText();
                UpdateCaret();
            }));
            textBox.OnEnter.AddListener((Action)CommitActive);
            textBox.OnFocusLost.AddListener((Action)(() => Close(false)));

            return textBox;
        }

        private static bool IsPointerInsideInput()
        {
            if (activeInputCollider == null) return false;
            var camera = Camera.main;
            if (camera == null) return false;
            var mousePosition = camera.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = activeInputCollider.transform.position.z;
            return activeInputCollider.OverlapPoint(mousePosition);
        }

        private static void UpdateCaret()
        {
            if (activeInput == null || activeOutput == null) return;

            var committedText = activeInput.text ?? "";
            var compositionText = activeInput.compoText ?? "";
            var caretPosition = Math.Clamp(activeInput.caretPos, 0, committedText.Length);
            var displayedText = committedText.Insert(caretPosition, compositionText);

            if (activeInput.hasFocus)
            {
                activeCaretTimer = (activeCaretTimer + Time.deltaTime) % 1f;
                if (activeCaretTimer < 0.5f)
                {
                    var displayedCaretPosition = caretPosition + compositionText.Length;
                    displayedText = displayedText.Insert(displayedCaretPosition, "<color=#8cffff>|</color>");
                }
            }

            activeOutput.text = displayedText;
        }

        private static int GetCharacterLimit(OptionItem option)
        {
            var max = option switch
            {
                IntegerOptionItem integerOption => integerOption.Rule.MaxValue,
                FloatOptionItem floatOption => Mathf.CeilToInt(floatOption.Rule.MaxValue),
                _ => 999
            };

            var limit = Math.Max(1, max.ToString(CultureInfo.InvariantCulture).Length);
            return option is FloatOptionItem ? limit + 3 : limit;
        }

        private static void SanitizeActiveText()
        {
            if (sanitizing || activeInput == null) return;

            var before = activeInput.text ?? "";
            var caret = Math.Clamp(activeInput.caretPos, 0, before.Length);
            var allowDecimal = CanUseDecimal();
            var normalized = NormalizeNumber(before, allowDecimal);
            var limit = Math.Max(1, activeInput.characterLimit);
            if (normalized.Length > limit) normalized = normalized[..limit];

            if (before == normalized) return;

            sanitizing = true;
            var normalizedCaret = NormalizeNumber(before[..caret], allowDecimal).Length;
            if (normalizedCaret > normalized.Length) normalizedCaret = normalized.Length;
            activeInput.SetText(normalized);
            activeInput.caretPos = normalizedCaret;
            sanitizing = false;
        }

        private static bool CanUseDecimal()
        {
            if (activeOption is FloatOptionItem) return true;
            return activeNumberOption != null && activeNumberOption.intOptionName is AmongUs.GameOptions.Int32OptionNames.Invalid;
        }

        private static string NormalizeNumber(string value, bool allowDecimal)
        {
            if (string.IsNullOrEmpty(value)) return "";

            var chars = new char[value.Length];
            var length = 0;
            var hasDecimalPoint = false;
            var decimalLength = 0;
            const char fullWidthZero = '\uFF10';
            const char fullWidthNine = '\uFF19';
            const char fullWidthPeriod = '\uFF0E';
            const char ideographicPeriod = '\u3002';
            foreach (var c in value)
            {
                if (allowDecimal && (c == '.' || c == fullWidthPeriod || c == ideographicPeriod))
                {
                    if (hasDecimalPoint) continue;
                    hasDecimalPoint = true;
                    chars[length++] = '.';
                    continue;
                }

                if (c is >= '0' and <= '9')
                {
                    if (hasDecimalPoint && ++decimalLength > 2) continue;
                    chars[length++] = c;
                    continue;
                }
                if (c is >= fullWidthZero and <= fullWidthNine)
                {
                    if (hasDecimalPoint && ++decimalLength > 2) continue;
                    chars[length++] = (char)('0' + c - fullWidthZero);
                }
            }
            return new string(chars, 0, length);
        }

        private static void CommitActive()
        {
            if (committing || activeInput == null) return;

            committing = true;
            SanitizeActiveText();

            if (dirty && !string.IsNullOrEmpty(activeInput.text))
            {
                ApplyValue(activeInput.text);
            }

            CleanupActive();
            committing = false;
        }

        private static void ApplyValue(string text)
        {
            switch (activeOption)
            {
                case IntegerOptionItem integerOption:
                    {
                        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return;
                        var clamped = (int)Math.Clamp(parsed, integerOption.Rule.MinValue, integerOption.Rule.MaxValue);
                        integerOption.SetValue(integerOption.Rule.GetNearestIndex(clamped));
                        break;
                    }
                case FloatOptionItem floatOption:
                    {
                        if (!float.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)) return;
                        var clamped = Mathf.Clamp(parsed, floatOption.Rule.MinValue, floatOption.Rule.MaxValue);
                        floatOption.SetValue(floatOption.Rule.GetNearestIndex(clamped));
                        break;
                    }
            }

            if (activeNumberOption != null)
            {
                ApplyNumberOptionValue(activeNumberOption, text);
            }
        }

        private static int GetCharacterLimit(NumberOption numberOption)
        {
            var max = Mathf.CeilToInt(numberOption.ValidRange.max);
            var limit = Math.Max(1, max.ToString(CultureInfo.InvariantCulture).Length);
            return numberOption.intOptionName is AmongUs.GameOptions.Int32OptionNames.Invalid ? limit + 3 : limit;
        }

        private static void ApplyNumberOptionValue(NumberOption numberOption, string text)
        {
            if (numberOption.intOptionName is AmongUs.GameOptions.Int32OptionNames.Invalid)
            {
                if (!float.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)) return;
                numberOption.Value = SnapNumberOptionValue(numberOption, parsed);
            }
            else
            {
                if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return;
                numberOption.Value = SnapNumberOptionValue(numberOption, parsed);
            }

            numberOption.UpdateValue();
            GameOptionsSender.RpcSendOptions();
            OptionShower.Update = true;
        }

        private static float SnapNumberOptionValue(NumberOption numberOption, float value)
        {
            var min = numberOption.ValidRange.min;
            var max = numberOption.ValidRange.max;
            var clamped = Mathf.Clamp(value, min, max);

            if (IsLightOption(numberOption))
            {
                if (clamped < 1f)
                {
                    return GetNearest(clamped, new[]
                    {
                        0.25f, 0.38f, 0.5f, 0.63f, 0.75f, 0.88f
                    });
                }

                return RoundToSecondDecimal(clamped);
            }

            var increment = numberOption.Increment;
            if (increment <= 0f) return clamped;

            var steps = Mathf.Round((clamped - min) / increment);
            return Mathf.Clamp(min + steps * increment, min, max);
        }

        private static bool IsLightOption(NumberOption numberOption)
            => numberOption.floatOptionName is AmongUs.GameOptions.FloatOptionNames.ImpostorLightMod
                or AmongUs.GameOptions.FloatOptionNames.CrewLightMod;

        private static float RoundToSecondDecimal(float value)
            => Mathf.Round(value * 100f) / 100f;

        private static float GetNearest(float value, float[] values)
        {
            var nearest = values[0];
            var distance = Mathf.Abs(value - nearest);
            for (var i = 1; i < values.Length; i++)
            {
                var nextDistance = Mathf.Abs(value - values[i]);
                if (nextDistance >= distance) continue;
                nearest = values[i];
                distance = nextDistance;
            }
            return nearest;
        }

        private static void CleanupActive()
        {
            activeValueText?.gameObject.SetActive(true);

            if (activeOutput != null)
            {
                Object.Destroy(activeOutput.gameObject);
            }
            if (activeInput != null)
            {
                Object.Destroy(activeInput.gameObject);
            }

            activeInput = null;
            activeOutput = null;
            activeValueText = null;
            activeOption = null;
            activeStringOption = null;
            activeNumberOption = null;
            activeTargetObject = null;
            activeInputCollider = null;
            activeStartFrame = 0;
            activeCaretTimer = 0f;
            initializing = false;
            sanitizing = false;
            dirty = false;
        }
    }
}
