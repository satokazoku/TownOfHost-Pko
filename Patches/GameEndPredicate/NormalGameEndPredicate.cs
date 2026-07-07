/*using System.Linq;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
// ===== ゲーム終了条件 =====
// 通常ゲーム用
namespace TownOfHost
{
    class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            if (CheckGameEndByTask(out reason)) return true;
            if (CheckGameEndBySabotage(out reason)) return true;

            return false;
        }

        public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (Assassin.assassin?.NowState is Assassin.AssassinMeeting.Collected) return false;

            int Imp = 0;
            int Jackal = 0;
            int Crew = 0;
            int Remotekiller = 0;
            int GrimReaper = 0;
            int MilkyWay = 0;
            int FoxAndCrew = 0;
            int MadBetrayer = 0;
            int Pavlov = 0;
            int StandMasterCount = 0;
            // ★ 変化済みヴィラン（キルボタン持ち）は Crew から除外して別カウント
            int VillainActive = 0;

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                if (pc.GetCustomRole() is CustomRoles.MadBetrayer)
                {
                    Roles.Madmate.MadBetrayer.CheckCount(ref Crew, ref MadBetrayer);
                    continue;
                }
                switch (pc.GetCountTypes())
                {
                    case CountTypes.Fox:
                    case CountTypes.Crew:
                        // ★ 変化済みヴィランは Crew ではなく VillainActive としてカウント
                        if (pc.GetRoleClass() is Villain villain && villain.isVillain)
                        {
                            VillainActive++;
                        }
                        else
                        {
                            Crew++;
                            FoxAndCrew++;
                        }
                        break;
                    case CountTypes.Impostor: Imp++; break;
                    case CountTypes.Jackal: Jackal++; break;
                    case CountTypes.Remotekiller: Remotekiller++; break;
                    case CountTypes.GrimReaper: GrimReaper++; break;
                    case CountTypes.MilkyWay: MilkyWay++; break;
                    case CountTypes.Pavlov: Pavlov++; break;
                    case CountTypes.StandMaster: StandMasterCount++; break;
                }
            }
            if (Jackal == 0 && (CustomRoles.Jackal.IsPresent() || CustomRoles.JackalMafia.IsPresent() || CustomRoles.JackalAlien.IsPresent() || CustomRoles.JackalHadouHo.IsPresent() || CustomRoles.JackalWolf.IsPresent()))
                foreach (var player in PlayerCatch.AllAlivePlayerControls)
                {
                    if ((player.Is(CustomRoles.Jackaldoll) && JackalDoll.BossAndSidekicks.ContainsKey(player.PlayerId)) || player.Is(CustomRoles.Tama))
                    {
                        Jackal++;
                        Crew--;
                        FoxAndCrew--;
                        break;
                    }
                }
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.GetRoleClass() is Assassin assassin && !pc.IsAlive())
                {
                    Imp += assassin.NowState is Assassin.AssassinMeeting.WaitMeeting or Assassin.AssassinMeeting.CallMetting or Assassin.AssassinMeeting.Guessing ? 1 : 0;
                }
            }

            bool standMasterAlive = PlayerCatch.AllAlivePlayerControls
                .Any(pc => pc.Is(CustomRoles.StandMaster));

            // ★ ヴィラン勝利判定
            //   変化済みヴィランが生存者の半数以上 かつ 他の生存者が無害（Crew/Fox のみ）
            if (VillainActive > 0
                && Imp == 0 && Jackal == 0 && Remotekiller == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0
                && VillainActive * 2 >= VillainActive + FoxAndCrew)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Villain, byte.MaxValue);
                foreach (var pc in PlayerCatch.AllPlayerControls
                    .Where(pc => pc.GetRoleClass() is Villain v && v.isVillain))
                {
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                }
                return true;
            }

            if (Imp == 0 && FoxAndCrew == 0 && Jackal == 0 && Remotekiller == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0
                && VillainActive == 0) //全滅
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            }
            else if (Lovers.CheckPlayercountWin())
            {
                reason = GameOverReason.ImpostorsByKill;
            }
            else if (Imp == 1 && Crew == 0 && GrimReaper == 1)//死神勝利(1)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.GrimReaper, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.GrimReaper);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.GrimReaper)?.PlayerId ?? byte.MaxValue);
            }
            else if (Jackal == 0 && Remotekiller == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && VillainActive == 0
                && FoxAndCrew <= Imp) //インポスター勝利
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Impostor, byte.MaxValue);
            }
            else if (Imp == 0 && Remotekiller == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && VillainActive == 0
                && FoxAndCrew <= Jackal) //ジャッカル勝利
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jackal, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalMafia);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalAlien);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackaldoll);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalHadouHo);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Tama);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalWolf);
            }
            else if (Imp == 0 && Jackal == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && VillainActive == 0
                && FoxAndCrew <= Remotekiller)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Remotekiller, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Remotekiller);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.Remotekiller)?.PlayerId ?? byte.MaxValue);
            }
            else if (Jackal == 0 && Imp == 0 && GrimReaper == 1 && Remotekiller == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0
                && VillainActive == 0)//死神勝利(2)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.GrimReaper, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.GrimReaper);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.GrimReaper)?.PlayerId ?? byte.MaxValue);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && VillainActive == 0
                && FoxAndCrew <= MilkyWay)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.MilkyWay, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Vega);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Altair);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && MilkyWay == 0
                && Pavlov == 0 && StandMasterCount == 0 && VillainActive == 0
                && FoxAndCrew <= MadBetrayer)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.MadBetrayer, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.MadBetrayer);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && GrimReaper == 0
                && MilkyWay == 0 && MadBetrayer == 0 && StandMasterCount == 0
                && VillainActive == 0
                && FoxAndCrew <= Pavlov && PavlovDog.HasAliveDog())
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Pavlov, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovDog);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovOwner);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovDogImprint);
            }
            else if (standMasterAlive
                && Imp == 0 && Jackal == 0 && Remotekiller == 0 && GrimReaper == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0
                && VillainActive == 0
                && FoxAndCrew <= StandMasterCount)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.StandMaster, byte.MaxValue);
                foreach (var pc in PlayerCatch.AllPlayerControls
                    .Where(pc => pc.Is(CustomRoles.StandMaster) || pc.Is(CustomRoles.Stand)))
                {
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                }
            }
            else if (Jackal == 0 && Remotekiller == 0 && MadBetrayer == 0
                && MilkyWay == 0 && Pavlov == 0 && StandMasterCount == 0
                && VillainActive == 0 && Imp == 0) //クルー勝利
            {
                reason = GameOverReason.CrewmatesByVote;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Crewmate, byte.MaxValue);
            }
            else return false; //勝利条件未達成

            return true;
        }
    }
}*/
//　上ヴィラン用
using System.Linq;

