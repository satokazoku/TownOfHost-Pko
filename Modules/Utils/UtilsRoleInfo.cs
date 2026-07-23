using static TownOfHost.Utils;
using static TownOfHost.Translator;
using static TownOfHost.ChatCommands;
using static TownOfHost.UtilsRoleText;
using static TownOfHost.UtilsShowOption;
using System.Collections.Generic;
using TownOfHost.Roles.Core;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using AmongUs.GameOptions;
using TownOfHost.Roles.AddOns.Common;

namespace TownOfHost
{
    class UtilsRoleInfo
    {
        public static string LastL = "N";
        public static void GetRolesInfo(string role, byte player = 255)
        {
            if ((Options.HideGameSettings.GetBool() || (Options.HideSettingsDuringGame.GetBool() && GameStates.IsInGame)) && player != byte.MaxValue)
            {
                SendMessage(GetString("Message.HideGameSettings"), player);
                return;
            }
            // 初回のみ処理
            if (roleCommands == null)
            {
#pragma warning disable IDE0028  // Dictionary初期化の簡素化をしない
                roleCommands = new Dictionary<CustomRoles, string>();

                // GM
                roleCommands.Add(CustomRoles.GM, "gm");

                // Impostor役職
                roleCommands.Add((CustomRoles)(-1), $"【=== {GetString("Impostor")} ===】");  // 区切り用
                roleCommands.Add(CustomRoles.Shapeshifter, "She");
                //roleCommands.Add(CustomRoles.Phantom, "Pha");//アプデ対応用 仮
                roleCommands.Add(CustomRoles.Viper, "vi");
                ConcatCommands(CustomRoleTypes.Impostor);

                // Madmate役職
                roleCommands.Add((CustomRoles)(-2), $"【=== {GetString("Madmate")} ===】");  // 区切り用
                ConcatCommands(CustomRoleTypes.Madmate);
                roleCommands.Add(CustomRoles.SKMadmate, "sm");

                // Crewmate役職
                roleCommands.Add((CustomRoles)(-3), $"【=== {GetString("Crewmate")} ===】");  // 区切り用
                roleCommands.Add(CustomRoles.Engineer, "Eng");
                roleCommands.Add(CustomRoles.Scientist, "Sci");
                roleCommands.Add(CustomRoles.Tracker, "Trac");
                roleCommands.Add(CustomRoles.Noisemaker, "Nem");//アプデ対応用 仮
                roleCommands.Add(CustomRoles.Detective, "Det");

                ConcatCommands(CustomRoleTypes.Crewmate);

                // Neutral役職
                roleCommands.Add((CustomRoles)(-4), $"【=== {GetString("Neutral")} ===】");  // 区切り用
                ConcatCommands(CustomRoleTypes.Neutral);
                // 属性
                roleCommands.Add((CustomRoles)(-5), $"【=== {GetString("Addons")} ===】");  // 区切り用
                                                                                          //ラスト
                roleCommands.Add(CustomRoles.Workhorse, "wh");
                roleCommands.Add(CustomRoles.LastNeutral, "ln");
                roleCommands.Add(CustomRoles.LastImpostor, "li");
                roleCommands.Add(CustomRoles.OneWolf, "Ow");
                //バフ
                roleCommands.Add(CustomRoles.Watching, "wat");
                roleCommands.Add(CustomRoles.Speeding, "sd");
                roleCommands.Add(CustomRoles.Guarding, "gi");
                roleCommands.Add(CustomRoles.Guesser, "Gr");
                roleCommands.Add(CustomRoles.Moon, "Mo");
                roleCommands.Add(CustomRoles.VoteTracker, "Vt");
                roleCommands.Add(CustomRoles.Lighting, "Li");
                roleCommands.Add(CustomRoles.Management, "Dr");
                roleCommands.Add(CustomRoles.Connecting, "Cn");
                roleCommands.Add(CustomRoles.Serial, "Se");
                roleCommands.Add(CustomRoles.PlusVote, "Pv");
                roleCommands.Add(CustomRoles.Opener, "Oe");
                //roleCommands.Add(CustomRoles.AntiTeleporter, "At");
                roleCommands.Add(CustomRoles.Revenger, "Re");
                roleCommands.Add(CustomRoles.Seeing, "Se");
                roleCommands.Add(CustomRoles.Autopsy, "Au");
                roleCommands.Add(CustomRoles.Tiebreaker, "tb");
                roleCommands.Add(CustomRoles.MagicHand, "MaH");
                roleCommands.Add(CustomRoles.Powerful, "pf");
                roleCommands.Add(CustomRoles.Absorb, "Abs");

                //デバフ
                roleCommands.Add(CustomRoles.SlowStarter, "sl");
                roleCommands.Add(CustomRoles.NonReport, "Nr");
                roleCommands.Add(CustomRoles.Notvoter, "nv");
                roleCommands.Add(CustomRoles.Water, "wt");
                roleCommands.Add(CustomRoles.Transparent, "tr");
                roleCommands.Add(CustomRoles.Slacker, "sl");
                roleCommands.Add(CustomRoles.Stamina, "st");
                roleCommands.Add(CustomRoles.Jumbo, "J");
                roleCommands.Add(CustomRoles.Securer, "Su");
                roleCommands.Add(CustomRoles.Sealer, "Sea");
                roleCommands.Add(CustomRoles.Clumsy, "lb");
                roleCommands.Add(CustomRoles.Elector, "El");
                roleCommands.Add(CustomRoles.Amnesia, "am");
                roleCommands.Add(CustomRoles.InfoPoor, "IP");
                roleCommands.Add(CustomRoles.Sunglasses, "Sun");
                //第三
                roleCommands.Add(CustomRoles.Lovers, "lo");
                roleCommands.Add(CustomRoles.OneLove, "ol");
                roleCommands.Add(CustomRoles.MadonnaLovers, "Ml");
                roleCommands.Add(CustomRoles.CupidLovers, "Cl");
                roleCommands.Add(CustomRoles.Amanojaku, "Ama");

                roleCommands.Add((CustomRoles)(-7), $"== {GetString("GhostRole")} ==");  // 区切り用
                                                                                         //幽霊
                roleCommands.Add(CustomRoles.Ghostbuttoner, "Bbu");
                roleCommands.Add(CustomRoles.GhostSaboteur, "Gsa");
                roleCommands.Add(CustomRoles.GhostFloodlight, "Gfl");
                roleCommands.Add(CustomRoles.GhostSaboteur, "Gsa");
                roleCommands.Add(CustomRoles.GhostNoiseSender, "NiS");
                roleCommands.Add(CustomRoles.GhostReseter, "Res");
                roleCommands.Add(CustomRoles.GhostRumour, "Rum");
                roleCommands.Add(CustomRoles.GuardianAngel, "Gan");
                roleCommands.Add(CustomRoles.DemonicCrusher, "DCr");
                roleCommands.Add(CustomRoles.DemonicSupporter, "DSu");
                roleCommands.Add(CustomRoles.DemonicTracker, "DTr");
                roleCommands.Add(CustomRoles.DemonicVenter, "Dve");
                roleCommands.Add(CustomRoles.AsistingAngel, "AsA");

                // HAS
                roleCommands.Add((CustomRoles)(-6), $"== {GetString("HideAndSeek")} ==");  // 区切り用
                roleCommands.Add(CustomRoles.HASFox, "hfo");
                roleCommands.Add(CustomRoles.HASTroll, "htr");
                LastL = "N";
#pragma warning restore IDE0028
            }

            var msg = "";
            var rolemsg = $"{GetString("Command.h_args")}";
            if (Main.DebugVersion is false)//デバッグバージョンなら役職一覧表示させへん。
            {
                switch (role)
                {
                    case "i":
                    case "I":
                    case "imp":
                    case "インポスター":
                    case "impostor":
                    case "impostors":
                    case "インポス":
                        rolemsg = $"{GetString("h_r_impostor").Color(Palette.ImpostorRed)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsImpostor()))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                    case "c":
                    case "C":
                    case "crew":
                    case "crewmate":
                    case "クルー":
                    case "クルーメイト":
                        rolemsg = $"{GetString("h_r_crew").Color(Color.blue)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsCrewmate()))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                    case "n":
                    case "N":
                    case "Neu":
                    case "Neutral":
                    case "第三":
                    case "第三陣営":
                    case "ニュートラル":
                        rolemsg = $"{GetString("h_r_Neutral").Color(Palette.DisabledGrey)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsNeutral()))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                    case "m":
                    case "Mad":
                    case "マッド":
                    case "狂人":
                    case "M":
                        rolemsg = $"{GetString("h_r_MadMate").Color(ModColors.MadMateOrenge)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsMadmate()))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                    case "a":
                    case "A":
                    case "Addon":
                    case "アドオン":
                    case "属性":
                    case "モディフィア":
                    case "重複役職":
                        rolemsg = $"{GetString("h_r_Addon").Color(ModColors.AddonsColor)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsAddOn() || r.Key is CustomRoles.Amanojaku))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                    case "g":
                    case "G":
                    case "Ghost":
                    case "幽霊":
                    case "幽霊役職":
                        rolemsg = $"{GetString("h_r_GhostRole").Color(ModColors.GhostRoleColor)}</u><size=50%>";
                        foreach (var im in roleCommands.Where(r => r.Key.IsGhostRole()))
                        {
                            if (!Event.CheckRole(im.Key)) continue;
                            rolemsg += $"\n{GetString($"{im.Key}")}({im.Value})";
                        }
                        if (player == byte.MaxValue) player = 0;
                        SendMessage(rolemsg, player);
                        return;
                }
            }
            foreach (var roledata in roleCommands)
            {
                var roleName = roledata.Key.ToString();
                var roleShort = roledata.Value;

                if (string.Compare(role, roleName, true) == 0 || string.Compare(role, roleShort, true) == 0)
                {
                    if (!Event.CheckRole(roledata.Key)) goto infosend;
                    var roleInfo = roledata.Key.GetRoleInfo();
                    if (roleInfo != null && roleInfo.Description != null)
                    {
                        SendMessage(roleInfo.Description.FullFormatHelp, sendTo: player, checkl: true);
                        var addhaverole = roleInfo.AddHaveRole?.Invoke();
                        if (addhaverole is not null and not CustomRoles.NotAssigned)
                        {
                            var addroleInfo = addhaverole.Value.GetRoleInfo();
                            if (addroleInfo != null && addroleInfo.Description != null)
                                SendMessage(addroleInfo.Description.FullFormatHelp, player, ColorString(GetRoleColor(roledata.Key), GetString("AddRoleInfoTitle")), checkl: true);
                        }
                    }
                    // RoleInfoがない役職は従来の処理
                    else
                    {
                        if (roledata.Key.IsAddOn() || roledata.Key.IsLovers() || roledata.Key == CustomRoles.Amanojaku || roledata.Key.IsGhostRole()) SendMessage(GetAddonsHelp(roledata.Key), sendTo: player);
                        else SendMessage(ColorString(GetRoleColor(roledata.Key), "<b><line-height=2.0pic><size=150%>" + GetString(roleName) + "\n<line-height=1.8pic><size=90%>" + GetString($"{roleName}Info")) + "\n<line-height=1.3pic></b><size=60%>\n" + GetString($"{roleName}InfoLong"), sendTo: player);
                    }
                    return;
                }
            }

            if (GetRoleByInputName(role, out var hr, true))
            {
                if (hr is CustomRoles.Crewmate or CustomRoles.Impostor) SendMessage(msg, player);
                var roleInfo = hr.GetRoleInfo();
                if (roleInfo != null && roleInfo.Description != null)
                {
                    SendMessage(roleInfo.Description.FullFormatHelp, sendTo: player, checkl: true);
                    var addhaverole = roleInfo.AddHaveRole?.Invoke();
                    if (addhaverole is not null and not CustomRoles.NotAssigned)
                    {
                        var addroleInfo = addhaverole.Value.GetRoleInfo();
                        if (addroleInfo != null && addroleInfo.Description != null)
                            SendMessage(addroleInfo.Description.FullFormatHelp, player, ColorString(GetRoleColor(hr), GetString("AddRoleInfoTitle")), checkl: true);
                    }
                }
                // RoleInfoがない役職は従来の処理
                else
                {
                    if (hr.IsAddOn() || hr.IsLovers() || hr == CustomRoles.Amanojaku || hr.IsGhostRole()) SendMessage(GetAddonsHelp(hr), sendTo: player);
                    else SendMessage(ColorString(GetRoleColor(hr), "<b><line-height=2.0pic><size=150%>" + GetString($"{hr}") + "\n<line-height=1.8pic><size=90%>" + GetString($"{hr}Info")) + "\n<line-height=1.3pic></b><size=60%>\n" + GetString($"{hr}InfoLong"), sendTo: player);
                }
                return;
            }
            goto infosend;

        infosend:
            msg += rolemsg;
            if (player == byte.MaxValue) player = 0;
            SendMessage(msg, player);
        }
        /// <summary>
        /// 複数登録するor特別な奴以外はしなくてよい。
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string FixRoleNameInput(string text)
        {
            return text.RemoveHtmlTags() switch
            {
                "GM" or "gm" or "ゲームマスター" => GetString("GM"),

                //インポスター
                "イビルゲッサー" or "いびるげっさー" or "いびげ" or "イビゲ" => GetString("EvilGuesser"),
                "イビルムービング" or "いびるむーびんぐ" => GetString("EvilMoving"),
                "ミニマリスト" or "みにまりすと" => GetString("Minimalist"),
                "イビルリンカー" or "いびるりんかー" => GetString("EvilLinker"),
                "自爆魔" or "じばくま" or "ジバクマ" => GetString("Bomber"),
                "スウーパー" or "すうーぱー" => GetString("Super"),
                "イビルスタンドマスター" or "いびるすたんどますたー" => GetString("EvilStandMaster"),
                "波動砲" or "はどうほう" => GetString("WaveCannon"),
                "ダブルキラー" or "だぶるきらー" or "だぶきら" or "ダブキラ" => GetString("DoubleKiller"),
                "侍" or "さむらい" or "サムライ" => GetString("Samurai"),
                "テレポーター" or "てれぽーたー" or "テレポ" or "てれぽ" => GetString("Teleporter"),
                "エボルバー" or "えぼるばー" or "エボ" or "えぼ" => GetString("Evolver"),
                "ビギナーインポスター" or "びぎなーいんぽすたー" or "びぎいん" or "ビギイン" => GetString("BeginnerImpostor"),
                "マスマーダー" or "ますまーだー" or "ますま" or "マスマ" => GetString("MassMurderer"),
                "タイムリーパー" or "たいむりーぱー" or "タイリ" or "たいり" => GetString("TimeReaper"),
                "チェイサー" or "ちぇいさー" => GetString("Chaser"),
                "エイリアン" or "えいりあん" => GetString("Alien"),
                "ハイジャックエイリアン" or "はいじゃっくえいりあん" => GetString("HijackAlien"),
                "ジャンパー" or "じゃんぱー" => GetString("Jumper"),
                "イビルハッカー" or "いびるはっかー" or "イビハカ" or "いびはか" => GetString("EvilHacker"),
                "イビルトラッカー" or "いびるとらっかー" or "イビトラ" or "いびとら" => GetString("EvilTracker"),
                "イビルギャンブラー" or "いびるぎゃんぶらー" or "イビギャン" or "いびぎゃん" => GetString("EvilGambler"),
                "イビルサテライト" or "いびるさてらいと" or "イビサテ" or "いびさて" => GetString("EvilSatellite"),
                "イビルアドナー" or "いびるあどなー" or "イビアド" or "いびあど" => GetString("EvilAddoner"),
                "イビルメーカー" or "いびるめーかー" or "イビメ" or "いびめ" => GetString("EvilMaker"),
                "イビルテラー" or "いびるてらー" or "イビテラ" or "いびてら" => GetString("EvilTeller"),
                "イビルブレンダー" or "イビルブレンダー" => GetString("EvilBlender"),
                "バルーナー" or "ばるーなー" => GetString("Baluner"),
                "爆弾魔" or "ボマー" or "ばくだんま" or "ぼまー" => GetString("Bomber"),
                "マジシャン" or "まじしゃん" => GetString("Magician"),
                "プロボーラー" or "ぷろぼーらー" => GetString("ProBowler"),
                "テレポートキラー" or "てれぽーときらー" or "テレキラ" or "てれきら" => GetString("TeleportKiller"),
                "花火職人" or "はなびしょくにん" or "はなびしょくじん" or "はなび" or "花火" => GetString("FireworksExpert"),
                "コネクトセーバー" or "こねくとせーばー" or "コネセ" or "こねせ" => GetString("ConnectSaver"),
                "ウィッチ" or "うぃっち" or "まじょ" or "マジョ" or "魔女" => GetString("Witch"),
                "スナイパー" or "すないぱー" or "すな" or "スナ" or "砂" => GetString("Sniper"),
                "アーチャー" or "あーちゃー" => GetString("Archer"),
                "ペンギン" or "ぺんぎん" => GetString("Penguin"),
                "タイムシーフ" or "たいむしーふ" => GetString("TimeThief"),
                "ヴァンパイア" or "ヴぁんぱいあ" or "バンパイア" or "ばんぱいあ" or "ヴァンプ" or "ヴぁんぷ" or "ばんぷ" => GetString("Vampire"),
                "パペッティア" or "ぱぺってぃあ" or "ぱぺ" or "パペ" => GetString("Puppeteer"),
                "ウォーロック" or "うぉーろっく" or "ウォロ" or "うぉろ" => GetString("Warlock"),
                "メアー" or "めあー" or "だーくきらー" or "ダークキラー" => GetString("Mare"),
                "プログレスキラー" or "ぷろぐれすきらー" or "ぷろきら" or "プロキラ" => GetString("ProgressKiller"),
                "インサイダー" or "いんさいだー" or "インサイ" or "いんさい" => GetString("Insider"),
                "モグラ" or "もぐら" => GetString("Mole"),
                "アンチレポーター " or "あんちれぽーたー" or "アンレポ" or "あんれぽ" => GetString("AntiReporter"),
                "イレイサー" or "いれいさー" => GetString("Eraser"),
                "シェイプマスター" or "しぇいぷますたー" or "シェイマス" or "しぇいます" => GetString("ShapeMaster"),
                "シェイプキラー" or "しぇいぷきらー" or "シェイキラ" or "しぇいきら" => GetString("ShapeKiller"),
                "ステルス" or "すてるす" => GetString("Stealth"),
                "ネコカボチャ" or "ねこかぼちゃ" or "ネコカボ" or "ねこかぼ" or "猫カボチャ" or "猫かぼちゃ" or "猫かぼ" or "猫カボ" => GetString("CatPumpkin"),
                "マフィア" or "まふぃあ" => GetString("Mafia"),
                "カリスマスター" or "かりすますたー" or "カリスマ" or "かりすま" => GetString("CharismaMaster"),
                "大狼" or "たいろう" or "大老" or "おおろう" => GetString("Tairou"),//適当に入れたけどおおろうって何?
                "アンフォーチュナー" or "あんふぉーちゅなー" or "案フォーチュナー" or "アンフォ" or "あんふぉ" or "案フォ" or "案ふぉ" => GetString("Unfortuner"),
                "シリアルキラー" or "しりあるきらー" or "シリキラ" or "しりきら" or "シリアル" or "しりある" => GetString("SerialKiller"),//ちなみに俺は呼び方シリアルが好き
                "バウンティハンター" or "ばうんてぃはんたー" or "バウンティ" or "ばうんてぃ" or "ばうはん" or "バウハン" => GetString("BountyHunter"),
                "カーサー" or "かーさー" or "カーサ" or "かーさ" => GetString("Curser"),
                "リミッター" or "りみったー" or "リミッタ" or "りみった" or "りみた" or "リミタ" => GetString("Limiter"),//りみたは今誤字ったから！
                "リローダー" or "りろーだー" => GetString("Reloader"),
                "クイックキラー" or "くいっくきらー" or "クイキラ" or "くいきら" => GetString("QuickKiller"),
                "ノーティファー" or "のーてぃふぁー" or "ノーティ" or "のーてぃ" => GetString("Notifier"),
                "記憶喪失者" or "きおくそうしつしゃ" or "記憶喪失" or "きおくそうしつ" or "偽シェリフ" or "にせシェリフ" or "にせしぇりふ" or "偽狼少年" or "にせ狼少年" => GetString("Amnesia"),
                "ボーダーキラー" or "ぼーだーきらー" or "ボダキラ" or "ぼだきら" => GetString("BorderKiller"),
                "デクレッシェンド" or "でくれっしぇんど" or "デクレ" or "でくれ" => GetString("Decrescendo"),
                "プロボウラー" or "プロボーラー" => GetString("ProBowler"),
                "一途な狼" or "一途" or "いちず" => GetString("EarnestWolf"),
                "インポスター" or "いんぽすたー" or "いんぽ" or "インポ" or "いんぽす" or "インポス" or "imp" => GetString("Impostor"),
                "シェイプシフター" or "しぇいぷしふたー" or "しぇいぷしふ" or "シェイプシフ" or "しぇいぷ" or "シェイプ" => GetString("Shapeshifter"),
                "ファントム" or "ふぁんとむ" or "亡霊" or "ぼうれい" or "ボウレイ" => GetString("Phantom"),
                "ヴァイパー" or "バイパー" => GetString("Viper"),

                //マッドメイト
                "マッドメイト" or "まっどめいと" or "マッド" or "まっど" or "まどめ" or "マドメ" => GetString("Madmate"),
                "マッドスニッチ" or "まっどすにっち" => GetString("MadSnitch"),
                "マッドガーディアン" or "まっどがーでぃあん" => GetString("MadGuardian"),
                "マッドスーサイド" or "まっどすーさいど" or "崇拝者" or "スーサイド" => GetString("MadSuicide"),
                "マッドトラッカー" or "まっどとらっかー" => GetString("MadTracker"),
                "マッドチェンジャー" or "まっどちぇんじゃー" => GetString("MadChanger"),
                "マッドベイト" or "まっどべいと" => GetString("MadBait"),
                "マッドテラー" or "まっどてらー" => GetString("MadTeller"),
                "マッドハッカー" or "まっどはっかー" => GetString("MadHacker"),
                "マッドリデュース" or "まっどりでゅーす" => GetString("MadReduce"),
                "マッドアベンジャー" or "まっどあべんじゃー" => GetString("MadAvenger"),
                "マッドジェスター" or "まっどじぇすたー" => GetString("MadJester"),
                "マッドべトレイヤー" or "まっどべとれいやー" or "マッドベとレイヤー" => GetString("MadBetrayer"),
                "マッドワーカー" or "まっどわーかー" => GetString("MadWorker"),
                "マッドシェリフ" or "まっどしぇりふ" => GetString("MadSheriff"),
                "黒猫" or "くろねこ" or "クロネコ" => GetString("BlackCat"),
                "サイドキックマッドメイト" or "さいどきっくまっどめいと" or "skまっど" or "SKまっど" or "SKマッド" or "SKまっど" => GetString("SKMadmate"),

                //クルーメイト
                "ナイスイレイサー" or "ないすいれいさー" => GetString("NiceEraser"),
                "ナイスゲッサー" or "ないすげっさー" or "ないげ" or "ナイゲ" => GetString("NiceGuesser"),
                "ナイス猫又" or "ないすねこまた" or "ないねこ" or "ナイネコ" => GetString("NiceNekomata"),
                "ナイスリンカー" or "ないすりんかー" or "ないりん" or "ナイリン" => GetString("NiceLinker"),
                "ナイストラッパー" or "ないすとらっぱー" or "ないとら" or "ナイトラ" => GetString("NiceTrapper"),
                "ナイステレポーター" or "ないすてれぽーたー" or "ないてれ" or "ナイテレ" => GetString("NiceTeleporter"),
                "魔法少女" or "まほうしょうじょ" or "マホウショウジョ" or "まほしょう" or "マホショウ" => GetString("MagicalGirl"),
                "村長" or "そんちょう" or "ソンチョウ" or "むらちょう" or "ムラチョウ" => GetString("VillageChief"),
                "波動砲シェリフ" or "はどうほうしぇりふ" => GetString("WaveCannonSheriff"),
                "あやしい占い師" or "怪しい占い師" or "あやしいうらないし" or "アヤシイウラナイシ" => GetString("SuspiciousTeller"),
                "かけだし占い師" or "駆け出し占い師" or "かけだしうらないし" or "カケダシウラナイシ" => GetString("BeginnerTeller"),
                "ぽんこつ占い師" or "ポンコツ占い師" => GetString("PonkotuTeller"),
                "霊媒師" or "れいばいし" or "レイバイシ" => GetString("Medium"),
                "ジェイラー" or "じぇいらー" => GetString("Jailer"),
                "ニムロッド" or "にむろっど" or "にむろつど" or "ニムロツド" => GetString("Nimrod"),
                "ワルキューレ" or "わるきゅーれ" or "わるきゆーれ" or "ワルキユーレ" => GetString("Walkure"),
                "牛乳屋" or "ぎゅうにゅうや" or "ギュウニュウヤ" => GetString("Milkman"),
                "賢者" or "けんじゃ" or "ケンジャ" => GetString("Sage"),
                "子方" or "こかた" or "コカタ" or "しかた" or "シカタ" => GetString("Apprentice"),
                "ラビット" or "らびっと" or "うさぎ" or "ウサギ" or "兎" or "兔" => GetString("Rabbit"),
                "サンタ" or "さんた" or "ひげのおじさん" or "メリークリスマス！！" => GetString("Santa"),
                "ムービング" or "むーびんぐ" => GetString("Moving"),
                "プクプク" or "ぷくぷく" or "ﾌﾟｸﾌﾟｸ" => GetString("Pukupuku"),
                "ヒッチハイカー" or "ひっちはいかー" or "ストーカー" => GetString("Hitchhiker"),
                "オールラウンダー" or "おーるだうんだー" or "オルラ" or "おるら" => GetString("Allrounder"),
                "ナイスアドナー" or "ナイアド" or "ないあど" => GetString("NiceAddoer"),
                "ナイスロガー" or "ないすろがー" => GetString("NiceLogger"),
                "フォーチュナー" or "ふぉーちゅなー" or "ふぉーつなー" => GetString("Fortuner"),
                "シェリフ" or "しぇりふ" or "猿" or "さる" => GetString("Sheriff"),
                "ミーティングシェリフ" or "みーてぃんぐしぇりふ" => GetString("MeetingSheriff"),
                "メイヤー" or "めいやー" => GetString("Mayor"),
                "巫女" or "みこ" or "ミコ" or "ふじょ" or "フジョ" => GetString("ShrineMaiden"),
                "捜査官" or "そうさかん" or "ソウサカン" => GetString("Inspector"),
                "ディクテーター" or "でぃくてーたー" => GetString("Dictator"),
                "ホワイトハッカー" or "ほわいとはっかー" or "ほわは" or "ホワハ" => GetString("WhiteHacker"),
                "天秤" or "てんびん" or "テンビン" => GetString("Balancer"),
                "観察者" or "かんさつしゃ" or "カンサツシャ" => GetString("Observer"),
                "アナライザー" or "あならいざー" => GetString("Analyzer"),
                "ウルトラスター" or "うるとらすたー" or "ひかるごみ" or "光るゴミ" or "スター" => GetString("UltraStar"),
                "タスクスター" or "たすくすたー" => GetString("TaskStar"),
                "パン屋" or "ぱんや" or "パンヤ" or "ベーカリー" => GetString("Bakery"),
                "エクスプレス" or "えくすぷれす" or "しんかんせん" or "新幹線" or "シンカンセン" => GetString("Express"),
                "ストルナー" or "すとるなー" or "属性サンタ" => GetString("Stolener"),
                "ベイト" or "べいと" => GetString("Bait"),
                "インセンダー" or "いんせんだー" => GetString("InSender"),
                "ガードマスター" or "がーどますたー" or "しーるだー" or "シールダー" => GetString("GuardMaster"),
                "君臨者" or "くんりんしゃ" or "クンリンシャ" or "きんぐ" or "キング" => GetString("King"),
                "トラッパー" or "とらっぱー" => GetString("Trapper"),
                "スニッチ" or "すにっち" => GetString("Snitch"),
                "ドクター" or "どくたー" or "いしゃ" or "医者" or "イシャ" => GetString("Doctor"),
                "シーア" or "しーあ" => GetString("Seer"),
                "サイキック" or "さいきっく" => GetString("Psychic"),
                "サテライト" or "さてらいと" => GetString("Satelite"),
                "ライター" or "らいたー" => GetString("Lighter"),
                "スピードブースター" or "すぴーどぶーすたー" => GetString("SpeedBooster"),
                "タイムマネージャー" or "たいむまねーじゃー" => GetString("TimeManager"),
                "エフィシェンシー" or "えふぃしぇんしー" or "えふぃふぇんしー" or "エフィフェンシー" or "えふぃふぇん" or "えふぃしぇん" or "エフェフェン" or "エフィシェン" => GetString("Efficient"),
                "サボタージュマスター" or "さぼたーじゅますたー" or "さぼます" or "サボマス" => GetString("SabotageMaster"),
                "キャンドルライター" or "きゃんどるらいたー" or "きゃんらい" or "キャンライ" => GetString("キャンライ"),
                "雪だるま" or "ゆきだるま" or "ユキダルマ" => GetString("Snowman"),
                "ウォーカー" or "うぉーかー" => GetString("Walker"),
                "カムバッカー" or "かむばっかー" or "かむば" or "カムバ" or "噛むバッカー" => GetString("ComeBacker"),
                "アンドロイド" or "あんどろいど" or "すまほ" or "android" => GetString("Android"),
                "スタッフ" or "すたっふ" => GetString("Staff"),
                "トイレファン" or "といれふぁん" or "といれ" or "トイレ" => GetString("ToiletFan"),
                "ベントマスター" or "べんとますたー" => GetString("VentMaster"),
                "ベントオープナー" or "べんとおーぷなー" => GetString("VentOpener"),
                "ベントハンター" or "べんとはんたー" => GetString("VentHunter"),
                "エンジニア" or "えんじにあ" => GetString("Engineer"),
                "科学者" or "かがくしゃ" => GetString("Scientist"),
                "トラッカー" or "とらっかー" => GetString("Tracker"),
                "ノイズメーカー" or "のいずめーかー" => GetString("Noisemaker"),
                "クルー" or "クルーメイト" or "くるーめいと" or "くるー" => GetString("Crewmate"),
                "狼少年" or "オオカミ少年" or "おおかみ少年" or "おおかみしょうねん" or "オオカミショウネン" => GetString("WolfBoy"),
                "ギャスプ" or "ギャプス" or "ギャスフ" or "ぎゃすぷ" => GetString("Gasp"),//これを許していいのか。
                "さつまといも" or "さつもといも" or "satsumatoimo" or "satsumotoimo" => GetString("SatsumatoImo"),
                "さつまといもc" or "さつもといもc" or "satsumatoimoc" or "satsumotoimoc" => GetString("SatsumatoImoC"),
                "さつまといもm" or "さつもといもm" or "satsumatoimom" or "satsumotoimom" => GetString("SatsumatoImoM"),
                //第3陣営
                "独裁者" or "どくさいしゃ" or "ドクサイシャ" or "dokusaisha" => GetString("Autocrat"),
                "アーソニスト" or "あーそにすと" or "asonisuto" => GetString("Arsonist"),
                "ジェスター" or "じぇすたー" or "jesuta" => GetString("Jester"),
                "テロリスト" or "てろりすと" or "terorisuto" => GetString("Terrorist"),
                "エクスキューショナー" or "えくすきゅーしょなー" or "ekusukyushona" => GetString("Executioner"),
                "シュレディンガーの猫" or "しゅれでぃんがーのねこ" or "シュレ猫" or "しゅれねこ" or "shuredinganoneko" or "shureneko" => GetString("SchrodingerCat"),
                "オポチュニスト" or "おぽちゅにすと" or "opochunisuto" => GetString("Opportunist"),
                "フリーター" or "ふりーたー" or "furita" => GetString("Freeter"),
                "神" or "かみ" or "カミ" or "kami" => GetString("God"),
                "マグロ" or "まぐろ" or "maguro" => GetString("Tuna"),
                "陰陽師" or "おんみょうじ" or "オンミョウジ" or "onmyoji" => GetString("Onmyoji"),
                "式神" or "しきがみ" or "シキガミ" or "shikigami" => GetString("Shikigami"),
                "ゾンビ" or "ぞんび" or "zonbi" => GetString("Zombie"),
                "エゴイスト" or "えごいすと" or "egoisuto" => GetString("Egoist"),
                "ジャッカル" or "じゃっかる" or "jakkaru" or "じゃっこー" or "jk" => GetString("Jackal"),
                "波動砲ジャッカル" or "はどうほうじゃっかる" or "ハドウホウジャッカル" or "hadouhoujakkaru" => GetString("JackalHadouHo"),
                "弾" or "たま" or "タマ" or "tama" => GetString("Tama"),
                "ジャッカルエイリアン" or "じゃっかるえいりあん" or "jakkarueirian" => GetString("JackalAlien"),
                "ジャッカルウルフ" or "じゃっかるうるふ" or "jakkaruurufu" => GetString("JackalWolf"),
                "ジャッカルマフィア" or "じゃっかるまふぃあ" or "jakkarumafia" => GetString("JackalMafia"),
                "ジャッカルドール" or "じゃっかるどーる" or "jakkarudoru" or "さいどきっく" or "サイドキック" or "どーる" or "ドール" => GetString("Jackaldoll"),
                "ペスト医師" or "ぺすといし" or "ペストイシ" or "pesutoishi" => GetString("PlagueDoctor"),
                "リモートキラー" or "りもーときらー" or "rimotokira" => GetString("Remotekiller"),
                "シェフ" or "しぇふ" or "shefu" => GetString("Chef"),
                "カウントキラー" or "かうんときらー" or "kauntokira" or "かうきら" or "カウキラ" => GetString("CountKiller"),
                "死神" or "しにがみ" or "シニガミ" or "shinigami" => GetString("GrimReaper"),
                "マドンナ" or "まどんな" or "madonna" => GetString("Madonna"),
                "ワーカホリック" or "わーかほりっく" or "wakahorikku" => GetString("Workaholic"),
                "モノクラー" or "ものくらー" or "monokura" => GetString("Monochromer"),
                "ドッペルゲンガー" or "どっぺるげんがー" or "dopperugenga" or "どっぺる" or "ドッペル" => GetString("DoppelGanger"),
                "マスメディア" or "ますめでぃあ" or "masumedia" => GetString("MassMedia"),
                "カメレオン" or "かめれおん" or "kamereon" => GetString("Chameleon"),
                "バンカー" or "ばんかー" or "banka" => GetString("Banker"),
                "バケネコ" or "ばけねこ" or "bakeneko" => GetString("BakeCat"),
                "怪盗" or "かいとう" or "カイトウ" or "kaito" => GetString("PhantomThief"),
                "カースメーカー" or "かーすめーかー" or "kasumeka" => GetString("CurseMaker"),
                "妖狐" or "ようこ" or "ヨウコ" or "yoko" or "きつね" or "キツネ" or "kitsune" => GetString("Fox"),
                "サンタクロース" or "さんたくろーす" or "santakurosu" => GetString("SantaClaus"),
                "ヴァルチャー" or "ゔぁるちゃー" or "varucha" or "バルチャー" or "ばるちゃー" or "barucha" => GetString("Vulture"),
                "裏切者" or "うらぎりもの" or "ウラギリモノ" or "uragirimono" => GetString("Turncoat"),
                "藁人形" or "わらにんぎょう" or "ワラニンギョウ" or "waraningyo" => GetString("Strawdoll"),
                "ミッショナー" or "みっしょなー" or "misshona" => GetString("Missioneer"),
                "愚か者" or "おろかもの" or "オロカモノ" or "orokamono" => GetString("Fool"),
                "キューピッド" or "きゅーぴっど" or "kyupiddo" => GetString("Cupid"),
                "イーター" or "いーたー" or "ita" => GetString("Eater"),
                "スペランカー" or "すぺらんかー" or "superanka" => GetString("Spelunker"),
                "忘却者" or "ぼうきゃくしゃ" or "ボウキャクシャ" or "bokyakusha" => GetString("Oblivion"),
                "パブロフの犬" or "ぱぶろふのいぬ" or "paburofunoinu" => GetString("PavlovDog"),
                "パブロフのオーナー" or "ぱぶろふのおーなー" or "paburofunoonaa" => GetString("PavlovOwner"),
                "ぱぶろふ" or "パブロフ" or "paburofu" => GetString("PavlovDogImprint"),
                "モイラ" or "もいら" or "moira" => GetString("Moira"),
                "毒入りパン屋" or "どくいりぱんや" or "ドクイリパンヤ" or "dokuiripanya" => GetString("PoisonedBakery"),
                "チャッター" or "ちゃったー" or "chatta" => GetString("Chatter"),
                "爆ぜ師" or "はぜし" or "ハゼシ" or "hazeshi" => GetString("LoversBreaker"),
                "自殺願望者" or "じさつがんぼうしゃ" or "ジサツガンボウシャ" or "jisatsuganbosha" => GetString("Suicider"),
                "スタンドマスター" or "すたんどますたー" or "sutandomasuta" => GetString("StandMaster"),
                "スタンド" or "すたんど" or "sutando" => GetString("Stand"),
                "シャイボーイ" or "しゃいぼーい" or "shaiboi" => GetString("Shyboy"),
                "ヴィラン" or "ゔぃらん" or "viran" => GetString("Villain"),
                "スクラッチャー" or "すくらっちゃー" or "sukuraccha" => GetString("Scratcher"),
                "モニカ(未完成)" or "もにか" or "モニカ" or "monikamikansei" or "モニカ" or "もにか" or "monika" => GetString("Monika"),
                "鬼" or "おに" or "オニ" or "oni" => GetString("Ogre"),
                "弁護士" or "べんごし" or "ベンゴシ" or "bengoshi" => GetString("Lawyer"),
                "追跡者" or "ついせきしゃ" or "ツイセキシャ" or "tsuisekisha" => GetString("Pursuer"),
                "決闘者" or "けっとうしゃ" or "ケットウシャ" or "kettosha" => GetString("Duelist"),
                "海賊" or "かいぞく" or "カイゾク" or "kaizoku" => GetString("Pirate"),
                "一味" or "いちみ" or "イチミ" or "ichimi" => GetString("Gang"),
                "ポーカーフェイス" or "ぽーかーふぇいす" or "pokafeisu" => GetString("PokerFace"),
                "一番目の仔豚" or "いちばんめのこぶた" or "イチバンメノコブタ" or "ichibanmenokobuta" => GetString("TheFirstLittlePig"),
                "二番目の仔豚" or "にばんめのこぶた" or "ニバンメノコブタ" or "nibanmenokobuta" => GetString("TheSecondLittlePig"),
                "三番目の仔豚" or "さんばんめのこぶた" or "サンバンメノコブタ" or "sanbanmenokobuta" => GetString("TheThirdLittlePig"),
                "ハッピージェスター" or "はっぴーじぇすたー" or "happijesuta" => GetString("HappyJester"),
                "バットガール" or "ばっとがーる" or "battogaru" => GetString("BatGirl"),
                "アマテラス" or "あまてらす" or "amaterasu" => GetString("Amateras"),
                "ルーラー" or "るーらー" or "ru-ra-" or "ruler" => GetString("Ruler"),
                "被虐者" or "ひぎゃくしゃ" or "ヒギャクシャ" or "higyakusha" => GetString("Victim"),
                //幽霊役職
                //コンビネーション
                "ラバーズ" or "らばーず" or "rabazu" or "リア充" or "恋人" => GetString("Lovers"),
                "レッドラバーズ" or "れっどらばーず" or "reddorabazu" => GetString("RedLovers"),
                "イエローラバーズ" or "いえろーらばーず" or "ierorabazu" => GetString("YellowLovers"),
                "ブルーラバーズ" or "ぶるーらばーず" or "bururabazu" => GetString("BlueLovers"),
                "グリーンラバーズ" or "ぐりーんらばーず" or "gurinrabazu" => GetString("GreenLovers"),
                "ホワイトラバーズ" or "ほわいとらばーず" or "howaitorabazu" => GetString("WhiteLovers"),
                "パープルラバーズ" or "ぱーぷるらばーず" or "papururabazu" => GetString("PurpleLovers"),
                "マドンナラバーズ" or "まどんならばーず" or "madonnarabazu" => GetString("MadonnaLovers"),
                "キューピッドラバーズ" or "きゅーぴっどらばーず" or "kyupiddorabazu" => GetString("CupidLovers"),
                "片思い" or "かたおもい" or "カタオモイ" or "kataomoi" or "片想い" => GetString("OneLove"),
                "天邪鬼" or "あまのじゃく" or "アマノジャク" or "amanojaku" => GetString("Amanojaku"),
                "徒党" or "ととう" or "トトウ" or "toto" => GetString("Faction"),
                "ドライバーとブレイド" => GetString("Driver"),

                "織姫と彦星" or "おりひめとひこぼし" or "オリヒメトヒコボシ" or "orihimetohikoboshi" => GetString("Vega"),
                _ => text,
            };
        }
        static readonly Dictionary<string, string> KanaRomajiMap = new()
        {
            ["あ"] = "a",
            ["い"] = "i",
            ["う"] = "u",
            ["え"] = "e",
            ["お"] = "o",
            ["か"] = "ka",
            ["き"] = "ki",
            ["く"] = "ku",
            ["け"] = "ke",
            ["こ"] = "ko",
            ["さ"] = "sa",
            ["し"] = "shi",
            ["す"] = "su",
            ["せ"] = "se",
            ["そ"] = "so",
            ["た"] = "ta",
            ["ち"] = "chi",
            ["つ"] = "tsu",
            ["て"] = "te",
            ["と"] = "to",
            ["な"] = "na",
            ["に"] = "ni",
            ["ぬ"] = "nu",
            ["ね"] = "ne",
            ["の"] = "no",
            ["は"] = "ha",
            ["ひ"] = "hi",
            ["ふ"] = "fu",
            ["へ"] = "he",
            ["ほ"] = "ho",
            ["ま"] = "ma",
            ["み"] = "mi",
            ["む"] = "mu",
            ["め"] = "me",
            ["も"] = "mo",
            ["や"] = "ya",
            ["ゆ"] = "yu",
            ["よ"] = "yo",
            ["ら"] = "ra",
            ["り"] = "ri",
            ["る"] = "ru",
            ["れ"] = "re",
            ["ろ"] = "ro",
            ["わ"] = "wa",
            ["を"] = "wo",
            ["ん"] = "n",
            ["が"] = "ga",
            ["ぎ"] = "gi",
            ["ぐ"] = "gu",
            ["げ"] = "ge",
            ["ご"] = "go",
            ["ざ"] = "za",
            ["じ"] = "ji",
            ["ず"] = "zu",
            ["ぜ"] = "ze",
            ["ぞ"] = "zo",
            ["だ"] = "da",
            ["ぢ"] = "ji",
            ["づ"] = "zu",
            ["で"] = "de",
            ["ど"] = "do",
            ["ば"] = "ba",
            ["び"] = "bi",
            ["ぶ"] = "bu",
            ["べ"] = "be",
            ["ぼ"] = "bo",
            ["ぱ"] = "pa",
            ["ぴ"] = "pi",
            ["ぷ"] = "pu",
            ["ぺ"] = "pe",
            ["ぽ"] = "po",
            ["ぁ"] = "a",
            ["ぃ"] = "i",
            ["ぅ"] = "u",
            ["ぇ"] = "e",
            ["ぉ"] = "o",
            ["ゃ"] = "ya",
            ["ゅ"] = "yu",
            ["ょ"] = "yo",
            ["きゃ"] = "kya",
            ["きゅ"] = "kyu",
            ["きょ"] = "kyo",
            ["しゃ"] = "sha",
            ["しゅ"] = "shu",
            ["しょ"] = "sho",
            ["ちゃ"] = "cha",
            ["ちゅ"] = "chu",
            ["ちょ"] = "cho",
            ["にゃ"] = "nya",
            ["にゅ"] = "nyu",
            ["にょ"] = "nyo",
            ["ひゃ"] = "hya",
            ["ひゅ"] = "hyu",
            ["ひょ"] = "hyo",
            ["みゃ"] = "mya",
            ["みゅ"] = "myu",
            ["みょ"] = "myo",
            ["りゃ"] = "rya",
            ["りゅ"] = "ryu",
            ["りょ"] = "ryo",
            ["ぎゃ"] = "gya",
            ["ぎゅ"] = "gyu",
            ["ぎょ"] = "gyo",
            ["じゃ"] = "ja",
            ["じゅ"] = "ju",
            ["じょ"] = "jo",
            ["びゃ"] = "bya",
            ["びゅ"] = "byu",
            ["びょ"] = "byo",
            ["ぴゃ"] = "pya",
            ["ぴゅ"] = "pyu",
            ["ぴょ"] = "pyo",
            ["ふぁ"] = "fa",
            ["ふぃ"] = "fi",
            ["ふぇ"] = "fe",
            ["ふぉ"] = "fo",
            ["ゔぁ"] = "va",
            ["ゔぃ"] = "vi",
            ["ゔ"] = "vu",
            ["ゔぇ"] = "ve",
            ["ゔぉ"] = "vo",
            ["ヴぁ"] = "va",
            ["ヴぃ"] = "vi",
            ["ヴ"] = "vu",
            ["ヴぇ"] = "ve",
            ["ヴぉ"] = "vo",
        };

