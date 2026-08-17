/*
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Impostor;

public sealed class Conjurer : RoleBase, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Conjurer),
            player => new Conjurer(player),
            CustomRoles.Conjurer,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            560100,
            SetupOptionItem,
            "cnj",
            OptionSort: (3, 8),
            from: From.SuperNewRoles
        );

    public Conjurer(PlayerControl player) : base(RoleInfo, player)
    {
        BeaconCooldown = OptionBeaconCooldown.GetFloat();
        CanKillImpostor = OptionCanKillImpostor.GetBool();
        ShowFlash = OptionShowFlash.GetBool();
        CanAddLength = OptionCanAddLength.GetFloat();

        beaconPositions = new();
        beaconObjects = new();
    }

    static OptionItem OptionBeaconCooldown;
    static OptionItem OptionCanKillImpostor;
    static OptionItem OptionShowFlash;
    static OptionItem OptionCanAddLength;

    static float BeaconCooldown;
    static bool CanKillImpostor;
    static bool ShowFlash;
    static float CanAddLength;

    enum OptionName
    {
        ConjurerBeaconCooldown,
        ConjurerCanKillImpostor,
        ConjurerShowFlash,
        ConjurerCanAddLength,
    }

    private readonly List<Vector2> beaconPositions;
    private readonly List<GameObject> beaconObjects;
    public int BeaconCount => beaconPositions.Count;

    static void SetupOptionItem()
    {
        OptionBeaconCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.ConjurerBeaconCooldown,
            new(1f, 60f, 1f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCanAddLength = FloatOptionItem.Create(RoleInfo, 11, OptionName.ConjurerCanAddLength,
            new(1f, 40f, 1f), 10f, false).SetValueFormat(OptionFormat.Multiplier);
        OptionCanKillImpostor = BooleanOptionItem.Create(RoleInfo, 12, OptionName.ConjurerCanKillImpostor, false, false);
        OptionShowFlash = BooleanOptionItem.Create(RoleInfo, 13, OptionName.ConjurerShowFlash, false, false);
    }

    public float CalculateKillCooldown() => 30f;
    public bool CanUseKillButton() => false;
    public bool CanUseImpostorVentButton() => true;
    public bool CanUseSabotageButton() => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = BeaconCooldown;
    }

    public override void Add()
    {
        ClearBeacons();
    }

    public override void OnDestroy() => ClearBeacons();

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        if (!Player.IsAlive()) return;

        if (BeaconCount < 3)
        {
            var pos = Player.GetTruePosition();

            if (BeaconCount > 0 && Vector2.Distance(pos, beaconPositions[^1]) > CanAddLength)
                return;

            if (AmongUsClient.Instance.AmHost)
            {
                ExecuteAddBeacon(pos);
            }
            else
            {
                using var sender = CreateSender();
                sender.Writer.Write((byte)0);
                sender.Writer.Write(pos.x);
                sender.Writer.Write(pos.y);
            }
        }
        else
        {
            if (AmongUsClient.Instance.AmHost)
            {
                ExecuteTriangleKill();
            }
            else
            {
                using var sender = CreateSender();
                sender.Writer.Write((byte)1);
            }
        }

        _ = new LateTask(() =>
        {
            if (!Player.IsAlive()) return;
            AURoleOptions.PhantomCooldown = BeaconCooldown;
            Player.RpcResetAbilityCooldown();
        }, 0.1f, "Conjurer.CDReset", true);
    }

    void ExecuteAddBeacon(Vector2 pos)
    {
        if (BeaconCount >= 3) return;

        using var sender = CreateSender();
        sender.Writer.Write((byte)2);
        sender.Writer.Write(pos.x);
        sender.Writer.Write(pos.y);
    }

    void ExecuteTriangleKill()
    {
        if (BeaconCount < 3) return;

        var poly = beaconPositions.ToArray();
        int kills = 0;

        foreach (var pc in AllAlivePlayerControls.ToArray())
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (!CanKillImpostor && pc.GetCustomRole().IsImpostor()) continue;
            if (!PointInPolygon(pc.GetTruePosition(), poly)) continue;

            pc.SetRealKiller(Player);
            Player.RpcMurderPlayer(pc);
            kills++;
        }

        if (ShowFlash) AllPlayerKillFlash();

        UtilsGameLog.AddGameLog("Conjurer",
            $"{UtilsName.GetPlayerColor(Player)} 三角形キル発動！{kills}人キル");

        using var sender = CreateSender();
        sender.Writer.Write((byte)3);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte rpcType = reader.ReadByte();
        if (rpcType == 0)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            ExecuteAddBeacon(pos);
        }
        else if (rpcType == 1)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            ExecuteTriangleKill();
        }
        else if (rpcType == 2)
        {
            var pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            if (!beaconPositions.Any(p => Vector2.Distance(p, pos) < 0.01f))
            {
                beaconPositions.Add(pos);

                if (Player.AmOwner)
                    beaconObjects.Add(CreateBeaconVisual(pos, BeaconCount - 1));
            }

            if (Player?.AmOwner == true)
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);

            UtilsGameLog.AddGameLog("Conjurer",
                $"{UtilsName.GetPlayerColor(Player)} ビーコン設置 ({BeaconCount}/3)");
        }
        else if (rpcType == 3)
        {
            ClearBeacons();
        }
    }

    private void ClearBeacons()
    {
        beaconPositions.Clear();
        foreach (var obj in beaconObjects)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        beaconObjects.Clear();

        if (Player?.AmOwner == true)
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    private static GameObject CreateBeaconVisual(Vector2 pos, int index)
    {
        try
        {
            var obj = new GameObject($"ConjurerBeacon_{index}");
            obj.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f);
            obj.layer = 5;
            var sr = obj.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(8, 8);
            var px = new Color[64];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(0.61f, 0.19f, 1f, 0.85f);
            tex.SetPixels(px); tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);
            obj.transform.localScale = Vector3.one * 0.25f;
            return obj;
        }
        catch (System.Exception e) { Logger.Error(e.ToString(), "Conjurer.Visual"); return null; }
    }

    public override void OnStartMeeting()
    {
        ClearBeacons();
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive() || isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (BeaconCount == 3)
            return $"{size}<color={color}>▲三角形完成！ファントムでキル発動！</color>";

        bool inRange = BeaconCount == 0 ||
            Vector2.Distance(Player.GetTruePosition(), beaconPositions[^1]) <= CanAddLength;

        if (!inRange)
            return $"{size}<color=#888888>範囲外 ({CanAddLength}m 以内に近づいてください)</color>";

        return $"{size}<color={color}>ファントムでビーコン設置 ({BeaconCount}/3)</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        return $"<color={RoleInfo.RoleColorCode}>({BeaconCount}/3)</color>";
    }

    public override string GetAbilityButtonText()
        => BeaconCount < 3 ? GetString("ConjurerBeaconButton") : GetString("ConjurerStartButton");

    public override bool OverrideAbilityButton(out string text)
    {
        text = BeaconCount < 3 ? "Conjurer_Beacon" : "Conjurer_Start";
        return true;
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        var old = poly[^1];
        for (int i = 0; i < poly.Length; i++)
        {
            var cur = poly[i];
            Vector2 p1, p2;
            if (cur.x > old.x) { p1 = old; p2 = cur; } else { p1 = cur; p2 = old; }
            if ((p1.x < p.x) == (p.x <= p2.x) &&
                (p.y - p1.y) * (p2.x - p1.x) < (p2.y - p1.y) * (p.x - p1.x))
                inside = !inside;
            old = cur;
        }
        return inside;
    }
}
*/