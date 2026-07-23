using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime.InteropTypes;
using HarmonyLib;
using UnityEngine;
using AmongUs.Data;
using AmongUs.GameOptions;

using TownOfHost.Modules;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.AddOns.Impostor;
using TownOfHost.Roles.AddOns.Neutral;
using TownOfHost.Roles.Ghost;
using TownOfHost.Roles.Madmate;
using static TownOfHost.Translator;
using static TownOfHost.UtilsRoleText;
using TownOfHost.Patches;
using TownOfHost.Attributes;
using Hazel;
using InnerNet;

namespace TownOfHost
{
    public static class Utils
    {
        public static bool IsActive(SystemTypes type)
        {
            if (GameStates.IsFreePlay && CustomSpawnEditor.ActiveEditMode) return false;
            if (ShipStatus.Instance is null) return false;
            // ないものはfalse
            if (!ShipStatus.Instance.Systems.ContainsKey(type))
            {
                return false;
            }
            int mapId = Main.NormalOptions.MapId;
            switch (type)
            {
                case SystemTypes.Electrical:
                    {
                        var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                        return SwitchSystem != null && SwitchSystem.IsActive;
                    }
                case SystemTypes.Reactor:
                    {
                        if (mapId == 2) return false;
                        else
                        {
                            var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                            return ReactorSystemType != null && ReactorSystemType.IsActive;
                        }
                    }
                case SystemTypes.Laboratory:
                    {
                        if (mapId != 2) return false;
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType != null && ReactorSystemType.IsActive;
                    }
                case SystemTypes.LifeSupp:
                    {
                        if (mapId is 2 or 4) return false;
                        var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                        return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                    }
                case SystemTypes.Comms:
                    {
                        if (mapId is 1 or 5)
                        {
                            var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                            return HqHudSystemType != null && HqHudSystemType.IsActive;
                        }
                        else
                        {
                            var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                            return HudOverrideSystemType != null && HudOverrideSystemType.IsActive;
                        }
                    }
                case SystemTypes.HeliSabotage:
                    {
                        var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                        return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                    }
                case SystemTypes.MushroomMixupSabotage:
                    {
                        var mushroomMixupSabotageSystem = ShipStatus.Instance.Systems[type].TryCast<MushroomMixupSabotageSystem>();
                        return mushroomMixupSabotageSystem != null && mushroomMixupSabotageSystem.IsActive;
                    }
                default:
                    return false;
            }
        }
        public static bool IsCriticalSabotage(this SystemTypes system) =>
        system is SystemTypes.Laboratory or SystemTypes.HeliSabotage or SystemTypes.Reactor or SystemTypes.LifeSupp;

