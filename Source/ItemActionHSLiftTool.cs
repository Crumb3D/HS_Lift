using System;
using UnityEngine.Scripting;

// Hold-E radial while this tool is in hand. Aim at the block the command needs, then pick from the wheel.
// The game only opens an item radial from Action1 (secondary). Vanilla wheel has 13 slices.
[Preserve]
public class ItemActionHSLiftTool : ItemAction
{
    public static bool IsHolding(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSLiftTool) return true;
        return false;
    }

    // Any placed block that is not air or terrain (dirt/stone ground). Wood, steel, frames, panels, doors all count.
    public static bool IsBuildBlock(WorldRayHitInfo hit)
    {
        if (hit == null || !hit.bHitValid) return false;
        var world = GameManager.Instance.World;
        if (world == null) return false;
        var bv = world.GetBlock(hit.hit.blockPos);
        if (bv.isair) return false;
        var b = bv.Block;
        if (b == null || b.shape == null || b.shape.IsTerrain()) return false;
        return true;
    }

    public static void OpenRadial(EntityPlayerLocal player)
    {
        if (player == null || player.playerUI == null || player.playerUI.xui == null) return;
        var radial = player.playerUI.xui.RadialWindow;
        if (radial == null || radial.IsOpen) return;
        radial.Open();
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return;
        foreach (var a in holding.Actions)
        {
            if (!(a is ItemActionHSLiftTool)) continue;
            a.SetupRadial(radial, player);
            return;
        }
    }

    public override bool HasRadial()
    {
        return true;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased) return;
        try
        {
            var player = _actionData != null && _actionData.invData != null ? _actionData.invData.holdingEntity as EntityPlayerLocal : null;
            if (player != null) GameManager.ShowTooltip(player, Localization.Get("hsliftToolHint"));
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Setup tool click failed", e);
        }
    }

    public override string CanInteract(ItemActionData _actionData)
    {
        return Localization.Get("hsliftToolHint");
    }

    public override void SetupRadial(XUiC_Radial radial, EntityPlayerLocal player)
    {
        try
        {
            radial.ResetRadialEntries();
            HSLiftConfiguration.Editing();
            var d = HSLiftConfiguration.Data ?? new HSLiftConfigData();
            if (d.Panels == null) d.Panels = new System.Collections.Generic.List<HSLiftPanelEntry>();
            bool needCorner1 = d.Corner1 == null;
            bool needCorner2 = d.Corner2 == null;
            bool needFloor = d.Floors == null || d.Floors.Count < 2;
            bool needGround = d.HasCar && needFloor;
            bool needPanel = d.Panels.Count < 2;
            bool needExclude = d.HasCar && (d.ExcludedColumns == null || d.ExcludedColumns.Count == 0);
            Add(radial, 0, "ui_game_symbol_players", Localization.Get("hsliftRadialPed"), false);
            Add(radial, 1, "ui_game_symbol_assemble", Localization.Get("hsliftRadialVehicle"), false);
            Add(radial, 2, "ui_game_symbol_map", Localization.Get("hsliftRadialUse"), false);
            Add(radial, 3, "ui_game_symbol_map_waypoint_set", Localization.Get("hsliftRadialCorner1"), needCorner1);
            Add(radial, 4, "ui_game_symbol_map_cursor", Localization.Get("hsliftRadialCorner2"), needCorner2);
            Add(radial, 5, "ui_game_symbol_map_house", Localization.Get("hsliftRadialFloor"), needFloor);
            Add(radial, 6, "ui_game_symbol_lightbulb", Localization.Get("hsliftRadialPanel"), needPanel);
            Add(radial, 7, "ui_game_symbol_book", Localization.Get("hsliftRadialGround"), needGround);
            Add(radial, 8, "ui_game_symbol_lock", Localization.Get("hsliftRadialExclude"), needExclude);
            radial.SetCommonData(
                default(GUI_2.UIUtils.ButtonIcon),
                HandleCommand,
                new XUiC_Radial.RadialContextHoldingSlotIndex(player.inventory.holdingItemIdx),
                -1,
                false,
                StillHolding);
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Setup tool radial failed", e);
        }
    }

    static void Add(XUiC_Radial radial, int i, string icon, string label, bool needsSetup)
    {
        // Empty slice text: name only in the middle. Icon stays in the circle.
        radial.CreateRadialEntry(i, icon, "UIAtlas", "", label, false);
        SetNeedSetupGear(radial, i, needsSetup);
    }

    static void SetNeedSetupGear(XUiC_Radial radial, int i, bool needsSetup)
    {
        try
        {
            if (radial == null || radial.menuItem == null || i < 0 || i >= radial.menuItem.Length) return;
            var entry = radial.menuItem[i];
            if (entry == null) return;
            var sprites = entry.GetChildrenByViewType<XUiV_Sprite>();
            if (sprites == null) return;
            for (int s = 0; s < sprites.Length; s++)
            {
                if (sprites[s] == null || sprites[s].ID != "hsliftNeedSetup") continue;
                sprites[s].IsVisible = needsSetup;
                return;
            }
        }
        catch { }
    }

    static bool StillHolding(XUiC_Radial radial, XUiC_Radial.RadialContextAbs context)
    {
        var ctx = context as XUiC_Radial.RadialContextHoldingSlotIndex;
        var player = radial != null && radial.xui != null && radial.xui.playerUI != null ? radial.xui.playerUI.entityPlayer : null;
        if (player == null || ctx == null || player.inventory.holdingItemIdx != ctx.ItemSlotIndex) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSLiftTool) return true;
        return false;
    }

    static void HandleCommand(XUiC_Radial sender, int commandIndex, XUiC_Radial.RadialContextAbs context)
    {
        try
        {
            var player = sender.xui.playerUI.entityPlayer;
            string msg;
            switch (commandIndex)
            {
                case 0: msg = HSLiftSetup.Execute("type", "ped", "", player); break;
                case 1: msg = HSLiftSetup.Execute("type", "vehicle", "", player); break;
                case 2: msg = HSLiftSetup.Execute("select", "", "", player); break;
                case 3: msg = HSLiftSetup.Execute("corner1", "", "", player); break;
                case 4: msg = HSLiftSetup.Execute("corner2", "", "", player); break;
                case 5: msg = HSLiftSetup.Execute("floor", "add", "", player); break;
                case 6: msg = HSLiftSetup.Execute("panel", "", "", player); break;
                case 7: msg = HSLiftSetup.Execute("floor", "ground", "", player); break;
                case 8: msg = HSLiftSetup.Execute("exclude", "1", "", player); break;
                default: return;
            }
            HSLiftSetup.Tell(player, msg);
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Setup tool failed", e);
        }
    }
}
