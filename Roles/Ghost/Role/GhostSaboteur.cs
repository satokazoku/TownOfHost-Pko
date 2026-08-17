// ★ CustomRoles に GhostSaboteur を追加してください
// ★ GhostRoleCore.Init()  に GhostSaboteur.Init() を追加
// ★ GhostRoleCore.Setup() に GhostSaboteur.SetupCustomOption() を追加
// ★ UseAbility(ghost, target) を Ghostbuttoner.UseAbility と同じ場所で呼ぶ
using System.Collections.Generic;
using Hazel;
using InnerNet;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Ghost;

/// <summary>
/// 修霊 (GhostSaboteur)
/// 死亡したクルー陣営（設定によってはマッドメイトも）に付与される幽霊役職。
/// 守護能力を発動すると、有効なサボタージュを修正する。
/// </summary>
public static class GhostSaboteur
{
    static GhostRoleAssingData Data;
    private static readonly int Id = 16700; // ★ 他と被らない Id に変更してください

    // ─── オプション ──────────────────────────────────────────────
    public static OptionItem CoolDown;
    public static OptionItem Count;
    public static OptionItem CanFixReactor;
    public static OptionItem CanFixElectrical;
    public static OptionItem CanFixComms;
    public static OptionItem CanFixHeli;
    static OptionItem AssignMadmate;

    // ─── 内部状態 ─────────────────────────────────────────────────
    public static List<byte> PlayerIdList = new();
    public static Dictionary<byte, int> Counts = new();

