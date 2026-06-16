using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost;

class Triplets
{
    public static Dictionary<byte, HashSet<byte>> TripletsList = new();
    public static List<byte> DieTripletsList = new();

    public static void Init()
    {
        SubRoleRPCSender.AddHandler(CustomRoles.Triplets, ReceiveRPC);
    }

    public static void AssingAndReset()
    {
        TripletsList = new();
        DieTripletsList = new();
        var Sets = CustomRoles.Triplets.GetRealCount();

        if (Sets <= 0) return;
        List<PlayerControl> AssingPlayers = new();

        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role is CustomRoles.GM or CustomRoles.BakeCat || role.IsImpostor()) continue;
            if (pc.IsNeutralKiller()) continue;
            if (pc.Is(CustomRoles.Twins)) continue;

            if (role.IsNeutral() && !OptionCanAssingCantKillNeutral.GetBool()) continue;
            if (role.IsMadmate() && !OptionCanAssingMadmate.GetBool()) continue;

            AssingPlayers.Add(pc);
        }

        if (AssingPlayers.Count < 3) return;

        for (var i = 0; i < Sets; i++)
        {
            if (AssingPlayers.Count < 3) break;

            var pc = PickRandom(AssingPlayers);
            var pc2 = PickRandom(AssingPlayers);
            var pc3 = PickRandom(AssingPlayers);

            if (pc is null || pc2 is null || pc3 is null) break;

            SetTriplets(pc.PlayerId, pc2.PlayerId, pc3.PlayerId, true);
            RpcSetTriplets(pc.PlayerId, pc2.PlayerId, pc3.PlayerId);

            Logger.Info($"{pc.GetRealName()} & {pc2.GetRealName()} & {pc3.GetRealName()}", "Triplets");
        }
    }

    private static PlayerControl PickRandom(List<PlayerControl> players)
    {
        var list = players.OrderBy(x => Guid.NewGuid()).ToArray();
        var player = list[IRandom.Instance.Next(list.Length)];
        players.Remove(player);
        return player;
    }

    private static void SetTriplets(byte playerId, byte playerId2, byte playerId3, bool setSubRole)
    {
        var ids = new[] { playerId, playerId2, playerId3 }.Distinct().ToArray();
        if (ids.Length != 3) return;

        foreach (var id in ids)
        {
            TripletsList[id] = new HashSet<byte>(ids.Where(partnerId => partnerId != id));
            if (setSubRole) PlayerState.GetByPlayerId(id).SetSubRole(CustomRoles.Triplets);
        }
    }

    public static bool TryGetMembers(byte playerId, out HashSet<byte> members)
    {
        if (TripletsList.TryGetValue(playerId, out var partners))
        {
            members = new HashSet<byte>(partners) { playerId };
            return true;
        }

        members = null;
        return false;
    }

    public static bool IsTripletWith(byte seerId, byte targetId)
        => seerId != targetId && TryGetMembers(seerId, out var members) && members.Contains(targetId);

    public static bool ShouldSendChatTo(byte senderId, PlayerControl receiver, bool includeSender)
    {
        if (receiver == null) return false;
        if (!receiver.IsAlive()) return true;
        if (!TryGetMembers(senderId, out var members)) return false;
        if (!includeSender && receiver.PlayerId == senderId) return false;
        return members.Contains(receiver.PlayerId);
    }

    #region  Options
    public static OptionItem OptionCanAssingMadmate;
    public static OptionItem OptionCanAssingCantKillNeutral;
    public static OptionItem OptionTripletsDiefollow;
    public static OptionItem OptionTripletsAddWin;
    public static void SetUpTripletsOptions()
    {
        SetupRoleOptions(77950, TabGroup.Combinations, CustomRoles.Triplets, new(1, 7, 1));
        OptionCanAssingMadmate = BooleanOptionItem.Create(77960, "CanAssingMadmate", false, TabGroup.Combinations, false)
            .SetSubRoleOptionItem(CustomRoles.Triplets);
        OptionCanAssingCantKillNeutral = BooleanOptionItem.Create(77961, "CanAssingCantKillNeutral", false, TabGroup.Combinations, false)
            .SetSubRoleOptionItem(CustomRoles.Triplets);
        ObjectOptionitem.Create(77973, "AddonOption", true, "", TabGroup.Combinations).SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Triplets);
        OptionTripletsDiefollow = BooleanOptionItem.Create(77971, "TripletsDiefollow", false, TabGroup.Combinations, false)
            .SetSubRoleOptionItem(CustomRoles.Triplets);
        OptionTripletsAddWin = BooleanOptionItem.Create(77972, "TripletsAddWin", false, TabGroup.Combinations, false)
            .SetSubRoleOptionItem(CustomRoles.Triplets);
    }
    #endregion

    public static void CheckAddWin()
    {
        if (!OptionTripletsAddWin.GetBool()) return;
        if (Modules.SuddenDeathMode.NowSuddenDeathMode) return;

        bool flug = false;
        foreach (var triplets in GetGroups())
        {
            if (!triplets.Any(IsWinner)) continue;

            foreach (var id in triplets)
            {
                if (CustomWinnerHolder.CantWinPlayerIds.Contains(id) && CustomWinnerHolder.WinnerTeam.IsLovers()) continue;
                if (CustomWinnerHolder.WinnerIds.Contains(id)) continue;
                if (CustomWinnerHolder.WinnerRoles.Contains(id.GetPlayerControl()?.GetCustomRole() ?? CustomRoles.Emptiness)) continue;

                CustomWinnerHolder.CantWinPlayerIds.Remove(id);
                Logger.Info($"{id}:Triplets add win", "Triplets");
                CustomWinnerHolder.WinnerIds.Add(id);
                if (!flug)
                {
                    flug = true;
                    CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Triplets);
                }
            }
        }
    }

    private static bool IsWinner(byte id)
        => CustomWinnerHolder.WinnerIds.Contains(id)
        || CustomWinnerHolder.WinnerRoles.Contains(id.GetPlayerControl()?.GetCustomRole() ?? CustomRoles.Emptiness);

    public static void TripletsReset(byte leftid)
    {
        if (!TryGetMembers(leftid, out var members)) return;

        foreach (var id in members)
            TripletsList.Remove(id);
    }

    public static void TripletsSuicide(bool isExiled = false)
    {
        if (!OptionTripletsDiefollow.GetBool() || !CustomRoles.Triplets.IsPresent()) return;
        isExiled |= AntiBlackout.IsCached || GameStates.CalledMeeting || GameStates.ExiledAnimate;

        foreach (var triplets in GetGroups())
        {
            if (triplets.All(id => DieTripletsList.Contains(id))) continue;

            var aliveTriplets = triplets
                .Select(PlayerCatch.GetPlayerById)
                .Where(pc => pc != null && pc.IsAlive())
                .ToArray();

            if (triplets.Count - aliveTriplets.Length < 2) continue;

            foreach (var triplet in aliveTriplets)
            {
                PlayerState.GetByPlayerId(triplet.PlayerId).DeathReason = CustomDeathReason.FollowingSuicide;
                if (isExiled)
                {
                    triplet.RpcExileV3();
                }
                else
                {
                    triplet.RpcMurderPlayerV2(triplet);
                }
            }

            foreach (var id in triplets)
                if (!DieTripletsList.Contains(id))
                    DieTripletsList.Add(id);
        }
    }

    private static IEnumerable<HashSet<byte>> GetGroups()
    {
        var seenGroups = new HashSet<string>();
        foreach (var id in TripletsList.Keys.ToArray())
        {
            if (!TryGetMembers(id, out var members)) continue;

            var key = string.Join(",", members.OrderBy(memberId => memberId));
            if (seenGroups.Add(key)) yield return members;
        }
    }

    public static void RpcSetTriplets(byte playerId, byte playerId2, byte playerId3)
    {
        using var sender = new SubRoleRPCSender(CustomRoles.Triplets, playerId);
        sender.Writer.Write(playerId2);
        sender.Writer.Write(playerId3);
    }

    public static void ReceiveRPC(MessageReader reader, byte playerId)
    {
        var playerId2 = reader.ReadByte();
        var playerId3 = reader.ReadByte();

        SetTriplets(playerId, playerId2, playerId3, true);
    }
}
