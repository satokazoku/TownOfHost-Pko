using HarmonyLib;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch]
    class MainMenuColorPatch
    {
        static readonly Color32 MenuColor = new(255, 150, 49, 255);      // #FF9631
        static readonly Color32 MenuHoverColor = new(255, 176, 90, 255);

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void StartPostfix(MainMenuManager __instance) => Recolor(__instance);

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ResetScreen))]
        [HarmonyPostfix]
        public static void ResetScreenPostfix(MainMenuManager __instance) => Recolor(__instance);

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenGameModeMenu))]
        [HarmonyPostfix]
        public static void OpenGameModePostfix(MainMenuManager __instance) => Recolor(__instance);

        static void Recolor(MainMenuManager menu)
        {
            if (menu == null) return;

            SetButtonColor(menu.playButton);
            SetButtonColor(menu.inventoryButton);
            SetButtonColor(menu.shopButton);
            SetButtonColor(menu.newsButton);
            SetButtonColor(menu.myAccountButton);
            SetButtonColor(menu.settingsButton);
            SetButtonColor(menu.creditsButton);
            SetButtonColor(menu.quitButton);
            SetButtonColor(menu.PlayOnlineButton);
            SetButtonColor(menu.playLocalButton);
            SetButtonColor(menu.howToPlayButton);

            var bg = GameObject.Find("BackgroundTexture");
            if (bg != null)
            {
                var render = bg.GetComponent<SpriteRenderer>();
                if (render != null)
                    render.color = MenuColor;
            }
        }

        static void SetButtonColor(PassiveButton button)
        {
            if (button == null) return;

            if (button.inactiveSprites != null)
            {
                var sr = button.inactiveSprites.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = MenuColor;
            }
            if (button.activeSprites != null)
            {
                var sr = button.activeSprites.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = MenuHoverColor;
            }
        }
    }
}