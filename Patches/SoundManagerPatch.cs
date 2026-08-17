using HarmonyLib;

namespace TownOfHost
{
    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.CrossFadeSound))]
    class SoundManagerCrossFadeSoundPatch
    {
        public static void Prefix(SoundManager __instance, [HarmonyArgument(0)] string name, [HarmonyArgument(2)] ref float maxVolume)
        {
            if (name == "MapTheme")
            {
                maxVolume = GameStates.canmusic ? Main.MapTheme.Value : 0;
            }
        }
    }
}