using AmongUs.GameOptions;
using HarmonyLib;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
    class SwitchGameModePatch
    {
        public static void Postfix(GameModes gameMode)
        {
            Main.HnSFlag = false;
            if (gameMode == GameModes.HideNSeek)
            {
                Main.HnSFlag = true;
                if (!DebugModeManager.AmDebugger)
                {
                    ErrorText.Instance.HnSFlag = true;
                    ErrorText.Instance.AddError(ErrorCode.HnsUnload);
                    Harmony.UnpatchAll();
                    Main.Instance.Unload();
                }
            }
        }
    }
}