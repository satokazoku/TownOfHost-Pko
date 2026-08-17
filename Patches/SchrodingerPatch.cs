using System.Collections.Generic;

namespace TownOfHost
{
    public static class SchrodingerPatch
    {
        public static Dictionary<byte, byte> PlayerData = new();

        /// <summary>
        /// 複数選択できる第三陣営キラーでのご主人が勝利した時のみ勝利する登録
        /// 単独陣営以外の動作は保証しない
        /// </summary>
        public static void SAddPlayer(PlayerControl killer, PlayerControl Cat)
        {
            if (PlayerData.ContainsKey(Cat.PlayerId))
            {
                Logger.Info($"{Cat.name}のデータは既に存在するため追加できませんでした。", "SchrodingerPatch");
                return;
            }
            PlayerData.Add(Cat.PlayerId, killer.PlayerId);
            Logger.Info($"{Cat.name}のご主人を{killer.name}に設定", "SchrodingerPatch");
        }

        /// <summary>
        /// 指定した猫のリセット
        /// </summary>
        public static void SremovePlayer(PlayerControl Cat)
        {
            if (!PlayerData.ContainsKey(Cat.PlayerId))
            {
                Logger.Info($"{Cat.name}のデータは存在しないため、リセットできませんでした。", "SchrodingerPatch");
                return;
            }
            PlayerData.Remove(Cat.PlayerId);
            Logger.Info($"{Cat.name}をリセット", "SchrodingerPatch");
        }

        /// <summary>
        /// データのリセット 初期化時に自動で呼び出される、手動で呼び出しても動く
        /// </summary>
        public static void SResetPlayer()
        {
            PlayerData.Clear();
            Logger.Info("データをリセットしました", "SchrodingerPatch");
        }

        /// <summary>
        /// 追加勝利データに追加する、勝利が確定する前は基本的に使わない。
        /// </summary>
        public static void SWin(PlayerControl winkiller)
        {
            foreach (var cat in PlayerData)
                if (cat.Value == winkiller.PlayerId)
                    CustomWinnerHolder.WinnerIds.Add(cat.Key);
            Logger.Info("追加勝利を設定しました。", "SchrodingerPatch");
        }
    }
}