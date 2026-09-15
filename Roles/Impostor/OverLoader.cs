using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using Hazel;
using System.Collections.Generic;
using System;

namespace TownOfHost.Roles.Impostor;

public sealed class OverLoader : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(OverLoader),
            player => new OverLoader(player),
            CustomRoles.OverLoader,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            128000,
            SetupOptionItem,
            "ov",
            OptionSort: (4, 5),
            from: From.TownOfHost_K
        );
    public OverLoader(PlayerControl player)
    : base(
        RoleInfo,
        player
        )
    {

    }
    static OptionItem OptionCoolDown;
    bool IsOverLoad;
    float OverLoadTimer;
    static void SetupOptionItem()
    {
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = OptionCoolDown.GetFloat();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        OverLoadTimer -= Time.fixedDeltaTime;
    }
    public override void OnStartMeeting()
    {

    }
    public override void OnReportDeadBody(PlayerControl _, NetworkedPlayerInfo __) => IsOverLoad = false;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Jumper_Ability";
        return true;
    }
    public override string GetAbilityButtonText()
    {
        return GetString("Jumpertext");
    }
}
