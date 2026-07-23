using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.AddOns.Neutral;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Ghost;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
    class GameEndChecker
    {
        private static GameEndPredicate predicate;
        public static bool Prefix()
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            if (predicate == null)
            {
                Logger.Warn("ゲーム終了判定が未初期化のため、現在のゲームモードに合わせて初期化します", "GameEndChecker");
                EnsurePredicate();
                return false;
            }

            if (Main.DontGameSet && CustomWinnerHolder.WinnerTeam != CustomWinner.Draw) return false;

            if (CustomSpawnEditor.ActiveEditMode) return false;

            //後追い処理等が終わってないなら中断
            if (predicate is NormalGameEndPredicate && 0 < Main.AfterMeetingDeathPlayers.Count)
            {
                if (3 < GameStates.turntimer && GameStates.task)
                {
                    try
                    {
                        Main.AfterMeetingDeathPlayers.Do(x =>
                        {
                            var player = PlayerCatch.GetPlayerById(x.Key);
                            var roleClass = CustomRoleManager.GetByPlayerId(x.Key);
                            var requireResetCam = player?.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true;
                            var state = PlayerState.GetByPlayerId(x.Key);
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}を{x.Value}で死亡させました", "GameEndChecker");
                            state.DeathReason = x.Value;
                            player?.RpcExileV3();
                            state.SetDead();
                            if (x.Value == CustomDeathReason.Suicide)
                                player?.SetRealKiller(player, true);
                            if (requireResetCam)
                                player?.ResetPlayerCam(1f);
                            if (roleClass is Executioner executioner && executioner.TargetId == x.Key)
                                Executioner.ChangeRoleByTarget(x.Key);
                        });
                        Main.AfterMeetingDeathPlayers.Clear();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"{ex}", "GameEndDeath");
                    }
                }
                else return false;
            }
            //廃村用に初期値を設定
            var reason = GameOverReason.ImpostorsByKill;

            predicate.CheckForEndGame(out reason);
            var isSabotageEnd = reason == GameOverReason.ImpostorsBySabotage;
            var lockSabotageWinner = isSabotageEnd
                && CustomWinnerHolder.WinnerTeam is not CustomWinner.Default and not CustomWinner.None;
            var lockDrawWinner = CustomWinnerHolder.WinnerTeam == CustomWinner.Draw;
            var lockWinner = lockSabotageWinner || lockDrawWinner;

            if (!lockWinner)
            {
                Zombie.TryTakeOverCrewWin(ref reason);
                Onmyoji.TryTakeOverCrewWin(ref reason);
                BatGirl.TryTakeOverSoloWin(ref reason);
            }

            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Default)
            {
                PlayerCatch.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

                if (Options.CurrentGameMode != CustomGameMode.Standard || !SuddenDeathMode.NowSuddenDeathMode)
                    switch (CustomWinnerHolder.WinnerTeam)
                    {
                        case CustomWinner.Crewmate:
                            PlayerCatch.AllPlayerControls
                                .Where(pc => pc.Is(CustomRoleTypes.Crewmate) && !pc.GetCustomRole().IsLovers()
                                && !pc.Is(CustomRoles.Amanojaku) && !pc.Is(CustomRoles.Jackaldoll) && !pc.Is(CustomRoles.SKMadmate) && !pc.Is(CustomRoles.Tama)
                                && MagicalGirl.CanWinAsCrewmateStaff(pc))
                                .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                            //if (Monochromer.CheckWin(reason)) break;
                            foreach (var pc in PlayerCatch.AllPlayerControls)
                            {
                                if (pc.GetCustomRole() is CustomRoles.SKMadmate or CustomRoles.Jackaldoll ||
                                    pc.IsLovers())
                                    CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
                            }
                            break;
                        case CustomWinner.Impostor:
                            PlayerCatch.AllPlayerControls
                                .Where(pc => (pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoleTypes.Madmate) || pc.Is(CustomRoles.SKMadmate)) && (!pc.GetCustomRole().IsLovers() || !pc.Is(CustomRoles.Jackaldoll) || !pc.Is(CustomRoles.Tama)))
                                .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                            if (!lockWinner && Egoist.CheckWin()) break;
                            if (!lockWinner)
                            {
                                foreach (var pc in PlayerCatch.AllPlayerControls)
                                {
                                    if (pc.GetCustomRole() is CustomRoles.Jackaldoll ||
                                        pc.IsLovers())
                                        CustomWinnerHolder.CantWinPlayerIds.Add(pc.PlayerId);
                                }
                            }
                            break;
                        default:
                            //ラバー勝利以外の時にラバーをしめt...勝利を剥奪する処理。
                            //どーせ追加なら追加勝利するやろし乗っ取りなら乗っ取りやし。
                            if (lockWinner || CustomWinnerHolder.WinnerTeam.IsLovers())
                                break;
                            PlayerCatch.AllPlayerControls
                                .Where(p => p.IsLovers())
                                .Do(p => CustomWinnerHolder.CantWinPlayerIds.Add(p.PlayerId));
                            break;
                    }

                // 勝者固定経路でも、乗っ取りではない個人勝利条件は必ず適用する。
                foreach (var beginner in CustomRoleManager.AllActiveRoles.Values.OfType<BeginnerImpostor>())
                    beginner.EnforceDummyKillWinRequirement();

                // 勝者固定経路でも、加虐者・被虐者の個別条件を必ず反映する。
                foreach (var abuser in CustomRoleManager.AllActiveRoles.Values.OfType<Abuser>())
                    abuser.EnforceWinRequirement();
                foreach (var victim in CustomRoleManager.AllActiveRoles.Values.OfType<Victim>())
                    victim.EnforceFactionWin();

                if (!lockWinner && SuddenDeathMode.NowSuddenDeathTemeMode && !(CustomWinnerHolder.WinnerTeam is CustomWinner.SuddenDeathRed or CustomWinner.SuddenDeathBlue or CustomWinner.SuddenDeathGreen or CustomWinner.SuddenDeathYellow or CustomWinner.PurpleLovers))
                {
                    SuddenDeathMode.TeamAllWin();
                }
                if (!lockWinner && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None)
                {
                    if (!reason.Equals(GameOverReason.CrewmatesByTask))
                    {
                        Lovers.LoversSoloWin(ref reason);
                    }
                    if (reason.Equals(GameOverReason.CrewmatesByTask))
                    {
                        PlayerCatch.AllPlayerControls
                            .Where(pc => pc.IsLovers())
                            .Do(lover => CustomWinnerHolder.CantWinPlayerIds.Add(lover.PlayerId));
                    }
                    Lovers.LoversAddWin();

                    foreach (var pc in PlayerCatch.AllPlayerControls.Where(pc => !CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) || pc.GetCustomRole() is CustomRoles.Turncoat or CustomRoles.AllArounder))
                    {
                        if (!pc.IsLovers() && !pc.Is(CustomRoles.Amanojaku))
                        {
                            if (pc.GetRoleClass() is IAdditionalWinner additionalWinner)
                            {
                                var winnerRole = pc.GetCustomRole();
                                if (additionalWinner.CheckWin(ref winnerRole))
                                {
                                    Logger.Info($"{pc.Data.GetLogPlayerName()}:{winnerRole}での追加勝利", "AdditinalWinner");
                                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                                    CustomWinnerHolder.AdditionalWinnerRoles.Add(winnerRole);
                                    continue;
                                }
                            }
                        }
                        LastNeutral.CheckAddWin(pc, reason);
                        Amanojaku.CheckWin(pc, reason);
                    }
                }
                if (!lockWinner && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw)
                {
                    CurseMaker.CheckWin();
                    Fox.SFoxCheckWin(ref reason);
                    Tuna.CheckWin(ref reason);
                    Spelunker.CheckWin(ref reason);
                    Chatter.CheckWin(ref reason);
                    Zombie.TryTakeOverCrewWin(ref reason);

                    // ★ 神の勝利チェック
                    foreach (var pc in PlayerCatch.AllAlivePlayerControls.Where(p => p.Is(CustomRoles.God)))
                    {
                        if (TownOfHost.Roles.Neutral.God.RequireTasksToWinOpt?.GetBool() == true)
                        {
                            var taskState = PlayerState.GetByPlayerId(pc.PlayerId)?.GetTaskState();
                            if (taskState == null) continue;
                            if (!UtilsTask.HasTasks(pc.Data, false)) continue;

                            var required = TownOfHost.Roles.Neutral.God.TaskCountOpt?.GetInt() ?? 0;
                            if (required > 0)
                            {
                                if (taskState.CompletedTasksCount < required) continue;
                            }
                            else
                            {
                                if (taskState.AllTasksCount <= 0) continue;
                                if (!taskState.IsTaskFinished) continue;
                            }
                        }

                        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.God, byte.MaxValue))
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.CantWinPlayerIds.Remove(pc.PlayerId);
                            reason = GameOverReason.ImpostorsByKill;
                        }
                    }
                }
                if (!lockWinner)
                {
                    AsistingAngel.CheckAddWin();
                    foreach (var phantomthiefplayer in PlayerCatch.AllAlivePlayerControls.Where(pc => pc.GetCustomRole() is CustomRoles.PhantomThief))
                    {
                        if (phantomthiefplayer.GetRoleClass() is PhantomThief phantomThief)
                        {
                            phantomThief.CheckWin();
                        }
                    }
                    foreach (var player in PlayerCatch.AllPlayerControls)
                    {
                        var roleclass = player.GetRoleClass();
                        roleclass?.CheckWinner(reason);
                    }
                    Twins.CheckAddWin();
                    Triplets.CheckAddWin();
                    Faction.CheckWin();
                }

                if (lockDrawWinner)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                }

                Ruler.ApplyGameEndRules();

                if (isSabotageEnd && Options.OptionSabotageFinAllKill.GetBool())
                {
                    PlayerCatch.AllAlivePlayerControls
                        .Where(player => !player.IsWinner())
                        .Do(player =>
                        {
                            player.GetPlayerState().DeathReason = CustomDeathReason.Kill;
                            player.RpcMurderPlayer(player);
                        });
                }

                ShipStatus.Instance.enabled = false;
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate && (reason.Equals(GameOverReason.CrewmatesByTask) || reason.Equals(GameOverReason.CrewmatesByVote)))
                    reason = GameOverReason.ImpostorsByKill;

                Logger.Info($"{CustomWinnerHolder.WinnerTeam} ({reason})", "Winner");
                CustomWinnerHolder.winners.Do(winner => Logger.Info($"winnerteam:{winner} ({reason})", "Winner"));

                if (Options.OutroCrewWinreasonchenge.GetBool() && (reason.Equals(GameOverReason.CrewmatesByTask) || reason.Equals(GameOverReason.CrewmatesByVote)))
                    reason = GameOverReason.ImpostorsByVote;

                StartEndGame(reason);
                predicate = null;
            }
            return false;
        }
        public static void StartEndGame(GameOverReason reason)
        {
            AmongUsClient.Instance.StartCoroutine(CoEndGame(AmongUsClient.Instance, reason).WrapToIl2Cpp());
        }
        private static IEnumerator CoEndGame(AmongUsClient self, GameOverReason reason)
        {
            GameStates.IsOutro = true;

            List<byte> ReviveRequiredPlayerIds = new();
            var winner = CustomWinnerHolder.WinnerTeam;
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (winner == CustomWinner.Draw)
                {
                    SetGhostRole(ToGhostImpostor: true);
                    continue;
                }
                bool canWin = CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) ||
                        CustomWinnerHolder.WinnerRoles.Contains(pc.GetCustomRole());
                canWin &= !CustomWinnerHolder.CantWinPlayerIds.Contains(pc.PlayerId);
                bool isCrewmateWin = reason.Equals(GameOverReason.CrewmatesByVote) || reason.Equals(GameOverReason.CrewmatesByTask);
                SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin);

                void SetGhostRole(bool ToGhostImpostor)
                {
                    var isDead = pc.Data.IsDead;
                    if (!isDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);
                    if (ToGhostImpostor)
                    {
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: ImpostorGhostに変更", "ResetRoleAndEndGame");
                        pc.RpcSetRole(RoleTypes.ImpostorGhost, false);
                    }
                    else
                    {
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: CrewmateGhostに変更", "ResetRoleAndEndGame");
                        pc.RpcSetRole(RoleTypes.CrewmateGhost, false);
                    }
                    pc.Data.IsDead = isDead;
                }
            }

            if (PlayerCatch.AnyModClient())
            {
                var winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, Hazel.SendOption.Reliable);
                CustomWinnerHolder.WriteTo(winnerWriter);
                self.FinishRpcImmediately(winnerWriter);
            }

            yield return new WaitForSeconds(EndGameDelay);

            if (ReviveRequiredPlayerIds.Count > 0)
            {
                for (int i = 0; i < ReviveRequiredPlayerIds.Count; i++)
                {
                    var playerId = ReviveRequiredPlayerIds[i];
                    var playerInfo = GameData.Instance.GetPlayerById(playerId);
                    playerInfo.IsDead = false;
                }
                GameDataSerializePatch.SerializeMessageCount++;
                RPC.RpcSyncAllNetworkedPlayer();
                GameDataSerializePatch.SerializeMessageCount--;
                yield return new WaitForSeconds(EndGameDelay);
            }
            yield return new WaitForSeconds(EndGameDelay);
            try
            {
                SetRoleSummaryText();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "SetRoleSummaryText");
                Logger.seeingame("非クライアントへのアウトロテキスト生成中にエラーが発生しました。");
            }
            yield return new WaitForSeconds(EndGameDelay);

            GameManager.Instance.RpcEndGame(reason, false);
            CheckGetNomalAchievement.CallEndGame(reason);
        }
        private static void SetRoleSummaryText(CustomRpcSender sender = null)
        {
            var winners = new List<PlayerControl>();
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)) winners.Add(pc);
            }
            foreach (var team in CustomWinnerHolder.WinnerRoles)
            {
                winners.AddRange(PlayerCatch.AllPlayerControls.Where(p => p.Is(team) && !winners.Contains(p)));
            }
            foreach (var id in CustomWinnerHolder.CantWinPlayerIds)
            {
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc == null) continue;
                winners.Remove(pc);
            }

            List<byte> winnerList = new();
            if (winners.Count != 0)
                foreach (var pc in winners)
                {
                    if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw && pc.Is(CustomRoles.GM)) continue;
                    if (CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) && winnerList.Contains(pc.PlayerId)) continue;
                    if (CustomWinnerHolder.CantWinPlayerIds.Contains(pc.PlayerId)) continue;

                    winnerList.Add(pc.PlayerId);
                }
            var (CustomWinnerText, CustomWinnerColor, _, _, _) = UtilsGameLog.GetWinnerText(winnerList: winnerList);
            //CustomWinnerText = TownOfHost.Roles.Neutral.BatGirl.NormalizeWinnerText(CustomWinnerText);
            var winnerSize = GetScale(CustomWinnerText.RemoveHtmlTags().Length, 2, 3.3);
            CustomWinnerText = $"<size={winnerSize}>{CustomWinnerText}</size>";
            static double GetScale(int input, double min, double max)
                => min + (max - min) * (1 - (double)(input - 1) / 13);

            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;
                if (pc == null) continue;
                var target = (winnerList.Contains(pc.PlayerId) ? pc : (winnerList.Count == 0 ? pc : PlayerCatch.GetPlayerById(winnerList.OrderBy(pc => pc).FirstOrDefault()) ?? pc)) ?? pc;
                var targetname = Main.AllPlayerNames[target.PlayerId];
                var text = $"<voffset=25>{CustomWinnerText}\n<voffset=0>{targetname}\n\n<voffset=24><size=40%><{Main.ModColor}>TownOfHost-Pko</color><#ffffff>v.{Main.PluginShowVersion}</size>";// sb.ToString() +$"\n</align><voffset=23>{CustomWinnerText}\n<voffset=45><size=1.75>{targetname}";
                if (text.Length > 320)
                {
                    Logger.Warn($"Gamelog:{text}", "SetRoleSummary");
                    text = text.RemoveDeltext("</color>");
                    if (text.Length > 320)
                    {
                        text = text.RemoveColorTags();
                    }
                }
                if (sender == null)
                {
                    target.RpcSetNamePrivate(text, true, pc, true);
                }
                else
                {
                    sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetName, pc.GetClientId())
                        .Write(pc.Data.NetId)
                        .Write(text)
                        .Write(true)
                        .EndRpc();
                }
            }
        }
        private const float EndGameDelay = 0.2f;

        private static void EnsurePredicate()
        {
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SuddenDeath:
                    SetPredicateToSadness();
                    break;
                case CustomGameMode.MurderMystery:
                    SetPredicateToMurderMystery();
                    break;
                case CustomGameMode.HideAndSeek:
                    SetPredicateToHideAndSeek();
                    break;
                case CustomGameMode.TaskBattle:
                    SetPredicateToTaskBattle();
                    break;
                default:
                    SetPredicateToNormal();
                    break;
            }
        }

        public static void SetPredicateToNormal() => predicate = new NormalGameEndPredicate();
        public static void SetPredicateToHideAndSeek() => predicate = new HideAndSeekGameEndPredicate();
        public static void SetPredicateToTaskBattle() => predicate = new TaskBattle.TaskBattleGameEndPredicate();
        public static void SetPredicateToMurderMystery() => predicate = new MurderMystery.MurderMysteryGameEndPredicate();
        public static void SetPredicateToDummyHunter() => predicate = new DummyHunter.DummyHunterGameEndPredicate();

        public static void SetPredicateToSadness() => predicate = new SadnessGameEndPredicate();
    }
}
