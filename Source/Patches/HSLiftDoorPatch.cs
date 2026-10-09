using System;
using HarmonyLib;

// Composite door commands arrive as "TEFeatureDoor:open" / "TEFeatureDoor:close".
// Ped: cabin + landing at a floor open/close together. Vehicle: one garage/roll-up set per landing, no cabin pair.
// Opening is blocked unless the parked car is at that floor. Close does not send the car; pick a floor with hold E.
[HarmonyPatch(typeof(BlockCompositeTileEntity), "OnBlockActivated",
    new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class HSLiftDoorPatch
{
    static bool IsCommand(string name, string cmd)
    {
        return name != null && (name == cmd || name.EndsWith(":" + cmd, StringComparison.Ordinal));
    }

    static bool Prefix(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player, ref bool __result)
    {
        try
        {
            if (_commandName != null && _commandName.StartsWith("hsliftFloor", StringComparison.Ordinal))
            {
                var playerLift = HSLiftConfiguration.LiftForPlayer(_player);
                if (playerLift != null) HSLiftConfiguration.Use(playerLift);
                int floor = HSLiftFloorMenu.Parse(_commandName, playerLift ?? HSLiftConfiguration.Data);
                if (floor >= 0)
                {
                    var err = HSLiftController.RequestFromDoorMenu(_player, floor);
                    if (err != null && _player != null && !HSLiftFloorMenu.IsAlreadyHere(err))
                        GameManager.ShowTooltip(_player, string.Format(Localization.Get("hsliftNotReady"), err));
                    __result = true;
                    return false;
                }
            }
            if (HSLiftActivation.IsTake(_commandName) && HSLiftDoors.IsElevatorDoor(_blockValue.Block))
            {
                var takeParent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
                if (HSLiftDoors.BindDoor(takeParent, _world.GetBlock(takeParent)) != null)
                {
                    __result = true;
                    return false;
                }
            }
            bool open = IsCommand(_commandName, "open");
            bool close = IsCommand(_commandName, "close");
            if ((!open && !close) || !HSLiftDoors.IsElevatorDoor(_blockValue.Block)) return true;
            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var parentBv = _world.GetBlock(parent);
            if (HSLiftDoors.BindDoor(parent, parentBv) == null)
            {
                HSLiftDebug.Info("Door " + _commandName + " at " + parent + " is not bound to a lift");
                return true;
            }
            bool behind = HSLiftDoors.CarIsBehind(parent, parentBv);
            bool vehicle = HSLiftConfiguration.IsVehicle;
            var doorFloor = HSLiftConfiguration.FloorForPanel(parent.y);
            bool parkedHere = behind || (doorFloor != null && HSLiftConfiguration.Data.CurrentY == doorFloor.Y && !HSLiftController.IsMoving);
            HSLiftDebug.Info("Door " + _commandName + " at " + parent + " parkedHere=" + parkedHere + " behind=" + behind);

            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (close)
            {
                if (parkedHere && !vehicle)
                {
                    if (world != null) HSLiftDoors.CloseAtCar(world);
                    if (HSLiftNet.IsRemoteClient)
                    {
                        var id = HSLiftConfiguration.Data != null ? HSLiftConfiguration.Data.ElevatorId : "";
                        HSLiftNet.SendDoors(id, false);
                    }
                    else HSLiftController.OnLiftDoorClosed(_player);
                    __result = true;
                    return false;
                }
                return true;
            }
            if (parkedHere)
            {
                if (world != null && HSLiftDoors.IsCarDoor(world, parent) && !HSLiftDoors.HasLandingOutside(world, parent))
                {
                    if (_player != null)
                        GameManager.ShowTooltip(_player, Localization.Get("hsliftDoorNoLanding"));
                    Audio.Manager.BroadcastPlayByLocalPlayer(parent.ToVector3() + UnityEngine.Vector3.one * 0.5f,
                        vehicle ? "door_garage_metal_locked" : "door_elevator_locked");
                    HSLiftDebug.Verbose("Blocked opening lift door at " + parent + " (no landing outside)");
                    __result = false;
                    return false;
                }
                if (!vehicle)
                {
                    if (world != null) HSLiftDoors.OpenAtCar(world);
                    if (HSLiftNet.IsRemoteClient)
                    {
                        var id = HSLiftConfiguration.Data != null ? HSLiftConfiguration.Data.ElevatorId : "";
                        HSLiftNet.SendDoors(id, true);
                    }
                    __result = true;
                    return false;
                }
                return true;
            }

            if (_player != null)
            {
                string msg;
                if (HSLiftController.IsMoving) msg = Localization.Get("hsliftDoorMoving");
                else
                {
                    var problem = HSLiftController.RequestFromLockedDoor(parent);
                    msg = problem == null ? Localization.Get("hsliftCalled") : string.Format(Localization.Get("hsliftNotReady"), problem);
                }
                GameManager.ShowTooltip(_player, msg);
            }
            Audio.Manager.BroadcastPlayByLocalPlayer(parent.ToVector3() + UnityEngine.Vector3.one * 0.5f,
                HSLiftConfiguration.IsVehicle ? "door_garage_metal_locked" : "door_elevator_locked");
            HSLiftDebug.Verbose("Blocked opening lift door at " + parent + " (car at Y" + HSLiftConfiguration.Data.CurrentY + ", state " + HSLiftController.State + ")");
            __result = false;
            return false;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Door check failed", e);
            return true;
        }
    }

}

// Standing in the parked car (no inside panel), the lift door's hold-E menu also lists the floors.
[HarmonyPatch(typeof(BlockCompositeTileEntity), "GetBlockActivationCommands",
    new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
public static class HSLiftDoorMenuPatch
{
    static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing, ref BlockActivationCommand[] __result)
    {
        try
        {
            if (!HSLiftDoors.IsElevatorDoor(_blockValue.Block)) return;
            var list = new System.Collections.Generic.List<BlockActivationCommand>();
            if (__result != null)
                foreach (var c in __result)
                    if (!HSLiftActivation.IsTake(c.text)) list.Add(c);
            HSLiftActivation.PutOpenCloseFirst(list);

            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var parentBv = _world.GetBlock(parent);
            if (HSLiftDoors.BindDoor(parent, parentBv) == null)
            {
                __result = list.ToArray();
                return;
            }

            var d = HSLiftConfiguration.Data;
            var player = _entityFocusing as EntityPlayerLocal;
            if (!HSLiftController.IsMoving && d.HasCar && d.Floors.Count > 1
                && HSLiftController.PlayerInCar(player)
                && HSLiftDoors.CarIsBehind(parent, parentBv))
                list.AddRange(HSLiftFloorMenu.Commands(d));

            __result = list.ToArray();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Door floor menu failed", e);
        }
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), "GetActivationText",
    new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
public static class HSLiftDoorTextPatch
{
    static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing, ref string __result)
    {
        try
        {
            if (!HSLiftDoors.IsElevatorDoor(_blockValue.Block)) return;
            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var te = _world.GetTileEntity(parent) as TileEntityComposite;
            var door = te != null ? te.GetFeature<TEFeatureDoor>() : null;
            if (door == null) return;
            __result = door.IsOpen() ? Localization.Get("hsliftDoorClose") : Localization.Get("hsliftDoorOpen");
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Door activation text failed", e);
        }
    }
}
