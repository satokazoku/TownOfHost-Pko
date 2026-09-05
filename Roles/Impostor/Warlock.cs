using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static Sentry.MeasurementUnit;

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
    }
    public override void OnDestroy()
    {
        CursedPlayer = null;
    }

    PlayerControl CursedPlayer;
    public static OptionItem Optiondouki;
    public static OptionItem OptionAbilityCoolDown;
    public static OptionItem OptionCantMovetime;
    public static OptionItem OptionCantmove;
    bool IsCursed;
    bool IsCantMove;
    Vector2 pos;
    enum OptionName
    {
        WarlockDouki,
        WarlockCantMovetime,
        WarlockKoutyoku,
    }


    private static void SetUpOptionItem()
    {
        OptionAbilityCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCantmove = BooleanOptionItem.Create(RoleInfo, 11, OptionName.WarlockKoutyoku, true, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCantMovetime = FloatOptionItem.Create(RoleInfo, 12, OptionName.WarlockCantMovetime, OptionBaseCoolTime, 5f, false, OptionCantmove)
            .SetValueFormat(OptionFormat.Seconds);
        Optiondouki = BooleanOptionItem.Create(RoleInfo, 13, OptionName.WarlockDouki, true, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add()
    {
        CursedPlayer = null;
        IsCursed = false;
        IsCantMove = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (IsCantMove && OptionCantmove.GetBool())
        {
            Player.RpcSnapToForced(pos);
        }
    }

    public override string GetAbilityButtonText() => GetString("WarlockCurseButtonText");

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = IsCursed ? 0.1f : OptionAbilityCoolDown.GetFloat();
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public override void AfterMeetingTasks()
    {
        IsCantMove = false;
    }


    bool IUsePhantomButton.IsresetAfterKill => Optiondouki.GetBool();
    bool IUsePhantomButton.IsPhantomRole => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = Optiondouki.GetBool();

        if (IsCursed)
        {
            if (CursedPlayer != null && CursedPlayer.IsAlive() && AmongUsClient.Instance.AmHost)
            {
                Vector2 cpPos = CursedPlayer.transform.position;
                Dictionary<PlayerControl, float> candidateList = new();
                float distance;
                foreach (PlayerControl candidatePC in PlayerCatch.AllAlivePlayerControls)
                {
                    if (candidatePC != CursedPlayer && !candidatePC.Is(CustomRoles.King) && !candidatePC.Is(CustomRoles.Autocrat))
                    {
                        distance = Vector2.Distance(cpPos, candidatePC.transform.position);
                        candidateList.Add(candidatePC, distance);
                        Logger.Info($"{candidatePC?.Data?.GetLogPlayerName()}の位置{distance}", "Warlock");
                    }
                }
                var nearest = candidateList.OrderBy(c => c.Value).FirstOrDefault();
                var killTarget = nearest.Key;
                if (CustomRoleManager.OnCheckMurder(Player, killTarget, CursedPlayer, killTarget, true, false, 2))
                {
                    Logger.Info($"{killTarget.GetNameWithRole().RemoveHtmlTags()}was killed", "Warlock");
                    RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
                }
                CursedPlayer = null;
                Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
                if (killTarget.IsTeammate(Player))
                {
                    Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
                }
                IsCantMove = true;
                pos = Player.transform.position;
            }
            IsCursed = false;
            ResetCooldown = true;
            _ = new LateTask(() =>
            {
                IsCantMove = false;
            }, OptionCantMovetime.GetFloat(), "Warlock_koutyoku", true);
        }
        else
        {
            ResetCooldown = false;

            CursedPlayer = Player.GetKillTarget(true);
            if (CursedPlayer != null)
            {
                IsCursed = true;
            }
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
