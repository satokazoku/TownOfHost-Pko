using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Madmate;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Bomber : RoleBase, IImpostor, IUsePhantomButton
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Bomber),
                player => new Bomber(player),
                CustomRoles.Bomber,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                2600,
                SetupOptionItem,
                "bb",
                OptionSort: (3, 1),
                Desc: () => string.Format(GetString("BomberDesc"), OptionBomberExplosion.GetInt(), OptionMinKillDelay.GetFloat(), OptionMaxKillDelay.GetFloat(), OptionKillChance.GetFloat()),
                from: From.ExtremeRoles
            );
        public Bomber(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            Blastrange = OptionBlastrange.GetFloat();
            BomberExplosionPlayers.Clear();
            BomberExplosion = OptionBomberExplosion.GetInt();
            Cooldown = OptionCooldown.GetFloat();
            maxbomb = 0;
            IsDetectioned = false;
            Bombtime = 0f;
            IsInstallation = false;
        }

        static OptionItem OptionMinKillDelay;
        static OptionItem OptionBlastrange;
        static OptionItem OptionBomberExplosion;
        static OptionItem OptionCooldown;
        static OptionItem OptionKillChance;
        static OptionItem OptionInstallationTime;
        static OptionItem OptionMaxKillDelay;
        enum OptionName
        {
            BomberKillDelay,
            blastrange,
            BomberKillChace,
            BomberInstallationTime,
            BomberMaxKillDelay
        }

        static float Blastrange;
        static float Cooldown;
        int BomberExplosion;
        float KillDelay;
        public static bool IsDetectioned;
        public bool CanBeLastImpostor { get; } = false;
        Dictionary<byte, float> BomberExplosionPlayers = new(14);
        PlayerControl Bombtarget;
        float Bombtime;
        bool IsInstallation;

        private static void SetupOptionItem()
        {
            OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0.5f, 180f, 0.5f), 45f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionInstallationTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.BomberInstallationTime, new(0f, 30f, 0.5f), 2.5f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionBomberExplosion = IntegerOptionItem.Create(RoleInfo, 12, GeneralOption.OptionCount, new(1, 14, 1), 2, false)
                .SetValueFormat (OptionFormat.Times);
            OptionBlastrange = FloatOptionItem.Create(RoleInfo, 13, OptionName.blastrange, new(0.5f, 30f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Multiplier);
            OptionKillChance = FloatOptionItem.Create(RoleInfo, 14, OptionName.BomberKillChace, new(0f, 100f, 2.5f), 50f, false).SetValueFormat(OptionFormat.Percent);
            OptionMinKillDelay = FloatOptionItem.Create(RoleInfo, 15, OptionName.BomberKillDelay, new(0f, 180f, 1f), 10f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionMaxKillDelay = FloatOptionItem.Create(RoleInfo, 16, OptionName.BomberMaxKillDelay, new(0f, 180f, 1f), 20f, false)
                .SetValueFormat(OptionFormat.Seconds);

        }

        private void SendRPC()
        {
            using var sender = CreateSender();
            sender.Writer.Write(BomberExplosion);
            sender.Writer.Write(maxbomb);
            sender.Writer.Write(IsDetectioned);
            sender.Writer.Write(IsInstallation);
            sender.Writer.Write(KillDelay);
        }
        public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = false;
            ResetCooldown = false;

            if (BomberExplosion <= 0) return;

            Bombtarget = Player.GetKillTarget(true);
            if (Bombtarget == null) return;
            if (BomberExplosionPlayers.ContainsKey(Bombtarget.PlayerId))
            Bombtime = 0f;
            IsInstallation = true;
        }
        public void InstallationBomb()
        {
            Logger.Info($"{Player?.Data?.GetLogPlayerName() ?? "???"} => {Bombtarget?.Data?.GetLogPlayerName() ?? "失敗"}", "Bomber");
            if (Bombtarget == null || BomberExplosionPlayers.ContainsKey(Bombtarget?.PlayerId ?? byte.MaxValue)) return;
            if (Bombtarget.Is(CustomRoles.Madpsycho))
            {
                if (Madpsycho.CanPsycho)
                {
                    PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = Madpsycho.deathReasons[Madpsycho.OptionDeathReason.GetValue()];
                    Bombtarget.RpcMurderPlayer(Player);
                    return;
                }
            }
            if (!BomberExplosionPlayers.TryAdd(Bombtarget.PlayerId, 0f)) return;
            SendAddRPC(Bombtarget.PlayerId);
            Jizo.BomCheckroom(Player.GetPlainShipRoom(), Player);
            IsInstallation = false;
            BomberExplosion--;
            SendRPC();
            ResetCooldown();
            Player.SetKillCooldown(target: Bombtarget);
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
            KillDelay = Random.Range(OptionMinKillDelay.GetFloat(), OptionMaxKillDelay.GetFloat());
        }
        public void ResetCooldown()
        {
            AURoleOptions.PhantomCooldown = BomberExplosion <= 0 ? 200f : Cooldown;
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(log: false);
            Player.SyncSettings();
        }
        bool IUsePhantomButton.IsPhantomRole => BomberExplosion > 0;
        public override string GetProgressText(bool comms = false, bool gamelog = false) => Utils.ColorString(0 < BomberExplosion ? Color.red : Color.gray, $"({BomberExplosion})");
        public override void OnFixedUpdate(PlayerControl _)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !Player.IsAlive()) return;

            foreach (var (targetId, timer) in BomberExplosionPlayers.ToArray())
            {
                if (KillDelay <= timer)
                {
                    var target = PlayerCatch.GetPlayerById(targetId);
                    if (target.IsAlive())
                    {
                        if (IsDetectioned)
                        {
                            Jizo.BomKilled = true;
                        }
                        var pos = target.transform.position;
                        var count = 0;
                        foreach (var target2 in PlayerCatch.AllAlivePlayerControls)
                        {
                            System.Random rand = new();
                            var dis = Vector2.Distance(pos, target2.transform.position);
                            if (dis > Blastrange) continue;
                            if (target2.Is(CustomRoleTypes.Impostor) && target2.PlayerId != Player.PlayerId) continue;
                            if (rand.Next(100) < OptionKillChance.GetFloat())
                            {
                                if (CustomRoleManager.OnCheckMurder(Player, target2, target2, target2, true, true, 1, deathReason: CustomDeathReason.Bombed))
                                {
                                    count++;
                                    RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
                                    Logger.Info($"{target2.name}を爆発させました。", "bomber");
                                }
                            }
                            if (maxbomb <= count) maxbomb = count;
                        }
                    }
                    BomberExplosionPlayers.Remove(targetId);
                    SendRemoveRPC(targetId);
                }
                else
                {
                    BomberExplosionPlayers[targetId] += Time.fixedDeltaTime;
                }
            }
            if (IsInstallation)
            {
                if (!Bombtarget.IsAlive())
                {
                    IsInstallation = false;
                }
                if (OptionInstallationTime.GetFloat() - Bombtime <= 0f)
                {
                    InstallationBomb();
                }
                else
                {
                    float dis;
                    dis = Vector2.Distance(Player.transform.position, Bombtarget.transform.position);
                    var Distance = 0f;
                    switch (AURoleOptions.KillDistance)
                    {
                        case 0:
                            Distance = 1f;
                            break;
                        case 1:
                            Distance = 1.75f;
                            break;
                        case 2:
                            Distance = 2.5f;
                            break;
                    }
                    if (dis <= Distance)
                    {
                        Bombtime += Time.fixedDeltaTime;
                        AURoleOptions.PhantomCooldown = OptionInstallationTime.GetFloat() - Bombtime;
                        Player.MarkDirtySettings();
                        Player.RpcResetAbilityCooldown(log: false);
                        if (OptionInstallationTime.GetFloat() - Bombtime <= 0f)
                        {
                            InstallationBomb();
                        }
                    }
                    else
                    {
                        IsInstallation = false;
                        AURoleOptions.PhantomCooldown = 0.1f;
                        Player.MarkDirtySettings();
                        Player.RpcResetAbilityCooldown(log: false);
                        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
                        Player.SyncSettings();
                    }
                }
            }
        }
        public override bool OverrideAbilityButton(out string text)
        {
            text = "Bomber_Ability";
            return true;
        }
        public override string GetAbilityButtonText()
            => GetString("BomberAbilitytext");
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = BomberExplosion <= 0 ? 200f : Cooldown;
        }

        public override void AfterMeetingTasks()
        {
            IsDetectioned = false;
        }

        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;
            if (seen.PlayerId != seer.PlayerId || isForMeeting || BomberExplosion <= 0 || !Player.IsAlive()) return "";

            if (isForHud) return GetString("PhantomButtonKilltargetLowertext");
            return $"<size=50%>{GetString("PhantomButtonKilltargetLowertext")}</size>";
        }
        public override void CheckWinner(GameOverReason reason)
        {
            if (3 <= maxbomb) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            if (5 <= maxbomb) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
        }
        int maxbomb;
        public static Dictionary<int, Achievement> achievements = new();
        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            Jizo.Checkroom(Player.GetPlainShipRoom(), Player);
        }
        private void SendAddRPC(byte targetId)
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)1); // typeId: 追加
            sender.Writer.Write(targetId);
        }
        private void SendRemoveRPC(byte targetId)
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)2); // typeId: 削除
            sender.Writer.Write(targetId);
        }

        public override void ReceiveRPC(MessageReader reader)
        {
            var typeId = reader.ReadByte();
            switch (typeId)
            {
                case 0: 
                    BomberExplosion = reader.ReadInt32();
                    maxbomb = reader.ReadInt32();
                    IsDetectioned = reader.ReadBoolean();
                    IsInstallation = reader.ReadBoolean();
                    KillDelay = reader.ReadSingle();
                    break;
                case 1:
                    BomberExplosionPlayers.TryAdd(reader.ReadByte(), 0f);
                    break;
                case 2:
                    BomberExplosionPlayers.Remove(reader.ReadByte());
                    break;
            }
        }
        [Attributes.PluginModuleInitializer]
        public static void Load()
        {
            var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
            var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
            achievements.Add(0, n1);
            achievements.Add(1, l1);
        }
    }
}
