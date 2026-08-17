using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(JoinGameButton), nameof(JoinGameButton.OnClick))]
    class JoinGameButtonPatch
    {
        public static string GameId = "";
        public static void Prefix(JoinGameButton __instance)
        {
            if (__instance.GameIdText == null) return;
            if (__instance.GameIdText.text == "" && Regex.IsMatch(GUIUtility.systemCopyBuffer.Trim('\r', '\n'), @"^[A-Z]{6}$"))
            {
                Logger.Info($"{GUIUtility.systemCopyBuffer}", "ClipBoard");
                __instance.GameIdText.SetText(GUIUtility.systemCopyBuffer.Trim('\r', '\n'));
            }
            GameId = __instance.GameIdText.text;
        }
    }
    [HarmonyPatch(typeof(JoinGameButton), nameof(JoinGameButton.Start))]
    class JoinGameButtonStartPatch
    {
        public static void Prefix(JoinGameButton __instance)
        {
            var QButton = GameObject.Find("NormalMenu/Buttons/JoinGameButton/JoinGameMenu/ChatTypeOptions/Quick");
            if (!QButton) return;
            var button = UnityEngine.Object.Instantiate(QButton, QButton.transform.parent.parent);
            var text = button.transform.Find("Text_TMP").GetComponent<TextMeshPro>();
            var passive = button.GetComponent<PassiveButton>();
            button.name = "Restore";
            button.transform.localPosition = new Vector3(-1.5f, 0.65f, -9.55f);
            button.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            UnityEngine.Object.Destroy(button.transform.Find("Highlight").gameObject);
            text.DestroyTranslator();
            text.text = "前回のコードを復元";
            passive.OnClick = new();
            passive.OnClick.AddListener((Action)(() =>
            {
                if (JoinGameButtonPatch.GameId != "")
                    __instance.GameIdText.SetText(JoinGameButtonPatch.GameId);
            }));
        }
    }
}