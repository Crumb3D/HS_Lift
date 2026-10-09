using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

// Puts cabin PNGs on unused opaque atlas slots. Paintbrush Metal group: Lift Floor / Wall / Ceiling / Outside.
// A paint's TextureId is a uvMapping id, not an array slot: uvMapping[id].index is the slot.
// New ids go past the end of uvMapping; slots are only ones no uvMapping entry uses.
// No free slot (3.3 packs its array to fit) = the arrays are copied GPU-side into bigger ones.
// If that fails the paints are hidden.
[HarmonyPatch(typeof(TextureAtlasBlocks), "LoadTextureAtlas")]
public static class HSLiftAtlasPatch
{
    public static int FloorId = 508;
    public static int WallId = 509;
    public static int CeilingId = 510;
    public static int OutsideId = 511;
    public static bool Available;
    const int SilverId = 267;
    static readonly string[] Names = { "hs_lift_floor", "hs_lift_wall", "hs_lift_ceiling", "hs_lift_outside" };

    static void Postfix(TextureAtlasBlocks __instance, int _idx)
    {
        try
        {
            if (_idx != MeshDescription.MESH_OPAQUE) return;
            if (GameManager.IsDedicatedServer) return;
            if (__instance == null) return;
            Available = false;
            var arr = __instance.diffuseTexture as Texture2DArray;
            if (arr == null)
            {
                HSLiftDebug.Warn("Opaque atlas is not a Texture2DArray; Lift paints are hidden.");
                BindPaintIds();
                return;
            }
            int[] ids, slots;
            if (!PickSlots(__instance, arr.depth, out ids, out slots))
            {
                int vanillaDepth = arr.depth;
                if (!GrowArrays(__instance, Names.Length))
                {
                    HSLiftDebug.Warn("No free slot in the block texture array (" + vanillaDepth + " slots, all used by vanilla) and it could not be enlarged. Lift paints are hidden.");
                    BindPaintIds();
                    return;
                }
                arr = (Texture2DArray)__instance.diffuseTexture;
                var map = __instance.uvMapping ?? new UVRectTiling[0];
                for (int i = 0; i < Names.Length; i++)
                {
                    slots[i] = vanillaDepth + i;
                    ids[i] = map.Length + i;
                }
                HSLiftDebug.Info("Block texture array enlarged " + vanillaDepth + " -> " + arr.depth + " slots for the Lift paints.");
            }
            FloorId = ids[0];
            WallId = ids[1];
            CeilingId = ids[2];
            OutsideId = ids[3];
            bool ok = true;
            for (int i = 0; i < Names.Length; i++)
                ok &= Install(__instance, arr, ids[i], slots[i], Names[i] + ".png", Names[i]);
            Available = ok;
            HSLiftDebug.Info("Cabin paints " + (ok ? "ready" : "FAILED") + ": ids " + FloorId + "/" + WallId + "/" + CeilingId + "/" + OutsideId
                + " on slots " + slots[0] + "/" + slots[1] + "/" + slots[2] + "/" + slots[3] + " (atlas " + arr.depth + " slots, " + arr.format + ")");
            BindPaintIds();
        }
        catch (Exception e)
        {
            Available = false;
            BindPaintIds();
            HSLiftDebug.Error("Cabin paintbrush atlas failed", e);
        }
    }

    // Reuses our own entries on a texture-quality reload; otherwise new ids past the end of uvMapping.
    static bool PickSlots(TextureAtlasBlocks atlas, int depth, out int[] ids, out int[] slots)
    {
        ids = new int[Names.Length];
        slots = new int[Names.Length];
        var map = atlas.uvMapping ?? new UVRectTiling[0];
        bool reuse = true;
        for (int i = 0; i < Names.Length; i++)
        {
            ids[i] = FindByName(map, Names[i]);
            if (ids[i] < 0 || map[ids[i]].index <= 0 || map[ids[i]].index >= depth) { reuse = false; break; }
            slots[i] = map[ids[i]].index;
        }
        if (reuse) return true;

        var used = new HashSet<int> { 0 };
        for (int i = 0; i < map.Length; i++)
        {
            if (string.IsNullOrEmpty(map[i].textureName)) continue;
            int n = Mathf.Clamp(Mathf.RoundToInt(map[i].uv.width * map[i].uv.height), 1, 16);
            for (int k = 0; k < n; k++) used.Add(map[i].index + k);
        }
        int found = 0;
        for (int s = depth - 1; s > 0 && found < Names.Length; s--)
            if (!used.Contains(s)) slots[found++] = s;
        if (found < Names.Length) return false;
        for (int i = 0; i < Names.Length; i++) ids[i] = map.Length + i;
        return true;
    }

