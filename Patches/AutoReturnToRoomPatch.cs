using HarmonyLib;

namespace TownOfHost.Patches
{
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.ShowButtons))]
    public static class AutoReturnToRoomPatch
    {
        private static bool ReturnScheduled;

        public static void Postfix(EndGameManager __instance)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (!Options.OptionAutoReturnRoom.GetBool()) return;

            if (Options.OptionAutoReturnRoomGM.GetBool() && !Options.EnableGM.GetBool())
                return;

            if (ReturnScheduled) return;
            ReturnScheduled = true;

            _ = new LateTask(() =>
            {
                ReturnScheduled = false;
                if (!AmongUsClient.Instance.AmHost) return;

                var nav = DestroyableSingleton<EndGameNavigation>.Instance;
                nav?.NextGame();
            }, 5f, "AutoReturnToRoom", true);
        }
    }
}
