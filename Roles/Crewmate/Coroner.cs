using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Modules;

namespace TownOfHost.Roles.Crewmate
{
    [HarmonyPatch]
    public sealed class Coroner : RoleBase
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Coroner),
                player => new Coroner(player),
                CustomRoles.Coroner,
                () => RoleTypes.Crewmate,
                CustomRoleTypes.Crewmate,
                991000,
                SetupOptionItem,
                "co",
                colorCode: "#B4B4B4",
                OptionSort: (2, 10),
                from: From.TownOfHost_Temp,
                isNewRole: true
            );

        public static OptionItem OptionInformationAmount;
        public static OptionItem OptionDeathTimeAccuracy;
        public static OptionItem OptionShowReport;

        public class DeadBodyData
        {
            public byte VictimId;        // 被害者ID
            public float DeathTime;       // 死亡ゲーム内時刻
            public Vector2 DeathPosition; // 死亡座標
            public string RoomName;      // 死亡部屋名
            public CustomRoleTypes KillerFaction; // 犯人の陣営
        }

        public static Dictionary<byte, DeadBodyData> AllKillRecords = new();
        public static List<string> PendingReports = new();

        public Coroner(PlayerControl player) : base(RoleInfo, player)
        {
            hasTasks = () => HasTask.True;
        }

        private static void SetupOptionItem()
        {
            OptionInformationAmount = FloatOptionItem.Create(
                RoleInfo, 1210, "InformationAmount",
                new(1f, 4f, 1f), 2f, false);

            OptionDeathTimeAccuracy = FloatOptionItem.Create(
                RoleInfo, 1211, "DeathTimeAccuracy",
                new(5f, 60f, 5f), 15f, false)
                .SetValueFormat(OptionFormat.Seconds);

            OptionShowReport = BooleanOptionItem.Create(
                RoleInfo, 1212, "ShowReport", false, false);
        }

        public override void OverrideTrueRoleName(
            ref Color roleColor, ref string roleText)
        {
            roleColor = RoleInfo.RoleColor;
            roleText = GetString("Coroner");
        }

        public override void ApplyGameOptions(IGameOptions opt)
        {
        }

        public override void OnStartMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (PendingReports.Count == 0) return;

            byte sendTo = OptionShowReport.GetBool() ? (byte)255 : Player.PlayerId;

            _ = new LateTask(() =>
            {
                if (!GameStates.IsMeeting) return;

                foreach (var report in PendingReports)
                {
                    Utils.SendMessage(report, sendTo, title: $"<#B4B4B4>[{GetString("Coroner")}]</color>");
                }
                PendingReports.Clear();
            }, 0.5f, "CoronerReportDelay", true);
        }

        public override void Add()
        {
            if (AmongUsClient.Instance.AmHost)
            {
                CustomRoleManager.OnMurderPlayerOthers.Add(OnMurderPlayerHook);
            }
        }

        private static void OnMurderPlayerHook(MurderInfo info)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (info == null || info.AttemptTarget == null || info.AttemptKiller == null) return;

            byte targetId = info.AttemptTarget.PlayerId;
            string roomName = info.AttemptTarget.GetShipRoomName();
            if (string.IsNullOrEmpty(roomName)) roomName = "???";

            CustomRoleTypes killerFaction = info.AttemptKiller.GetCustomRole().GetCustomRoleTypes();

            var killData = new DeadBodyData
            {
                VictimId = targetId,
                DeathTime = Time.time,
                DeathPosition = info.AttemptTarget.transform.position,
                RoomName = roomName,
                KillerFaction = killerFaction
            };

            if (AllKillRecords.ContainsKey(targetId))
                AllKillRecords[targetId] = killData;
            else
                AllKillRecords.Add(targetId, killData);
        }

        public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (reporter == null || target == null) return;

            if (reporter.PlayerId == Player.PlayerId)
            {
                if (AllKillRecords.TryGetValue(target.PlayerId, out var data))
                {
                    string victimName = target.PlayerName ?? "???";
                    ExecuteAutopsy(victimName, data);
                }
            }
        }

        private static void ExecuteAutopsy(string victimName, DeadBodyData data)
        {
            List<string> availableClues = new List<string>();

            float accuracy = OptionDeathTimeAccuracy.GetFloat();
            float timePassed = Time.time - data.DeathTime;
            int minTime = Mathf.Max(0, Mathf.FloorToInt(timePassed / accuracy) * Mathf.FloorToInt(accuracy));
            int maxTime = minTime + Mathf.FloorToInt(accuracy);
            availableClues.Add(string.Format(GetString("AutopsyClue.Time"), minTime, maxTime));

            availableClues.Add(string.Format(GetString("AutopsyClue.Location"), data.RoomName));


            var targetState = PlayerState.GetByPlayerId(data.VictimId);
            CustomDeathReason finalReason = targetState?.DeathReason ?? CustomDeathReason.Kill;
            string reasonStr = Utils.GetDeathReason(finalReason);
            availableClues.Add(string.Format(GetString("AutopsyClue.Reason"), reasonStr));

            string factionStr = GetString($"CustomRoleTypes.{data.KillerFaction}");
            availableClues.Add(string.Format(GetString("AutopsyClue.Faction"), factionStr));

            int amount = Mathf.RoundToInt(OptionInformationAmount.GetFloat());
            amount = Mathf.Min(amount, availableClues.Count);

            var selectedClues = availableClues.OrderBy(x => Guid.NewGuid()).Take(amount).ToList();

            string titleStr = string.Format(GetString("AutopsyReportTitle"), victimName);
            string reportText = titleStr + "\n" + string.Join("\n", selectedClues);
            PendingReports.Add(reportText);
        }

        [HarmonyPatch(typeof(CustomRoleManager), nameof(CustomRoleManager.Initialize))]
        class ResetCoronerDataPatch
        {
            static void Prefix()
            {
                AllKillRecords.Clear();
                PendingReports.Clear();
            }
        }
    }
}
