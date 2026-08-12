using AmongUs.GameOptions;
using Hazel;
using JetBrains.Annotations;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Neutral;

public sealed class Obolus : RoleBase, ILNKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Obolus),
            player => new Obolus(player),
            CustomRoles.Obolus,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            56200,
            SetupOptionItem,
            "obl",
            "#ba841e",
            (2, 0),
            true,
            /*assignInfo: new RoleAssignInfo(CustomRoles.Obolus, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1)
            },*/
            Desc: () =>
            {
                return string.Format(GetString("ObolusDesc"),OptionAddWin.GetBool() ? GetString("AddWin") : GetString("SoloWin"));
            },
        from: From.UchuAddon
        );
    public Obolus(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
        CanKill = true;
    }

    public bool ImpostorKilled;
    static OptionItem OptionKillCooldown;
    public static OptionItem OptionCanVent;

    public static bool CanVent;
    private static float KillCooldown;

    private bool CanKill;

    static OptionItem OptionAddWin;

    enum OptionName
    {
        CountKillerAddWin
    }
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 50);

        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false);
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 13, OptionName.CountKillerAddWin, false, false);
        RoleAddAddons.Create(RoleInfo, 14);
    }

    private bool Addwin => OptionAddWin.GetBool();
    public float CalculateKillCooldown() => KillCooldown;
    public override void Add()
    {
        var playerId = Player.PlayerId;
        KillCooldown = OptionKillCooldown.GetFloat();

        CanKill = true;
        Player.ResetKillCooldown();
        Player.SyncSettings();

    }


    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ImpostorKilled);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        var impostorKilled = reader.ReadBoolean();
        var count = reader.ReadInt32();

        ImpostorKilled = impostorKilled;


        // 受信した側がホストの場合、オプションを尊重して単独勝利を行う
        if (impostorKilled && AmongUsClient.Instance.AmHost)
        {
            if (!OptionAddWin.GetBool())
            {
                ForceSoloWin();
            }
        }
    }
    public bool CanUseKillButton() => Player.IsAlive() && CanKill;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => CanVent;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!Is(killer)) return;
        if (!CanKill)
        {
            info.DoKill = false;
            SendRPC();
            return;
        }
        else if ((target.GetCustomRole().IsImpostor() || target.GetCustomRole() is CustomRoles.Egoist))
        {
            CanKill = false;
            info.DoKill = true;
            ImpostorKilled = true;

            SendRPC();

            if (AmongUsClient.Instance.AmHost && !OptionAddWin.GetBool())
            {
                ForceSoloWin();
            }
        }
        else
        {
            CanKill = false;
            ImpostorKilled = false;
            SendRPC();
        }
    }

    public void Win()
    {
        if (OptionAddWin.GetBool()) return;

        if (AmongUsClient.Instance.AmHost)
        {
            ForceSoloWin();
        }
    }

    private void ForceSoloWin()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Obolus, Player.PlayerId))
        {
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (Player.IsAlive())
        {
            if (Addwin)
            {
                if (ImpostorKilled)
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
        else
        {
            return false;
        }
    }
 
    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;
        CanKill = true;
    }

    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
    }
}