using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
// ===== ゲーム終了条件 =====
// 通常ゲーム用
namespace TownOfHost
{
    class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            if (CheckGameEndByTask(out reason)) return true;
            if (CheckGameEndBySabotage(out reason)) return true;

            return false;
        }

        public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (Assassin.assassin?.NowState is Assassin.AssassinMeeting.Collected) return false;

            int Imp = 0;
            int Jackal = 0;
            int Crew = 0;
            int Remotekiller = 0;
            int GrimReaper = 0;
            int MilkyWay = 0;
            int FoxAndCrew = 0;
            int MadBetrayer = 0;
            int Pavlov = 0;
            int StandMasterCount = 0;
            // ★ Monika: ゴミ箱に居ない生存モニカ数（モニカ独自勝利は Monika.cs で処理）
            int MonikaCount = 0;

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                // ★ Monika: ゴミ箱レイヤーに居るプレイヤーは半死亡扱い。
                //    「ゴミ箱に居ない生存者」のみをカウント対象にするため、生存カウントから除外する。
                if (Monika.MonikaTrashLayer.Contains(pc.PlayerId)) continue;
                // ★ Monika本人はキル勢だが、勝利判定は Monika.cs 側で独自に行うため
                //    通常の陣営カウントには含めず、生存フラグだけ立てる。
                if (pc.Is(CustomRoles.Monika)) { MonikaCount++; continue; }
                if (pc.GetCustomRole() is CustomRoles.MadBetrayer)
                {
                    Roles.Madmate.MadBetrayer.CheckCount(ref Crew, ref MadBetrayer);
                    continue;
                }
                switch (pc.GetCountTypes())
                {
                    case CountTypes.Fox:
                    case CountTypes.Crew: Crew++; FoxAndCrew++; break;
                    case CountTypes.Impostor: Imp++; break;
                    case CountTypes.Jackal: Jackal++; break;
                    case CountTypes.Remotekiller: Remotekiller++; break;
                    case CountTypes.GrimReaper: GrimReaper++; break;
                    case CountTypes.MilkyWay: MilkyWay++; break;
                    case CountTypes.Pavlov: Pavlov++; break;
                    case CountTypes.StandMaster: StandMasterCount++; break;
                }
            }
            if (Jackal == 0 && (CustomRoles.Jackal.IsPresent() || CustomRoles.JackalMafia.IsPresent() || CustomRoles.JackalAlien.IsPresent() || CustomRoles.JackalHadouHo.IsPresent() || CustomRoles.JackalWolf.IsPresent()))
                foreach (var player in PlayerCatch.AllAlivePlayerControls)
                {
                    if ((player.Is(CustomRoles.Jackaldoll) && JackalDoll.BossAndSidekicks.ContainsKey(player.PlayerId)) || player.Is(CustomRoles.Tama))
                    {
                        Jackal++;
                        Crew--;
                        FoxAndCrew--;
                        break;
                    }
                }
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc.GetRoleClass() is Assassin assassin && !pc.IsAlive())
                {
                    Imp += assassin.NowState is Assassin.AssassinMeeting.WaitMeeting or Assassin.AssassinMeeting.CallMetting or Assassin.AssassinMeeting.Guessing ? 1 : 0;
                }
            }

            bool standMasterAlive = PlayerCatch.AllAlivePlayerControls
                .Any(pc => pc.Is(CustomRoles.StandMaster));

            // ★ Monika: モニカが生存している間は、通常の陣営全滅判定でゲームを終わらせない。
            //    （モニカの勝利/敗北判定は Monika.cs の CheckWinConditions で行う）
            if (MonikaCount > 0
                && Imp == 0 && Jackal == 0 && Remotekiller == 0 && GrimReaper == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0)
            {
                return false;
            }

            if (Imp == 0 && FoxAndCrew == 0 && Jackal == 0 && Remotekiller == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0) //全滅
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            }
            else if (Lovers.CheckPlayercountWin())
            {
                reason = GameOverReason.ImpostorsByKill;
            }
            else if (Imp == 1 && Crew == 0 && GrimReaper == 1)//死神勝利(1)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.GrimReaper, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.GrimReaper);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.GrimReaper)?.PlayerId ?? byte.MaxValue);
            }
            else if (Jackal == 0 && Remotekiller == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && FoxAndCrew <= Imp) //インポスター勝利
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Impostor, byte.MaxValue);
            }
            else if (Imp == 0 && Remotekiller == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && FoxAndCrew <= Jackal) //ジャッカル勝利
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jackal, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalMafia);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalAlien);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackaldoll);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalHadouHo);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Tama);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JackalWolf);
            }
            else if (Imp == 0 && Jackal == 0 && MilkyWay == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && FoxAndCrew <= Remotekiller)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Remotekiller, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Remotekiller);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.Remotekiller)?.PlayerId ?? byte.MaxValue);
            }
            else if (Jackal == 0 && Imp == 0 && GrimReaper == 1 && Remotekiller == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0 && StandMasterCount == 0)//死神勝利(2)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.GrimReaper, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.GrimReaper);
                CustomWinnerHolder.NeutralWinnerIds.Add(PlayerCatch.AllPlayerControls
                    .FirstOrDefault(pc => pc.GetCustomRole() is CustomRoles.GrimReaper)?.PlayerId ?? byte.MaxValue);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && MadBetrayer == 0
                && Pavlov == 0 && StandMasterCount == 0 && FoxAndCrew <= MilkyWay)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.MilkyWay, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Vega);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Altair);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && MilkyWay == 0
                && Pavlov == 0 && StandMasterCount == 0 && FoxAndCrew <= MadBetrayer)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.MadBetrayer, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.MadBetrayer);
            }
            else if (Imp == 0 && Jackal == 0 && Remotekiller == 0 && GrimReaper == 0
                && MilkyWay == 0 && MadBetrayer == 0 && StandMasterCount == 0
                && FoxAndCrew <= Pavlov && PavlovDog.HasAliveDog())
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Pavlov, byte.MaxValue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovDog);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovOwner);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PavlovDogImprint);
            }
            else if (standMasterAlive
                && Imp == 0 && Jackal == 0 && Remotekiller == 0 && GrimReaper == 0
                && MilkyWay == 0 && MadBetrayer == 0 && Pavlov == 0
                && FoxAndCrew <= StandMasterCount)
            {
                reason = GameOverReason.ImpostorsByKill;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.StandMaster, byte.MaxValue);
                foreach (var pc in PlayerCatch.AllPlayerControls
                    .Where(pc => pc.Is(CustomRoles.StandMaster) || pc.Is(CustomRoles.Stand)))
                {
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                }
            }
            else if (Jackal == 0 && Remotekiller == 0 && MadBetrayer == 0
                && MilkyWay == 0 && Pavlov == 0 && StandMasterCount == 0 && Imp == 0) //クルー勝利
            {
                reason = GameOverReason.CrewmatesByVote;
                CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Crewmate, byte.MaxValue);
            }
            else return false; //勝利条件未達成

            return true;
        }
    }
}