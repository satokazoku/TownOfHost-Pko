namespace TownOfHost.Roles.Core.Interfaces;

public interface IKillFlashSeeable
{
    /// <summary>
    /// キルフラッシュ発生時、自身がキルフラッシュを見れるか。
    /// nullで返した場合、falseとして強制終了する
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool? CheckKillFlash(MurderInfo info) => true;
}
