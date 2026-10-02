using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Crewmate;

public sealed class Stray : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Stray),
            player => new Stray(player),
            CustomRoles.Stray,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            39200,
            SetupOptionItem,
            "str",
            "#ffffff",
            (5, 6),
            true,
            Desc: () =>
            {
                return string.Format(GetString("StrayDesc"), OptionKillCount.GetInt());
            },
            countType: CountTypes.None
        );
    public Stray(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        KillCount = OptionKillCount.GetInt();
    }
    static OptionItem OptionKillCool;
    static OptionItem OptionKillCount;
    //以下英雄設定
    public static OptionItem OptHeroNeedTaskCount;
    public static OptionItem OptHeroFlash;
    //以下罪人設定
    public static OptionItem OptionNeedToDead;
    public static OptionItem OptionDeathType;
    //以下弔人設定
    static OptionItem OptionMournerDeadCount;
    public static OptionItem OptionCanSeeRoleCount;
    public static OptionItem OptNeedTaskCount;
    int KillCount;
    HashSet<byte> DeadCrewid = new();
    public override void Add()
    {
        DeadCrewid.Clear();
    }
    enum OptionName
    {
        SheriffShotLimit,
        OnmyojiWinTaskCount,
        HeroKillFlash,
        SinnerNeedToDeath,
        SinnerDeathType,
        MournerDeadCount,
        MournerSeeRoleCount
    }
    public enum DeathType
    {
        Kill,
        Vote
    }
    public override bool CanUseAbilityButton() => false;
    float IKiller.CalculateKillCooldown() => OptionKillCool.GetFloat();
    private static void SetupOptionItem()
    {
        ObjectOptionitem.Create(RoleInfo, 9, "Stray", true, null).SetOptionName(() => "Stray Setting").SetColor(RoleInfo.RoleColor);
        OptionKillCool = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionKillCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.SheriffShotLimit, new(1, 14, 1), 2, false).SetValueFormat(OptionFormat.Times);
        OverrideTasksData.Create(RoleInfo, 12);

        ObjectOptionitem.Create(RoleInfo, 30, "Stray", true, null).SetOptionName(() => "Hero Setting").SetColor(Hero.RoleInfo.RoleColor);
        OptHeroNeedTaskCount = IntegerOptionItem.Create(RoleInfo, 31, OptionName.OnmyojiWinTaskCount, new(1, 255, 1), 5, false).SetValueFormat(OptionFormat.Pieces);
        OptHeroFlash = BooleanOptionItem.Create(RoleInfo, 32, OptionName.HeroKillFlash, true, false);

        ObjectOptionitem.Create(RoleInfo, 40, "Stray", true, null).SetOptionName(() => "Sinner Setting").SetColor(Sinner.RoleInfo.RoleColor);
        OptionNeedToDead = BooleanOptionItem.Create(RoleInfo, 41, OptionName.SinnerNeedToDeath, true, false);
        var deathtypee = Enum.GetNames(typeof(DeathType));
        OptionDeathType = StringOptionItem.Create(RoleInfo, 42, OptionName.SinnerDeathType, deathtypee, 0, false, OptionNeedToDead);

        ObjectOptionitem.Create(RoleInfo, 50, "Stray", true, null).SetOptionName(() => "Mourner Setting").SetColor(Mourner.RoleInfo.RoleColor);
        OptionMournerDeadCount = IntegerOptionItem.Create(RoleInfo, 51, OptionName.MournerDeadCount, new(1, 14, 1), 5, false).SetValueFormat(OptionFormat.Players);
        OptNeedTaskCount = IntegerOptionItem.Create(RoleInfo, 52, OptionName.OnmyojiWinTaskCount, new(1, 255, 1), 4, false).SetValueFormat(OptionFormat.Pieces);
        OptionCanSeeRoleCount = IntegerOptionItem.Create(RoleInfo, 53, OptionName.MournerSeeRoleCount, new(1, 14, 1), 2, false).SetValueFormat(OptionFormat.Players);

        HideRoleOptions(CustomRoles.Hero);
        HideRoleOptions(CustomRoles.Sinner);
        HideRoleOptions(CustomRoles.Mourner);
    }
    internal static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null &&
            Options.CustomRoleSpawnChances.TryGetValue(role, out var spawnOption))
        {
            spawnOption.SetHidden(true);
        }

        if (Options.CustomRoleCounts != null &&
            Options.CustomRoleCounts.TryGetValue(role, out var countOption))
        {
            countOption.SetHidden(true);
        }
    }
    bool IKiller.CanUseSabotageButton() => false;
    bool IKiller.CanUseImpostorVentButton() => false;
    bool CanChangeHero = true;
    public override void OnFixedUpdate(PlayerControl player)
    {
        foreach (var p in PlayerCatch.AllPlayerControls)
        {
            if (p.Is(CustomRoleTypes.Crewmate) && !p.IsAlive())
            {
                DeadCrewid.Add(p.PlayerId);
            }
        }
        if (DeadCrewid.Count >= OptionMournerDeadCount.GetInt())
        {
            Player.RpcSetCustomRole(CustomRoles.Mourner);
        }
    }
    //迷者なら負け。
    public override void CheckWinner(GameOverReason reason)
    {
        if (Player.GetCustomRole() is CustomRoles.Stray)
        {
            CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
        }
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false) => Utils.ColorString(Color.yellow, $"({KillCount})");
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (KillCount <= 0)
        {
            info.DoKill = false;
            return;
        }
        --KillCount;
        SendRPC();
        if (KillCount <= 0)
        {
            if (CanChangeHero)
            {
                Player.RpcSetCustomRole(CustomRoles.Hero);
            }
            else
            {
                Player.RpcSetCustomRole(CustomRoles.Sinner);
            }
            return;
        }
        var target = info.AttemptTarget;
        if (target.Is(CustomRoleTypes.Crewmate))
        {
            CanChangeHero = false;
            SendRPC();
        }
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(KillCount);
        sender.Writer.Write(CanChangeHero);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        KillCount = reader.ReadInt32();
        CanChangeHero = reader.ReadBoolean();
    }
}
public sealed class Hero : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hero),
            player => new Hero(player),
            CustomRoles.Hero,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            39300,
            SetupOptionItem,
            "her",
            "#ffd700",
            (5, 6),
            Desc: () =>
            {
                if (Stray.OptHeroFlash.GetBool())
                {
                    return string.Format(GetString("HeroDesc2"));
                }
                return string.Format(GetString("HeroDesc1"));
            },
            assignInfo: new RoleAssignInfo(CustomRoles.Hero, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            }
        );
    public Hero(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
    }
    private static void SetupOptionItem()
    {
        Stray.HideRoleOptions(CustomRoles.Hero);
        Stray.HideRoleOptions(CustomRoles.Sinner);
        Stray.HideRoleOptions(CustomRoles.Mourner);
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (Player.GetCustomRole() is CustomRoles.Hero && !MyTaskState.HasCompletedEnoughCountOfTasks(Stray.OptHeroNeedTaskCount.GetInt()))
        {
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Crewmate) return;

            CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
        }
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (!Stray.OptHeroFlash.GetBool()) return;
        Utils.AllPlayerKillFlash();
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref UnityEngine.Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;

        if (!Player.IsAlive())
        {
            enabled = true;
            roleColor = StringHelper.CodeColor("#ffd700");
            roleText = GetString("Hero");
            addon = false;
        }
    }
}
public sealed class Sinner : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Sinner),
            player => new Sinner(player),
            CustomRoles.Sinner,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            39400,
            SetupOptionItem,
            "sir",
            "#4d0505",
            (5, 6),
            Desc: () =>
            {
                if (Stray.OptionNeedToDead.GetBool())
                {
                    if (Stray.OptionDeathType.GetValue() is (int)Stray.DeathType.Vote)
                    {
                        return string.Format(GetString("SinnerDesc"), GetString("Vote"));
                    }
                    return string.Format(GetString("SinnerDesc"), GetString("Kill"));
                }
                return string.Format(GetString("SinnerDesc2"));
            },
            assignInfo: new RoleAssignInfo(CustomRoles.Sinner, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            },
            countType: CountTypes.None
        );
    public Sinner(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        IsKilled = false;
        IsExiled = false;
    }
    bool IsExiled;
    bool IsKilled;
    private static void SetupOptionItem()
    {
        Stray.HideRoleOptions(CustomRoles.Hero);
        Stray.HideRoleOptions(CustomRoles.Sinner);
        Stray.HideRoleOptions(CustomRoles.Mourner);
    }
    public override void Add()
    {
        IsKilled = false;
        IsExiled = false;
    }
    public override void ChengeRoleAdd()
    {
        IsKilled = false;
        IsExiled = false;
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        IsKilled = true;
        SendRPC();
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled.PlayerId == Player.PlayerId)
        {
            IsExiled = true;
            SendRPC();
        }
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsKilled);
        sender.Writer.Write(IsExiled);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        IsKilled = reader.ReadBoolean();
        IsExiled = reader.ReadBoolean();
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (Player.GetCustomRole() is CustomRoles.Sinner && CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate)
        {
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Crewmate) return;

            if (!Stray.OptionNeedToDead.GetBool()) return;
            if (Stray.OptionDeathType.GetValue() is (int)Stray.DeathType.Vote && !IsExiled)
            {
                CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
                CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
            }
            if (Stray.OptionDeathType.GetValue() is (int)Stray.DeathType.Kill && !IsKilled)
            {
                CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
                CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
            }
        }
    }
}
public sealed class Mourner : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Mourner),
            player => new Mourner(player),
            CustomRoles.Mourner,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            39500,
            SetupOptionItem,
            "Mer",
            "#5f7285",
            (5, 6),
            assignInfo: new RoleAssignInfo(CustomRoles.Mourner, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            },
            countType: CountTypes.None
        );
    public Mourner(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
    }
    int SeeCount;
    List<byte> SeeRoleTargets = new();
    byte targetId;
    CustomRoles role;
    public override void Add()
    {
        SeeCount = Stray.OptionCanSeeRoleCount.GetInt();
    }
    private static void SetupOptionItem()
    {
        Stray.HideRoleOptions(CustomRoles.Hero);
        Stray.HideRoleOptions(CustomRoles.Sinner);
        Stray.HideRoleOptions(CustomRoles.Mourner);
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (Player.GetCustomRole() is CustomRoles.Hero && !MyTaskState.HasCompletedEnoughCountOfTasks(Stray.OptNeedTaskCount.GetInt()))
        {
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Crewmate) return;

            CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
            CustomWinnerHolder.WinnerIds.Remove(Player.PlayerId);
        }
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (SeeCount <= 0) return;
        --SeeCount;
        SeeRoleTargets.Add(target.PlayerId);
        _ = new LateTask(() =>
        {
            UseAbility(target.PlayerId);
        }, 5f, "", true);
        SendRPC();
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!seen) return;
        if (!Player.IsAlive()) return;

        var role = seen.GetCustomRole();

        if (SeeRoleTargets.Contains(seen.PlayerId))
        {
            roleText = GetString($"{role}");
            roleColor = UtilsRoleText.GetRoleColor(role);
        }
    }
    private void UseAbility(byte targetId)
    {
        const string title = "<#66a6ff>霊媒めっせーじ</color>";

        var target = PlayerCatch.GetPlayerById(targetId);
        role = target.GetTellResults(Player);

        SendTellRPC(target.PlayerId, role);

        string roleText = $"<b>{Utils.ColorString(UtilsRoleText.GetRoleColor(role), GetString(role.ToString()))}</b>";
        var remaining = string.Format(GetString("RemainingCount"), SeeCount);

        var text = $"{UtilsName.GetPlayerColor(target, true)}は{roleText}でした。\n{remaining}";
        Utils.SendMessage(text, Player.PlayerId, title);
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(0);
        sender.Writer.Write(SeeCount);
        sender.Writer.Write((byte)SeeRoleTargets.Count);
        foreach (var id in SeeRoleTargets)
            sender.Writer.Write(id);
    }
    void SendTellRPC(byte Id, CustomRoles role)
    {
        using var sender = CreateSender();
        sender.Writer.Write(1);
        sender.Writer.Write(Id);
        sender.Writer.Write((int)role);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        var typeId = reader.ReadByte();

        switch (typeId)
        {
            case 0:
                SeeCount = reader.ReadInt32();
                SeeRoleTargets.Clear();
                var count = reader.ReadByte();
                for (int i = 0; i < count; i++)
                    SeeRoleTargets.Add(reader.ReadByte());
                break;
            case 1:
                targetId = reader.ReadByte();
                role = (CustomRoles)reader.ReadPackedInt32();
                break;
        }
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var canUse = Player.IsAlive() && SeeCount > 0;
        return Utils.ColorString(canUse ? Color.cyan : Color.gray, $"({SeeCount})");
    }
}