        public static SystemTypes GetCriticalSabotageSystemType() => (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor,
        };
        //誰かが死亡したときのメソッド
        public static void TargetDies(MurderInfo info)
        {
            PlayerControl killer = info.AppearanceKiller, target = info.AttemptTarget;

            if (!target.Data.IsDead || GameStates.IsMeeting) return;

            List<PlayerControl> Players = new();
            foreach (var seer in PlayerCatch.AllPlayerControls)
            {
                if (KillFlashCheck(info, seer))
                {
                    seer.KillFlash();
                }
            }
        }
        public static bool KillFlashCheck(MurderInfo info, PlayerControl seer)
        {
            PlayerControl killer = info.AppearanceKiller, target = info.AttemptTarget;

            if (seer.Is(CustomRoles.GM)) return true;

            if (seer.Data.IsDead && (Options.GhostCanSeeKillflash.GetBool() || !Options.GhostOptions.GetBool()) && !seer.Is(CustomRoles.AsistingAngel) && (!seer.IsGhostRole() || Options.GhostRoleCanSeeKillflash.GetBool()) && target != seer) return true;
            if (seer.Data.IsDead || killer == seer || target == seer) return false;

            //ラスポスで付いてるのに！とかが一応ありえる。
            var check = false;

            if (seer.GetRoleClass() is IKillFlashSeeable killFlashSeeable)
            {
                if (Amnesia.CheckAbility(seer))
                {
                    var role = killFlashSeeable.CheckKillFlash(info);
                    if (role is null) return false;
                    check |= role is true;
                }
            }


            if (seer.Is(CustomRoles.LastImpostor) && LastImpostor.GiveSeeing.GetBool()) check |= !IsActive(SystemTypes.Comms) || LastImpostor.SeeingCanSeeComms.GetBool();
            if (seer.Is(CustomRoles.LastNeutral) && LastNeutral.GiveSeeing.GetBool()) check |= !IsActive(SystemTypes.Comms) || LastNeutral.SeeingCanSeeComms.GetBool();

            if (RoleAddAddons.GetRoleAddon(seer.GetCustomRole(), out var data, seer, subrole: CustomRoles.Seeing))
                if (data.GiveSeeing.GetBool()) check |= !IsActive(SystemTypes.Comms) || data.SeeingCanSeeComms.GetBool();

            if (SuddenDeathMode.SuddenCanSeeKillflash.GetBool()) return true;

            return check || seer.GetCustomRole() switch
            {
                // IKillFlashSeeable未適用役職はここに書く
                _ => (SatsumatoImo.UsesMadmateCommonSettings(seer) && Options.MadmateCanSeeKillFlash.GetBool())
                || (seer.Is(CustomRoles.Seeing) && (!IsActive(SystemTypes.Comms) || Seeing.OptionCanSeeActiveComms.GetBool()))
            };
        }
        public static bool NowKillFlash = false;
        public static void KillFlash(this PlayerControl player, bool force = false)
        {
            //キルフラッシュ(ブラックアウト+リアクターフラッシュ)の処理
            bool ReactorCheck = IsActive(GetCriticalSabotageSystemType());

            var Duration = Options.KillFlashDuration.GetFloat();
            if (ReactorCheck) Duration += 0.2f; //リアクター中はブラックアウトを長くする

            //実行
            var state = PlayerState.GetByPlayerId(player.PlayerId);
            if (!force && !GameStates.CalledMeeting) state.IsBlackOut = true; //ブラックアウト
            if (player.PlayerId == 0 && !force)
            {
                FlashColor(new(1f, 0f, 0f, 0.5f));
                if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
            }
            else if (!ReactorCheck) player.ReactorFlash(0f); //リアクターフラッシュ
            player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                if (!GameStates.CalledMeeting) state.IsBlackOut = false; //ブラックアウト解除
                player.MarkDirtySettings();
            }, Options.KillFlashDuration.GetFloat(), "RemoveKillFlash");
        }
        public static void AllPlayerKillFlash()
        {
            if (SuddenDeathMode.NowSuddenDeathMode || Options.CurrentGameMode is CustomGameMode.MurderMystery) return;

            if (IsActive(SystemTypes.Reactor) || IsActive(SystemTypes.HeliSabotage))
            {
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    pc.KillFlash(true);
                }
                return;
            }
            var systemtypes = GetCriticalSabotageSystemType();
            ShipStatus.Instance.RpcUpdateSystem(systemtypes, 128);

            NowKillFlash = true;
            _ = new LateTask(() =>
            {
                ShipStatus.Instance.RpcUpdateSystem(systemtypes, 16);

                if (Main.NormalOptions.MapId == 4) //Airship用
                {
                    var player = PlayerCatch.AllAlivePlayerControls.FirstOrDefault(pc => pc.PlayerId != PlayerControl.LocalPlayer.PlayerId);
                    if (player == null) player = PlayerControl.LocalPlayer;

                    MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.None, AmongUsClient.Instance.HostId);
                    messageWriter.Write((byte)systemtypes);
                    messageWriter.WriteNetObject(player);
                    messageWriter.Write((byte)17);
                    AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                }
            }, Options.KillFlashDuration.GetFloat(), "Fix Reactor");
            _ = new LateTask(() => NowKillFlash = false, Options.KillFlashDuration.GetFloat() * 2, "", true);
        }
        public static void BlackOut(this IGameOptions opt, bool IsBlackOut)
        {
            if (IsBlackOut)
            {
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
                opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
            }
            return;
        }
        public static bool CanDeathReasonKillerColor(this byte playerId)
        {
            var pc = PlayerCatch.GetPlayerById(playerId);
            if (pc == null) return false;
            var isAlive = pc.IsAlive();
            var GhostRole = pc.IsGhostRole();
            if (!isAlive && GhostRole) return Options.GhostRoleCanSeeKillerColor.GetBool();
            if (!isAlive && !GhostRole) return Options.GhostCanSeeKillerColor.GetBool() || !Options.GhostOptions.GetBool();
            return false;
        }
        public static string GetVitalText(byte playerId, bool? RealKillerColor = false, bool alivecolor = true)
        {
            var state = PlayerState.GetByPlayerId(playerId);

            if (state == null) return GetString("DeathReason.Disconnected");

            string deathReason = state.IsDead ? GetString("DeathReason." + state.DeathReason) : GetString("Alive");
            switch (RealKillerColor)
            {
                case true:
                    {
                        var KillerId = state.GetRealKiller();
                        if (alivecolor is false && KillerId == byte.MaxValue) return deathReason;
                        //rgb(97, 128, 163)
                        Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : (state.DeathReason == CustomDeathReason.etc ? new Color32(97, 128, 163, 255) : new Color32(120, 120, 120, 255));
                        deathReason = ColorString(color, deathReason);
                    }
                    break;
                case null:
                    deathReason = $"<#80ffdd>{deathReason}</color>";
                    break;
            }
            return deathReason;
        }
        public static string GetDeathReason(CustomDeathReason status)
        {
            return GetString("DeathReason." + Enum.GetName(typeof(CustomDeathReason), status));
        }
        public static void ShowTimer(byte PlayerId = byte.MaxValue) => SendMessage(GetTimer(), PlayerId);
        public static string GetTimer()
        {
            var sb = new StringBuilder();
            float timerValue = GameStartManagerPatch.GetTimer();
            int minutes = (int)timerValue / 60;
            int seconds = (int)timerValue % 60;
            return $"{minutes:00}:{seconds:00}";
        }
        public static void ShowHelp(byte to = 255)
        {
            var tpinfo = "";
            var text = "";
            var targetPlayer = PlayerCatch.AllPlayerControls.FirstOrDefault(player => player.PlayerId == to);
            var isModerator = Moderator.IsModerator(targetPlayer);

            // ★ 禁止コマンドはヘルプに表示しない
            bool cmdRestricted = Options.OptionCommandSetting?.GetBool() ?? false;
            bool Show(OptionItem opt) => !cmdRestricted || !(opt?.GetBool() ?? false);

            if (GameStates.IsLobby)
            {
                // tp コマンド
                if (Show(Options.OptionCommandTp))
                {
                    tpinfo += $"\n/tp o - {GetString("Command.tpo")}";
                    tpinfo += $"\n/tp i - {GetString("Command.tpi")}";
                    tpinfo += $"\n/allplayertp(apt) - {GetString("Command.apt")}";
                }
            }

            text = GetString("CommandList")
                + "<size=60%><line-height=1.3pic>";

            if (to == 0)
            {
                //ホスト限定（制限対象外）
                text += $"<size=80%></line-height>\n<#8cffff>【~~~~~~~{GetString("OnlyHost")}~~~~~~~】</color></size><line-height=1.3pic>"
                    + $"\n/rename(r) - {GetString("Command.rename")}"
                    + $"\n/cdis - {GetString("Command.dis")}"
                    + $"\n/sw - {GetString("Command.sw")}"
                    + $"\n/forceend(fe) - {GetString("Command.forceend")}"
                    + $"\n/mw - {GetString("Command.mw")}"
                    + $"\n/kf - {GetString("Command.kf")}"
                    + $"\n/addwhite(aw) - {GetString("Command.addwhite")}"
                    + $"\n/set rule(s r) - {GetString("Command.set_rule")}"
                    + $"\n/mod id|name|color - {GetString("Command.modadd")}"
                    + $"\n/mod delete id|name|color - {GetString("Command.moddel")}"
                    + $"\n/gc - {GetString("Command.globalconnect")}";

                if (Options.OptionStreamerSetting.GetBool())
                {
                    text += $"\n/exempt(ex) - {GetString("Command.exempt")}"
                        + $"\n/exempt(ex) id|name|color - {GetString("Command.exempt")}"
                        + $"\n/exempt(ex) delete id|name|color - {GetString("Command.exempt")}"
                        + $"\n/kp - {GetString("Command.kp")}"
                        + $"\n/aj - {GetString("Command.aj")}";
                }

                text += "\n/ws - マッチメイキングのサブテキストをセットします。";

                text += $"<size=80%></line-height>\n<#028760>【~~~~~~~{GetString("OnlyClient")}~~~~~~~】</color></size><line-height=1.3pic>"
                    + $"\n/dump - {GetString("Command.dump")}";
            }

            //全員
            text += $"<size=80%></line-height>\n<#918877>【~~~~~~~{GetString("Allplayer")}~~~~~~~】</color></size><line-height=1.3pic>";
            if (Show(Options.OptionCommandNow))
                text += $"\n/now(n) - {GetString("Command.now")}";

            if (Show(Options.OptionCommandNowRole))
                text += $"\n/now role(n r) - {GetString("Command.nowrole")}";
            if (Show(Options.OptionCommandNowSet))
                text += $"\n/now set(n s) - {GetString("Command.nowset")}";
            if (Show(Options.OptionCommandNowW))
                text += $"\n/now w(n w) - {GetString("Command.nowwin")}";
            if (Show(Options.OptionCommandHNow))
                text += $"\n/h now(h n) - {GetString("Command.h_now")}";
            if (Show(Options.OptionCommandHRoles))
                text += $"\n/h roles(h r ) {GetString("Command.h_roles")}";
            if (Show(Options.OptionCommandMyrole))
                text += $"\n/myrole(m) - {GetString("Command.m")}";
            if (Show(Options.OptionCommandMeetinginfo))
                text += $"\n/meetinginfo(mi,/day) - {GetString("Command.mi")}";
            if (Show(Options.OptionCommandNumberDNumber))
                text += $"\n/(number)d(number) - {GetString("Command.(number)d(number)")}";
            if (Show(Options.OptionCommand8ball))
                text += $"\n/8ball - {GetString("Command.8ball")}";
            if (Show(Options.OptionCommandPko))
                text += $"\n/pko - {GetString("Command.pko")}";
            if (Show(Options.OptionCommandRename))
                text += $"\n/rename(r) - {GetString("Command.rename")}";
            if (Show(Options.OptionCommandRule))
                text += $"\n/rule(rl) - {GetString("Command.rule")}";

            //条件付きコマンド（制限対象外
            if (ShouldShowGuesserCommand())
                text += $"\n/bt - {GetString("Command.bt")}";
            if (Event.CheckRole(CustomRoles.Amateras) && CustomRoles.Amateras.IsPresent())
                text += $"\n/wi - {GetString("Command.wi")}";
            if (Options.ImpostorHideChat.GetBool())
                text += $"\n/sc - {GetString("Command.impchat")}";
            if (Options.JackalHideChat.GetBool())
                text += $"\n/sc - {GetString("Command.jacchat")}";
            if (Options.LoversHideChat.GetBool())
                text += $"\n/lc - {GetString("Command.LoverChat")}";
            if (Options.CupidHideChat.GetBool())
                text += $"\n/lc - {GetString("Command.CupidChat")}";
            if (Options.ConnectingHideChat.GetBool())
                text += $"\n/cc - {GetString("Command.ConnectingChat")}";
            if (Options.TwinsHideChat.GetBool())
                text += $"\n/tc - {GetString("Command.TwinsChat")}";
            if (Options.OnmyojiHideChat.GetBool())
                text += $"\n/sc - {GetString("Command.OnmyojiChat")}";
            if (Options.PavlovHideChat.GetBool())
                text += $"\n/sc - {GetString("Command.PavlovChat")}";
            if (Options.StandHideChat.GetBool())
                text += $"\n/sc - {GetString("Command.StandChat")}";
            if (Options.FreeterHideChat.GetBool())
                text += $"\n/fc - {GetString("Command.FreeterChat")}";

            //ロビー限定
            if (GameStates.IsLobby)
            {
                if (Show(Options.OptionCommandLastresult))
                    text += $"\n/lastresult(l) - {GetString("Command.lastresult")}";
                if (Show(Options.OptionCommandKilllog))
                    text += $"\n/killlog(kl) - {GetString("Command.killlog")}";
                if (Show(Options.OptionCommandTimer))
                    text += $"\n/timer - {GetString("Command.timer")}";
            }

            //モデレーターコマンド（制限対象外)
            if (isModerator)
            {
                text += $"<size=80%></line-height>\n<#ADE0EE>【~~~~~~~{GetString("ModeratorCommand")}~~~~~~~】</color></size><line-height=1.3pic>"
                    + $"\n/fe - {GetString("Command.fe")}"
                    + $"\n/sw - {GetString("Command.Mkf")}"
                    + $"\n/say - {GetString("Command.Msay")}"
                    + $"\n/start - {GetString("Command.start")}"
                    + $"\n/kf - {GetString("Command.kf")}"
                    + $"\n/mf - {GetString("Command.mf")}"
                    + $"\n/ban - {GetString("Command.ban")}"
                    + $"\n/kick - {GetString("Command.kick")}";
            }

            if (Main.UseYomiage.Value)
                text += $"\n/voice - {GetString("Command.voice")}";

            SendMessage(text + tpinfo, to, checkl: true);
        }

        private static bool ShouldShowGuesserCommand()
        {
            // 試合中は実際の配役・属性と、その配役に有効な付与設定を確認する。
            if (GameStates.IsInGame)
            {
                foreach (var player in PlayerCatch.AllPlayerControls)
                {
                    if (player == null) continue;
                    if (player.Is(CustomRoles.Guesser)
                        || player.Is(CustomRoles.NiceGuesser)
                        || player.Is(CustomRoles.EvilGuesser))
                        return true;

                    var role = player.GetCustomRole();
                    if (role is CustomRoles.Cakeshop or CustomRoles.SantaClaus or CustomRoles.Fortuner)
                        return true;
                    if (RoleAddAddons.GetRoleAddon(role, out var addonOptions, player, subrole: CustomRoles.Guesser)
                        && addonOptions.GiveGuesser.GetBool())
                        return true;
                }

                if (CustomRoles.LastImpostor.IsPresent() && LastImpostor.GiveGuesser.GetBool()) return true;
                if (CustomRoles.LastNeutral.IsPresent() && LastNeutral.GiveGuesser.GetBool()) return true;
                return false;
            }

            // ロビーでは未配役なので、現在のプリセットで出現・付与し得るかを確認する。
            return CustomRoles.Guesser.IsEnable()
                || CustomRoles.NiceGuesser.IsEnable()
                || CustomRoles.EvilGuesser.IsEnable()
                || CustomRolesHelper.CheckGuesser()
                || (CustomRoles.LastImpostor.IsEnable() && LastImpostor.GiveGuesser.GetBool())
                || (CustomRoles.LastNeutral.IsEnable() && LastNeutral.GiveGuesser.GetBool());
        }

        static readonly Regex UnderlineRegex = new(@"<u>(.*?)</u>", RegexOptions.Singleline | RegexOptions.Compiled);
        public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool checkl = true, bool isTowSend = false, bool setsize = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (text.RemoveHtmlTags() == "") return;
            if (title == "") title = $"<{Main.ModColor}>" + GetString($"DefaultSystemMessageTitle") + "</color>";
            if (IsRestriction() && isTowSend && title == "NonTitle") title = "";

            var towsend = "";
            if (checkl && text.Length > 300)
            {
                var sendtext = "";
                var alltext = text.Split("\n");
                (string size, string color, string hi, string b) tag = ("", "", "", "");
                var sizeTags = new Stack<string>();
                var colorTags = new Stack<string>();
                var lineHeightTags = new Stack<string>();
                var boldTags = new Stack<string>();
                var oldtext = text;
                var i = 0;
                for (i = 0; sendtext.Length < 280 || ((sendtext.Split("\n")?.Count() ?? 0) < 10); i++)
                {
                    if ((sendtext.Length + alltext[i].Length > 280 || ((sendtext.Split("\n")?.Count() ?? 0) > 10)) && i > 0)
                    {
                        i--;
                        break;
                    }
                    sendtext += $"{alltext[i]}\n";
                    var tagtex = alltext[i].RemoveText().RemoveDeltext("　").RemoveDeltext(" ").Split("<");
                    foreach (var tagtext in tagtex)
                    {
                        if (tagtext == "") continue;
                        switch (tagtext.Substring(0, 1))
                        {
                            case "s":
                                if (!tagtext.Contains("sub"))
                                {
                                    tag.size = $"<{tagtext.Split(">")[0]}>";
                                    sizeTags.Push(tag.size);
                                }
                                break;
                            case "c":
                            case "#":
                                tag.color = $"<{tagtext.Split(">")[0]}>";
                                colorTags.Push(tag.color);
                                break;
                            case "l":
                                tag.hi = $"<{tagtext.Split(">")[0]}>";
                                lineHeightTags.Push(tag.hi);
                                break;
                            case "b":
                                tag.b = $"<{tagtext.Split(">")[0]}>";
                                boldTags.Push(tag.b);
                                break;
                            case "/":
                                switch (tagtext.Substring(1, 1))
                                {
                                    case "s":
                                        if (!tagtext.Contains("sub"))
                                        {
                                            if (sizeTags.Count > 0) sizeTags.Pop();
                                            tag.size = sizeTags.Count > 0 ? sizeTags.Peek() : "";
                                        }
                                        break;
                                    case "c":
                                        if (colorTags.Count > 0) colorTags.Pop();
                                        tag.color = colorTags.Count > 0 ? colorTags.Peek() : "";
                                        break;
                                    case "l":
                                        if (lineHeightTags.Count > 0) lineHeightTags.Pop();
                                        tag.hi = lineHeightTags.Count > 0 ? lineHeightTags.Peek() : "";
                                        break;
                                    case "b":
                                        if (boldTags.Count > 0) boldTags.Pop();
                                        tag.b = boldTags.Count > 0 ? boldTags.Peek() : "";
                                        break;
                                }
                                break;
                        }
                    }
                }
                var send = "";
                for (var ii = i + 1; ii < alltext.Count(); ii++)
                {
                    send += $"{alltext[ii]}\n";
                }
                if (tag.color == "<#ffffff>") tag.color = "";
                towsend = $"{tag.b}{tag.hi}{tag.color}{tag.size}{send}";
                text = sendtext;
            }
            if (text.RemoveHtmlTags().StartsWith("\n") || text.RemoveHtmlTags().StartsWith("\r"))
            {
                var alltext = text.Split("\n");
                bool first = true;
                text = "";
                alltext.Do(t =>
                {
                    if (first)
                    {
                        text += t.RemoveDeltext("\n").RemoveDeltext("\r");
                        first = false;
                        return;
                    }
                    text += t + "\n";
                });
            }
            var fir = "<align=\"left\">";
            text = text.RemoveDeltext("color=#", "#").RemoveDeltext("FF>", ">");
            title = title.RemoveDeltext("color=#", "#").RemoveDeltext("FF>", ">");
            if (setsize is false) text = $"<size=70%>{(IsRestriction() ? "<#ffffff>" : "")}{text}";
            if (IsRestriction())
            {
                var sendtext = text;
                if (sendtext.RemoveHtmlTags().RemoveDeltext("\n") != "")
                {
                    if (!VersionInfoManager.GetCustomFlag(1))
                        sendtext = UnderlineRegex.Replace(text, m => $"<u><line-height=1.5em>{m.Groups[1].Value}</line-height></u>");
                    if (isTowSend && title == "") Main.MessagesToSend.Add(($" ", sendTo, $"{fir}{sendtext}"));
                    else Main.MessagesToSend.Add(($" ", sendTo, $"{fir}{title}\n{sendtext}"));
                }
            }
            else
                Main.MessagesToSend.Add(($"{fir}{text}", sendTo, $"{fir}{title}"));
            if (towsend is not "")
            {
                SendMessage(towsend, sendTo, IsRestriction() ? "NonTitle" : title, true, isTowSend: true);
            }
        }

        /// <summary> ホストがロビーでチャットする時に使用します </summary>
        public static void SendChat(string text)
        {
            if (GameStates.InGame) return;
            var name = Main.nickName == string.Empty ? DataManager.player.Customization.Name : Main.nickName;
            Main.MessagesToSend.Add((text, byte.MaxValue, name));
        }
        public static void SetChatVisible(this PlayerControl pc, bool visible = true)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (pc.AmOwner)
            {
                // ホスト自身（ローカル）の場合は直接UIをアクティブにする
                var hud = DestroyableSingleton<HudManager>.Instance;
                if (hud != null && hud.Chat != null)
                {
                    hud.Chat.SetVisible(visible);
                    hud.Chat.HideBanButton();
                }
                return;
            }

            // --- ここからEHRのバニラ向けUI偽装パケット送信 ---
            bool dead = pc.Data.IsDead;
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(pc.OwnerId);
            writer.StartMessage(4);
            writer.WritePacked(HudManager.Instance.MeetingPrefab.SpawnId);
            writer.WritePacked(-2);
            writer.Write((byte)SpawnFlags.None);
            writer.WritePacked(1);
            uint netIdCnt = AmongUsClient.Instance.NetIdCnt;
            AmongUsClient.Instance.NetIdCnt = netIdCnt + 1U;
            writer.WritePacked(netIdCnt);
            writer.StartMessage(1);
            writer.WritePacked(0);
            writer.EndMessage();
            writer.EndMessage();

            // 一瞬だけプレイヤーを死んでいる（チャット可能）と偽装して同期
            pc.Data.IsDead = visible;
            writer.StartMessage(1);
            writer.WritePacked(pc.Data.NetId);
            pc.Data.Serialize(writer, true);
            writer.EndMessage();

            // 偽の会議終了パケットを送り、UIを更新させる
            writer.StartMessage(2);
            writer.WritePacked(netIdCnt);
            writer.Write((byte)RpcCalls.CloseMeeting);
            writer.EndMessage();

            // 元の生死状態に戻して同期
            pc.Data.IsDead = dead;
            writer.StartMessage(1);
            writer.WritePacked(pc.Data.NetId);
            pc.Data.Serialize(writer, true);
            writer.EndMessage();

            writer.StartMessage(5);
            writer.WritePacked(netIdCnt);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();

            AmongUsClient.Instance.timer -= AmongUsClient.Instance.MinSendInterval;
        }

        /// <summary>
        /// 生存者全員のチャットを強制解放する
        /// </summary>
        public static void SetChatVisibleForAll()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                pc.SetChatVisible(true);
            }
        }

        /// <param name="pc">seer</param>
        /// <param name="force">強制かつ全員に送信</param>
        public static void ApplySuffix(PlayerControl pc, bool force = false, bool countdown = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (GameStates.IsOutro) return;
            if (GameStates.IsFreePlay) return;
            if (PlayerControl.LocalPlayer == null) return;

            var Iscountdown = countdown || GameStates.IsCountDown;
            string name = DataManager.player.Customization.Name;
            if (Main.nickName != "") name = Main.nickName;
            string n = name;
            bool RpcTimer = false;
            if (AmongUsClient.Instance.IsGameStarted)
            {
                if (!Camouflage.PlayerSkins.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var color)) return;

                if (Options.ColorNameMode.GetBool() && Main.nickName == "") name = Palette.GetColorName(color.ColorId);
            }
            else if (GameStates.IsLobby)
            {
                if (!Iscountdown)
                {
                    switch (Options.GetSuffixMode())
                    {
                        case SuffixModes.None:
                            break;
                        case SuffixModes.TOH:
                            name += $"<size=75%>(<{Main.ModColor}>TOH-P v{Main.PluginShowVersion})</color></size>";
                            break;
                        case SuffixModes.Streaming:
                            name += $"<size=75%>(<{Main.ModColor}>{GetString("SuffixMode.Streaming")})</color></size>";
                            break;
                        case SuffixModes.Recording:
                            name += $"<size=75%>(<{Main.ModColor}>{GetString("SuffixMode.Recording")})</color></size>";
                            break;
                        case SuffixModes.RoomHost:
                            name += $"<size=75%>(<{Main.ModColor}>{GetString("SuffixMode.RoomHost")})</color></size>";
                            break;
                        case SuffixModes.OriginalName:
                            name += $"<size=75%>(<{Main.ModColor}>{DataManager.player.Customization.Name})</color></size>";
                            break;
                        case SuffixModes.Timer:
                            if (GameStates.IsLocalGame
                            || Iscountdown) break;
                            float timerValue = GameStartManagerPatch.GetTimer();
                            if (timerValue < GameStartManagerPatch.Timer2 - 2 || GameStartManagerPatch.Timer2 < 25)
                                GameStartManagerPatch.Timer2 = timerValue;
                            timerValue = GameStartManagerPatch.Timer2;
                            int minutes = (int)timerValue / 60;
                            int seconds = (int)timerValue % 60;
                            string Color = "<#00ffff>";
                            if (minutes <= 4) Color = "<#9acd32>";//5分切ったら
                            if (minutes <= 2) Color = "<#ffa500>";//3分切ったら。
                            if (minutes <= 0) Color = "<#ff1919>";//1分切ったら。
                            name += $"<size=75%>({Color}{minutes:00}:{seconds:00}</color>)</size>";
                            RpcTimer = true;
                            break;
                    }
                }
            }
            //Dataのほう変えるのはなぁっておもいました。うん。
            if ((name != PlayerControl.LocalPlayer.name || countdown) && !PlayerControl.LocalPlayer.name.Contains("マーリン") && !PlayerControl.LocalPlayer.name.Contains("どちらも") && !RpcTimer && PlayerControl.LocalPlayer.CurrentOutfitType == PlayerOutfitType.Default)
            {
                if (0 < Main.MessagesToSend.Count)
                {
                    PlayerControl.LocalPlayer.RpcSetName(name);
                    if (!Iscountdown && GameStates.IsLobby) _ = new LateTask(() => ApplySuffix(null, force: true), 0.2f, "LobySetName", null);
                }
            }

            if (GameStates.IsLobby && !Iscountdown && (force || (pc.name != "Player(Clone)" && pc.PlayerId != PlayerControl.LocalPlayer.PlayerId && !pc.IsModClient())))
            {
                if (AmongUsClient.Instance.IsGameStarted) return;
                var sb = new StringBuilder();

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.StandardHAS: sb.Append($"\r\n").Append(ColorString(Color.yellow, GetString("StandardHAS"))); break;
                    case CustomGameMode.HideAndSeek: sb.Append($"\r\n").Append(ColorString(Color.red, GetString("HideAndSeek"))); break;
                    case CustomGameMode.TaskBattle: sb.Append($"\r\n").Append(ColorString(Color.cyan, GetString("TaskBattle"))); break;
                    case CustomGameMode.SuddenDeath: sb.Append("\r\n").Append(ColorString(GetRoleColor(CustomRoles.Comebacker), GetString("SuddenDeathMode"))); break;
                    case CustomGameMode.MurderMystery: sb.Append("\r\n").Append($"<#1a389c>{GetString("MurderMystery")}"); break;
                }
                n = "<line-height=-100%>\n<b></line-height>" + name + $"\n<line-height=-{(sb.Length is 0 ? "200" : "300")}%>\n<size=60%><line-height=0%><{Main.ModColor}>TownOfHost-Pko <#ffffff>v{Main.PluginShowVersion}<line-height=100%>{sb.ToString()}</size>\n<size=0> ";
                if (force)
                    PlayerCatch.AllPlayerControls.DoIf(x => x.name != "Player(Clone)" && x.PlayerId != PlayerControl.LocalPlayer.PlayerId && !x.IsModClient(), x => PlayerControl.LocalPlayer.RpcSetNamePrivate(n, true, x, true));
                else if (pc.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                    PlayerControl.LocalPlayer.RpcSetNamePrivate(n, true, pc);
            }
        }
        public static void SendGuardDate(byte playerId = byte.MaxValue)
        {
            StringBuilder sb = new();

            sb.Append($"{GetString("GuardData")}\n<size=60%>{GetString("GuardDataInfo")}");
            sb.Append($"<size=80%>{string.Format(GetString("GuardPower"), 0)}</size>\n{GetString("Normal")}\n");
            sb.Append($"<size=80%>{string.Format(GetString("KillPower"), 1)}</size>\n");

            var i = 0;
            bool Ishedder = false;
            CustomRoles[] killLv1 = [CustomRoles.UltraStar, CustomRoles.NekoKabocha , CustomRoles.Puppeteer , CustomRoles.Sniper, CustomRoles.TeleportKiller
            ,CustomRoles.Bomber , CustomRoles.Vampire,CustomRoles.Remotekiller];
            CustomRoles[] GuardLv1 = [CustomRoles.GuardMaster, CustomRoles.Guarding, CustomRoles.OneWolf, CustomRoles.VentOpener];
            CustomRoles[] killLv2 = [CustomRoles.Ballooner, CustomRoles.FireWorks, CustomRoles.Warlock, CustomRoles.GrimReaper];
            CustomRoles[] GuardLv2 = [CustomRoles.Fox, CustomRoles.MadGuardian];
            CustomRoles[] KillLv3 = [CustomRoles.Jumper];
            CustomRoles[] GuardLv9 = [CustomRoles.King, CustomRoles.Autocrat];
            CustomRoles[] KillLv10 = [CustomRoles.ConnectSaver, CustomRoles.Shyboy, CustomRoles.Limiter, CustomRoles.EarnestWolf, CustomRoles.CurseMaker];

            sb.Append($"{GetString("DeathReason.Kill")}{(i is 3 ? "\n" : "　")}");
            i = i + 1 % 4;
            foreach (var role in killLv1)
            {
                string infotext = "";
                if (UltraStar.OptionCheckKill.GetBool() && role is CustomRoles.UltraStar)
                {
                    killLv2.AddItem(role);
                    continue;
                }
                if (role is CustomRoles.NekoKabocha) infotext = $"({GetString("DeathReason.Revenge")})";
                if (role is CustomRoles.Sniper) infotext = $"({GetString("DeathReason.Sniped")})";
                if (role is CustomRoles.TeleportKiller) infotext = $"({GetString("DeathReason.TeleportKill")})";
                if (role is CustomRoles.Bomber) infotext = $"({GetString("DeathReason.Bombed")})";
                if (role.IsEnable())
                {
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in GuardLv1)
            {
                string infotext = "";
                if (role is CustomRoles.VentOpener && !VentOpener.OptionBlockKill.GetBool()) continue;
                if (role is CustomRoles.VentOpener) infotext = $"({GetString("DeathReason.Revenge")})";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("GuardPower"), 1)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in killLv2)
            {
                string infotext = "";
                if (role is CustomRoles.Bomber or CustomRoles.FireWorks) infotext = $"({GetString("DeathReason.Bombed")})";
                if (role is CustomRoles.GrimReaper) infotext = $"({GetString("DeathReason.Grim")})";
                if (role is CustomRoles.Warlock) infotext = $"({GetString("DeathReason.Spell")})";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("KillPower"), 2)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in GuardLv2)
            {
                string infotext = "";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("GuardPower"), 2)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in KillLv3)
            {
                string infotext = "";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("KillPower"), 3)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in GuardLv9)
            {
                string infotext = "";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("GuardPower"), 9)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            i = 0;
            Ishedder = true;
            foreach (var role in KillLv10)
            {
                string infotext = "";
                //if (role is CustomRoles.Shyboy && Shyboy.OptionShyDieBom.GetBool() is false) continue;
                if (role is CustomRoles.Shyboy or CustomRoles.Limiter) infotext = $"({GetString("DeathReason.Bombed")})";
                if (role.IsEnable())
                {
                    if (Ishedder)
                    {
                        sb.Append($"\n<size=80%>{string.Format(GetString("KillPower"), 10)}</size>\n");
                        Ishedder = false;
                    }
                    sb.Append($"{GetRoleColorAndtext(role)}{infotext}{(i is 3 ? "\n" : "　")}");
                    i = i + 1 % 4;
                }
            }
            SendMessage(sb.ToString(), playerId, checkl: true);
        }
        #region AfterMeetingTasks
        public static bool CantUseVent;
        public static List<byte> RoleSendList = new();
        public static void AfterMeetingTasks()
        {
            MovingPlatformBehaviourPatch.SetPlatfrom();
            GameStates.CalledMeeting = false;
            if (DummyHunter.IsThisMode) DummyHunter.AfterMeeting();
            //天秤会議だと送らない
            if (Balancer.Id == 255 && Balancer.target1 != 255 && Balancer.target2 != 255 && (!Options.firstturnmeeting || !MeetingStates.First))
            {
                foreach (var roleClass in CustomRoleManager.AllActiveRoles.Values)
                {
                    if (roleClass is Balancer balancer) balancer.BalancerAfterMeetingTasks();
                }

                // さつまといもの処理だよ！
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    var roleClass = pc.GetRoleClass();
                    if (roleClass == null) continue;
                    if (!pc.Is(CustomRoles.SatsumatoImoC) && !pc.Is(CustomRoles.SatsumatoImoM)) continue;
                    roleClass.AfterMeetingTasks();
                    roleClass.ChangeColor();
                }

                GameStates.ExiledAnimate = false;
            }
            else
            {
                if (!Options.firstturnmeeting || !MeetingStates.First)
                {
                    Amanojaku.Assign();
                    if (Amnesia.OptionCanRealizeDay.GetBool() && Amnesia.OptionRealizeDayCount.GetInt() <= UtilsGameLog.day)
                    {
                        foreach (var pc in PlayerCatch.AllPlayerControls)
                        {
                            if (pc.Is(CustomRoles.Amnesia))
                            {
                                if (!RoleSendList.Contains(pc.PlayerId)) RoleSendList.Add(pc.PlayerId);
                                Amnesia.RemoveAmnesia(pc.PlayerId);
                            }
                        }
                    }
                }
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    var roleClass = pc.GetRoleClass();
                    if (!Options.firstturnmeeting || !MeetingStates.First) roleClass?.AfterMeetingTasks();
                    pc.GetRoleClass()?.ChangeColor();//会議後、役職変更されてものやつ。
                }
                if (!Options.firstturnmeeting || !MeetingStates.First)
                {
                    if (AsistingAngel.CanSetAsistTarget())
                    {
                        AsistingAngel.Limit++;
                    }

                    UtilsGameLog.day++;
                    UtilsGameLog.AddGameLogsub("\n" + string.Format(GetString("Message.Day").RemoveDeltext("【").RemoveDeltext("】"), UtilsGameLog.day).Color(Palette.Orange));
                }
            }

            if (AmongUsClient.Instance.AmHost)
            {
                if (Options.AirShipVariableElectrical.GetBool()) AirShipElectricalDoors.Initialize();
                DoorsReset.ResetDoors();
                // 空デデンバグ対応 会議後にベントを空にする
                var ventilationSystem = ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType) ? systemType.TryCast<VentilationSystem>() : null;
                if (ventilationSystem != null)
                {
                    ventilationSystem.PlayersInsideVents.Clear();
                    ventilationSystem.IsDirty = true;
                }
                GuessManager.Reset();//会議後にリセット入れる
                GameStates.ExiledAnimate = false;

                // 画面切り替えの暗転が終わる頃にチャットを強制表示 (オプションがオンの場合のみ)
                if (Options.OptionGameChatSetting.GetBool())
                {
                    _ = new LateTask(() => SetChatVisibleForAll(), 0.5f, "ShowChatAfterMeeting");
                }
            }
        }
        #endregion
        public static void ChangeInt(ref int ChangeTo, int input, int max)
        {
            var tmp = ChangeTo * 10;
            tmp += input;
            ChangeTo = Math.Clamp(tmp, 0, max);
        }
        public static string PadRightV2(this object text, int num)
        {
            int bc = 0;
            var t = text.ToString();
            foreach (char c in t) bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
            return t?.PadRight(Mathf.Max(num - (bc - t.Length), 0));
        }
        #region Remove
        public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", "");
        public static string RemoveColorTags(this string str)
        {
            var removecolor = Regex.Replace(str, "</?color(=#[0-9a-fA-F]*)?>", "");
            removecolor = Regex.Replace(removecolor, "<#[^>]*?>", "");
            return removecolor;
        }
        public static string RemoveSizeTags(this string str) => Regex.Replace(str, "</?size[^>]*?>", "");
        public static string RemoveGiveAddon(this string str) => Regex.Replace(str, "を付与する", "");
        public static string RemoveSN(this string str) => Regex.Replace(str, "\n", "");
        public static string Changebr(this string str, bool nokosu) => Regex.Replace(str, "\n", $"{(nokosu ? "<br>\n" : "<br>")}");
        public static string RemoveaAlign(this string str) => Regex.Replace(str, "align", "");
        public static string RemoveDeltext(this string str, string del, string set = "") => Regex.Replace(str, del, set);
        public static string RemoveText(this string str, bool Update = false)
        {
            bool Skip = false;
            string returns = "";
            if (Update)
            {
                for (var i = 0; i < str.Length; i++)
                {
                    string text = "";
                    text = str.Substring(i, 1);
                    if (text == Regex.Replace(text, "[0-9]", "")) continue;
                    returns += text;
                }
                return returns;
            }
            for (var i = 0; i < str.Length; i++)
            {
                string text = "";
                text = str.Substring(i, 1);

                {
                    if (text == "<")
                        Skip = true;
                    if (text == ">")
                    {
                        returns += ">";
                        Skip = false;
                    }

                    if (Skip)
                        returns += text;
                    else
                        if (text != ">")
                        {
                            if (text == "\n")
                                returns += "\n ";
                            else if (text == "\r")
                                returns += "\r";
                            else
                                returns += " ";
                        }
                }
            }
            return returns;
        }
        static string[] Systemmark =
        ["★", "◎", "【=", "▽", "Φ", "♥", "∈", "Ψ"];
        public static bool IsSystemMessage(this string text)
        {
            if (text.RemoveHtmlTags() != text) return true;
            /*
            foreach (var mark in Systemmark)
            {
                if (text.Contains(mark)) return true;
            }*/
            return false;
        }
        #endregion
        public static void FlashColor(Color color, float duration = 1f)
        {
            var hud = DestroyableSingleton<HudManager>.Instance;
            if (hud.FullScreen == null) return;
            var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
            if (obj == null)
            {
                obj = GameObject.Instantiate(hud.FullScreen.gameObject, hud.transform);
                obj.name = "FlashColor_FullScreen";
            }
            hud.StartCoroutine(Effects.Lerp(duration, new Action<float>((t) =>
            {
                obj.SetActive(t != 1f);
                obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a)); //アルファ値を0→目標→0に変化させる
            })));
        }

        public static string ColorString(Color32 color, string str) => $"<#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";
        /// <summary>
        /// Darkness:１の比率で黒色と元の色を混ぜる。マイナスだと白色と混ぜる。
        /// </summary>
        public static Color ShadeColor(this Color color, float Darkness = 0)
        {
            bool IsDarker = Darkness >= 0; //黒と混ぜる
            if (!IsDarker) Darkness = -Darkness;
            float Weight = IsDarker ? 0 : Darkness; //黒/白の比率
            float R = (color.r + Weight) / (Darkness + 1);
            float G = (color.g + Weight) / (Darkness + 1);
            float B = (color.b + Weight) / (Darkness + 1);
            return new Color(R, G, B, color.a);
        }

        /// <summary>
        /// 乱数の簡易的なヒストグラムを取得する関数
        /// <params name="nums">生成した乱数を格納したint配列</params>
        /// <params name="scale">ヒストグラムの倍率 大量の乱数を扱う場合、この値を下げることをお勧めします。</params>
        /// </summary>
        public static string WriteRandomHistgram(int[] nums, float scale = 1.0f)
        {
            int[] countData = new int[nums.Max() + 1];
            foreach (var num in nums)
            {
                if (0 <= num) countData[num]++;
            }
            StringBuilder sb = new();
            for (int i = 0; i < countData.Length; i++)
            {
                // 倍率適用
                countData[i] = (int)(countData[i] * scale);

                // 行タイトル
                sb.AppendFormat("{0:D2}", i).Append(" : ");

                // ヒストグラム部分
                for (int j = 0; j < countData[i]; j++)
                    sb.Append('|');

                // 改行
                sb.Append('\n');
            }

            // その他の情報
            sb.Append("最大数 - 最小数: ").Append(countData.Max() - countData.Min());

            return sb.ToString();
        }

        public static bool TryCast<T>(this Il2CppObjectBase obj, out T casted)
        where T : Il2CppObjectBase
        {
            casted = obj.TryCast<T>();
            return casted != null;
        }
        public static float Round(this float value, float digit)
        {
            var roundvalue = Mathf.Round(value / digit);
            if ((roundvalue * digit).ToString().Contains("."))
            {
                var roundvaluestring = $"{roundvalue * digit}";
                string[] ages = roundvaluestring.Split(".");
                if (ages[1].Count() > digit.ToString().RemoveDeltext("0.", ".").Count())
                {
                    var ages1 = ages[1].ToString().Substring(0, digit.ToString().RemoveDeltext("0.", ".").Count());

                    return float.TryParse(ages[0] + "." + ages1, out var result) ? result : roundvalue * digit;
                }
            }
            return roundvalue * digit;
        }
        public const string AdditionalWinnerMark = "<#dddd00>★</color>";
        public const string AdditionalAliveWinnerMark = "<#349121>★</color>";

        public static void SyncAllSettings()
        {
            // 設定を同期するための処理をここに記述
            // 例: 各プレイヤーの設定をサーバーと同期する
            Logger.Info("Syncing all settings...", "Utils");
            // 実際の同期処理をここに実装
        }
        /// <summary>
        /// バニラサーバーの処理を行うか
        /// </summary>
        /// <returns></returns>
        public static bool IsRestriction()
        {
            //デバッグならカスタム・ローカル問わずバニラと同処理。
            if (DebugModeManager.AmDebugger && DebugModeManager.EnableTOHPDebugMode.GetBool()) return true;
            //カスタムサーバー か ローカルゲームならfalseを返す
            if (Main.IsCs() || GameStates.IsLocalGame) return false;

            return true;
        }
        public static Dictionary<int, ICollection<(byte sentto, string title, string text)>> meetingsendhis = new();
        [GameModuleInitializer]
        public static void Init()
        {
            meetingsendhis = new();
            GameDataSerializePatch.DontTouch = false;
            Camouflage.ventplayr.Clear();
            PlayerCatch.OldAlivePlayerControles.Clear();
            ReportDeadBodyPatch.DontReport.Clear();
            RandomSpawn.SpawnMap.NextSporn.Clear();
            RandomSpawn.SpawnMap.NextSpornName.Clear();
            Patches.ISystemType.VentilationSystemUpdateSystemPatch.NowVentId.Clear();
            CoEnterVentPatch.VentPlayers.Clear();
            MeetingHudPatch.Oniku = "";
            MeetingHudPatch.Send = "";
            MeetingHudPatch.Title = "";
            MeetingVoteManager.Voteresult = "";
            IUsePhantomButton.IPPlayerKillCooldown.Clear();
            CustomButtonHud.CantJikakuIsPresent = null;
            RoleSendList.Clear();
            UtilsNotifyRoles.ExtendedMeetingText = "";
            Roles.Madmate.MadAvenger.Skill = false;
            Roles.Neutral.JackalDoll.NowSideKickCount = 0;
            Balancer.Id = 255;
            Main.DisableTaskPlayerList = new();
            Stolener.Killers.Clear();
            Options.firstturnmeeting = Options.FirstTurnMeeting.GetBool() && Options.CurrentGameMode is CustomGameMode.Standard;
            CoEnterVentPatch.OldOnEnterVent = new();

            if (Options.CantUseVentMode.GetBool() && (Options.CantUseVentTrueCount.GetFloat() >= PlayerCatch.AllAlivePlayerControls.Count())) CantUseVent = true;
            else CantUseVent = false;
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
        public static class IntroCutsceneOnDestroyPatchForChat
        {
            public static void Postfix()
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    if (Options.OptionGameChatSetting.GetBool())
                    {
                        _ = new LateTask(() => Utils.SetChatVisibleForAll(), 0.5f, "ShowChatOnGameStart");
                    }
                }
            }
        }
    }
}
