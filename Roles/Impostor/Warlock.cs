//ウォーロックワンクリに。これでTORに近づいたかな...?
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MS.Internal.Xml.XPath;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Neutral;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor;

public sealed class Warlock : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Warlock),
            player => new Warlock(player),
            CustomRoles.Warlock,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            8100,
            SetUpOptionItem,
            "wa",
            OptionSort: (4, 4),
            from: From.TheOtherRoles
        );
    public Warlock(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        PhantomCooldown = OptionPhantomCooldown.GetFloat();
    }
    public override void OnDestroy()
    {
        CursedPlayer = null;
    }

    PlayerControl CursedPlayer;
    bool IsCursed;
    static OptionItem OptionPhantomCooldown;
    static float PhantomCooldown;

    enum OptionName
    {
        WarlcokPhantomCooldown,
    }
    static void SetUpOptionItem()
    {
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.WarlcokPhantomCooldown, new(0f, 60f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add()
    {
        CursedPlayer = null;
        IsCursed = false;
    }
    public override string GetAbilityButtonText() => GetString("WarlockCurseButtonText");

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = PhantomCooldown;
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!IsCursed)
        {
            PlayerControl nearest = null;
            AdjustKillCooldown = false;
            ResetCooldown = true;

            float minDist = Main.NormalOptions.KillDistance switch
            {
                0 => 1f,
                1 => 1.8f,
                _ => 2.5f
            };

            foreach (var target in PlayerCatch.AllAlivePlayerControls)
            {
                if (target.PlayerId == Player.PlayerId) continue;
                if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;

                float dist = Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition());
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = target;
                }
            }

            if (nearest != null)
            {
                IsCursed = true;
                CursedPlayer = nearest;
                Logger.Info($"{CursedPlayer}が呪われた", "Warlock");
            }
            else
            {
                Logger.Info("呪いの対象が見つかりませんでした", "Warlock");
            }

            return;
        }

        // 呪われている時：呪われたプレイヤー側で近傍を殺す処理を試みる
        if (CursedPlayer != null && CursedPlayer.IsAlive())
        {
            Vector2 cpPos = CursedPlayer.transform.position;
            Dictionary<PlayerControl, float> candidateList = new();
            foreach (PlayerControl candidatePC in PlayerCatch.AllAlivePlayerControls)
            {
                if (candidatePC == CursedPlayer) continue;
                if (candidatePC.Is(CustomRoles.King) || candidatePC.Is(CustomRoles.Autocrat)) continue;

                float distance = Vector2.Distance(cpPos, candidatePC.transform.position);
                candidateList.Add(candidatePC, distance);
                Logger.Info($"{candidatePC?.Data?.GetLogPlayerName()}の位置{distance}", "Warlock");
            }

            var nearest = candidateList.OrderBy(c => c.Value).FirstOrDefault();
            var killTarget = nearest.Key;

            if (killTarget != null)
            {
                // 重要修正:
                // - AttemptKiller を"ウォーロック本人 (Player)"にしてキル権限を持たせる
                // - AppearanceKiller を "呪われたプレイヤー (CursedPlayer)" にして見た目は呪い側で表示させる
                if (CustomRoleManager.OnCheckMurder(Player, killTarget, CursedPlayer, killTarget, true, false, 2))
                {
                    Logger.Info($"{killTarget.GetNameWithRole().RemoveHtmlTags()} was killed by cursed player {CursedPlayer.GetNameWithRole().RemoveHtmlTags()}", "Warlock");
                }
                else
                {
                    Logger.Info($"OnCheckMurder が false を返しました: attempt={Player?.GetNameWithRole()} appearance={CursedPlayer?.GetNameWithRole()} target={killTarget?.GetNameWithRole()}", "Warlock");
                }

                Player.SetKillCooldown();
                // 状態リセット
                CursedPlayer = null;
                IsCursed = false;

                Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
                if (killTarget.IsTeammate(Player))
                    Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            }
            else
            {
                // ターゲットなし: 状態解除
                CursedPlayer = null;
                IsCursed = false;
                Logger.Info("呪い実行時の対象が見つかりませんでした。呪いを解除します。", "Warlock");
            }
        }
        else
        {
            // 呪われているが対象が存在しない/死亡している場合はクリア
            CursedPlayer = null;
            IsCursed = false;
            AdjustKillCooldown = false;
            ResetCooldown = null;
            Logger.Info("呪いが解除されました", "Warlock");
        }
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        CursedPlayer = null;
        IsCursed = false;
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Warlock_Ability";
        return true;
    }
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 5, 0, 0);
        var sp1 = new Achievement(RoleInfo, 1, 1, 2, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, sp1);
    }
}