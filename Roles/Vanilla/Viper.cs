using System.Collections.Generic;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Vanilla;

public sealed class Viper : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Viper),
            player => new Viper(player),
            RoleTypes.Viper,
            SetUpCustomOption,
            from: From.AmongUs
        );
    public Viper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        ViperDissolveTime = OptViperDissolveTime.GetFloat();
        killcool = OptKillcool.GetFloat();
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        ViperKilledPlayers.Clear();
    }
    static OptionItem OptKillcool; static float killcool;
    public static OptionItem OptViperDissolveTime; public static float ViperDissolveTime;
    public static OptionItem OptShowMark;
    static List<byte> MeltedPlayers = new();
    static Dictionary<byte, float> ViperKilledPlayers = new();
    enum Op
    {
        ViperShowMark
    }

    public static void SetUpCustomOption()
    {
        OptKillcool = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.KillCooldown, OptionBaseCoolTime, 30, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptViperDissolveTime = FloatOptionItem.Create(RoleInfo, 5, StringNames.ViperDissolveTime, new(0, 180, 1), 15, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptShowMark = BooleanOptionItem.Create(RoleInfo, 6, Op.ViperShowMark, true, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ViperDissolveTime = ViperDissolveTime;
    }
    float IKiller.CalculateKillCooldown() => killcool;
    bool IKiller.OverrideKillButtonText(out string text)
    {
        text = GetString(StringNames.ViperAbility);
        return true;
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        ViperKilledPlayers.Add(info.AttemptTarget.PlayerId, ViperDissolveTime);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        foreach (var (pid, timer) in ViperKilledPlayers)
        {
            if (timer < 0)
            {
                MeltedPlayers.Add(pid);
                ViperKilledPlayers.Remove(pid);
            }
            else
            {
                ViperKilledPlayers[pid] -= Time.fixedDeltaTime;
            }
        }
    }
    public override void AfterMeetingTasks()
    {
        ViperKilledPlayers.Clear();
        MeltedPlayers.Clear();
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!OptShowMark.GetBool()) return "";
        if (isForMeeting)
        {
            //if (MeltedPlayers.Contains(seen.PlayerId)) return $"<color={RoleInfo.RoleColorCode}>×</color>";
            if (MeltedPlayers.Contains(seen.PlayerId)) return $"<color=#6f4204>×</color>";
        }
        return "";
    }
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 10, 0, 0);
        achievements.Add(0, n1);
    }
}