    static Texture2DArray grownDiffuse, grownNormal, grownSpecular;

    // The vanilla arrays stay owned by MeshDescription (it unloads them as assets), so the enlarged
    // copies only replace the atlas fields that MeshDescription binds to the block materials.
    // The copies must be made non-readable before the GPU copy: a later Apply() would upload the
    // empty CPU buffer over every slice and black out vanilla paints.
    static bool GrowArrays(TextureAtlasBlocks atlas, int extra)
    {
        if (SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None) return false;
        var diff = atlas.diffuseTexture as Texture2DArray;
        if (diff == null) return false;
        int depth = diff.depth + extra;
        var newDiff = Grow(diff, depth);
        if (newDiff == null) return false;
        var normal = atlas.normalTexture as Texture2DArray;
        var specular = atlas.specularTexture as Texture2DArray;
        var newNormal = normal != null ? Grow(normal, depth) : null;
        var newSpecular = specular != null ? Grow(specular, depth) : null;
        if ((normal != null && newNormal == null) || (specular != null && newSpecular == null))
        {
            DestroyTex(newDiff);
            DestroyTex(newNormal);
            DestroyTex(newSpecular);
            return false;
        }
        atlas.diffuseTexture = newDiff;
        if (newNormal != null) atlas.normalTexture = newNormal;
        if (newSpecular != null) atlas.specularTexture = newSpecular;
        DestroyTex(grownDiffuse);
        DestroyTex(grownNormal);
        DestroyTex(grownSpecular);
        grownDiffuse = newDiff;
        grownNormal = newNormal;
        grownSpecular = newSpecular;
        return true;
    }

    static Texture2DArray Grow(Texture2DArray src, int depth)
    {
        try
        {
            var flags = src.mipmapCount > 1
                ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain
                : UnityEngine.Experimental.Rendering.TextureCreationFlags.None;
            var dst = new Texture2DArray(src.width, src.height, depth, src.graphicsFormat, flags, src.mipmapCount);
            dst.name = src.name + "_hslift";
            dst.filterMode = src.filterMode;
            dst.wrapModeU = src.wrapModeU;
            dst.wrapModeV = src.wrapModeV;
            dst.anisoLevel = src.anisoLevel;
            dst.mipMapBias = src.mipMapBias;
            dst.Apply(false, true);
            int copy = Math.Min(src.depth, depth);
            for (int e = 0; e < copy; e++)
                Graphics.CopyTexture(src, e, dst, e);
            return dst;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Could not enlarge texture array " + src.name + ": " + e.Message);
            return null;
        }
    }

    static void DestroyTex(Texture t)
    {
        if (t != null) UnityEngine.Object.Destroy(t);
    }

    static int FindByName(UVRectTiling[] map, string name)
    {
        for (int i = 0; i < map.Length; i++)
            if (map[i].textureName == name) return i;
        return -1;
    }