        static string NormalizeRomajiInput(string text)
            => Regex.Replace(KanaToRomaji(text ?? ""), @"[^a-z0-9]", string.Empty).ToLowerInvariant();

        static string KanaToRomaji(string text)
        {
            var result = "";
            var doubleNext = false;
            for (var i = 0; i < text.Length; i++)
            {
                var current = NormalizeKana(text[i]);
                if (current == 'っ')
                {
                    doubleNext = true;
                    continue;
                }
                if (current == 'ー')
                {
                    var vowel = result.LastOrDefault(ch => "aeiou".Contains(ch));
                    if (vowel != default) result += vowel;
                    continue;
                }

                var key = current.ToString();
                if (i + 1 < text.Length)
                {
                    var next = NormalizeKana(text[i + 1]);
                    var combined = $"{current}{next}";
                    if (KanaRomajiMap.TryGetValue(combined, out var combinedRomaji))
                    {
                        result += doubleNext && combinedRomaji.Length > 0 ? $"{combinedRomaji[0]}{combinedRomaji}" : combinedRomaji;
                        doubleNext = false;
                        i++;
                        continue;
                    }
                }

                if (KanaRomajiMap.TryGetValue(key, out var romaji))
                {
                    result += doubleNext && romaji.Length > 0 ? $"{romaji[0]}{romaji}" : romaji;
                    doubleNext = false;
                }
                else if (char.IsLetterOrDigit(current))
                {
                    result += char.ToLowerInvariant(current);
                    doubleNext = false;
                }
            }
            return result;
        }

