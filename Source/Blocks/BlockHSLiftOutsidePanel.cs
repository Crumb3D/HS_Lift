using System;
using UnityEngine;

public class BlockHSLiftOutsidePanel : BlockPowered
{
    public override TileEntityPowered CreateTileEntity(Chunk chunk)
    {
        return base.CreateTileEntity(chunk);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift == null) return new BlockActivationCommand[0];
            HSLiftConfiguration.Use(lift);
            return new[] { new BlockActivationCommand("hsliftCall", "electric_switch", true, false, null) };
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Outside panel commands failed", e);
            return new BlockActivationCommand[0];
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift == null)
                return Localization.Get("hsliftUnregistered");
            var ctrl = HSLiftController.Of(lift);
            if (ctrl != null && ctrl.IsThisMoving) return Localization.Get("hsliftBusy");
            var notReady = NotReadyText(lift);
            if (notReady != null) return notReady;
            return Localization.Get("hsliftCall");
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Outside panel text failed", e);
            return "";
        }
    }

    public static string NotReadyText(HSLiftConfigData d)
    {
        string problem;
        if (!HSLiftPower.HasWorkingPower(d, out problem))
            return string.Format(Localization.Get("hsliftNotReady"), problem);
        return null;
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSLiftPanelCollider.Ensure(_ebcd, _blockPos);
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (HSLiftActivation.IsTake(_commandName))
            return true;
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift != null) HSLiftConfiguration.Use(lift);
            var problem = HSLiftController.RequestFromOutsidePanel(pos);
            if (problem != null && _player != null)
                GameManager.ShowTooltip(_player, string.Format(Localization.Get("hsliftNotReady"), problem));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Outside panel press failed", e);
        }
        return true;
    }

    public static Vector3i ParentPos(Vector3i pos, BlockValue bv)
    {
        try
        {
            if (bv.ischild && bv.Block != null)
                return bv.Block.multiBlockPos.GetParentPos(pos, bv);
        }
        catch { }
        return pos;
    }
}

public static class HSLiftPanelCollider
{
    public static void Ensure(BlockEntityData ebcd, Vector3i pos)
    {
        if (ebcd == null || ebcd.transform == null) return;
        try
        {
            if (ebcd.transform.Find("HSLiftPanelCollider") != null) return;
            var go = new GameObject("HSLiftPanelCollider");
            go.transform.SetParent(ebcd.transform, false);
            go.transform.localPosition = new Vector3(0.5f, 1f, 0.5f);
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.85f, 1.6f, 0.85f);
            box.center = Vector3.zero;
            box.isTrigger = true;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Panel look collider failed at " + pos, e);
        }
    }
}
