using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Bakery : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Bakery),
            player => new Bakery(player),
            CustomRoles.Bakery,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            30600,
            SetupOptionItem,
            "bak",
            "#8f6121",
            (4, 2),
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );

    private static OptionItem PoisonedBakeryChanceOption;

    public Bakery(PlayerControl player)
    : base(RoleInfo, player)
    {
        RareRoute = false;
        RouteNumber = 0;
        IsOnlyNomalMessage = false;
    }

    bool RareRoute;
    int RouteNumber;
    static bool IsOnlyNomalMessage;

    public static void SetupOptionItem()
    {
        PoisonedBakeryChanceOption = FloatOptionItem.Create(
            RoleInfo, 12, "PoisonedBakeryRole77",
            new(0f, 100f, 1f), 25f, false
        ).SetValueFormat(OptionFormat.Percent);

        ObjectOptionitem.Create(RoleInfo, 20, "PoisonedBakeryOption", true, "")
            .SetOptionName(() => "PoisonedBakery Option");
    }

    public override void Add()
    {
        var bakerycount = 0;
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (pc.GetCustomRole() is CustomRoles.Bakery) bakerycount++;
            if (pc.GetCustomRole() is CustomRoles.AllArounder && AllArounder.RandomBakery.GetBool())
            {
                IsOnlyNomalMessage = true;
                break;
            }
        }
        if (bakerycount > 1) IsOnlyNomalMessage = true;
    }

    public override string MeetingAddMessage()
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return "";
        if (Player.IsAlive())
        {
            string BakeryTitle = $"<size=90%><color=#8f6121>{GetString("Message.BakeryTitle")}</size></color>";
            return BakeryTitle + "\n<size=70%>" + BakeryMeg() + "</size>\n";
        }
        return "";
    }

    string BakeryMeg()
    {
        if (IsOnlyNomalMessage) return GetString("Message.Bakery");
        int rect = IRandom.Instance.Next(1, 101);
        int dore = IRandom.Instance.Next(1, 101);
        int meg = IRandom.Instance.Next(1, 4);
        var kisetu = "";
        if (DateTime.Now.Month is 1 or 2 or 12) kisetu = "winter";
        if (DateTime.Now.Month is 3 or 4 or 5) kisetu = "spring";
        if (DateTime.Now.Month is 6 or 7 or 8) kisetu = "summer";
        if (DateTime.Now.Month is 9 or 10 or 11) kisetu = "fall";

        if (!RareRoute)
        {
            if (rect <= 15)
            {
                RareRoute = true;
                if (dore <= 15) { RouteNumber = 1; return GetString("Message.Bakery1"); }
                else if (dore <= 35) { RouteNumber = 2; return string.Format(GetString("Message.Bakery2"), GetString($"{kisetu}")); }
                else if (dore <= 65) { RouteNumber = 3; return string.Format(GetString("Message.Bakery3"), (MapNames)Main.NormalOptions.MapId, GetString($"{kisetu}.Ba")); }
                else { RouteNumber = 4; return GetString($"Message.Bakery4.{meg}"); }
            }
            return GetString("Message.Bakery");
        }
        else
        {
            switch (RouteNumber)
            {
                case 1:
                    int sns = IRandom.Instance.Next(1, 11);
                    int Like = IRandom.Instance.Next(0, 126);
                    if (Like <= 25) Like = 0; else Like -= 25;
                    int Ripo = IRandom.Instance.Next(0, Like + 5 + 26);
                    if (Ripo <= 25) Ripo = 0; else Ripo -= 25;
                    if (sns is 9) return string.Format(GetString($"Message.Bakery1.9"), $"{IRandom.Instance.Next((UtilsGameLog.day - 1) * 5, UtilsGameLog.day * 5) * 10}") + string.Format("\n　<color=#ff69b4>♥</color>{0}　<color=#7cfc00>Θ</color>{1}", Like, Ripo);
                    if (sns is 8) return GetString("Message.Bakery1.8");
                    return GetString($"Message.Bakery1.{sns}") + string.Format("\n　<color=#ff69b4>♥</color>{0}　<color=#7cfc00>Θ</color>{1}", Like, Ripo);
                case 2: return GetString($"Message.Bakery2.{meg}");
                case 3: return string.Format(GetString($"Message.Bakery3.{meg}"), GetString($"{kisetu}.Ba"));
                case 4: return rect <= 50 ? GetString("Message.Bakery") : GetString($"Message.Bakery4.{meg}");
            }
        }
        return "なんかエラー起きてるよ(´-ω-`)\nホストさんログ取って提出して☆";
    }

    public override void AfterMeetingTasks()
    {
        try
        {
            base.AfterMeetingTasks();
            if (!AmongUsClient.Instance.AmHost) return;
            if (Player == null || !Player.IsAlive()) return;
            if (PoisonedBakeryChanceOption == null) return;

            var chance = PoisonedBakeryChanceOption.GetFloat();
            var rand = IRandom.Instance.Next(1, 101);

            if (rand <= chance)
            {
                Player.RpcSetCustomRole(CustomRoles.PoisonedBakery);
                Utils.SendMessage(
                    "<color=#a83232>パン屋が<b><size=120%>パン屋(N)</b></size>に変化しました。\n毒入りパンを配るようになりました。</color>",
                    Player.PlayerId
                );

                _ = new LateTask(() =>
                {
                    if (Player.GetRoleClass() is PoisonedBakery pb)
                        pb.SelectFirstPoison();
                }, 0.15f, "BakeryFirstPoison", true);
            }
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(Bakery));
        }
    }

    public static string BakeryMark()
    {
        var bakerys = PlayerCatch.AllAlivePlayerControls.Where(pc =>
        {
            if (pc.GetRoleClass() is AllArounder aa)
                return aa.NowRole is AllArounder.NowMode.Bakery && aa.CanUseAbility();
            return pc.GetCustomRole() is CustomRoles.Bakery;
        });
        if (!bakerys.Any()) return "";
        return $" <#8f6121><rotate=-20>§</rotate></color>{(bakerys.Count() > 1 ? $"×{bakerys.Count()}" : "")}";
    }
}