    static void BindPaintIds()
    {
        var list = BlockTextureData.list;
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null) Apply(list[i]);
    }

    public static void Apply(BlockTextureData paint)
    {
        var n = paint.Name;
        int id;
        if (n == "txName_HSLiftFloor") id = FloorId;
        else if (n == "txName_HSLiftWall") id = WallId;
        else if (n == "txName_HSLiftCeiling") id = CeilingId;
        else if (n == "txName_HSLiftOutside") id = OutsideId;
        else return;
        // Creative still lists hidden paints; plain silver beats a random vanilla texture.
        paint.TextureID = (ushort)(Available ? id : SilverId);
        paint.Hidden = !Available;
    }

    static bool Install(TextureAtlasBlocks atlas, Texture2DArray arr, int id, int slot, string file, string name)
    {
        var png = LoadPng(file, name);
        if (png == null) return false;
        if (!WriteSlice(arr, slot, png))
        {
            HSLiftDebug.Warn("Could not write " + name + " into atlas slot " + slot + " (" + arr.format + ").");
            return false;
        }
        int silverSlot = SilverSlot(atlas.uvMapping);
        CopySlice(atlas.normalTexture as Texture2DArray, silverSlot, slot);
        CopySlice(atlas.specularTexture as Texture2DArray, silverSlot, slot);
        SetUv(atlas, id, slot, name);
        return true;
    }

    static int SilverSlot(UVRectTiling[] map)
    {
        if (map != null && SilverId < map.Length && map[SilverId].index > 0) return map[SilverId].index;
        return 0;
    }

    static void SetUv(TextureAtlasBlocks atlas, int id, int slot, string name)
    {
        var map = atlas.uvMapping ?? new UVRectTiling[0];
        if (map.Length <= id)
        {
            var grown = new UVRectTiling[id + 1];
            Array.Copy(map, grown, map.Length);
            map = grown;
            atlas.uvMapping = map;
        }
        UVRectTiling uv = SilverId < map.Length ? map[SilverId] : default(UVRectTiling);
        uv.uv = new Rect(0f, 0f, 1f, 1f);
        if (uv.blockW < 1) uv.blockW = 1;
        if (uv.blockH < 1) uv.blockH = 1;
        if (uv.color.a < 0.01f && uv.color.r < 0.01f && uv.color.g < 0.01f && uv.color.b < 0.01f)
            uv.color = Color.white;
        uv.index = slot;
        uv.textureName = name;
        map[id] = uv;
    }

    static bool WriteSlice(Texture2DArray arr, int slot, Texture2D src)
    {
        var fitted = Fit(src, arr.width, arr.height);
        if (fitted == null) return false;
        Texture2D conv = null;
        try
        {
            conv = Encode(fitted, arr);
            if (conv == null) return false;
            int mips = Math.Min(conv.mipmapCount, arr.mipmapCount);
            for (int m = 0; m < mips; m++)
                Graphics.CopyTexture(conv, 0, m, arr, slot, m);
            return true;
        }
        finally
        {
            if (conv != null) UnityEngine.Object.Destroy(conv);
            if (fitted != src) UnityEngine.Object.Destroy(fitted);
        }
    }

    // Block-compressed arrays (3.3 ships DXT1) can't be render targets, so compress on the CPU instead.
    static Texture2D Encode(Texture2D rgba, Texture2DArray arr)
    {
        bool mips = arr.mipmapCount > 1;
        if (arr.format == TextureFormat.DXT1 || arr.format == TextureFormat.DXT5)
        {
            var raw = new Texture2D(arr.width, arr.height, arr.format == TextureFormat.DXT1 ? TextureFormat.RGB24 : TextureFormat.RGBA32, mips, false);
            raw.SetPixels32(rgba.GetPixels32());
            raw.Apply(mips, false);
            raw.Compress(true);
            if (raw.format == arr.format) return raw;
            UnityEngine.Object.Destroy(raw);
            return null;
        }
        var conv = new Texture2D(arr.width, arr.height, arr.format, mips);
        conv.wrapMode = TextureWrapMode.Repeat;
        conv.filterMode = FilterMode.Bilinear;
        if (Graphics.ConvertTexture(rgba, conv)) return conv;
        UnityEngine.Object.Destroy(conv);
        return null;
    }

    static void CopySlice(Texture2DArray arr, int from, int to)
    {
        if (arr == null || from < 0 || to < 0 || from >= arr.depth || to >= arr.depth) return;
        for (int m = 0; m < arr.mipmapCount; m++)
            Graphics.CopyTexture(arr, from, m, arr, to, m);
    }

    static Texture2D Fit(Texture2D src, int w, int h)
    {
        if (src == null) return null;
        if (src.width == w && src.height == h) return src;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        dst.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
        dst.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        dst.wrapMode = TextureWrapMode.Repeat;
        return dst;
    }

    static Texture2D LoadPng(string file, string name)
    {
        try
        {
            var path = Path.Combine(HSLiftMod.ModPath ?? "", "Resources", file);
            if (!File.Exists(path))
            {
                HSLiftDebug.Warn("Cabin texture missing: " + path);
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Cabin texture load failed (" + file + "): " + e.Message);
            return null;
        }
    }
}

[HarmonyPatch(typeof(BlockTextureData), "Init")]
public static class HSLiftPaintIdPatch
{
    static void Postfix(BlockTextureData __instance)
    {
        if (__instance == null || string.IsNullOrEmpty(__instance.Name)) return;
        HSLiftAtlasPatch.Apply(__instance);
    }
}
