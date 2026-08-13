using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Impostor;
using UnityEngine;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Neutral
    {
        public sealed class Hunter : RoleBase, ILNKiller
        {
        bool IKiller.CanKill => false;
        public static readonly SimpleRoleInfo RoleInfo =
                SimpleRoleInfo.Create(
                    typeof(Hunter),
                    player => new Hunter(player),
                    CustomRoles.Hunter,
                    () => RoleTypes.Shapeshifter,
                    CustomRoleTypes.Neutral,
                    56400,
                    SetupOptionItem,
                    "hun",
                    "#cd853f",
                    (1, 5),
                    true,
                     assignInfo: new RoleAssignInfo(CustomRoles.Hunter, CustomRoleTypes.Neutral)
                    {
                        AssignCountRule = new(1, 1, 1)
                    },
                    countType: CountTypes.Hunter,
                    from: From.TownOfHost_Pko
                );
        public Hunter(PlayerControl player)
        : base(
            RoleInfo,
            player,
            () => HasTask.False
        )
        {
            darkenedPlayers = null;
            CanVent = OptionCanVent.GetBool();
            Cooldown = OptionCooldown.GetFloat();
            targeted = false;
            targetId = 255;
            PublicRoleColor = false;
            changetimer = OptionchangeColorTime.GetFloat();
            BoostTime = OptionSpeedBoostTime.GetFloat();
            savedSpeeds.Clear();
            SpeedBoost = OptionSpeedBoost.GetFloat();
            DarkenTime = darkenTimer = optionDarkenDuration.GetFloat();
        }

        private float darkenTimer;

        static float DarkenTime;
        static float BoostTime;
        static float SpeedBoost;
        public static OptionItem OptionSpeedBoost;
        public static OptionItem OptionKillCooldown;
        private static OptionItem OptionCooldown;
        public static OptionItem OptionCanVent;
        static OptionItem OptionHasImpostorVision;
        private static float Cooldown;
        public static bool CanVent;
        public static bool CanUseSabotage;

        bool targeted;
        public static bool PublicRoleColor;

        PlayerControl KillWaitPlayer;
        public static OptionItem OptionchangeColorTime;
        public float changetimer;
        public static OptionItem OptionSpeedBoostTime;
        public static FloatOptionItem optionDarkenDuration;
        public static OptionItem OptionDarkenRange;

        private PlayerControl[] darkenedPlayers;

        enum OptionName
        {
            HunterchangeColorTime,
            HunterSpeedBoostTime,
            HunterSpeedBoost,
            StealthDarkenDuration,
            HunterDarkenRange
        }

        private static void SetupOptionItem()
        {
            SoloWinOption.Create(RoleInfo, 8, defo: 0);
            OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 40f, false).SetValueFormat(OptionFormat.Seconds);
            OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
            OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, true, false);
            OptionchangeColorTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.HunterchangeColorTime,
                new(0f, 180f, 0.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionSpeedBoostTime = FloatOptionItem.Create(RoleInfo, 14, OptionName.HunterSpeedBoostTime,
                new(0f, 180f, 0.5f), 7.5f, false).SetValueFormat(OptionFormat.Seconds);
            OptionSpeedBoost = FloatOptionItem.Create(RoleInfo, 15, OptionName.HunterSpeedBoost,
                new(1f, 10f, 0.25f), 1.75f, false).SetValueFormat(OptionFormat.Multiplier);
            optionDarkenDuration = FloatOptionItem.Create(RoleInfo, 16, OptionName.StealthDarkenDuration, new(0.5f, 30f, 0.5f), 1f, false);
            optionDarkenDuration.SetValueFormat(OptionFormat.Seconds);
            OptionDarkenRange = FloatOptionItem.Create(RoleInfo, 17, OptionName.HunterDarkenRange, new(0f, 10f, 0.5f), 2.5f, false)
                .SetValueFormat(OptionFormat.Multiplier);
            RoleAddAddons.Create(RoleInfo, 20, NeutralKiller: true);
        }

        public float CalculateKillCooldown() => 0f;
        public bool CanUseSabotageButton() => false;
        public bool CanUseImpostorVentButton() => CanVent;

        readonly Dictionary<byte, float> savedSpeeds = new();

        private Vector2 HunterPos;
        private Vector2 targetPos;

        byte targetId;

        bool targetCankill;

        bool targetDied;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            opt.SetVision(OptionHasImpostorVision.GetBool());
            AURoleOptions.ShapeshifterCooldown = Cooldown;
        }
        public override void CheckWinner(GameOverReason reason)
        {
            if (3 <= MyState.GetKillCount()) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            if (Player.IsWinner(CustomWinner.Hunter) && !Player.IsLovers())
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (AmongUsClient.Instance.AmHost)
            {
                if (darkenedPlayers != null)
                {
                    // タイマーを減らす
                    darkenTimer -= Time.fixedDeltaTime;
                    // タイマーが0になったらみんなの視界を戻してタイマーと暗転プレイヤーをリセットする
                    if (darkenTimer <= 0)
                    {
                        ResetDarkenState();
                    }
                }
            }
            if (!targeted || KillWaitPlayer == null)
            {
                targeted = false;
                return;
            }
            // TOHYありがとう!!!!!!!!
            if (targeted && KillWaitPlayer)
            {
                HunterPos = Player.transform.position;
                targetPos = PlayerCatch.GetPlayerControl(targetId).GetTruePosition();
            }
            Vector2 difference = targetPos - HunterPos;
            // 距離の2乗を取得
            float sqrDistance = difference.sqrMagnitude;
            float checkRadius = 5f;
            var target = KillWaitPlayer;
            if (target.PlayerId == Player.PlayerId)
            {
                targeted = false;
                KillWaitPlayer = null;
                return;
            }
            if (sqrDistance <= checkRadius * checkRadius)
            {
                if (!AmongUsClient.Instance.AmHost) return; 
                Player.RpcSnapToForced(target.transform.position);
                if (!targetCankill)
                {
                    if (CustomRoleManager.OnCheckMurder(Player, Player, Player, Player, true, false, 2, CustomDeathReason.Suicide))
                    {
                        Player.SetRealKiller(Player);
                        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
                    }
                }
                else
                {
                    if (CustomRoleManager.OnCheckMurder(Player, target, target, target, true, false, 2, CustomDeathReason.Kill))
                    {
                        target.SetRealKiller(Player);
                        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
                    }
                }
                KillWaitPlayer = null;
                target = null;
                Player.MarkDirtySettings();
                PublicRoleColor = true;
                ApplySpeedEffect(Player, SpeedBoost);

                var DarkenRange = OptionDarkenRange.GetFloat();
                var playersToDarken = PlayerCatch.AllAlivePlayerControls
                    .Where(targetdark => !targetdark.Is(CustomRoles.Hunter))
                    .Where(targetdark => targetdark.PlayerId != Player.PlayerId)
                    .Where(targetdark => Ballooner.IsInExplosionRange(Player, targetdark, DarkenRange))
                    .ToArray();

                if (playersToDarken.Length > 0)
                {
                    DarkenPlayers(playersToDarken);
                }
                _ = new LateTask(() =>
                {
                    targeted = false;
                    PublicRoleColor = false;
                    Player.RpcResetAbilityCooldown();
                }, changetimer, "", true);
            }
        }

        private void ResetDarkenState()
        {
            if (darkenedPlayers != null)
            {
                foreach (var player in darkenedPlayers)
                {
                    PlayerState.GetByPlayerId(player.PlayerId).IsBlackOut = false;
                    player.MarkDirtySettings();
                }
                darkenedPlayers = null;
            }
            darkenTimer = DarkenTime;
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        }

        private void DarkenPlayers(IEnumerable<PlayerControl> playersToDarken)
        {
            darkenedPlayers = playersToDarken.ToArray();
            foreach (var player in playersToDarken)
            {
                PlayerState.GetByPlayerId(player.PlayerId).IsBlackOut = true;
                player.MarkDirtySettings();
            }
            if (0 < playersToDarken.Count()) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        }

        void ApplySpeedEffect(PlayerControl Player, float multiplier)
        {
            byte id = Player.PlayerId;
            if (!savedSpeeds.ContainsKey(id))
                savedSpeeds[id] = Main.AllPlayerSpeed.TryGetValue(id, out float s) ? s : 1f;
            Main.AllPlayerSpeed[id] = savedSpeeds[id] * multiplier;
            Player.MarkDirtySettings();
            _ = new LateTask(() =>
            {
                RemoveEffect(Player.PlayerId);
            }, BoostTime, "", true);
        }

        void RemoveEffect(byte playerId)
        {
            if (!savedSpeeds.TryGetValue(playerId, out float orig)) return;
            Main.AllPlayerSpeed[playerId] = orig;
            PlayerCatch.GetPlayerById(playerId)?.MarkDirtySettings();
            savedSpeeds.Remove(playerId);
        }
        public static bool KnowTargetRoleColor(PlayerControl target, bool isMeeting)
        {
            if (!isMeeting && target.Is(CustomRoles.Hunter))
            {
                if (PublicRoleColor)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public override bool CheckShapeshift(PlayerControl target, ref bool animate)
        {
            animate = false;
            if (!targeted)
            {
                if (target.IsAlive())
                {
                    KillWaitPlayer = target;
                    targeted = true;
                    targetId = target.PlayerId;
                    if (target.Is(CustomRoles.King) || target.Is(CustomRoles.Autocrat))
                    {
                        targetCankill = false;
                    }
                    else
                    {
                        targetCankill = true;
                    }
                    targetDied = false;

                }
                else
                {
                    targetDied = true;
                    _ = new LateTask(() =>
                    {
                        targetDied = false;
                    }, 5f, "", true);
                }
            }
            return false;
        }


        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;
            if (targetDied)
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>ターゲットが死亡しています。ターゲットを選びなおしてください</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";

            }
            if (!targeted)
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>シェイプシフトでターゲット指定</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";
            }
            else
            {
                var mes = $"<color={RoleInfo.RoleColorCode}>ターゲットに近づいてキル!</color>";
                return isForHud ? mes : $"<size=40%>{mes}</size>";
            }
        }
        public override void AfterMeetingTasks()
        {
            RemoveEffect(Player.PlayerId);
            targetId = 255;
            targetCankill = true;
            targeted = false;
            KillWaitPlayer = null;
        }

        public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
        [Attributes.PluginModuleInitializer]
        public static void Load()
        {
            var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
            var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
            var sp1 = new Achievement(RoleInfo, 2, 1, 0, 3, true);
            achievements.Add(0, n1);
            achievements.Add(1, l1);
            achievements.Add(2, sp1);
        }
    }
}
