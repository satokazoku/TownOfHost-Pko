using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class HateKiller : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(HateKiller),
            player => new HateKiller(player),
            CustomRoles.HateKiller,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            127000,
            SetupOptionItem,
            "htk",
            "#ff1919",
            (3, 7),
            assignInfo: new RoleAssignInfo(CustomRoles.HateKiller, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );
    public HateKiller(PlayerControl player) : base(RoleInfo, player)
    {
        currentKillCooldown = OptionKillCooldown.GetFloat();
        MinimumKillCooldown = OptionMinimumKillCool.GetFloat();
        ImpostorVotes = 0;
        selfvote = 0;
    }
    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionDecreaseKillCool;
    private static OptionItem OptionMinimumKillCool;
    private static OptionItem OptionCountImpostorVotes;
    private static OptionItem OptionCountSelfVotes;
    float currentKillCooldown;
    float MinimumKillCooldown;
    static int ImpostorVotes;
    static int selfvote;

    private enum OptionName
    {
        HateKillerDecreaseKillCool,
        HateKillerMinimumKillCool,
        HateKillerCountImpostorVotes,
        HateKillerCountSelfVotes
    }

    public float CalculateKillCooldown() => currentKillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 35f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDecreaseKillCool = FloatOptionItem.Create(RoleInfo, 11, OptionName.HateKillerDecreaseKillCool, new(0.5f, 180f, 0.5f), 4f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMinimumKillCool = FloatOptionItem.Create(RoleInfo, 12, OptionName.HateKillerMinimumKillCool, new(0.5f, 180f, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCountImpostorVotes = BooleanOptionItem.Create(RoleInfo, 13, OptionName.HateKillerCountImpostorVotes, false, false);
        OptionCountSelfVotes = BooleanOptionItem.Create(RoleInfo, 14, OptionName.HateKillerCountSelfVotes, false, false);
    }

    public override void Add()
    {
        currentKillCooldown = OptionKillCooldown.GetFloat();
        ImpostorVotes = 0;
    }

    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
    {
        if (!Player.IsAlive())
        {
            return true;
        }
        vote.TryGetValue(Player.PlayerId, out var count);
        if (!OptionCountImpostorVotes.GetBool())
        {
            count = count - ImpostorVotes;
        }
        if (!OptionCountSelfVotes.GetBool())
        {
            count = count - selfvote;
        }
        float Decrease = count * OptionDecreaseKillCool.GetFloat();
        currentKillCooldown = OptionKillCooldown.GetFloat() - Decrease;
        if (currentKillCooldown < MinimumKillCooldown)
        {
            currentKillCooldown = MinimumKillCooldown;
        }
        if (currentKillCooldown < 0.1f)
        {
            currentKillCooldown = 0.1f;
        }
        return true;
    }
    public override void AfterMeetingTasks()
    {
        ImpostorVotes = 0;
        selfvote = 0;
    }

    /// <summary>
    /// インポスターがヘイトキラーに投票した数を増やす(自投票除く)
    /// </summary>
    public static void AddImpVote()
    {
        ++ImpostorVotes;
    }
    /// <summary>
    /// ヘイトキラーが自投票した数を増やす
    /// </summary>
    public static void AddSelfVote()
    {
        ++selfvote;
    }
}
