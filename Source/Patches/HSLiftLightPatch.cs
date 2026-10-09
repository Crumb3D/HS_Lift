using HarmonyLib;

[HarmonyPatch(typeof(TileEntityPowered), "get_IsPowered")]
public static class HSLiftLightPowerPatch
{
    static void Postfix(TileEntityPowered __instance, ref bool __result)
    {
        if (__result || __instance == null) return;
        try
        {
            var b = __instance.block;
            if (b is BlockHSLiftOutsidePanel) return;
            if (!(b is BlockPoweredLight) && !(b is BlockSpotlight)) return;
            var tog = __instance as TileEntityPoweredBlock;
            if (tog != null && !tog.IsToggled) return;
            var pos = __instance.ToWorldPos();
            var lifts = HSLiftConfiguration.Lifts;
            if (lifts == null) return;
            for (int i = 0; i < lifts.Count; i++)
            {
                var d = lifts[i];
                if (d == null || !HSLiftCabinLights.InCar(d, pos)) continue;
                if (HSLiftPower.CabinHasPower(d))
                {
                    __result = true;
                    return;
                }
            }
        }
        catch { }
    }
}
