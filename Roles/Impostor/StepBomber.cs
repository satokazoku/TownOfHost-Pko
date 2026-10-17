using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor
{
    [HarmonyPatch]
    public sealed class StepBomber : RoleBase, IImpostor, IKiller
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(StepBomber),
                player => new StepBomber(player),
                CustomRoles.StepBomber,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Impostor,
                990000,
                SetupOptionItem,
                "sb",
                colorCode: "#FF0000",
                OptionSort: (3, 2),
                from: From.TownOfHost_Temp,
                isNewRole: true
            );

        public static OptionItem OptionStepsToDetonate;
        public static OptionItem OptionNotificationTiming;
        public static OptionItem OptionBlastRange;
        public static OptionItem OptionCooldown;
        public static OptionItem OptionMaxBombCount;
        public static OptionItem OptionCanKillImpostor;
        public static OptionItem OptionHasNormalKill;

        public static Dictionary<byte, byte> PlantedBombs = new Dictionary<byte, byte>();
        public static HashSet<byte> IgnitedPlayers = new HashSet<byte>();
        public static Dictionary<byte, Vector2> LastPositions = new Dictionary<byte, Vector2>();
        public static Dictionary<byte, float> RemainingSteps = new Dictionary<byte, float>();

        // Click state belongs to each StepBomber. Keeping this static makes
        // multiple StepBombers overwrite one another's pending target.
        private byte _pendingTargetId = byte.MaxValue;
        private float _clickWindowTimer = 0f;
        private const float DoubleClickThreshold = 0.3f;

        private int _bombCount = 1;

        public StepBomber(PlayerControl player) : base(RoleInfo, player)
        {
            _bombCount = Mathf.RoundToInt(OptionMaxBombCount.GetFloat());
        }

        public bool CanBeLastImpostor { get; } = true;
        public bool IsKiller => true;

        enum OptionName
        {
            StepsToDetonate,
            NotificationTiming,
            BlastRange,
            Cooldown,
            MaxBombCount,
            CanKillImpostor,
            HasNormalKill
        }

        private static void SetupOptionItem()
        {
            OptionStepsToDetonate = FloatOptionItem.Create(
                RoleInfo, 1110, "StepsToDetonate", new(5f, 300f, 5f), 20f, false);

            OptionNotificationTiming = FloatOptionItem.Create(
                RoleInfo, 1111, "NotificationTiming", new(0f, 2f, 1f), 0f, false);

            OptionBlastRange = FloatOptionItem.Create(
                RoleInfo, 1112, "BlastRange", new(1f, 20f, 1f), 3f, false)
                .SetValueFormat(OptionFormat.Multiplier);

            OptionCooldown = FloatOptionItem.Create(
                RoleInfo, 1113, GeneralOption.Cooldown, new(0f, 999f, 0.5f), 15f, false)
                .SetValueFormat(OptionFormat.Seconds);

            OptionMaxBombCount = FloatOptionItem.Create(
                RoleInfo, 1114, "MaxBombCount", new(1f, 10f, 1f), 1f, false);

            OptionCanKillImpostor = FloatOptionItem.Create(
                RoleInfo, 1115, "CanKillImpostor", new(0f, 1f, 1f), 0f, false);

            OptionHasNormalKill = BooleanOptionItem.Create(
                RoleInfo, 1116, "HasNormalKill", false, false);
        }

        public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
        {
            roleColor = RoleInfo.RoleColor;
            roleText = GetString("StepBomber");
        }

        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            if (info.AttemptTarget == null || info.AttemptTarget.Data.IsDead)
            {
                info.CanKill = false;
                info.DoKill = false;
                return;
            }

            byte targetId = info.AttemptTarget.PlayerId;

            // A target that already has a bomb must not fall through to the
            // normal-kill double-click branch. Otherwise a later click can
            // kill the target instead of leaving the bomb in place.
            if (PlantedBombs.ContainsKey(targetId))
            {
                info.CanKill = false;
                info.DoKill = false;
                ResetClickState();
                Logger.Info($"[StepBomber] Target already has a bomb: {targetId}", "StepBomber");
                return;
            }

            if (OptionHasNormalKill.GetBool())
            {
                if (_pendingTargetId == targetId && _clickWindowTimer > 0f)
                {
                    info.CanKill = true;
                    info.DoKill = true;
                    _pendingTargetId = byte.MaxValue;
                    _clickWindowTimer = 0f;
                    return;
                }

                _pendingTargetId = targetId;
                _clickWindowTimer = DoubleClickThreshold;

                info.CanKill = false;
                info.DoKill = false;
                return;
            }

            ExecuteBombPlant(info, targetId);
        }

        private void ExecuteBombPlant(MurderInfo info, byte targetId)
        {
            if (_bombCount <= 0 || PlantedBombs.ContainsKey(targetId))
            {
                info.CanKill = false;
                info.DoKill = false;
                return;
            }

            PlantedBombs.Add(targetId, Player.PlayerId);
            _bombCount--;

            Logger.Info($"[StepBomber] Bomb plant: {Player.Data.PlayerName} => {targetId}", "StepBomber");

            info.IsGuard = true;
            info.GuardPower = info.KillPower;
            info.CanKill = false;
            info.DoKill = false;
        }
        public void OnCheckMurderDontKill(MurderInfo info)
        {
            if (OptionHasNormalKill.GetBool() && _clickWindowTimer > 0f)
            {
                return;
            }

            Player.ResetKillCooldown();
            if (CheckMurderPatch.TimeSinceLastKill.ContainsKey(Player.PlayerId))
            {
                CheckMurderPatch.TimeSinceLastKill[Player.PlayerId] = 0f;
            }
        }

        public void OnMurderPlayerAsKiller(MurderInfo info)
        {
        }

        public override string GetProgressText(bool comms = false, bool gamelog = false)
            => Utils.ColorString(_bombCount > 0 ? Color.red : Color.gray, $"({_bombCount})");

        public override void ApplyGameOptions(IGameOptions opt)
        {
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            // 
            if (OptionHasNormalKill.GetBool() && _clickWindowTimer > 0f)
            {
                _clickWindowTimer -= Time.fixedDeltaTime;

                // 0.3秒間、2回目のクリックがされずにタイムアップした場合（単発押しが確定）
                if (_clickWindowTimer <= 0f && _pendingTargetId != byte.MaxValue)
                {
                    var target = PlayerCatch.GetPlayerById(_pendingTargetId);
                    if (target != null && target.IsAlive() && _bombCount > 0 && !PlantedBombs.ContainsKey(_pendingTargetId))
                    {
                        PlantedBombs.Add(_pendingTargetId, Player.PlayerId);
                        _bombCount--;

                        Player.ResetKillCooldown();
                        if (CheckMurderPatch.TimeSinceLastKill.ContainsKey(Player.PlayerId))
                        {
                            CheckMurderPatch.TimeSinceLastKill[Player.PlayerId] = 0f;
                        }
                        Player.SetKillCooldown(target: target);

                        Logger.Info($"[StepBomber] DoubleClick timed out. Bomb planted on: {target.Data.PlayerName}", "StepBomber");
                    }

                    // 判定用変数をリセット
                    _pendingTargetId = byte.MaxValue;
                    _clickWindowTimer = 0f;
                }
            }


            if (GameStates.CalledMeeting || GameStates.IsMeeting) return;
            if (!GameStates.IsInTask || IgnitedPlayers.Count == 0) return;

            foreach (var targetId in IgnitedPlayers.ToList())
            {
                var target = PlayerCatch.GetPlayerById(targetId);
                if (target == null || !target.IsAlive())
                {
                    CleanUpTarget(targetId);
                    continue;
                }

                Vector2 currentPos = target.transform.position;

                if (!LastPositions.ContainsKey(targetId))
                {
                    LastPositions[targetId] = currentPos;
                    RemainingSteps[targetId] = OptionStepsToDetonate.GetFloat();
                    continue;
                }

                float distance = Vector2.Distance(LastPositions[targetId], currentPos);

                if (distance > 0.12f)
                {
                    if (distance > 1.5f)
                    {
                        LastPositions[targetId] = currentPos;
                        continue;
                    }

                    float stepsMoved = distance / 0.4f;
                    RemainingSteps[targetId] -= stepsMoved;
                    LastPositions[targetId] = currentPos;

                    if (RemainingSteps[targetId] <= 0f)
                    {
                        Detonate(target);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        class SafeMeetingStartPatch
        {
            static void Postfix()
            {
                LastPositions.Clear();
                ResetAllClickStates();
                CheckAndTriggerNotification(0);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
        class SafeMeetingVotePatch
        {
            public static bool hasTriggeredVote = false;
            static void Postfix(MeetingHud __instance)
            {
                int currentState = Convert.ToInt32(__instance.state);

                if (currentState == 1 && !hasTriggeredVote)
                {
                    CheckAndTriggerNotification(1);
                    hasTriggeredVote = true;
                }
                else if (currentState != 1)
                {
                    hasTriggeredVote = false;
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
        class SafeMeetingClosePatch
        {
            static void Postfix()
            {
                CheckAndTriggerNotification(2);
            }
        }

        [HarmonyPatch(typeof(CustomRoleManager), nameof(CustomRoleManager.Initialize))]
        class GameInitializePatch
        {
            static void Prefix() => ResetAll();
        }

        public static void CheckAndTriggerNotification(int currentTiming)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (Mathf.RoundToInt(OptionNotificationTiming.GetFloat()) != currentTiming) return;

            foreach (var bomb in PlantedBombs.ToList())
            {
                byte targetId = bomb.Key;
                if (IgnitedPlayers.Contains(targetId)) continue;

                var target = PlayerCatch.GetPlayerById(targetId);
                if (target != null && target.IsAlive())
                {
                    Utils.SendMessage(GetString("StepBomberAlert"), target.PlayerId);
                    IgnitedPlayers.Add(targetId);
                }
            }
        }

        private static void Detonate(PlayerControl target)
        {
            if (target == null || !target.IsAlive()) return;

            byte targetId = target.PlayerId;
            Vector2 explodePos = target.transform.position;
            byte bomberId = PlantedBombs.TryGetValue(targetId, out var bId) ? bId : targetId;
            var bomber = PlayerCatch.GetPlayerById(bomberId);

            if (CustomRoleManager.OnCheckMurder(bomber, target, target, target, force: true, DontRoleAbility: true, Killpower: 1, deathReason: CustomDeathReason.Bombed))
            {
                RPC.PlaySoundRPC(bomberId, Sounds.KillSound);
                Utils.SendMessage(string.Format(GetString("StepBomberDetonate"), target.Data.PlayerName), target.PlayerId);
            }

            float range = OptionBlastRange.GetFloat();
            foreach (var alivePlayer in PlayerCatch.AllAlivePlayerControls)
            {
                if (alivePlayer.PlayerId == targetId) continue;

                if (Vector2.Distance(explodePos, alivePlayer.transform.position) <= range)
                {
                    if (OptionCanKillImpostor.GetFloat() < 0.5f && alivePlayer.GetCustomRole().IsImpostor())
                        continue;

                    if (CustomRoleManager.OnCheckMurder(bomber, alivePlayer, alivePlayer, alivePlayer, force: true, DontRoleAbility: true, Killpower: 1, deathReason: CustomDeathReason.Bombed))
                    {
                        Utils.SendMessage(string.Format(GetString("StepBomberSplash"), alivePlayer.Data.PlayerName), alivePlayer.PlayerId);
                    }
                }
            }

            CleanUpTarget(targetId);
        }

        public static void CleanUpTarget(byte targetId)
        {
            PlantedBombs.Remove(targetId);
            IgnitedPlayers.Remove(targetId);
            LastPositions.Remove(targetId);
            RemainingSteps.Remove(targetId);
        }

        public static void ResetAll()
        {
            PlantedBombs.Clear();
            IgnitedPlayers.Clear();
            LastPositions.Clear();
            RemainingSteps.Clear();
            ResetAllClickStates();
            SafeMeetingVotePatch.hasTriggeredVote = false;
        }

        private void ResetClickState()
        {
            _pendingTargetId = byte.MaxValue;
            _clickWindowTimer = 0f;
        }

        private static void ResetAllClickStates()
        {
            foreach (var role in CustomRoleManager.AllActiveRoles.Values.OfType<StepBomber>())
                role.ResetClickState();
        }
    }
}
