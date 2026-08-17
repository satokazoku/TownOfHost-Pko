using System.Collections.Generic;

namespace TownOfHost
{
    public static class PlayerSkinPatch
    {
        static Dictionary<byte, int> color = new();
        static Dictionary<byte, uint> level = new();
        static Dictionary<byte, string> hat = new(), namep = new(), skin = new(), visor = new(), name = new(), pet = new();

        /// <summary>
        /// プレイヤーのスキンをセーブ
        /// </summary>
        public static void Save(PlayerControl player)
        {
            name[player.PlayerId] = player.name;
            color[player.PlayerId] = player.Data.DefaultOutfit.ColorId;
            hat[player.PlayerId] = player.Data.DefaultOutfit.HatId;
            skin[player.PlayerId] = player.Data.DefaultOutfit.SkinId;
            visor[player.PlayerId] = player.Data.DefaultOutfit.VisorId;
            namep[player.PlayerId] = player.Data.DefaultOutfit.NamePlateId;
            level[player.PlayerId] = player.Data.PlayerLevel;
            pet[player.PlayerId] = player.Data.DefaultOutfit.PetId;
        }
        /// <summary>
        /// プレイヤーのスキンをロード(n c h s v np lev pet)
        /// </summary>
        public static (string name, int color, string hat, string skin, string visor, string nameplate, uint level, string pet) Load(PlayerControl player)
        {
            return (name[player.PlayerId], color[player.PlayerId], hat[player.PlayerId], skin[player.PlayerId], visor[player.PlayerId], namep[player.PlayerId], level[player.PlayerId], pet[player.PlayerId]);
        }
        /// <summary>
        /// 保存されたプレイヤーのデータを削除
        /// </summary>
        public static void Remove(PlayerControl player)
        {
            name.Remove(player.PlayerId);
            color.Remove(player.PlayerId);
            hat.Remove(player.PlayerId);
            skin.Remove(player.PlayerId);
            visor.Remove(player.PlayerId);
            namep.Remove(player.PlayerId);
            level.Remove(player.PlayerId);
            pet.Remove(player.PlayerId);
        }
        /// <summary>
        /// 保存されたデータを全て削除
        /// </summary>
        public static void RemoveAll()
        {
            name.Clear();
            color.Clear();
            hat.Clear();
            skin.Clear();
            visor.Clear();
            namep.Clear();
            level.Clear();
            pet.Clear();
        }
    }
}