        static char NormalizeKana(char c)
            => c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c;

        public static bool GetRoleByInputName(string input, out CustomRoles output, bool includeVanilla = false)
        {
            output = new();
            input = Regex.Replace(input, @"[0-9]+", string.Empty);
            input = Regex.Replace(input, @"\s", string.Empty);
            input = Regex.Replace(input, @"[\x01-\x1F,\x7F]", string.Empty);
            input = input.ToLower().Trim().Replace("是", string.Empty);
            if (input == "" || input == string.Empty) return false;
            var romajiInput = NormalizeRomajiInput(input);
            input = FixRoleNameInput(input).ToLower();
            foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))
            {
                if (!includeVanilla && role.IsVanilla() && role != CustomRoles.GuardianAngel) continue;
                var roleName = GuessManager.ChangeNormal2Vanilla(role);
                if (input == roleName
                    || (!string.IsNullOrEmpty(romajiInput)
                        && (romajiInput == NormalizeRomajiInput(role.ToString())
                            || romajiInput == NormalizeRomajiInput(GetString(role.ToString()))
                            || romajiInput == NormalizeRomajiInput(roleName))))
                {
                    output = role;
                    return true;
                }
            }
            return false;
        }
        private static void ConcatCommands(CustomRoleTypes roleType)
        {
            var roles = CustomRoleManager.AllRolesInfo.Values.Where(role => role.CustomRoleType == roleType);
            foreach (var role in roles)
            {
                if (role.ChatCommand is null) continue;
                roleCommands[role.RoleName] = role.ChatCommand;
            }
        }
        public static void SetRoleLists()
        {
            //役職選定後に処理する奴。
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                var role = pc.GetCustomRole();
                var roletype = role.GetCustomRoleTypes();
                var OneLovespace = pc.Is(CustomRoles.OneLove) ? ColorString(GetRoleColor(CustomRoles.OneLove), GetString("OneLove") + " ") : "";
                var color = Palette.CrewmateBlue;
                var roleClass = CustomRoleManager.GetByPlayerId(pc.PlayerId);
                switch (roletype)
                {
                    case CustomRoleTypes.Impostor or CustomRoleTypes.Madmate:
                        color = Palette.ImpostorRed;
                        break;
                    case CustomRoleTypes.Neutral:
                        color = GetRoleColor(role);
                        break;
                }
                UtilsGameLog.LastLog[pc.PlayerId] = "<b>" + ColorString(Main.PlayerColors[pc.PlayerId], Main.AllPlayerNames[pc.PlayerId] + "</b>");
                UtilsGameLog.LastLogRole[pc.PlayerId] = $"<b>{OneLovespace}" + ColorString(GetRoleColor(role), GetString($"{role}")) + "</b>";
                PlayerCatch.AllPlayerFirstTypes.Add(pc.PlayerId, roletype);

                //Addons
                var state = pc.GetPlayerState();
                if (pc.Is(CustomRoles.Guarding)) state.HaveGuard[1] += Guarding.HaveGuard;
                //RoleAddons
                if (RoleAddAddons.GetRoleAddon(role, out var data, pc, subrole: [CustomRoles.Guarding]))
                {
                    if (data.GiveGuarding.GetBool()) state.HaveGuard[1] += data.Guard.GetInt();
                }
                if (!Main.AllPlayerKillCooldown.ContainsKey(pc.PlayerId))
                    Main.AllPlayerKillCooldown.Add(pc.PlayerId, Options.DefaultKillCooldown);

                var rolebasetype = pc.GetCustomRole().GetRoleInfo()?.BaseRoleType.Invoke() ?? RoleTypes.GuardianAngel;
                if (rolebasetype is RoleTypes.GuardianAngel)
                {
                    rolebasetype = pc.Data.Role.Role;
                }
                state.NowRoleType = rolebasetype;
            }

            if (Lovers.OneLovePlayer.BelovedId != byte.MaxValue && GameModeManager.IsStandardClass())
            {
                UtilsGameLog.LastLogRole[Lovers.OneLovePlayer.BelovedId] += ColorString(GetRoleColor(CustomRoles.OneLove), "♡");
                if (Lovers.OneLovePlayer.doublelove) UtilsGameLog.LastLogRole[Lovers.OneLovePlayer.OneLove] += ColorString(GetRoleColor(CustomRoles.OneLove), "♡");
            }
        }
    }
}
