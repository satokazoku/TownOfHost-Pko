using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;

namespace TownOfHost.Patches.ISystemType;

[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
public static class SabotageSystemTypeUpdateSystemPatch
{
    private static readonly LogHandler logger = Logger.Handler(nameof(SabotageSystemType));

    static byte amount;
    public static bool Prefix([HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        var newReader = MessageReader.Get(msgReader);
        amount = newReader.ReadByte();
        newReader.Recycle();

        var nextSabotage = (SystemTypes)amount;
        logger.Info($"PlayerName: {player.GetNameWithRole().RemoveHtmlTags()}, SabotageType: {nextSabotage}");

        //HASモードではサボタージュ不可
        if (Options.CurrentGameMode == CustomGameMode.HideAndSeek || Options.IsStandardHAS) return false;

        if (Options.CurrentGameMode is CustomGameMode.SuddenDeath or CustomGameMode.MurderMystery) return false;
        if (GameStates.CalledMeeting && !Utils.IsActive(nextSabotage)) return false;

        if (!CustomRoleManager.OnSabotage(player, nextSabotage))
        {
            return false;
        }
        var roleClass = player.GetRoleClass();
        if (roleClass is IKiller killer && !player.Is(CustomRoles.DemonicSupporter))
        {
            //そもそもサボタージュボタン使用不可ならサボタージュ不可
            if (!killer.CanUseSabotageButton()) return false;
            //その他処理が必要であれば処理
            if (roleClass.OnInvokeSabotage(nextSabotage))
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    Main.SabotageType = (SystemTypes)amount;
                    var sb = Translator.GetString($"sb.{(SystemTypes)amount}");
                    if (!Main.IsActiveSabotage)
                        UtilsGameLog.AddGameLog($"Sabotage", string.Format(Translator.GetString("Log.Sabotage"), UtilsName.GetPlayerColor(player, false), sb));
                    Main.IsActiveSabotage = true;
                    Main.LastSab = player.PlayerId;
                    Main.SabotageActivetimer = 0;
                }
            }
            return roleClass.OnInvokeSabotage(nextSabotage);
        }
        else
        {
            return CanSabotage(player);
        }
    }
    private static bool CanSabotage(PlayerControl player)
    {
        //サボタージュ出来ないキラー役職はサボタージュ自体をキャンセル
        if (!player.Is(CustomRoleTypes.Impostor) && !player.Is(CustomRoles.DemonicSupporter))
        {
            return false;
        }
        if (AmongUsClient.Instance.AmHost)
        {
            if (!Main.IsActiveSabotage)
            {
                Main.SabotageType = (SystemTypes)amount;
                var sb = Translator.GetString($"sb.{(SystemTypes)amount}");

                UtilsGameLog.AddGameLog($"Sabotage", string.Format(Translator.GetString("Log.Sabotage"), UtilsName.GetPlayerColor(player, false), sb));
                Main.IsActiveSabotage = true;
                Main.LastSab = player.PlayerId;
                Main.SabotageActivetimer = 0;
            }
        }
        return true;
    }
    public static void Postfix(SabotageSystemType __instance, bool __runOriginal /* Prefixの結果，本体処理が実行されたかどうか */ )
    {
        if (!__runOriginal || (!Options.ModifySabotageCooldown.GetBool() && (MadWare.RemoveSabotageCooldown is 0)) || !AmongUsClient.Instance.AmHost)
        {
            return;
        }
        var timer = __instance.Timer;
        if (Options.ModifySabotageCooldown.GetBool()) timer = Options.SabotageCooldown.GetFloat();
        // サボタージュクールダウンを変更
        __instance.Timer = timer + MadWare.RemoveSabotageCooldown;
        __instance.IsDirty = true;
    }
}

[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Initialize))]
public static class ElectricTaskInitializePatch
{
    public static void Postfix()
    {
        UtilsOption.MarkEveryoneDirtySettings();
        _ = new LateTask(() => UtilsOption.MarkEveryoneDirtySettings(), 3.1f, "3sElec", true);
        _ = new LateTask(() => UtilsOption.MarkEveryoneDirtySettings(), 5.1f, "5sElec", true);
        if (!GameStates.IsMeeting)
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }
}
[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Complete))]
public static class ElectricTaskCompletePatch
{
    public static void Postfix()
    {
        SabotageComplete.CompleteSabotage(SystemTypes.Electrical);
        UtilsOption.MarkEveryoneDirtySettings();
        if (!GameStates.IsMeeting)
            UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }
}

[HarmonyPatch(typeof(HeliCharlesTask), nameof(HeliCharlesTask.Complete))]
public static class HeliCharlesTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.HeliSabotage);
}
[HarmonyPatch(typeof(HqHudOverrideTask), nameof(HqHudOverrideTask.Complete))]
public static class HqHudOverrideTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.Comms);
}
[HarmonyPatch(typeof(MushroomMixupSabotageTask), nameof(MushroomMixupSabotageTask.Complete))]
public static class MushroomMixupSabotageTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.MushroomMixupSabotage);
}
[HarmonyPatch(typeof(NoOxyTask), nameof(NoOxyTask.Complete))]
public static class NoOxyTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.LifeSupp);
}
[HarmonyPatch(typeof(ReactorTask), nameof(ReactorTask.Complete))]
public static class ReactorTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.Reactor);
}
[HarmonyPatch(typeof(HudOverrideTask), nameof(HudOverrideTask.Complete))]
public static class HudOverrideTaskCompletePatch
{
    public static void Postfix() => SabotageComplete.CompleteSabotage(SystemTypes.Comms);
}
public static class SabotageComplete
{
    public static void CompleteSabotage(SystemTypes system)
    {
        if (system is SystemTypes.Reactor && Main.NormalOptions?.MapId is 2) system = SystemTypes.Laboratory;

        if (Main.SabotageType is SystemTypes.Hallway || system != Main.SabotageType || Main.SabotageActivetimer <= 0) return;
        var sb = Translator.GetString($"sb.{Main.SabotageType}");
        if (Main.SabotageType == SystemTypes.MushroomMixupSabotage)
            UtilsGameLog.AddGameLog($"MushroomMixup", string.Format(Translator.GetString("Log.FixSab"), sb));
        else UtilsGameLog.AddGameLog($"{Main.SabotageType}", string.Format(Translator.GetString("Log.FixSab"), sb));
        var oldisactivesabotage = Main.IsActiveSabotage;
        Main.IsActiveSabotage = false;
        Main.SabotageActivetimer = 0;

        if (oldisactivesabotage is false) return;

        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            role.AfterSabotage(Main.SabotageType);
        }
    }
}