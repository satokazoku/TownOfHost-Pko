using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
    class
        ZiplinePatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] ZiplineBehaviour ziplineBehaviour, [HarmonyArgument(2)] bool fromTop)
        {
            if (AmongUsClient.Instance.AmHost is false) return true;

            //波動砲がチャージ中またはビーム中ならジップライン使用禁止
            if (__instance.GetRoleClass() is Roles.Impostor.HadouHo hadouHo)
            {
                if (hadouHo.IsCharging || hadouHo.ShowBeamMark)
                {
                    return false;
                }
            }

            //ジャッカル波動砲
            if (__instance.GetRoleClass() is Roles.Neutral.JackalHadouHo jackalHo)
            {
                if (jackalHo.IsCharging || jackalHo.ShowBeamMark)
                    return false;
            }

            //波動砲シェリフ
            if (__instance.GetRoleClass() is Roles.Crewmate.SheriffHadouHo sheriffHo)
            {
                if (sheriffHo.IsCharging || sheriffHo.ShowBeamMark)
                    return false;
            }

            return true;
        }
    }
}