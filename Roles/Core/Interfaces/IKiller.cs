namespace TownOfHost.Roles.Core.Interfaces;

/// <summary>
/// キルボタン持ち役職の必須要素
/// </summary>
public interface IKiller
{
    /// <summary>
    /// キル能力を持っているか
    /// </summary>
    public bool CanKill => true;
    /// <summary>
    /// キルボタン押下 == キルの役職か<br/>
    /// デフォルトでは<see cref="CanKill"/>をそのまま返す
    /// </summary>
    public bool IsKiller => CanKill;

    /// <summary>
    /// キルボタンを使えるかどうか
    /// デフォルトでは<see cref="CanKill"/>をそのまま返す
    /// </summary>
    /// <returns>trueを返した場合，キルボタンを使える</returns>
    public bool CanUseKillButton() => CanKill;
    /// <summary>
    /// キルクールダウンを計算する<br/>
    /// デフォルト: <see cref="Options.DefaultKillCooldown"/>
    /// </summary>
    /// <returns>キルクールダウン(秒)</returns>
    public float CalculateKillCooldown() => Options.DefaultKillCooldown;
    /// <summary>
    /// サボタージュボタンを使えるかどうか
    /// </summary>
    /// <returns>trueを返した場合，サボタージュボタンを使える</returns>
    public bool CanUseSabotageButton();
    /// <summary>
    /// ベントボタンを使えるかどうか
    /// デフォルトでは使用可能
    /// </summary>
    /// <returns>trueを返した場合，ベントボタンを使える</returns>
    public bool CanUseImpostorVentButton() => true;

    /// <summary>
    /// キラーとしてのCheckMurder処理<br/>
    /// <br/>"※キル後の処理をここでしない"<br/><br/>
    /// 通常キルはブロックされることを考慮しなくてもよい。<br/>
    /// 通常キル以外の能力はinfo.CanKill=falseの場合は効果発揮しないよう実装する。<br/>
    /// キルを行わない場合はinfo.DoKill=falseとする。<br/>
    /// (CanKill = ターゲットをキル出来るか。ガード等で弾かれる)<br/>
    /// (DoKill = キラーがキル出来るか。キルで塗るとかならfalse)
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    public void OnCheckMurderAsKiller(MurderInfo info) { }

    /// <summary>
    /// 防がれようと処理される奴。
    /// ここではinfoの値を弄らないでね。 
    /// </summary>
    /// <param name="info"></param>
    public void OnCheckMurderDontKill(MurderInfo info) { }

    /// <summary>
    /// キラーとしてのMurderPlayer処理
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    public void OnMurderPlayerAsKiller(MurderInfo info) { }

    /// <summary>
    /// キルボタンのテキストを変更します
    /// </summary>
    /// <param name="text">上書き後のテキスト</param>
    /// <returns>上書きする場合true</returns>
    public bool OverrideKillButtonText(out string text)
    {
        text = default;
        return false;
    }
    /// <summary>
    /// キルボタンの画像を変更します。
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public bool OverrideKillButton(out string text)
    {
        text = default;
        return false;
    }
    /// <summary>
    /// インポスターベントボタンの画像を変更します。
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public bool OverrideImpVentButton(out string text)
    {
        text = default;
        return false;
    }
}
