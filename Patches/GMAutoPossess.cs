using System.Linq;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;
using TMPro;

namespace TownOfHost.Patches;

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class GMAutoOpenHauntMenuPatch
{
    public static void Postfix()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;
        if (!Options.OptionGMAutoPossess.GetBool()) return;
        if (local.GetCustomRole() != CustomRoles.GM) return;
        if (local.IsAlive()) return;

        _ = new LateTask(() =>
        {
            try
            {
                var buttons = UnityEngine.Object.FindObjectsOfType<PassiveButton>();
                foreach (var btn in buttons)
                {
                    if (!btn.gameObject.activeInHierarchy) continue;

                    var tmpro = btn.GetComponentInChildren<TextMeshPro>();
                    string label = tmpro != null ? tmpro.text : "";
                    string objName = btn.gameObject.name.ToLower();

                    if (objName.Contains("haunt") || label.Contains("憑依") || label.ToLower().Contains("haunt"))
                    {
                        btn.OnClick?.Invoke();
                        break;
                    }
                }
            }
            catch { }
        }, 1.2f, "GMAutoOpenHauntMenu", true);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class GMAutoPossessPatch
{
    static PlayerControl currentTarget = null;
    static PlayerControl lastTarget = null;
    static float searchTimer = 0f;

    public static void Postfix(PlayerControl __instance)
    {
        if (__instance != PlayerControl.LocalPlayer) return;
        if (!Options.OptionGMAutoPossess.GetBool()) return;
        if (__instance.IsAlive() || MeetingHud.Instance != null) return;
        if (__instance.GetCustomRole() != CustomRoles.GM) return;

        if (currentTarget == null || !currentTarget.IsAlive())
        {
            searchTimer += Time.fixedDeltaTime;
            if (searchTimer > 1f)
            {
                searchTimer = 0f;
                var targets = PlayerCatch.AllAlivePlayerControls
                    .Where(p => p.PlayerId != __instance.PlayerId)
                    .ToList();
                if (targets.Count > 0)
                    currentTarget = targets[UnityEngine.Random.Range(0, targets.Count)];
            }
        }

        var hauntMenu = UnityEngine.Object.FindObjectOfType<HauntMenuMinigame>();
        if (hauntMenu != null && hauntMenu.gameObject.activeInHierarchy
            && currentTarget != null && currentTarget.IsAlive())
        {
            if (currentTarget != lastTarget)
            {
                hauntMenu.SetHauntTarget(currentTarget);
                lastTarget = currentTarget;
            }
        }
    }
}