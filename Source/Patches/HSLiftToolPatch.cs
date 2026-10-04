using System;
using HarmonyLib;

// Hold-E on a block is block-use first, so the setup tool never got the wheel. Steal E while this tool is out
// and looking at a player block (wood through steel, panels, doors) — not dirt/terrain.
[HarmonyPatch(typeof(PlayerMoveController), "HandleInteraction")]
public static class HSLiftToolPatch
{
    static bool Prefix(PlayerMoveController __instance, WorldRayHitInfo _hitInfo, bool _activate)
    {
        try
        {
            var player = AccessTools.Field(typeof(PlayerMoveController), "entityPlayerLocal").GetValue(__instance) as EntityPlayerLocal;
            var playerUI = AccessTools.Field(typeof(PlayerMoveController), "playerUI").GetValue(__instance) as LocalPlayerUI;
            bool holding = ItemActionHSLiftTool.IsHolding(player);
            if (holding) ClearPlacementPreview(__instance);
            bool show = holding && ItemActionHSLiftTool.IsBuildBlock(_hitInfo);
            if (!show)
            {
                ClearOurs(playerUI);
                return true;
            }

            if (playerUI != null)
                XUiC_InteractionPrompt.SetText(playerUI, Localization.Get("hsliftToolHint"));

            if (_activate)
                ItemActionHSLiftTool.OpenRadial(player);
            return false;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Setup tool E steal failed", e);
            return true;
        }
    }

    public static void ClearPlacementPreview(PlayerMoveController pmc)
    {
        if (pmc == null) return;
        try
        {
            var box = AccessTools.Field(typeof(PlayerMoveController), "focusBoxScript").GetValue(pmc) as RenderDisplacedCube;
            if (box != null) box.DestroyPreview();
        }
        catch { }
    }

    public static void ClearOurs(LocalPlayerUI playerUI)
    {
        if (playerUI == null) return;
        try
        {
            if (!PromptIsOurs(playerUI)) return;
            XUiC_InteractionPrompt.SetText(playerUI, null);
        }
        catch { }
    }

    public static bool PromptIsOurs(LocalPlayerUI playerUI)
    {
        var text = CurrentPrompt(playerUI);
        if (string.IsNullOrEmpty(text)) return false;
        var hint = Localization.Get("hsliftToolHint");
        return text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0
            || hint.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string CurrentPrompt(LocalPlayerUI playerUI)
    {
        if (playerUI == null || playerUI.xui == null) return null;
        var id = AccessTools.Field(typeof(XUiC_InteractionPrompt), "ID").GetValue(null) as string;
        if (string.IsNullOrEmpty(id)) return null;
        var wg = playerUI.xui.FindWindowGroupByName(id);
        if (wg == null) return null;
        var prompt = wg.Controller.GetChildByType<XUiC_InteractionPrompt>();
        return prompt != null ? prompt.Text : null;
    }
}

[HarmonyPatch(typeof(XUiC_InteractionPrompt), "Update")]
public static class HSLiftToolPromptClear
{
    static void Postfix(XUiC_InteractionPrompt __instance)
    {
        try
        {
            var hint = Localization.Get("hsliftToolHint");
            if (__instance == null || string.IsNullOrEmpty(__instance.Text)) return;
            if (__instance.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0
                && hint.IndexOf(__instance.Text, StringComparison.OrdinalIgnoreCase) < 0)
                return;
            var player = __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
            var hit = player != null ? player.HitInfo : null;
            if (ItemActionHSLiftTool.IsHolding(player) && ItemActionHSLiftTool.IsBuildBlock(hit)) return;
            if (__instance.xui != null) XUiC_InteractionPrompt.SetText(__instance.xui.playerUI, null);
        }
        catch { }
    }
}
