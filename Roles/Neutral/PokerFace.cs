using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Neutral;
public sealed class PokerFace : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(PokerFace),
            player => new PokerFace(player),
            CustomRoles.PokerFace,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            270600,
            SetupOptionItem,
            "pf",
            "#72d16b",
            (7, 3),
            true,
            tab: TabGroup.Combinations,
            countType: CountTypes.None,
            from: From.SuperNewRoles,
            assignInfo: new RoleAssignInfo(CustomRoles.PokerFace, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = new CustomRoles[3]
                {
                    CustomRoles.PokerFace,
                    CustomRoles.PokerFace,
                    CustomRoles.PokerFace
                }
            },
            combination: CombinationRoles.PokerFace
        );

    public PokerFace(PlayerControl player)
        : base(RoleInfo, player, () => HasTask.False)
    {
    }

    static OptionItem OptionAdditionalWin;
    static OptionItem OptionCanVent;

    static bool AdditionalWin;
    static bool CanVent;

    enum OptionName
    {
        PokerFaceAdditionalWin,
        PokerFaceAddOns,
    }

    private static void SetupOptionItem()
    {
        OptionAdditionalWin = BooleanOptionItem.Create(
            RoleInfo, 10, OptionName.PokerFaceAdditionalWin, false, false);

        SoloWinOption.Create(RoleInfo, 11, defo: 15);

        OptionCanVent = BooleanOptionItem.Create(
            RoleInfo, 12, GeneralOption.CanVent, false, false);
        RoleAddAddons.Create(RoleInfo, 14, NeutralKiller: false);
    }

    public override void Add()
    {
        AdditionalWin = OptionAdditionalWin.GetBool();
        CanVent = OptionCanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (!CanVent) return;
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => CanVent;

    public override void OverrideDisplayRoleNameAsSeer(
        PlayerControl seen, ref bool enabled, ref Color roleColor,
        ref string roleText, ref bool addon)
    {
        if (seen.PlayerId != Player.PlayerId && seen.Is(CustomRoles.PokerFace))
            enabled = true;
    }

    public override void OverrideDisplayRoleNameAsSeen(
        PlayerControl seer, ref bool enabled, ref Color roleColor,
        ref string roleText, ref bool addon)
    {
        if (seer.PlayerId != Player.PlayerId && seer.Is(CustomRoles.PokerFace))
            enabled = true;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer)) return "";
        if (seen.PlayerId == seer.PlayerId) return "";
        if (!seen.Is(CustomRoles.PokerFace)) return "";
        return seen.IsAlive()
            ? $" <color={RoleInfo.RoleColorCode}>♦</color>"
            : " <color=#888888>×</color>";
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!Player.IsAlive()) return;

        var allPF = AllPlayerControls.Where(pc => pc.Is(CustomRoles.PokerFace)).ToList();
        int groupSize = allPF.Count;

        if (groupSize < 3) return;

        int aliveCount = allPF.Count(pc => pc.IsAlive());
        if (aliveCount != 1) return;

        if (AdditionalWin)
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }
        else
        {
            if (CustomWinnerHolder.ResetAndSetAndChWinner(
                CustomWinner.PokerFace, Player.PlayerId, true))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
                CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            }
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var allPF = AllPlayerControls.Where(pc => pc.Is(CustomRoles.PokerFace)).ToList();
        int alivePartners = allPF.Count(pc => pc.IsAlive() && pc.PlayerId != Player.PlayerId);
        return $"<color={RoleInfo.RoleColorCode}>({alivePartners})</color>";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";

        var allPF = AllPlayerControls.Where(pc => pc.Is(CustomRoles.PokerFace)).ToList();
        int alivePartners = allPF.Count(pc => pc.IsAlive() && pc.PlayerId != Player.PlayerId);

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (alivePartners == 0)
            return $"{size}<color={color}>仲間は全員死亡！このまま生き残れば勝利！</color>";

        return $"{size}<color={color}>生存している仲間: {alivePartners}人</color>";
    }
}