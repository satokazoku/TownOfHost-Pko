using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Moving : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Moving),
            player => new Moving(player),
            CustomRoles.Moving,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            33100,
            SetupOptionItem,
            "mv",
            "#00FF00",
            (6, 8),
            from: From.SuperNewRoles
        );

    public Moving(PlayerControl player) : base(RoleInfo, player)
    {
        TeleportCooldown = OptionTeleportCooldown.GetFloat();

        markedPos = null;
        hasMarked = false;
        cooldownLeft = 0f;

        PetActionManager.Register(Player.PlayerId, OnPet);
    }

    static OptionItem OptionTeleportCooldown;
    static float TeleportCooldown;

    enum OptionName { MovingTeleportCooldown }

    static void SetupOptionItem()
    {
        OptionTeleportCooldown = FloatOptionItem.Create(
            RoleInfo, 10, OptionName.MovingTeleportCooldown,
            new(2.5f, 120f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    Vector2? markedPos;
    bool hasMarked;
    float cooldownLeft;

    void OnPet()
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!hasMarked)
        {
            markedPos = Player.transform.position;
            hasMarked = true;
            cooldownLeft = TeleportCooldown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            Utils.SendMessage(
                $"<color=#00ccff>ワープ先を設定しました！</color>\n<size=70%>ペットを撫でるといつでもワープします。</size>",
                Player.PlayerId);
            return;
        }

        if (cooldownLeft > 0f) return;

        if (markedPos.HasValue)
        {
            var capturedPos = markedPos.Value;
            cooldownLeft = TeleportCooldown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);

            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                Player.RpcSnapToForced(capturedPos);
                UtilsGameLog.AddGameLog("Moving",
                    $"{UtilsName.GetPlayerColor(Player)} がワープした → {capturedPos}");
            }, 0.5f, $"Moving.Warp.{Player.PlayerId}", true);
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (!hasMarked || cooldownLeft <= 0f) return;

        float prev = cooldownLeft;
        cooldownLeft -= Time.fixedDeltaTime;
        if (cooldownLeft < 0f) cooldownLeft = 0f;

        if (Mathf.FloorToInt(prev) != Mathf.FloorToInt(cooldownLeft))
        {
            Player.MarkDirtySettings();
            SendRpc();
        }
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = cooldownLeft > 0f ? cooldownLeft : TeleportCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!hasMarked)
            return $"{size}<color={color}>ペットを撫でてワープ先を設定</color>";
        return $"{size}<color={color}>ペットを撫でてワープ！</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(hasMarked);
        sender.Writer.Write(cooldownLeft);
        sender.Writer.Write(markedPos.HasValue);
        if (markedPos.HasValue)
        {
            sender.Writer.Write(markedPos.Value.x);
            sender.Writer.Write(markedPos.Value.y);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        hasMarked = reader.ReadBoolean();
        cooldownLeft = reader.ReadSingle();
        bool hasPos = reader.ReadBoolean();
        markedPos = hasPos
            ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
            : null;
    }
}