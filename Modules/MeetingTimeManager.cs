using System;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;

namespace TownOfHost.Modules
{
    public class MeetingTimeManager
    {
        private static int DiscussionTime;
        private static int VotingTime;
        private static int DefaultDiscussionTime;
        private static int DefaultVotingTime;

        public static void Init()
        {
            DefaultDiscussionTime = Main.RealOptionsData.GetInt(Int32OptionNames.DiscussionTime);
            DefaultVotingTime = Main.RealOptionsData.GetInt(Int32OptionNames.VotingTime);
            Logger.Info($"DefaultDiscussionTime:{DefaultDiscussionTime}, DefaultVotingTime{DefaultVotingTime}", "MeetingTimeManager.Init");
            ResetMeetingTime();
        }

        public static void ApplyGameOptions(IGameOptions opt)
        {
            opt.SetInt(Int32OptionNames.DiscussionTime, DiscussionTime);
            opt.SetInt(Int32OptionNames.VotingTime, VotingTime);
        }

        private static void ResetMeetingTime()
        {
            DiscussionTime = DefaultDiscussionTime;
            VotingTime = DefaultVotingTime;
        }

        public static void OnReportDeadBody()
        {
            // ★ ニムロッド会議
            if (Roles.Crewmate.Nimrod.IsExecutionMeeting())
            {
                Nimrod(Roles.Crewmate.Nimrod.meetingtime);
                return;
            }

            // 天秤会議
            if (Roles.Crewmate.Balancer.Id != 255
                && Roles.Crewmate.Balancer.target1 is not 255
                && Roles.Crewmate.Balancer.target2 is not 255)
            {
                Balancer(Roles.Crewmate.Balancer.meetingtime);
                return;
            }

            if (Assassin.assassin?.NowState is Assassin.AssassinMeeting.Guessing
                or Assassin.AssassinMeeting.CallMetting)
            {
                DiscussionTime = 0;
                VotingTime = Assassin.OptionAssassinMeetingTime.GetInt();
                return;
            }

            if (Options.AllAliveMeeting.GetBool() && PlayerCatch.IsAllAlive)
            {
                DiscussionTime = 0;
                VotingTime = Options.AllAliveMeetingTime.GetInt();
                Logger.Info($"DiscussionTime:{DiscussionTime}, VotingTime{VotingTime}", "MeetingTimeManager.OnReportDeadBody");
                return;
            }

            ResetMeetingTime();
            int BonusMeetingTime = 0;
            int MeetingTimeMin = 0;
            int MeetingTimeMax = 300;
            MeetingTimeMin = Options.LowerLimitVotingTime.GetInt();
            MeetingTimeMax = Options.MeetingTimeLimit.GetInt();

            foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            {
                if (role is IMeetingTimeAlterable meetingTimeAlterable)
                {
                    if (!role.Player.IsAlive() && meetingTimeAlterable.RevertOnDie)
                        continue;

                    var time = meetingTimeAlterable.CalculateMeetingTimeDelta();
                    Logger.Info($"会議時間-{role.Player.GetNameWithRole()}: {time} s", "MeetingTimeManager.OnReportDeadBody");
                    BonusMeetingTime += time;
                }
            }

            int TotalMeetingTime = DiscussionTime + VotingTime;
            BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMin, MeetingTimeMax) - TotalMeetingTime;
            if (BonusMeetingTime >= 0)
                VotingTime += BonusMeetingTime;
            else
            {
                DiscussionTime += BonusMeetingTime;
                if (DiscussionTime < 0)
                {
                    VotingTime += DiscussionTime;
                    DiscussionTime = 0;
                }
            }
            Logger.Info($"DiscussionTime:{DiscussionTime}, VotingTime{VotingTime}", "MeetingTimeManager.OnReportDeadBody");
        }

        public static void Balancer(int time)
        {
            DiscussionTime = 0;
            VotingTime = time;
        }

        // ★ ニムロッド会議用（Balancer と同じ実装）
        public static void Nimrod(int time)
        {
            DiscussionTime = 0;
            VotingTime = time;
        }
    }
}