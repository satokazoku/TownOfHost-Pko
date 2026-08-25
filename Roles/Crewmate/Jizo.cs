using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using Rewired;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static Il2CppSystem.Threading.SemaphoreSlim;
using static MonoMod.RuntimeDetour.DynamicHookGen;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Crewmate;

public sealed class Jizo : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jizo),
            player => new Jizo(player),
            CustomRoles.Jizo,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            38800,
            SetupOptionItem,
            "jz",
            "#a9a9a9",
            (1, 9),
            assignInfo: new RoleAssignInfo(CustomRoles.Jizo, CustomRoleTypes.Crewmate)
            {
                AssignCountRule = new(1, 1, 1) 
            }
        );

    public Jizo(PlayerControl player)
        : base(RoleInfo, player)
    {
        Cooldown = OptionCooldown.GetFloat();

        UseCount = OptionUseCount.GetInt();
        IsUsed = false;
        Duration = OptionDuration.GetFloat();
        KilledRoom = null;
        UsedRoom = null;
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく

        Killed = false;
        Detectioned = false;

        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
        vented = false;

        sendCount = 0;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;

        Notifyname = OptionNotifyName.GetBool();
    }

    public static OptionItem OptionCooldown;
    public static OptionItem OptionUseCount;
    public static OptionItem OptionDuration;
    public static OptionItem OptionKill;
    public static OptionItem OptionVent;
    public static OptionItem OptionNotifyName;
    public static SystemTypes? UsedRoom = null;

    float Duration;
    bool IsUsed;
    float Cooldown;
    float JizocooldownLeft;
    int UseCount;
    Vector2 pos = Vector2.zero;

    public static bool BomKilled;
    public static SystemTypes? BomKilledRoom = null;
    static PlayerControl BomKiller;
    static bool BomDetectioned;

    public static bool Killed;
    public static SystemTypes? KilledRoom = null;
    public static SystemTypes? VentedRoom = null;
    static PlayerControl Killer;
    static bool Detectioned;
    static bool vented;
    static PlayerControl VentUser;
    int sendCount;

    bool Notifyname;
    public override void Add()
    {
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく
        PetActionManager.Register(Player.PlayerId, PetUsed);
        RpcJizo(null);
        UsedRoom = null;
        KilledRoom = null;
        Killed = false;
        Detectioned = false;
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
        sendCount = 0;
    }
    public override void OnSpawn(bool initialState = false)
    {
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく

        Player.RpcResetAbilityCooldown(Sync: true);
        RpcJizo(null);
        Killed = false;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
    }
    enum OptionName
    {
        JizoDuration,
        JizoDetectionKill,
        JizoDetectionVent,
        JizoNotifyName
    }

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.JizoDuration, new(1f, 30f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 12, GeneralOption.OptionCount, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionKill = BooleanOptionItem.Create(RoleInfo, 13, OptionName.JizoDetectionKill, true, false);
        OptionVent = BooleanOptionItem.Create(RoleInfo, 14, OptionName.JizoDetectionVent, true, false);
        OptionNotifyName = BooleanOptionItem.Create(RoleInfo, 15, OptionName.JizoNotifyName, true, false);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = JizocooldownLeft > 0f ? JizocooldownLeft : 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive()) return;
        if (IsUsed)
        {
            Player.RpcSnapToForced(pos);
        }
        if (!AmongUsClient.Instance.AmHost) return;

        if (JizocooldownLeft > 0f)
        {
            JizocooldownLeft -= Time.fixedDeltaTime;
            if (JizocooldownLeft < 0f) JizocooldownLeft = 0f;
        }
    }

    void PetUsed()
    {
        if (!Player.IsAlive() || IsUsed || UseCount <= 0 || !AmongUsClient.Instance.AmHost || JizocooldownLeft > 0f)
        {
            return;
        }

        if (!IsUsed)
        {
            var room = Player.GetPlainShipRoom();

            var roomName = room.RoomId;
            RpcJizo(roomName);

            Logger.Info("注視開始", "Jizo");
            IsUsed = true;
            pos = Player.transform.position;
            Player.RpcSnapToForced(pos);

            JizocooldownLeft = Duration;
            --UseCount;
            Player.RpcResetAbilityCooldown(Sync: true);

            SendRPC();
            _ = new LateTask(() =>
            {
                IsUsed = false;
                UsedRoom = null;
                JizocooldownLeft = Cooldown;
                Logger.Info("注視終了", "Jizo");

                SendRPC();
            }, Duration, "Jizo_Use", true);
        }
    }
    private void RpcJizo(SystemTypes? roomType)
    {
        UsedRoom = roomType;
        using var sender = CreateSender();
        sender.Writer.Write((byte?)roomType ?? byte.MaxValue);
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(JizocooldownLeft);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        JizocooldownLeft = reader.ReadSingle();
        var roomId = reader.ReadByte();

        UsedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
    }

    /// <summary>
    /// 地蔵が注視中で同じ部屋にいるかをチェックする
    /// </summary>
    public static void Checkroom(PlainShipRoom room, PlayerControl Player)
    {
        if (room == null) return;
        if (room.RoomId != UsedRoom) return;
        if (Detectioned || BomDetectioned) return;
        if (Killer != null || BomKiller != null) return;
        if (!OptionKill.GetBool()) return;
        Detectioned = true;
        Killed = true;
        KilledRoom = room.RoomId;
        Killer = Player;
    }
    /// <summary>
    /// 地蔵が注視中で同じ部屋にいるかをチェックする(ボマー専用)
    /// </summary>
    public static void BomCheckroom(PlainShipRoom room, PlayerControl Player)
    {
        if (room == null) return;
        if (room.RoomId != UsedRoom) return;
        if (BomKiller != null) return;
        if (!OptionKill.GetBool()) return;
        BomDetectioned = true;
        BomKilledRoom = room.RoomId;
        BomKiller = Player;
        Bomber.IsDetectioned = true;
    }

    public static void BomClear()
    {
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(UseCount > 0 ? Color.green : Color.gray, $"({UseCount})");
        return progress;
    }
    public override void AfterMeetingTasks()
    {
        JizocooldownLeft = Cooldown;
        IsUsed = false;
        Player.RpcResetAbilityCooldown(Sync: true);
        SendRPC();
        RpcJizo(null);
        KilledRoom = null;
        Killed = false;
        Detectioned = false;
        sendCount = 0;
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
    }

    public override void OnReportDeadBody(PlayerControl _, NetworkedPlayerInfo __)
    {
        if (!Detectioned && !BomDetectioned)
        {
            return;
        }
        if (Killed)
        {
            Utils.SendMessage(string.Format(GetString("JizoKillText"), GetString($"{KilledRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText2"), UtilsName.GetPlayerColor(Player, true), Player), Killer.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText")), Killer.PlayerId);
            }
            ++sendCount;
        }
        if (vented || sendCount < 1)
        {
            Utils.SendMessage(string.Format(GetString("JizoVentText"), GetString($"{VentedRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoVenterText2"), UtilsName.GetPlayerColor(Player, true), Player), VentUser.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoVenterText")), VentUser.PlayerId);
            }
            ++sendCount;
        }
        if (BomKilled || sendCount < 1)
        {
            Utils.SendMessage(string.Format(GetString("JizoKillText"), GetString($"{KilledRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText2"), UtilsName.GetPlayerColor(Player, true), Player), Killer.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText")), Killer.PlayerId);
            }
            ++sendCount;
        }

        UsedRoom = null;
        KilledRoom = null;

        Killed = false;
        Killer = null;

        vented = false;
        VentedRoom = null;
        VentUser = null;

        Detectioned = false;
        sendCount = 0;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        return $"{$"{UsedRoom}で注視使用中"}</size>";
    }
    public static bool OnEnterVentOthers(PlayerPhysics physics, int ventId)
    {
        if (!OptionVent.GetBool()) return true;
        var user = physics.myPlayer;
        if (Main.NormalOptions.MapId is 0)
        {
            if (ventId == 2 && UsedRoom == SystemTypes.Cafeteria)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 7 && UsedRoom == SystemTypes.Weapons)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (UsedRoom == SystemTypes.Nav)
            {
                if (ventId == 12 || ventId == 13)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
            }
            if (ventId == 10 && UsedRoom == SystemTypes.Shields)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 0 && UsedRoom == SystemTypes.Admin)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 6 && UsedRoom == SystemTypes.MedBay)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 5 && UsedRoom == SystemTypes.Security)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 9 && UsedRoom == SystemTypes.LowerEngine)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 4 && UsedRoom == SystemTypes.UpperEngine)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (UsedRoom == SystemTypes.Reactor)
            {
                if (ventId == 11 || ventId == 8)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
            }
            return true;
        }
        if (Main.NormalOptions.MapId is 1)
        {
            if (ventId == 11 && UsedRoom == SystemTypes.Launchpad)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 8 && UsedRoom == SystemTypes.Launchpad)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 1 && UsedRoom == SystemTypes.Balcony)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 2 && UsedRoom == SystemTypes.Cafeteria)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 6 && UsedRoom == SystemTypes.Admin)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 5 && UsedRoom == SystemTypes.Office)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 10 && UsedRoom == SystemTypes.LockerRoom)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 9)
            {
                if (UsedRoom == SystemTypes.Decontamination || UsedRoom == SystemTypes.Decontamination2 || UsedRoom == SystemTypes.Decontamination3)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
            }
            if (ventId == 3 && UsedRoom == SystemTypes.Reactor)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 4 && UsedRoom == SystemTypes.Laboratory)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 7 && UsedRoom == SystemTypes.Greenhouse)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            return true;
        }
        if (Main.NormalOptions.MapId is 2)
        {
            if (ventId == 0 && UsedRoom == SystemTypes.Electrical)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 2 && UsedRoom == SystemTypes.LifeSupp)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 4 && UsedRoom == SystemTypes.Office)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 5 && UsedRoom == SystemTypes.Admin)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 6 && UsedRoom == SystemTypes.Laboratory)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            return true;
        }
        if (Main.NormalOptions.MapId is 4)
        {
            if (ventId == 11 && UsedRoom == SystemTypes.CargoBay)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }

            if (ventId == 10 && UsedRoom == SystemTypes.Storage)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 9 && UsedRoom == SystemTypes.Showers)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (UsedRoom == SystemTypes.MainHall)
            {
                if (ventId == 5 || ventId == 6)
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 3 && UsedRoom == SystemTypes.Engine)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 4 && UsedRoom == SystemTypes.Kitchen)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 2 && UsedRoom == SystemTypes.ViewingDeck)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 1 && UsedRoom == SystemTypes.Cockpit)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (ventId == 0 && UsedRoom == SystemTypes.VaultRoom)
            {
                vented = true;
                VentUser = user;
                Detectioned = true;
                VentedRoom = UsedRoom;
                return true;
            }
            if (UsedRoom == SystemTypes.GapRoom)
            {
                if (ventId == 8 || ventId == 7)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
            }
            if (Main.NormalOptions.MapId is 5)
            {
                if (ventId == 4 && UsedRoom == SystemTypes.Laboratory)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
                if (ventId == 1 && UsedRoom == SystemTypes.Kitchen)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
                if (ventId == 5 && UsedRoom == SystemTypes.Reactor)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
                if (ventId == 2 && UsedRoom == SystemTypes.ViewingDeck)
                {
                    vented = true;
                    VentUser = user;
                    Detectioned = true;
                    VentedRoom = UsedRoom;
                    return true;
                }
                return true;
            }
            return true;
        }
        return true;
    }
}