using System;

// No tile entity and no wires, so it can travel with the car. Obeys the same power rule via the outside panels.
public class BlockHSLiftInsidePanel : Block
{
    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        var own = new System.Collections.Generic.List<BlockActivationCommand>();
        try
        {
            var pos = BlockHSLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null) return own.ToArray();
            HSLiftConfiguration.Use(lift);
            own.AddRange(HSLiftFloorMenu.Commands(lift));
        }
        catch (Exception e) { HSLiftDebug.Error("Inside panel floor list failed", e); }
        return own.ToArray();
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = BlockHSLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null)
                return Localization.Get("hsliftUnregistered");
            var ctrl = HSLiftController.Of(lift);
            if (ctrl != null && ctrl.IsThisMoving) return Localization.Get("hsliftBusy");
            var notReady = BlockHSLiftOutsidePanel.NotReadyText(lift);
            if (notReady != null) return notReady;
            return HSLiftFloorMenu.Prompt();
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Inside panel text failed", e);
            return "";
        }
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSLiftPanelCollider.Ensure(_ebcd, _blockPos);
    }

    // Tap E does nothing. Hold E opens the floor list (command overload below).
    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return true;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (HSLiftActivation.IsTake(_commandName))
            return true;
        try
        {
            var pos = BlockHSLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null)
            {
                if (_player != null)
                    GameManager.ShowTooltip(_player, Localization.Get("hsliftUnregistered"));
                return true;
            }
            HSLiftConfiguration.Use(lift);
            int floor = HSLiftFloorMenu.Parse(_commandName, lift);
            if (floor < 0) return true;
            var problem = HSLiftController.RequestFromInsidePanel(pos, floor);
            if (problem != null && _player != null && !HSLiftFloorMenu.IsAlreadyHere(problem))
                GameManager.ShowTooltip(_player, string.Format(Localization.Get("hsliftNotReady"), problem));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Inside panel press failed", e);
        }
        return true;
    }
}
