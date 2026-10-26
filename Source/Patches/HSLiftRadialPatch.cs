using System;
using System.Reflection;
using HarmonyLib;

// The game runs the first enabled radial entry on a tap of E. With more than two floors that
// must do nothing. Hold E still opens the wheel, because that selection happens after the radial is open.
[HarmonyPatch(typeof(XUiC_Radial), "CallContextAction")]
public static class HSLiftRadialPatch
{
    static readonly FieldInfo ContextField = AccessTools.Field(typeof(XUiC_Radial), "context");
    static readonly MethodInfo CloseMethod = AccessTools.Method(typeof(XUiC_Radial), "Close");

    static bool Prefix(XUiC_Radial __instance)
    {
        try
        {
            if (__instance == null || __instance.windowGroup == null || __instance.IsOpen) return true;
            var ctx = ContextField != null ? ContextField.GetValue(__instance) as XUiC_Radial.RadialContextBlock : null;
            if (ctx == null || !(ctx.BlockValue.Block is BlockHSLiftInsidePanel)) return true;
            var lift = HSLiftConfiguration.LiftForInsidePanel(ctx.BlockPos);
            if (lift == null || lift.Floors == null || lift.Floors.Count <= 2) return true;
            if (CloseMethod != null) CloseMethod.Invoke(__instance, null);
            return false;
        }
        catch (Exception e)
        {
            HSLiftDebug.Error("Floor tap guard failed", e);
            return true;
        }
    }
}
