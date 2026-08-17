using Sentry.Unity.NativeUtils;
using UnityEngine;
using static TownOfHost.StringHelper;

namespace TownOfHost;

public static class ColorHelper
{
    /// <summary>蛍光マーカーのような色合いの透過色に変換する</summary>
    /// <param name="bright">最大明度にするかどうか．黒っぽい色を黒っぽいままにしたい場合はfalse</param>
    public static Color ToMarkingColor(this Color color, bool bright = true)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        var markingColor = Color.HSVToRGB(h, s, bright ? MarkerVal : v).SetAlpha(MarkerAlpha);
        return markingColor;
    }
    /// <summary>白背景での可読性を保てる色に変換する</summary>
    public static Color ToReadableColor(this Color color)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        // 適切な彩度でない場合は彩度を変更
        /*if (s < ReadableSat)
        {
            s = ReadableSat;
        }
        // 適切な明度でない場合は明度を変更
        if (v > ReadableVal)
        {
            v = ReadableVal;
        }*/
        return Color.HSVToRGB(h, s, v);
    }

    /// <summary>マーカー色のS値 = 彩度</summary>
    private const float MarkerSat = 1f;
    /// <summary>マーカー色のV値 = 明度</summary>
    private const float MarkerVal = 1f;
    /// <summary>マーカー色のアルファ = 不透明度</summary>
    private const float MarkerAlpha = 0.125f;
    /// <summary>白背景テキスト色の最大S = 彩度</summary>
    private const float ReadableSat = 0.6f;
    /// <summary>白背景テキスト色の最大V = 明度</summary>
    private const float ReadableVal = 0.5f;
}
public class ModColors
{
    //こんびf7c114
    public static Color32 ModColor = CodeColor(Main.ModColor);
    public static Color32 ImpostorRed = Palette.ImpostorRed;
    public static Color32 CrewMateBlue = Palette.CrewmateBlue;
    public static Color32 MadMateOrenge = CodeColor("#ff7f50");
    public static Color32 AddonsColor = CodeColor("#028760");
    public static Color32 GhostRoleColor = CodeColor("#8989d9");
    public static Color32 NeutralGray = CodeColor("#cccccc");
    public static Color32 JackalColor = CodeColor("#00b4eb");
    public static Color32 PavlovColor = CodeColor("#F4A96A");
    public static Color32 StandColor = CodeColor("#8B4513");
    public static Color32 SkeldColor = CodeColor("#666666");
    public static Color32 MiraHpColor = CodeColor("#ff6633");
    public static Color32 PolusColor = CodeColor("#980098");
    public static Color32 AirShipColor = CodeColor("#ff3300");
    public static Color32 FangleColor = CodeColor("#ff9900");
    public static Color32 bluegreen = CodeColor("#13a85d");

    /* ★ Player color ★ */
    public enum PlayerColor
    {
        Red, Blue, Green, Pink, Orange, Yellow, Black, white, Purple,
        Brown, Cyan, Lime, Maroon, Rose, Banana, Gray, Tan, Coral
    }
    public static Color32 Pink = CodeColor("#ee54bb"); public static string codepink = "#ee54bb";
    public static Color32 Orange = CodeColor("#f07d0d"); public static string codeorange = "#f07d0d";
    public static Color32 Yellow = CodeColor("#f6f657"); public static string codeyellow = "#f6f657";
    public static Color32 White = CodeColor("#d7e1f1"); public static string codewhite = "#d7e1f1";
    public static Color32 Cyan = CodeColor("#38ffdd"); public static string codecyan = "#38ffdd";
    public static Color32 Lime = CodeColor("#50f039"); public static string codelime = "#50f039";
    public static Color32 Rose = CodeColor("#ecc0d3"); public static string coderose = "#ecc0d3";
    public static Color32 Banana = CodeColor("#f0e7a8"); public static string codebanana = "#f0e7a8";
    public static Color32 Coral = CodeColor("#d76464"); public static string codecoral = "#d76464";
    public static Color32 Red = CodeColor("#c61111"); public static string codered = "#c61111";
    public static Color32 Blue = CodeColor("#132ed2"); public static string codeblue = "#132ed2";
    public static Color32 Green = CodeColor("#11802d"); public static string codegreen = "#11802d";
    public static Color32 Black = CodeColor("#3f474e"); public static string codeblack = "#3f474e";
    public static Color32 Purple = CodeColor("#6b2fbc"); public static string codepurple = "#6b2fbc";
    public static Color32 Brown = CodeColor("#71491e"); public static string codebrown = "#71491e";
    public static Color32 Maroon = CodeColor("#5f1d2e"); public static string codemaroon = "#5f1d2e";
    public static Color32 Gray = CodeColor("#758593"); public static string codegray = "#758593";
    public static Color32 Tan = CodeColor("#918877"); public static string codetan = "#918877";
}