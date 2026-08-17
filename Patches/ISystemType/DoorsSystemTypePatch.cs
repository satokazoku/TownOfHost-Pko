using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using System.Linq;

namespace TownOfHost.Patches.ISystemType;

[HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.UpdateSystem))]
public static class DoorsSystemTypeUpdateSystemPatch
{
    private static bool DoorsProgressing = false;
    public static bool Prefix(DoorsSystemType __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        byte amount;
        {
            var newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }
        if (player.Is(CustomRoles.Opener) || (RoleAddAddons.GetRoleAddon(player.GetCustomRole(), out var data, player, subrole: CustomRoles.Opener) && data.GiveOpener.GetBool()))
        {
            if (DoorsProgressing) return true;
            int mapId = Main.NormalOptions.MapId;
            if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) mapId = AmongUsClient.Instance.TutorialMapId;
            var shipStatus = ShipStatus.Instance;

            DoorsProgressing = true;
            if (mapId == 2)
            {
                //Polus
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 71, 72);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 67, 68);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 64, 66);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 73, 74);
            }
            else if (mapId == 4)
            {
                //Airship
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 64, 67);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 71, 73);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 74, 75);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 76, 78);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 68, 70);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 83, 84);
                ShipStatusUpdateSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 79, 82);
            }
            else if (mapId == 5)
            {
                // Fungle
                var openedDoorId = amount & DoorsSystemType.IdMask;
                var openedDoor = shipStatus.AllDoors.FirstOrDefault(door => door.Id == openedDoorId);
                if (openedDoor == null)
                {
                    Logger.Warn($"不明なドアが開けられました: {openedDoorId}", "");
                }
                else
                {
                    // 同じ部屋のドアで，今から開けるドアではないものを全部開ける
                    var room = openedDoor.Room;
                    foreach (var door in shipStatus.AllDoors)
                    {
                        if (door.Id != openedDoorId && door.Room == room)
                        {
                            door.SetDoorway(true);
                        }
                    }
                }
            }
            DoorsProgressing = false;
            return true;
        }
        if (Roles.AddOns.Common.Amnesia.CheckAbility(player))
            if (player.GetRoleClass() is ISystemTypeUpdateHook systemTypeUpdateHook && !systemTypeUpdateHook.UpdateDoorsSystem(__instance, amount))
            {
                return false;
            }
        return true;
    }
}
