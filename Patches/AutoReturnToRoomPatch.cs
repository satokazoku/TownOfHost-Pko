using HarmonyLib;

namespace TownOfHost.Patches
{
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.ShowButtons))]
    public static class AutoReturnToRoomPatch
    {
        private static bool ReturnScheduled;

        public static void Postfix(EndGameManager __instance)
        {
            // ホスト以外は実行しない
            if (!AmongUsClient.Instance.AmHost) return;

            // 自動戻り設定がOFFなら終了
            if (!Options.OptionAutoReturnRoom.GetBool()) return;

            // 「GMの場合のみ」がONなら GM 以外は終了
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
