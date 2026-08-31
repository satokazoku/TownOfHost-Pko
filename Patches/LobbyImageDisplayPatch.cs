using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost
{
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    class LobbyStartPatch
    {
        public const string LobbyLogoPath = "TownOfHost.Resources.TOHP.LobbyLogo.png";

        static Sprite lobbyLogoSprite;
        static GameObject lobbyPaintObject;
        static GameObject lobbyTitleObject;
        static bool firstLoad = true;

        public static void Prefix()
        {
            lobbyLogoSprite = UtilsSprite.LoadSprite(LobbyLogoPath, 220f);
        }

        public static void Postfix(LobbyBehaviour __instance)
        {
            var wait = firstLoad ? 0.25f : 0.05f;
            _ = new LateTask(() =>
            {
                if (__instance == null) return;
                SpawnLobbyPaint();
                SpawnTitle(__instance);
                firstLoad = false;
            }, wait, "PKO Lobby Decor", true);
        }

        static void SpawnLobbyPaint()
        {
            if (lobbyLogoSprite == null)
            {
                Logger.Warn($"ロビー画像が見つかりません: {LobbyLogoPath}", "LobbyStartPatch");
                return;
            }

            var leftBox = GameObject.Find("Leftbox");
            if (leftBox == null) return;

            if (lobbyPaintObject != null)
                Object.Destroy(lobbyPaintObject);

            lobbyPaintObject = Object.Instantiate(leftBox, leftBox.transform.parent);
            lobbyPaintObject.name = "PKO Lobby Paint";
            lobbyPaintObject.transform.localPosition = new Vector3(0.042f, -2.59f, -10.5f);

            var col = lobbyPaintObject.GetComponent<PolygonCollider2D>();
            if (col != null) Object.Destroy(col);

            var renderer = lobbyPaintObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = lobbyLogoSprite;
        }

        static void SpawnTitle(LobbyBehaviour lobby)
        {
            if (lobbyTitleObject != null)
                Object.Destroy(lobbyTitleObject);

            TextMeshPro src = null;
            foreach (var t in lobby.GetComponentsInChildren<TextMeshPro>(true))
            {
                src = t;
                break;
            }
            if (src == null) src = Object.FindObjectOfType<TextMeshPro>();
            if (src == null) return;

            var tmp = Object.Instantiate(src, lobby.transform);
            lobbyTitleObject = tmp.gameObject;
            lobbyTitleObject.name = "PKO Lobby Title";

            tmp.transform.localPosition = new Vector3(0.5f, 4.3f, -10f);
            tmp.transform.localScale = Vector3.one;

            var ap = tmp.GetComponent<AspectPosition>();
            if (ap != null) Object.Destroy(ap);

            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = tmp.fontSizeMax = tmp.fontSizeMin = 8.5f;
            tmp.enableWordWrapping = false;

            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.9f);

            if (tmp.fontMaterial != null)
            {
                tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
                tmp.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
                tmp.fontMaterial.SetFloat("_UnderlayOffsetX", 0f);
                tmp.fontMaterial.SetFloat("_UnderlayOffsetY", 0f);
                tmp.fontMaterial.SetFloat("_UnderlayDilate", 0.3f);
                tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.5f);
            }

            tmp.text = BuildTitleText();
        }

        static string BuildTitleText()
        {
            const string nameText = "TownOfHost-Pko";
            string versionText = $"v{Main.PluginVersion}";

            Color[] stops =
            [
                new Color(1.00f, 0.42f, 0.62f),
                new Color(1.00f, 0.75f, 0.30f),
                new Color(0.30f, 1.00f, 0.60f),
                new Color(0.30f, 0.75f, 1.00f),
            ];

            var sb = new StringBuilder();

            sb.Append("<b><i>");
            for (int i = 0; i < nameText.Length; i++)
            {
                float t = (float)i / (nameText.Length - 1) * (stops.Length - 1);
                int idx = Mathf.Clamp(Mathf.FloorToInt(t), 0, stops.Length - 2);
                Color c = Color.Lerp(stops[idx], stops[idx + 1], t - idx);
                string hex = ColorUtility.ToHtmlStringRGB(c);
                sb.Append($"<color=#{hex}>{nameText[i]}</color>");
            }
            sb.Append("</i></b>");

            sb.Append($"\n<size=45%><color=#BBCCFF><b>{versionText}</b></color></size>");

            return sb.ToString();
        }
    }
}