    // ─── 登録・初期化 ─────────────────────────────────────────────
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.GhostRoles, CustomRoles.GhostSaboteur,
            fromtext: UtilsOption.GetFrom(From.TownOfHost_Pko));

        Data = GhostRoleAssingData.Create(
            Id + 1, CustomRoles.GhostSaboteur, CustomRoleTypes.Crewmate);

        CoolDown = FloatOptionItem.Create(Id + 2, "Cooldown",
                new(0f, 180f, 0.5f), 25f, TabGroup.GhostRoles, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        Count = IntegerOptionItem.Create(Id + 3, "GhostSaboteurCount",
                new(0, 99, 1), 2, TabGroup.GhostRoles, false)
            .SetValueFormat(OptionFormat.Times)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        CanFixReactor = BooleanOptionItem.Create(Id + 4, "GhostSaboteurFixReactor",
                true, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        CanFixElectrical = BooleanOptionItem.Create(Id + 5, "GhostSaboteurFixElectrical",
                true, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        CanFixComms = BooleanOptionItem.Create(Id + 6, "GhostSaboteurFixComms",
                true, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        CanFixHeli = BooleanOptionItem.Create(Id + 7, "GhostSaboteurFixHeli",
                true, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);

        AssignMadmate = BooleanOptionItem.Create(Id + 8, "AssgingMadmate",
                false, TabGroup.GhostRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.GhostSaboteur])
            .SetParentRole(CustomRoles.GhostSaboteur);
    }

    public static void Init()
    {
        PlayerIdList = new();
        Counts.Clear();
        CustomRoleManager.MarkOthers.Add(OtherMark);
        SubRoleRPCSender.AddHandler(CustomRoles.GhostSaboteur, ReceiveRPC);
        Data.SubRoleType = AssignMadmate.GetBool()
            ? CustomRoleTypes.Madmate
            : CustomRoleTypes.Crewmate;
    }

    public static void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    // ─── 守護発動時に呼ぶ ────────────────────────────────────────
    public static void UseAbility(PlayerControl ghost, PlayerControl target)
    {
        if (!ghost.Is(CustomRoles.GhostSaboteur)) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!Counts.ContainsKey(ghost.PlayerId))
            Counts[ghost.PlayerId] = Count.GetInt();
        if (Counts[ghost.PlayerId] <= 0) return;

        if (!Main.IsActiveSabotage) return;

        var sb = Main.SabotageType;
        bool canFix = sb switch
        {
            SystemTypes.Reactor => CanFixReactor.GetBool(),
            SystemTypes.Laboratory => CanFixReactor.GetBool(),
            SystemTypes.Electrical => CanFixElectrical.GetBool(),
            SystemTypes.Comms => CanFixComms.GetBool(),
            SystemTypes.HeliSabotage => CanFixHeli.GetBool(),
            _ => false,
        };
        if (!canFix) return;

        FixSabotage(sb);

        Counts[ghost.PlayerId]--;
        SendRPC(ghost.PlayerId);

        Logger.Info(
            $"[GhostSaboteur] {ghost.Data?.GetLogPlayerName()} が {sb} を修正 " +
            $"残り {Counts[ghost.PlayerId]} 回",
            "GhostSaboteur");

        UtilsNotifyRoles.NotifyRoles(SpecifySeer: ghost);
        ghost.RpcResetAbilityCooldown();
    }

    // ─── サボタージュ修正 ─────────────────────────────────────────
    // ★ CS1061修正: RpcRepairSystem は存在しない
    //   → Utils.AllPlayerKillFlash でも使われている RpcUpdateSystem に統一
    private static void FixSabotage(SystemTypes systemType)
    {
        var ship = ShipStatus.Instance;
        if (ship?.Systems == null) return;

        switch (systemType)
        {
            // ─ リアクター / 研究所 ───────────────────────────────
            // AllPlayerKillFlash と同じシグナル値 16 で修復完了を送信
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
                if (ship.Systems.ContainsKey(systemType))
                    ship.RpcUpdateSystem(systemType, 16);
                break;

            // ─ 停電（電気系）────────────────────────────────────
            // ExpectedSwitches と ActualSwitches の差分を1スイッチずつ送る
            case SystemTypes.Electrical:
                if (ship.Systems.ContainsKey(SystemTypes.Electrical))
                {
                    var elec = ship.Systems[SystemTypes.Electrical]
                        .TryCast<SwitchSystem>();
                    if (elec != null)
                    {
                        byte diff = (byte)(elec.ExpectedSwitches ^ elec.ActualSwitches);
                        for (byte i = 0; i < 5; i++)
                            if ((diff & (1 << i)) != 0)
                                ship.RpcUpdateSystem(SystemTypes.Electrical, i);
                    }
                }
                break;

            // ─ 通信妨害 ──────────────────────────────────────────
            // Skeld/Polus(HudOverride): 0 を送ると修復
            // MIRA HQ(HqHud): コンソール 0/1 に 0x10 フラグ付きで送信
            case SystemTypes.Comms:
                if (ship.Systems.ContainsKey(SystemTypes.Comms))
                {
                    if (ship.Systems[SystemTypes.Comms]
                            .TryCast<HudOverrideSystemType>() != null)
                    {
                        ship.RpcUpdateSystem(SystemTypes.Comms, 0);
                    }
                    else if (ship.Systems[SystemTypes.Comms]
                                 .TryCast<HqHudSystemType>() != null)
                    {
                        ship.RpcUpdateSystem(SystemTypes.Comms, (byte)(0 | 0x10));
                        ship.RpcUpdateSystem(SystemTypes.Comms, (byte)(1 | 0x10));
                    }
                }
                break;

            // ─ ヘリコプター（Airship）────────────────────────────
            // HeliSabotageSystem は RpcUpdateSystem に WriteNetObject が必要なため
            // AllPlayerKillFlash と同じカスタム Writer 方式で送信
            case SystemTypes.HeliSabotage:
                if (ship.Systems.ContainsKey(SystemTypes.HeliSabotage))
                {
                    var player = PlayerControl.LocalPlayer;
                    // コンソール 0 (16) とコンソール 1 (17) の両方を修復
                    foreach (byte val in new byte[] { 16, 17 })
                    {
                        var writer = AmongUsClient.Instance.StartRpcImmediately(
                            ship.NetId,
                            (byte)RpcCalls.UpdateSystem,
                            SendOption.None);
                        writer.Write((byte)SystemTypes.HeliSabotage);
                        writer.WriteNetObject(player);
                        writer.Write(val);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }
                }
                break;
        }
    }

    // ─── 残り回数の表示 ──────────────────────────────────────────
    public static string OtherMark(PlayerControl seer, PlayerControl seen,
        bool isForMeeting = false)
    {
        seen ??= seer;
        if (seer != seen || !seer.Is(CustomRoles.GhostSaboteur)) return "";

        int count = Counts.TryGetValue(seer.PlayerId, out var c) ? c : Count.GetInt();
        return Utils.ColorString(
            UtilsRoleText.GetRoleColor(CustomRoles.GhostSaboteur).ShadeColor(-0.25f),
            $" ({count}/{Count.GetInt()})");
    }

    // ─── RPC ─────────────────────────────────────────────────────
    public static void SendRPC(byte playerId)
    {
        using var sender = new SubRoleRPCSender(CustomRoles.GhostSaboteur, playerId);
        sender.Writer.Write(Counts[playerId]);
    }

    public static void ReceiveRPC(MessageReader reader, byte playerId)
    {
        Counts[playerId] = reader.ReadInt32();
    }
}