public sealed class PoisonedBakery : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(PoisonedBakery),
            player => new PoisonedBakery(player),
            CustomRoles.PoisonedBakery,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            53800,
            SetupOptionItem,
            "poisoned_bak",
            "#FF4A5A",
            (5, 3)
        );

    public PlayerControl PoisonedPlayer = null;
    public static List<PoisonedBakery> Bakeries = new();
    public static List<byte> PoisonedPlayerIds = new();

    public PoisonedBakery(PlayerControl player) : base(RoleInfo, player) { }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 50);
        HideRoleOptions(CustomRoles.PoisonedBakery);
    }

    public static void HideRoleOptions(CustomRoles role)
    {
        if (Options.CustomRoleSpawnChances != null && Options.CustomRoleSpawnChances.TryGetValue(role, out var sp))
            sp.SetHidden(true);
        if (Options.CustomRoleCounts != null && Options.CustomRoleCounts.TryGetValue(role, out var cp))
            cp.SetHidden(true);
    }

    public override void Add()
    {
        base.Add();
        Bakeries.Add(this);
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Bakeries.Remove(this);
        CustomRoleManager.MarkOthers.Remove(GetMarkOthers);
        PoisonedPlayerIds.Clear();
    }

    public static bool CheckWin(ref GameOverReason reason)
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (!pc.IsAlive()) continue;
            if (pc.GetRoleClass() is not PoisonedBakery) continue;

            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.PoisonedBakery, pc.PlayerId, AddWin: false))
            {
                CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
                reason = GameOverReason.ImpostorsByKill;
                return true;
            }
        }
        return false;
    }

    public override void CheckWinner(GameOverReason reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        CheckWin(ref reason);
    }

    public override void OnStartMeeting() { }

    public void SelectFirstPoison()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;

        var targetList = PlayerCatch.AllAlivePlayerControls
            .Where(p => p.PlayerId != Player.PlayerId)
            .Where(p => !Main.AfterMeetingDeathPlayers.ContainsKey(p.PlayerId))
            .ToList();

        if (!targetList.Any()) return;

        var target = targetList[IRandom.Instance.Next(targetList.Count)];
        SetPoison(target);
        Utils.SendMessage(
            $"<color=#a83232><size=120%>{target.GetRealName()} に毒入りパンを配布しました。</size></color>",
            Player.PlayerId
        );
    }

    public override string MeetingAddMessage()
    {
        if (!Player.IsAlive() || PoisonedPlayer == null) return "";
        return $"<color=#a83232>毒入りパンを配布されたプレイヤー: {UtilsName.GetPlayerColor(PoisonedPlayer.PlayerId)}§</color>";
    }

    public override void AfterMeetingTasks()
    {
        base.AfterMeetingTasks();
        if (!AmongUsClient.Instance.AmHost) return;

        if (Player.IsAlive() && PoisonedPlayer != null && PoisonedPlayer.IsAlive())
        {
            PoisonedPlayer.SetRealKiller(Player);
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Poisoned, PoisonedPlayer.PlayerId);
        }

        if (!Player.IsAlive())
        {
            ClearPoison();
            return;
        }

        var targetList = PlayerCatch.AllAlivePlayerControls
            .Where(p => p.PlayerId != Player.PlayerId)
            .Where(p => !Main.AfterMeetingDeathPlayers.ContainsKey(p.PlayerId))
            .ToList();

        if (targetList.Any())
        {
            var target = targetList[IRandom.Instance.Next(targetList.Count)];
            SetPoison(target);
            Utils.SendMessage(
                $"<color=#a83232><size=120%>{target.GetRealName()} に毒入りパンを配布しました。</size></color>",
                Player.PlayerId
            );
        }
        else
        {
            ClearPoison();
        }
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (info.IsSuicide) return;
        if (AmongUsClient.Instance.AmHost) ClearPoison();
    }

    private void SetPoison(PlayerControl target)
    {
        PoisonedPlayer = target;
        if (!PoisonedPlayerIds.Contains(target.PlayerId))
            PoisonedPlayerIds.Add(target.PlayerId);
        SendRPC(true, target.PlayerId);
    }

    private void ClearPoison()
    {
        if (PoisonedPlayer != null)
            PoisonedPlayerIds.Remove(PoisonedPlayer.PlayerId);
        PoisonedPlayer = null;
        SendRPC(false, byte.MaxValue);
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (PoisonedPlayerIds.Contains(seen.PlayerId))
            return Utils.ColorString(new Color32(168, 50, 50, 255), "§");
        return "";
    }

    private void SendRPC(bool doPoison, byte target = 255)
    {
        using var sender = CreateSender();
        sender.Writer.Write(doPoison);
        sender.Writer.Write(target);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var doPoison = reader.ReadBoolean();
        var targetId = reader.ReadByte();

        if (doPoison && targetId != byte.MaxValue)
        {
            PoisonedPlayer = PlayerCatch.GetPlayerById(targetId);
            if (!PoisonedPlayerIds.Contains(targetId))
                PoisonedPlayerIds.Add(targetId);
        }
        else
        {
            if (PoisonedPlayer != null)
                PoisonedPlayerIds.Remove(PoisonedPlayer.PlayerId);
            PoisonedPlayer = null;
        }
    }
}
