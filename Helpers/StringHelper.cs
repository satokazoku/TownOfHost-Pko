using System.Text;
using UnityEngine;

namespace TownOfHost;

public static class StringHelper
{
    public static readonly Encoding shiftJIS = CodePagesEncodingProvider.Instance.GetEncoding("Shift_JIS");

    /// <summary>蛍光マーカーのような装飾をする</summary>
    /// <param name="self">文字列</param>
    /// <param name="color">元の色 自動で半透明の蛍光色に変換される</param>
    /// <param name="bright">最大明度にするかどうか．黒っぽい色を黒っぽいままにしたい場合はfalse</param>
    /// <returns>マーキング済文字列</returns>
    public static string Mark(this string self, Color color, bool bright = true)
    {
        var markingColor = color.ToMarkingColor(bright);
        var markingColorCode = ColorUtility.ToHtmlStringRGBA(markingColor);
        return $"<mark=#{markingColorCode}>{self}</mark>";
    }
    /// <summary>
    /// カラーからカラーコードに変換します。
    /// </summary>
    /// <param name="color">カラー</param>
    /// <returns></returns>
    public static string ColorCode(this Color32 color)
    {
        return "#" + ColorUtility.ToHtmlStringRGBA(color);
    }
    /// <summary>
    /// カラーコードからカラーに変換します
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public static Color CodeColor(string color)
    {
        if (!color.StartsWith("#")) color = $"#{color}";

        if (ColorUtility.TryParseHtmlString(color, out Color c))
            return c;

        Logger.Info($"{color}をcolorに変換できませんでした。", "StringHelper");
        return Color.white;
    }
    /// <summary>
    /// SJISでのバイト数を計算する
    /// </summary>
    public static int GetByteCount(this string self) => shiftJIS.GetByteCount(self);
}