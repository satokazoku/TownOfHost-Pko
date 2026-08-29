using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(EjectMainMenu), nameof(EjectMainMenu.PlacePlayer))]
    [HarmonyPriority(Priority.Last)]
    class EjectMainMenuPKOPatch
    {
        public const string PkoSpritePath = "TownOfHost.Resources.TOHP.PKO.png";
        /// <summary>色違いになる確率（1/N）。7 = 約14%</summary>
        const int ColorVariantChance = 7;
        const int ColorCount = 18;

        static Texture2D sourceTex;
        static Color32[] sourcePixels;
        static readonly Texture2D[] colorTex = new Texture2D[ColorCount];
        static Shader spriteShader;
        static bool loggedMissing;

        public static void Postfix(PlayerParticle part) => Apply(part);

        public static void Apply(PlayerParticle part)
        {
            if (part == null || part.myRend == null) return;
            if (!EnsureSource()) return;

            if (spriteShader == null)
                spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null) return;

            var ppu = 30f + IRandom.Instance.Next(50);
            var tex = sourceTex;

            // たまーに色違い
            if (IRandom.Instance.Next(ColorVariantChance) == 0)
            {
                var colorId = IRandom.Instance.Next(0, ColorCount);
                tex = GetColorTexture(colorId) ?? sourceTex;
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu);

            part.myRend.material.shader = spriteShader;
            part.myRend.material.mainTexture = tex;
            part.myRend.color = Color.white;
            part.myRend.sprite = sprite;
        }

        static Texture2D GetColorTexture(int colorId)
        {
            if (colorTex[colorId] != null) return colorTex[colorId];
            if (sourcePixels == null) return null;

            var target = (Color)Palette.PlayerColors[colorId];
            var pixels = new Color32[sourcePixels.Length];
            for (var i = 0; i < sourcePixels.Length; i++)
                pixels[i] = Recolor(sourcePixels[i], target);

            var tex = new Texture2D(sourceTex.width, sourceTex.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            colorTex[colorId] = tex;
            return tex;
        }

        static Color32 Recolor(Color32 src, Color target)
        {
            if (src.a < 12) return src;
            var c = (Color)src;
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (s < 0.2f || v < 0.1f) return src; // 白目・黒輪郭はそのまま
            Color.RGBToHSV(target, out var th, out _, out _);
            var o = Color.HSVToRGB(th, s, v);
            o.a = c.a;
            return o;
        }

        static bool EnsureSource()
        {
            if (sourceTex != null) return true;

            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(PkoSpritePath);
            if (stream == null)
            {
                if (!loggedMissing)
                {
                    loggedMissing = true;
                    Logger.Warn($"PKO画像が見つかりません: {PkoSpritePath}", "PKO");
                }
                return false;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ms.ToArray());
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            sourceTex = tex;
            sourcePixels = tex.GetPixels32();
            Logger.Info($"PKO画像を読み込み: {PkoSpritePath}", "PKO");
            return true;
        }

        public static bool IsPkoTexture(Texture tex)
        {
            if (tex == null) return false;
            if (tex == sourceTex) return true;
            for (var i = 0; i < colorTex.Length; i++)
                if (colorTex[i] == tex) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    class MainMenuPKOReplaceExistingPatch
    {
        public static void Postfix()
        {
            _ = new LateTask(ReplaceAll, 0.2f, "PKO ReplaceAll", true);
            _ = new LateTask(ReplaceAll, 1.0f, "PKO ReplaceAll2", true);
        }

        static void ReplaceAll()
        {
            var parts = UnityEngine.Object.FindObjectsOfType<PlayerParticle>();
            if (parts == null) return;
            foreach (var part in parts)
            {
                if (part == null || part.myRend == null) continue;
                if (EjectMainMenuPKOPatch.IsPkoTexture(part.myRend.sprite?.texture)) continue;
                EjectMainMenuPKOPatch.Apply(part);
            }
        }
    }
}