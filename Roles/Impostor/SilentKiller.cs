using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public sealed class SilentKiller : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(SilentKiller),
                player => new SilentKiller(player),
                CustomRoles.SilentKiller,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Impostor,
                127400,
                SetupOptionItem,
                "skl",
                OptionSort: (7, 13)
            );
        public SilentKiller(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            StopTime = OptStopTime.GetFloat();
            HidePos = new Vector2(45f, 0f);   // RPCの座標は±50程度に丸められるため、範囲内に収める(100だと50に切り詰められる)
            SyncTimer = 0f;
            RealPos.Clear();
            StopTimer.Clear();
            LastSid.Clear();
            NextReveal.Clear();
            LocalVisible.Clear();
            DeadedPlayers.Clear();
        }

        #region オプション

        static OptionItem KillCoolDown;
        static OptionItem OptStopTime;
        static OptionItem OptFlash;
        static OptionItem OptCanSeeNakama;
        static OptionItem OptCanSabotage;
        static OptionItem OptCanReactor;
        static OptionItem OptCanElec;
        private static void SetupOptionItem()
        {
            KillCoolDown = FloatOptionItem.Create(RoleInfo, 10, "KillCooldown", new(0, 180, 0.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);

            OptStopTime = FloatOptionItem.Create(RoleInfo, 11, "SilentKillerStopTime", new(0.5f, 60f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Seconds);

            OptFlash = BooleanOptionItem.Create(RoleInfo, 12, "SilentKillerKillFlash", false, false);

            OptCanSeeNakama = BooleanOptionItem.Create(RoleInfo, 13, "SilentKillerCanSeeNakama", false, false);

            OptCanSabotage = BooleanOptionItem.Create(RoleInfo, 14, "MinimalistCanSabotage", false, false);

            OptCanReactor = BooleanOptionItem.Create(RoleInfo, 15, "SirentKillerCanReactor", false, false, OptCanSabotage);

            OptCanElec = BooleanOptionItem.Create(RoleInfo, 16, "SirentKillerCanElec", false, false, OptCanSabotage);

            RoleAddAddons.Create(RoleInfo, 20, DefaaultOn: true);
        }
        #endregion

        #region 変数

        public float CalculateKillCooldown() => KillCoolDown.GetFloat();
        public bool CanUseSabotageButton() => OptCanSabotage.GetBool();

        float StopTime;
        Vector2 HidePos;

        const float SyncInterval = 0.15f;
        float SyncTimer;

        Dictionary<byte, Vector2> RealPos = new();
        Dictionary<byte, float> StopTimer = new();
        Dictionary<byte, ushort> LastSid = new();
        Dictionary<byte, float> NextReveal = new();
        Dictionary<byte, bool> LocalVisible = new();
        List<byte> DeadedPlayers = new();

        #endregion

        public void OnMurderPlayerAsKiller(MurderInfo info)
        {
            if (OptFlash.GetBool())
            {
                Utils.AllPlayerKillFlash();
            }
        }
        public override bool OnSabotage(PlayerControl player, SystemTypes systemType)
        {
            if (player.PlayerId != Player.PlayerId) return true;
            if (!OptCanSabotage.GetBool()) return false;

            if (systemType is SystemTypes.Reactor or SystemTypes.Laboratory or SystemTypes.HeliSabotage)
            {
                return OptCanReactor.GetBool();
            }
            else if (systemType is SystemTypes.Electrical or SystemTypes.MushroomMixupSabotage)
            {
                return OptCanElec.GetBool();
            }
            return true;
        }
        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (!player.IsAlive())
            {
                if (RealPos.Count > 0) RestoreAll();
                return;
            }
            if (!GameStates.IsInTask) return;

            SyncTimer += Time.fixedDeltaTime;
            bool doSync = SyncTimer >= SyncInterval;
            if (doSync) SyncTimer = 0f;

            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                if (p.PlayerId == player.PlayerId || !p.IsAlive()) continue;
                if (OptCanSeeNakama.GetBool() && p.GetCustomRole().IsImpostor()) continue;

                byte id = p.PlayerId;
                var cur = (Vector2)p.transform.position;

                bool atHide = Vector2.Distance(cur, HidePos) < 2f;

                StopTimer.TryGetValue(id, out var timer);
                if (!atHide)
                {
                    if (RealPos.TryGetValue(id, out var last) && Vector2.Distance(last, cur) < 0.01f)
                        timer += Time.fixedDeltaTime;
                    else
                        timer = 0f;
                    RealPos[id] = cur;
                }
                else
                {
                    timer += Time.fixedDeltaTime;
                }
                StopTimer[id] = timer;

                if (Player.AmOwner)
                {
                    SetLocalVisible(p, timer >= StopTime || p.inVent);
                    continue;
                }

                if (!doSync) continue;
                
                if ((timer >= StopTime || DeadedPlayers.Contains(id)) && RealPos.TryGetValue(id, out var real))
                {
                    bool need;
                    if (Player.AmOwner)
                    {
                        need = atHide;
                    }
                    else
                    {
                        NextReveal.TryGetValue(id, out var t);
                        need = Time.time >= t;
                    }
                    if (need)
                    {
                        NextReveal[id] = Time.time + 1f;
                        SnapTo(p, real);
                    }
                }
                else
                {
                    // 移動中 → 隠す
                    NextReveal[id] = 0f;
                    SnapTo(p, HidePos);
                }
            }
        }

        void SnapTo(PlayerControl target, Vector2 position)
        {
            var net = target.NetTransform;
            ushort num = (ushort)(net.lastSequenceId + 2);
            if (LastSid.TryGetValue(target.PlayerId, out var last) && !SidGreater(num, last))
                num = (ushort)(last + 1);
            LastSid[target.PlayerId] = num;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(net.NetId, (byte)RpcCalls.SnapTo, SendOption.None, Player.GetClientId());
            NetHelpers.WriteVector2(position, writer);
            writer.Write(num);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        static bool SidGreater(ushort a, ushort b) => a != b && (ushort)(a - b) < 32768;

        void SetLocalVisible(PlayerControl target, bool visible)
        {
            if (LocalVisible.TryGetValue(target.PlayerId, out var now) && now == visible) return;
            LocalVisible[target.PlayerId] = visible;
            target.Visible = visible;
        }

        void RestoreAll()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                if (p.PlayerId == Player.PlayerId) continue;
                if (Player.AmOwner)
                {
                    p.Visible = true;
                    continue;
                }
                if (RealPos.TryGetValue(p.PlayerId, out var real))
                    SnapTo(p, real);
            }
            RealPos.Clear();
            StopTimer.Clear();
            NextReveal.Clear();
            LocalVisible.Clear();
            SyncTimer = 0f;
        }
        //OnDeadだと死体動かせないから遅いので、
        //OnDeadの前に呼ぶ。
        public void Restore(byte id)
        {
            var p = PlayerCatch.GetPlayerControl(id);
            if (Player.AmOwner)
            {
                p.Visible = true;       
            }
            if (RealPos.TryGetValue(p.PlayerId, out var real))
                SnapTo(p, real);
            NextReveal.Remove(id);
            DeadedPlayers.Add(id);

            //上限突破させてるからうまくいかなくても見えるはず
            StopTimer[id] = 70f;
        }
        public override void OnStartMeeting() => RestoreAll();
        public override void OnDestroy() => RestoreAll();
    }
}