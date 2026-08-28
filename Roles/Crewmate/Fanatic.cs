using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static TownOfHost.Roles.Core.Interfaces.ISchrodingerCatOwner;


namespace TownOfHost.Roles.Crewmate;

public sealed class Fanatic : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Fanatic),
            player => new Fanatic(player),
            CustomRoles.Fanatic,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            38900,
            SetupOptionItem,
            "fnt",
            "#ff1919",
            (5, 2)
        );
    public Fanatic(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
        OmoikomiPlayers = 0;
        omoikomitarget1 = null;
        omoikomitarget2 = null;
        omoikomitarget3 = null;
        CantOmoikomiRoles = OptionCantOmoikomiRoles.GetNowRoleValue();
    }
    static List<CustomRoles> CantOmoikomiRoles;

    private static OptionItem OptionCanVent;
    public static FilterOptionItem OptionOmoikomiRole;
    public static OptionItem OptionOmoikomi;
    public static OptionItem OptionOmoikomiPick1;
    public static OptionItem OptionOmoikomiPick2;
    public static OptionItem OptionOmoikomiPick3;
    public static OptionItem OptionCanJikaku;
    public static AssignOptionItem OptionCantOmoikomiRoles;
    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    static int OmoikomiPlayers;
    static PlayerControl omoikomitarget1 = null;
    static PlayerControl omoikomitarget2 = null;
    static PlayerControl omoikomitarget3 = null;
    private bool IsCanOmoikomiRoles(CustomRoles role)
    => !CantOmoikomiRoles.Contains(role);

    enum OptionName
    {
        FanaticOmoikomi,
        FanaticOmoikomiRole,
        FanaticOmoikomiPick1,
        FanaticOmoikomiPick2,
        FanaticOmoikomiPick3,
        FanaticCanJikaku,
        FanaticCantOmoikomiroles
    }
    public static readonly CustomRoleTypes[] PickTypes =
    {
        CustomRoleTypes.Impostor,
        CustomRoleTypes.Crewmate,
        CustomRoleTypes.Neutral,
    };
    public static void SetupOptionItem()
    {
        var PickRoleTypesString = PickTypes.Select(x => x.ToString()).ToArray();

        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, true, false);
        OptionOmoikomiRole = FilterOptionItem.Create(RoleInfo, 11, OptionName.FanaticOmoikomiRole, 87, false, null, false, true, false, false, false, () => InvalidRoles());
        OptionOmoikomi = BooleanOptionItem.Create(RoleInfo, 12, OptionName.FanaticOmoikomi, true, false);
        OptionOmoikomiPick1 = StringOptionItem.Create(RoleInfo, 13, OptionName.FanaticOmoikomiPick1, PickRoleTypesString, 1, false, OptionOmoikomi);
        OptionOmoikomiPick2 = StringOptionItem.Create(RoleInfo, 14, OptionName.FanaticOmoikomiPick2, PickRoleTypesString, 1, false, OptionOmoikomi);
        OptionOmoikomiPick3 = StringOptionItem.Create(RoleInfo, 15, OptionName.FanaticOmoikomiPick3, PickRoleTypesString, 1, false, OptionOmoikomi);
        OptionCantOmoikomiRoles = AssignOptionItem.Create(RoleInfo, 16, OptionName.FanaticCantOmoikomiroles, 0, false, OptionOmoikomi, imp: true, mad: true, crew: true, neu: true);
        RoleAddAddons.Create(RoleInfo, 20);
        OverrideTasksData.Create(RoleInfo, 21);
    }
    static CustomRoles[] InvalidRoles()
    {
        return new[]
        {
        CustomRoles.Braid,
        CustomRoles.MadWare,
        CustomRoles.MadAvenger,
        CustomRoles.MadSuicide,
        CustomRoles.MadChanger,
        CustomRoles.MadTracker,
        CustomRoles.MadTeller,
        CustomRoles.BlackSanta,
        CustomRoles.MadHacker,
        CustomRoles.MadBetrayer,
        CustomRoles.Nue,
        CustomRoles.MadSheriff,
        CustomRoles.MadReduced,
        };
    }

    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => OptionOmoikomiRole.GetRole();
    public override CustomRoles Misidentify() => CanseeTrueRole() ? CustomRoles.Fanatic : OptionOmoikomiRole.GetRole();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Options.MadmateVentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = Options.MadmateVentMaxTime.GetFloat();
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => Options.MadmateCanMovedByVent.GetBool();

    public override void Add()
    {
        _ = new LateTask(() => {
            omoikomitarget1 = null;
            omoikomitarget2 = null;
            omoikomitarget3 = null;
            OverrideNamecolor();
        }, 2f, "assigng_wait", true);
    }

    private bool KnowsImpostor()
    {
        return MyTaskState.HasCompletedEnoughCountOfTasks(MadSnitch.OptionTaskTrigger.GetInt());
    }

    private bool CanseeTrueRole()
    {
        if (OptionCanJikaku == null || !OptionCanJikaku.GetBool()) return false;
        if (Player == null) return false;
        if (omoikomitarget1 == null || omoikomitarget2 == null || omoikomitarget3 == null) return false;

        return !omoikomitarget1.IsAlive() && !omoikomitarget2.IsAlive() && !omoikomitarget3.IsAlive();
    }
    private void OverrideNamecolor()
    {
        if (OmoikomiPlayers > 0) return;
        if (!OptionOmoikomi.GetBool())
        {
            return;
        }
        var pickType1 = PickTypes[OptionOmoikomiPick1.GetValue()];
        var pickType2 = PickTypes[OptionOmoikomiPick2.GetValue()];
        var pickType3 = PickTypes[OptionOmoikomiPick3.GetValue()];

        // 1人目
        var omoikomiPlayer1 = PlayerCatch.AllAlivePlayerControls
            .Where(pc =>
                pc != null &&
                pc != Player &&
                pc.IsAlive() &&
                pc.PlayerId != omoikomitarget2.PlayerId &&
                pc.PlayerId != omoikomitarget3.PlayerId &&
                IsCanOmoikomiRoles(pc.GetCustomRole()) &&
                pc.Is(pickType1)
            )
            .ToList();
        if (omoikomiPlayer1.Count == 0)
        {
            omoikomiPlayer1 = PlayerCatch.AllAlivePlayerControls
            .Where(pc =>
                pc != null &&
                pc != Player &&
                pc.IsAlive() &&
                pc.PlayerId != omoikomitarget2.PlayerId &&
                pc.PlayerId != omoikomitarget3.PlayerId &&
                IsCanOmoikomiRoles(pc.GetCustomRole()) &&
                pc.Is(CustomRoleTypes.Neutral)
            )
            .ToList();
            if (omoikomiPlayer1.Count == 0)
            {
                omoikomiPlayer1 = PlayerCatch.AllAlivePlayerControls
                .Where(pc =>
                    pc != null &&
                    pc != Player &&
                    pc.IsAlive() &&
                    pc.PlayerId != omoikomitarget2.PlayerId &&
                    pc.PlayerId != omoikomitarget3.PlayerId &&
                    IsCanOmoikomiRoles(pc.GetCustomRole())
                )
                .ToList();
            }
        }
        if (omoikomiPlayer1.Count != 0)
        {
            omoikomitarget1 = PickRandom(omoikomiPlayer1, 1).FirstOrDefault();
        }
        // 2人目
        var omoikomiPlayer2 = PlayerCatch.AllAlivePlayerControls
        .Where(pc =>
            pc != null &&
            pc != Player &&
            pc != omoikomitarget1 &&
            pc.IsAlive() &&
            pc.PlayerId != omoikomitarget1.PlayerId &&
            pc.PlayerId != omoikomitarget3.PlayerId &&
            IsCanOmoikomiRoles(pc.GetCustomRole()) &&
            pc.Is(pickType2)
        )
        .ToList();
        if (omoikomiPlayer2.Count == 0)
        {
            omoikomiPlayer2 = PlayerCatch.AllAlivePlayerControls
                    .Where(pc =>
                        pc != null &&
                        pc != Player &&
                        pc != omoikomitarget1 &&
                        pc.IsAlive() &&
                        pc.PlayerId != omoikomitarget1.PlayerId &&
                        pc.PlayerId != omoikomitarget3.PlayerId &&
                        IsCanOmoikomiRoles(pc.GetCustomRole()) &&
                        pc.Is(CustomRoleTypes.Neutral)
                    )
                    .ToList();
            if (omoikomiPlayer2.Count == 0)
            {
                omoikomiPlayer2 = PlayerCatch.AllAlivePlayerControls
                        .Where(pc =>
                            pc != null &&
                            pc != Player &&
                            pc != omoikomitarget1 &&
                            pc.IsAlive() &&
                            pc.PlayerId != omoikomitarget1.PlayerId &&
                            pc.PlayerId != omoikomitarget3.PlayerId &&
                            IsCanOmoikomiRoles(pc.GetCustomRole())
                        )
                        .ToList();
            }

        }

        if (omoikomiPlayer2.Count != 0)
        {
            omoikomitarget2 = PickRandom(omoikomiPlayer2, 1).FirstOrDefault();
        }

        // 3人目
        var omoikomiPlayer3 = PlayerCatch.AllAlivePlayerControls
            .Where(pc =>
                pc != null &&
                pc != Player &&
                pc != omoikomitarget1 &&
                pc != omoikomitarget2 &&
                pc.IsAlive() &&
                pc.PlayerId != omoikomitarget1.PlayerId &&
                pc.PlayerId != omoikomitarget2.PlayerId &&
                IsCanOmoikomiRoles(pc.GetCustomRole()) &&
                pc.Is(pickType3)
            )
            .ToList();
        if (omoikomiPlayer3.Count == 0)
        {
            omoikomiPlayer3 = PlayerCatch.AllAlivePlayerControls
            .Where(pc =>
                pc != null &&
                pc != Player &&
                pc.IsAlive() &&
                pc.PlayerId != omoikomitarget1.PlayerId &&
                pc.PlayerId != omoikomitarget2.PlayerId &&
                IsCanOmoikomiRoles(pc.GetCustomRole()) &&
                pc.Is(CustomRoleTypes.Neutral)
            )
            .ToList();
            if (omoikomiPlayer3.Count == 0)
            {
                omoikomiPlayer3 = PlayerCatch.AllAlivePlayerControls
                .Where(pc =>
                    pc != null &&
                    pc != Player &&
                    pc.IsAlive() &&
                    pc.PlayerId != omoikomitarget1.PlayerId &&
                    pc.PlayerId != omoikomitarget2.PlayerId &&
                    IsCanOmoikomiRoles(pc.GetCustomRole())
                )
                .ToList();
            }
        }

        if (omoikomiPlayer3.Count != 0)
        {
            omoikomitarget3 = PickRandom(omoikomiPlayer3, 1).FirstOrDefault();
        }
        Logger.Info($"インポスター数：{Main.NormalOptions.NumImpostors}人", "Fanatic");
        //マッドスニッチは個別処理だけど思い込む人までは決めておく
        if (OptionOmoikomiRole.GetRole() == CustomRoles.MadSnitch)
        {
            return;
        }
        Omoikomi();
    }
    private void Omoikomi()
    {
        if (OmoikomiPlayers > 0) return;

        if (Main.NormalOptions.NumImpostors == 1)
        {
            int value = UnityEngine.Random.Range(1, 4);
            if (value == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget1.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
                return;
            }
            else if (value == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget2.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
                return;
            }
            else if (value == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget3.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
                return;
            }
            return;
        }
        if (Main.NormalOptions.NumImpostors == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
        {
            int value = UnityEngine.Random.Range(1, 4);
            if (value == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget1.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
            }
            else if (value == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget2.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
            }
            else if (value == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                NameColorManager.Add(Player.PlayerId, omoikomitarget3.PlayerId, "#ff1919");
                ++OmoikomiPlayers;
            }
            if (value == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                int value2 = UnityEngine.Random.Range(1, 3);
                if (value2 == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget2.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                else if (value2 == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget3.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                return;
            }
            else if (value == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                int value2 = UnityEngine.Random.Range(1, 3);
                if (value2 == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget1.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                else if (value2 == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget3.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                return;
            }
            else if (value == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
            {
                int value2 = UnityEngine.Random.Range(1, 3);
                if (value2 == 1 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget1.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                else if (value2 == 2 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
                {
                    NameColorManager.Add(Player.PlayerId, omoikomitarget2.PlayerId, "#ff1919");
                    ++OmoikomiPlayers;
                    return;
                }
                return;
            }
        }
        if (Main.NormalOptions.NumImpostors == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
        {
            NameColorManager.Add(Player.PlayerId, omoikomitarget1.PlayerId, "#ff1919");
            ++OmoikomiPlayers;
        }
        if (Main.NormalOptions.NumImpostors == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
        {
            NameColorManager.Add(Player.PlayerId, omoikomitarget2.PlayerId, "#ff1919");
            ++OmoikomiPlayers;
        }
        if (Main.NormalOptions.NumImpostors == 3 && OmoikomiPlayers < Main.NormalOptions.NumImpostors)
        {
            NameColorManager.Add(Player.PlayerId, omoikomitarget3.PlayerId, "#ff1919");
            ++OmoikomiPlayers;
        }
    }
    private void CheckAndAddNameColorToImpostors()
    {
        if (!OptionOmoikomi.GetBool() || OptionOmoikomiRole.GetRole() != CustomRoles.MadSnitch || !KnowsImpostor())
        {
            return;
        }
        Omoikomi();
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (!OptionOmoikomi.GetBool() || OptionOmoikomiRole.GetRole() != CustomRoles.MadSnitch)
        {
            return true;
        }
        CheckAndAddNameColorToImpostors();
        return true;
    }
    private static List<PlayerControl> PickRandom(List<PlayerControl> source, int count)
    {
        var pool = source.ToList();
        var result = new List<PlayerControl>();
        var random = IRandom.Instance;

        while (result.Count < count && pool.Count > 0)
        {
            var index = random.Next(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
    public bool UpdateReactorSystem(ReactorSystemType reactorSystem, byte amount)
    {
        return false;
    }

    public bool UpdateHeliSabotageSystem(HeliSabotageSystem heliSabotageSystem, byte amount)
    {
        return false;
    }

    public bool UpdateLifeSuppSystem(LifeSuppSystemType lifeSuppSystem, byte amount)
    {
        return false;
    }

    public bool UpdateHudOverrideSystem(HudOverrideSystemType hudOverrideSystem, byte amount)
    {
        return false;
    }

    public bool UpdateHqHudSystem(HqHudSystemType hqHudSystemType, byte amount)
    {
        return false;
    }

    public bool UpdateSwitchSystem(SwitchSystem switchSystem, byte amount)
    {
        return false;
    }

    public bool UpdateDoorsSystem(DoorsSystemType doorsSystem, byte amount)
    {
        return true;
    }
}
