using HarmonyLib;

namespace TownOfHost.Patches;

[HarmonyPatch(typeof(HauntMenuMinigame))]
public static class HauntMenuMinigamePatch
{
    [HarmonyPatch(nameof(HauntMenuMinigame.FixedUpdate)), HarmonyPrefix]
    public static void FixedUpdatePrefix(HauntMenuMinigame __instance)
    {
        if (__instance.HauntTarget != null && (!PlayerControl.LocalPlayer.IsGhostRole() || Options.GhostRoleCanSeeOtherRoles.GetBool()) && (Options.GhostCanSeeOtherRoles.GetBool() || !Options.GhostOptions.GetBool()) && !PlayerControl.LocalPlayer.Is(Roles.Core.CustomRoles.AsistingAngel))
        {
            // 役職表示をカスタムロール名で上書き
            __instance.FilterText.text = UtilsRoleText.GetDisplayRoleName(PlayerControl.LocalPlayer, __instance.HauntTarget);
        }
    }
}
