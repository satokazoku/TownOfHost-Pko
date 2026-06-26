using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Damudo
    {
        private static readonly int Id = 70500;
        private static readonly Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Damudo);
        public static readonly string SubRoleMark = "";

        private static readonly string[] CurseActionOptions =
        {
            "Damudo.CurseImpostor",
            "Damudo.CurseMadmate",
            "Damudo.CurseTakeKillerRole"
        };
        private static readonly string[] AwarenessTurnOptions =
        {
            "Damudo.None",
            "2","3","4","5","6","7","8","9","10","11","12","13","14","15"
        };
        private static readonly HashSet<CustomRoles> UnsupportedTakeoverKillerRoles = new()
        {
            CustomRoles.CountKiller,
            CustomRoles.DoppelGanger,
            CustomRoles.GrimReaper,
            CustomRoles.Egoist,
            CustomRoles.Arsonist,
            CustomRoles.CurseMaker,
            CustomRoles.Missioneer,
            CustomRoles.Banker,
            CustomRoles.SchrodingerCat,
            CustomRoles.BakeCat
        };

        public static List<byte> playerIdList = new();
        private static readonly Dictionary<byte, HashSet<byte>> roleVisibleTargetsBySeer = new();
        private static readonly HashSet<byte> awarePlayerIds = new();
        private static readonly HashSet<byte> revealedDamudoPlayerIds = new();
        private static readonly HashSet<byte> transformedDamudoPlayerIds = new();

        private static OptionItem OptionCurseAction;
        private static OptionItem OptionCurseKillKiller;
        private static OptionItem OptionCurseKillDelay;
        private static OptionItem OptionCanAwareness;
        private static OptionItem OptionAwarenessTask;
        private static OptionItem OptionAwarenessKill;
        private static OptionItem OptionAwarenessTurn;

        public static void SetupCustomOption()
        {
            var spawn = SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Damudo, fromtext: UtilsOption.GetFrom(From.NebulaontheShip));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Damudo, true, true, true, true);

            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option")
                .SetSubRoleOptionItem(CustomRoles.Damudo);

            OptionCurseAction = StringOptionItem.Create(Id + 22, "DamudoCurseAction", CurseActionOptions, 0, TabGroup.Addons, false)
                .SetParent(spawn).SetParentRole(CustomRoles.Damudo);

            OptionCurseKillKiller = BooleanOptionItem.Create(Id + 23, "DamudoCurseKillKiller", false, TabGroup.Addons, false)
                .SetParent(spawn).SetParentRole(CustomRoles.Damudo);

            OptionCurseKillDelay = FloatOptionItem.Create(Id + 24, "DamudoCurseKillDelay", new(0f, 20f, 2.5f), 0f, TabGroup.Addons, false)
                .SetParentRole(CustomRoles.Damudo).SetParent(OptionCurseKillKiller).SetValueFormat(OptionFormat.Seconds);

            ObjectOptionitem.Create(Id + 30, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Awareness Option")
                .SetSubRoleOptionItem(CustomRoles.Damudo);

            OptionCanAwareness = BooleanOptionItem.Create(Id + 31, "DamudoCanAwareness", false, TabGroup.Addons, false)
                .SetParent(spawn).SetParentRole(CustomRoles.Damudo);

            OptionAwarenessTask = IntegerOptionItem.Create(Id + 32, "DamudoAwarenessTask", new(0, 15, 1), 0, TabGroup.Addons, false)
                .SetParentRole(CustomRoles.Damudo).SetParent(OptionCanAwareness).SetZeroNotation(OptionZeroNotation.Off);

            OptionAwarenessKill = IntegerOptionItem.Create(Id + 33, "DamudoAwarenessKill", new(0, 14, 1), 0, TabGroup.Addons, false)
                .SetParentRole(CustomRoles.Damudo).SetParent(OptionCanAwareness).SetZeroNotation(OptionZeroNotation.Off);

            OptionAwarenessTurn = StringOptionItem.Create(Id + 34, "DamudoAwarenessTurn", AwarenessTurnOptions, 0, TabGroup.Addons, false)
                .SetParentRole(CustomRoles.Damudo).SetParent(OptionCanAwareness);
        }

        public static void Init()
        {
            playerIdList = new();
            roleVisibleTargetsBySeer.Clear();
            awarePlayerIds.Clear();
            revealedDamudoPlayerIds.Clear();
            transformedDamudoPlayerIds.Clear();
            EnsureMarkRegistered();
        }

        public static void Add(byte playerId)
        {
            EnsureMarkRegistered();
            if (transformedDamudoPlayerIds.Contains(playerId)) return;
            if (!playerIdList.Contains(playerId))
            {
                playerIdList.Add(playerId);
            }
            CheckAwareness(playerId);
        }

        public static void OnStartMeeting()
        {
            EnsureMarkRegistered();
            playerIdList.RemoveAll(id => id.GetPlayerControl()?.IsAlive() != true);
            awarePlayerIds.RemoveWhere(id => !playerIdList.Contains(id));

            foreach (var playerId in playerIdList.ToArray())
            {
                CheckAwareness(playerId);
            }
        }

        public static void OnCompleteTask(PlayerControl pc)
        {
            if (pc == null || !playerIdList.Contains(pc.PlayerId)) return;
            CheckAwareness(pc.PlayerId);
        }

        public static void OnCheckMurder(MurderInfo info)
        {
            var (killer, target) = info.AttemptTuple;
            if (killer == null || target == null) return;
            if (!killer.IsAlive() || !target.IsAlive()) return;
            if (transformedDamudoPlayerIds.Contains(target.PlayerId)) return;
            if (!playerIdList.Contains(target.PlayerId) || !target.Is(CustomRoles.Damudo)) return;
            if (killer.PlayerId == target.PlayerId) return;
            if (ShouldPassThroughKill(info)) return;

            var originalRole = target.GetCustomRole();
            var changedRole = GetChangedRole(killer);
            var teamChanged = IsTeamChanged(originalRole, changedRole);

            // Kill is consumed, target survives and transforms.
            info.CanKill = false;
            info.DoKill = false;

            if (!teamChanged)
            {
                target.RpcReplaceSubRole(CustomRoles.Damudo, remove: true);
            }
            playerIdList.Remove(target.PlayerId);
            awarePlayerIds.Remove(target.PlayerId);
            transformedDamudoPlayerIds.Add(target.PlayerId);
            if (teamChanged) revealedDamudoPlayerIds.Add(target.PlayerId);
            else revealedDamudoPlayerIds.Remove(target.PlayerId);
            GrantRoleVisibility(killer.PlayerId, target.PlayerId);

            target.RpcSetCustomRole(changedRole, true, log: null);
            killer.SetKillCooldown();
            killer.RpcProtectedMurderPlayer(target);

            UtilsGameLog.AddGameLog("Damudo", string.Format(Translator.GetString("DamudoLog"),
                UtilsName.GetPlayerColor(target, true),
                UtilsRoleText.GetRoleColorAndtext(changedRole)));

            if (OptionCurseKillKiller.GetBool())
            {
                var delay = OptionCurseKillDelay.GetFloat();
                if (delay <= 0f)
                {
                    KillKillerByCurse(killer);
                }
                else
                {
                    _ = new LateTask(() => KillKillerByCurse(killer), delay, "DamudoCurseKill");
                }
            }

            UtilsNotifyRoles.NotifyRoles();
            UtilsOption.MarkEveryoneDirtySettings();
        }

        public static void OnMurderPlayer(MurderInfo info)
        {
            var killer = info?.AttemptKiller;
            var target = info?.AttemptTarget;
            if (target == null) return;
            playerIdList.Remove(target.PlayerId);
            awarePlayerIds.Remove(target.PlayerId);
            transformedDamudoPlayerIds.Remove(target.PlayerId);

            if (killer != null && playerIdList.Contains(killer.PlayerId))
            {
                CheckAwareness(killer.PlayerId);
            }
        }

        private static void KillKillerByCurse(PlayerControl killer)
        {
            if (killer == null || !killer.IsAlive()) return;
            if (GameStates.CalledMeeting) return;
            killer.RpcMurderPlayerV2(killer);
        }

        private static CustomRoles GetChangedRole(PlayerControl killer)
        {
            return OptionCurseAction.GetValue() switch
            {
                0 => CustomRoles.Impostor,
                1 => CustomRoles.Madmate,
                2 => GetKillerRoleOrFallback(killer),
                _ => CustomRoles.Impostor
            };
        }

        private static CustomRoles GetKillerRoleOrFallback(PlayerControl killer)
        {
            if (killer == null) return CustomRoles.Impostor;

            var role = killer.GetCustomRole();
            return role < CustomRoles.NotAssigned && role is not CustomRoles.GM
                ? role
                : CustomRoles.Impostor;
        }

        private static bool IsTeamChanged(CustomRoles beforeRole, CustomRoles afterRole)
            => NormalizeTeam(beforeRole.GetCustomRoleTypes()) != NormalizeTeam(afterRole.GetCustomRoleTypes());

        private static CustomRoleTypes NormalizeTeam(CustomRoleTypes roleType)
            => roleType == CustomRoleTypes.Madmate ? CustomRoleTypes.Impostor : roleType;

        private static bool ShouldPassThroughKill(MurderInfo info)
        {
            if (OptionCurseAction.GetValue() != 2) return false;

            var killer = info?.AttemptKiller;
            if (killer == null) return false;

            // JackalHadouHo: only HadouHo/SuperHadouHo kills are unsupported.
            if (killer.Is(CustomRoles.JackalHadouHo) && info.DeathReason == CustomDeathReason.Hit)
                return true;

            return UnsupportedTakeoverKillerRoles.Contains(killer.GetCustomRole());
        }

        private static int GetAwarenessTurnNeed()
            => OptionAwarenessTurn.GetValue() == 0 ? 0 : OptionAwarenessTurn.GetValue() + 1;

        private static bool CanAwareness()
        {
            if (!OptionCanAwareness.GetBool()) return false;
            return OptionAwarenessTask.GetInt() > 0
                || OptionAwarenessKill.GetInt() > 0
                || GetAwarenessTurnNeed() > 0;
        }

        private static void CheckAwareness(byte playerId)
        {
            if (!CanAwareness()) return;
            if (awarePlayerIds.Contains(playerId)) return;

            var pc = playerId.GetPlayerControl();
            if (pc == null || !pc.IsAlive()) return;

            var taskNeed = OptionAwarenessTask.GetInt();
            var killNeed = OptionAwarenessKill.GetInt();
            var turnNeed = GetAwarenessTurnNeed();

            var taskOk = taskNeed > 0 && (pc.GetPlayerTaskState()?.HasCompletedEnoughCountOfTasks(taskNeed) ?? false);
            var killOk = killNeed > 0 && (pc.GetPlayerState()?.Killcount ?? 0) >= killNeed;
            var turnOk = turnNeed > 0 && UtilsGameLog.day >= turnNeed;

            if (!taskOk && !killOk && !turnOk) return;

            awarePlayerIds.Add(playerId);
            Utils.SendMessage(Translator.GetString("DamudoAwakened"), playerId, UtilsRoleText.GetRoleColorAndtext(CustomRoles.Damudo));
        }

        private static void GrantRoleVisibility(byte seerId, byte targetId)
        {
            if (!roleVisibleTargetsBySeer.TryGetValue(seerId, out var targets))
            {
                targets = new HashSet<byte>();
                roleVisibleTargetsBySeer[seerId] = targets;
            }
            targets.Add(targetId);
        }

        public static bool CanSeeRole(PlayerControl seer, PlayerControl seen)
        {
            if (seer == null || seen == null) return false;
            if (seer.PlayerId == seen.PlayerId) return false;
            if (!IsSameCountTypeTeam(seer, seen)) return false;

            return roleVisibleTargetsBySeer.TryGetValue(seer.PlayerId, out var targets)
                && targets.Contains(seen.PlayerId);
        }

        public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
        {
            seen ??= seer;
            if (seen == null) return "";
            if (!revealedDamudoPlayerIds.Contains(seen.PlayerId)) return "";
            return Utils.ColorString(RoleColor, "◈");
        }

        public static bool IsRevealedDamudo(byte playerId)
            => revealedDamudoPlayerIds.Contains(playerId);

        private static void EnsureMarkRegistered()
        {
            if (!CustomRoleManager.MarkOthers.Contains(GetMarkOthers))
            {
                CustomRoleManager.MarkOthers.Add(GetMarkOthers);
            }
        }

        private static bool IsSameCountTypeTeam(PlayerControl a, PlayerControl b)
        {
            var ta = a.GetCountTypes();
            var tb = b.GetCountTypes();
            if (ta is CountTypes.None or CountTypes.OutOfGame) return false;
            if (tb is CountTypes.None or CountTypes.OutOfGame) return false;
            return ta == tb;
        }
    }
}
