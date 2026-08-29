using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch]
    class MainMenuColorPatch
    {
        const float HueShiftAmount = 0.28f;
        const int PreviewSize = 32;

        static readonly Dictionary<int, Texture2D> texCache = new();
        static readonly Dictionary<int, Sprite> spriteCache = new();
        static readonly HashSet<int> shiftedSpriteIds = new();
        static Texture2D previewTex;
        static MainMenuManager cachedMenu;
        static bool texturesReady;
        static bool texturesScheduled;

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        [HarmonyPostfix]
        public static void StartPostfix(MainMenuManager __instance)
        {
            Recolor(__instance, false);
            ScheduleTextures(__instance);
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ResetScreen))]
        [HarmonyPostfix]
        public static void ResetScreenPostfix(MainMenuManager __instance)
        {
            Recolor(__instance, texturesReady);
            if (!texturesReady) ScheduleTextures(__instance);
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenGameModeMenu))]
        [HarmonyPostfix]
        public static void OpenGameModePostfix(MainMenuManager __instance)
        {
            Recolor(__instance, texturesReady);
            if (!texturesReady) ScheduleTextures(__instance);
        }

        public static void Keep()
        {
            if (cachedMenu != null)
                Recolor(cachedMenu, texturesReady);
        }

        static void ScheduleTextures(MainMenuManager menu)
        {
            if (texturesReady || texturesScheduled || menu == null) return;
            texturesScheduled = true;
            _ = new LateTask(() =>
            {
                Recolor(menu, true);
                texturesReady = true;
                texturesScheduled = false;
            }, 0.15f, "MenuPurpleTex", true);
        }

        static void Recolor(MainMenuManager menu, bool doTextures)
        {
            if (menu == null) return;
            cachedMenu = menu;

            for (var i = 0; i < menu.transform.childCount; i++)
            {
                var child = menu.transform.GetChild(i);
                if (child.GetComponent<EjectMainMenu>() != null) continue;
                ApplyToRoot(child, doTextures);
            }
        }

        static void ApplyToRoot(Transform root, bool doTextures)
        {
            var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                if (sr.GetComponent<PlayerParticle>() != null) continue;

                var a = sr.color.a;
                var c = ShiftTealToPurple(sr.color);
                sr.color = new Color(c.r, c.g, c.b, a);

                if (doTextures && sr.sprite != null)
                    sr.sprite = GetShiftedSprite(sr.sprite);
            }

            var tmps = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmps)
            {
                if (tmp == null) continue;
                var a = tmp.color.a;
                var c = ShiftTealToPurple(tmp.color);
                tmp.color = new Color(c.r, c.g, c.b, a);
            }
        }

        static Color ShiftTealToPurple(Color c)
        {
            if (c.a < 0.01f) return c;
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (s < 0.15f) return c;
            if (h < 0.38f || h > 0.58f) return c;
            h += HueShiftAmount;
            if (h > 1f) h -= 1f;
            var o = Color.HSVToRGB(h, s, v);
            o.a = c.a;
            return o;
        }

        static bool IsTealHue(float h, float s)
            => s >= 0.15f && h > 0.38f && h < 0.58f;

        static Sprite GetShiftedSprite(Sprite original)
        {
            if (original == null || original.texture == null) return original;

            var sid = original.GetInstanceID();
            if (shiftedSpriteIds.Contains(sid)) return original;
            if (spriteCache.TryGetValue(sid, out var cached) && cached != null)
                return cached;

            var shiftedTex = GetShiftedTexture(original.texture);
            if (shiftedTex == null)
            {
                spriteCache[sid] = original;
                return original;
            }

            var rect = original.textureRect;
            var pivot = new Vector2(
                rect.width > 0 ? original.pivot.x / rect.width : 0.5f,
                rect.height > 0 ? original.pivot.y / rect.height : 0.5f);

            var created = Sprite.Create(
                shiftedTex,
                rect,
                pivot,
                original.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                original.border);
            created.name = original.name + "_purple";

            spriteCache[sid] = created;
            shiftedSpriteIds.Add(created.GetInstanceID());
            return created;
        }

        static Texture2D GetShiftedTexture(Texture src)
        {
            if (src == null) return null;
            var tid = src.GetInstanceID();
            if (texCache.TryGetValue(tid, out var cached))
                return cached;

            if (!CheapIsTealUi(src))
            {
                texCache[tid] = null;
                return null;
            }

            var readable = CopyReadable(src);
            if (readable == null)
            {
                texCache[tid] = null;
                return null;
            }

            var pixels = readable.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = (Color32)ShiftTealToPurple(pixels[i]);

            readable.SetPixels32(pixels);
            readable.Apply(false, false);
            texCache[tid] = readable;
            return readable;
        }

        static bool CheapIsTealUi(Texture src)
        {
            if (previewTex == null)
                previewTex = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false);

            var rt = RenderTexture.GetTemporary(PreviewSize, PreviewSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            previewTex.ReadPixels(new Rect(0, 0, PreviewSize, PreviewSize), 0, 0);
            previewTex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return ShouldShiftTexture(previewTex.GetPixels32());
        }

        static bool ShouldShiftTexture(Color32[] pixels)
        {
            var teal = 0;
            var colorful = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 32) continue;
                Color.RGBToHSV((Color)pixels[i], out var h, out var s, out _);
                if (s < 0.2f) continue;
                colorful++;
                if (IsTealHue(h, s)) teal++;
            }
            return colorful > 0 && (float)teal / colorful > 0.35f;
        }

        static Texture2D CopyReadable(Texture src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                filterMode = src.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                name = src.name + "_purple"
            };
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}