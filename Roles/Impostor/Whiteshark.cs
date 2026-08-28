using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppSystem.Collections.Generic;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static UnityEngine.UI.ContentSizeFitter;

namespace TownOfHost.Roles.Impostor;

public sealed class Whiteshark : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Whiteshark),
            player => new Whiteshark(player),
            CustomRoles.Whiteshark,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            126900,
            SetupOptionItem,
            "wsk",
            "#ff1919",
            OptionSort: (3, 6)
        );
    public Whiteshark(PlayerControl player)
    : base(RoleInfo, player)
    {
        StopTime = OptStopTime.GetFloat();
        stopTimer = 0f;
        isStopped = false;
        lastPosition = Vector2.zero;
        positionInitialized = false;
        spawnTimer = 0f;
        IsVented = false;

    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptStopTime;
    static OptionItem OptionCanVent;

    static float StopTime;
    float stopTimer;
    bool isStopped;
    Vector2 lastPosition;
    bool positionInitialized;
    float spawnTimer;
    static bool IsVented;
    float Last;
    float Cool;
    static float KillCooldown => OptionKillCooldown?.GetFloat() ?? 17.5f;
    static bool CanVent => OptionCanVent?.GetBool() ?? false;

    enum OptionName
    {
        TunaStopTime,
    }
    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 17.5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptStopTime = FloatOptionItem.Create(RoleInfo, 12, OptionName.TunaStopTime, new(0.5f, 180f, 0.5f), 4f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => CanVent;

    public override bool CanClickUseVentButton => CanVent;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVent;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = StopTime - stopTimer;
    }
    bool IUsePhantomButton.IsPhantomRole => true;

    bool IUsePhantomButton.IsresetAfterKill => false;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = true;
    }
    static bool IsUsingMovingPlatform(PlayerControl pc)
    {
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return true;
        if (pc.onLadder) return true;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship
            && Vector2.Distance(pc.GetTruePosition(), new Vector2(7.76f, 8.56f)) <= 1.9f) return true;
        if (pc.MyPhysics.Animations.Animator.GetCurrentAnimation()?.name?.Contains("Zipline") == true) return true;
        if (pc.MyPhysics.Animations.Animator.GetCurrentAnimation()?.name?.Contains("Platform") == true) return true;
        return false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!player.IsAlive()) return;
        if (GameStates.CalledMeeting || GameStates.Intro) return;

        spawnTimer += Time.fixedDeltaTime;
        if (spawnTimer < 5f)
        {
            stopTimer = 0f;
            isStopped = false;
            lastPosition = player.GetTruePosition();
            return;
        }

        if (IsUsingMovingPlatform(player))
        {
            stopTimer = 0f;
            isStopped = false;
            lastPosition = player.GetTruePosition();
            return;
        }

        var currentPos = player.GetTruePosition();

        if (!positionInitialized)
        {
            lastPosition = currentPos;
            positionInitialized = true;
            return;
        }

        float moved = Vector2.Distance(currentPos, lastPosition);
        lastPosition = currentPos;
        Cool = StopTime - stopTimer;
        if (moved < 0.01f)
        {
            if (!isStopped)
            {
                isStopped = true;
            }
            //ベント内だったら止まってる秒数増やさないので、クールだけ反映する
            if (IsVented)
            {
                Cool = StopTime - stopTimer;

                if (0.25 < Cool)
                {
                    AURoleOptions.PhantomCooldown = StopTime - stopTimer;

                    Cool = 0;
                    var cooldown = Cool;
                    if (Last != cooldown)
                    {
                        Last = cooldown;
                        Player.MarkDirtySettings();
                    }

                    Player.RpcResetAbilityCooldown(log: false);
                }
                return;
            }
            stopTimer += Time.fixedDeltaTime;
            if (stopTimer >= StopTime)
            {
                PlayerState.GetByPlayerId(player.PlayerId).DeathReason = CustomDeathReason.Suicide;
                player.RpcMurderPlayerV2(player);
                stopTimer = 0f;
                isStopped = false;
            }
        }
        else
        {
            stopTimer = 0f;
            isStopped = false;
        }
        Cool = StopTime - stopTimer;

        if (0.25 < Cool)
        {
            AURoleOptions.PhantomCooldown = StopTime - stopTimer;

            Cool = 0;
            var cooldown = Cool;
            if (Last != cooldown)
            {
                Last = cooldown;
                Player.MarkDirtySettings();
            }
            Player.RpcResetAbilityCooldown(log: false);
        }

    }

    public override void AfterMeetingTasks()
    {
        stopTimer = 0f;
        isStopped = false;
        positionInitialized = false;
        spawnTimer = 0f;
        IsVented = false;
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!CanVent)
        {
            return false;
        }
        else
        {
            IsVented = true;
            return true;
        }
    }
    public static void OnExitVent(PlayerPhysics physics)
    {
        IsVented = false;
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Suicider_Vent";
        return true;
    }
    public override string GetAbilityButtonText() => GetString("ShyBoyText");
}
