using System;
using HarmonyLib;
using UnityEngine;

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
            if (!ItemActionHSLiftTool.IsHolding(player)) return true;
            if (!ItemActionHSLiftTool.IsBuildBlock(_hitInfo)) return true;

            var playerUI = AccessTools.Field(typeof(PlayerMoveController), "playerUI").GetValue(__instance) as LocalPlayerUI;
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
}
