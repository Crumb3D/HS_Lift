using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(TextureAtlasBlocks), "LoadTextureAtlas")]
public static class HSLiftAtlasPatch
{
    static void Postfix(TextureAtlasBlocks __instance, int _idx)
    {
        HSLiftAtlas.TryAppend(__instance, _idx,
            "txName_HSLiftFloor", "txName_HSLiftWall", "txName_HSLiftCeiling",
            HSLiftMod.ModPath, HSLiftDebug.Info, HSLiftDebug.Warn);
    }
}

public static class HSLiftAtlas
{
    const string MarkerFloor = "hslift_floor";
    const string MarkerWall = "hslift_wall";
    const string MarkerCeil = "hslift_ceiling";

    public static void TryAppend(TextureAtlasBlocks atlas, int idx,
        string floorName, string wallName, string ceilName,
        string modPath, Action<string> info, Action<string> warn)
    {
        if (atlas == null || atlas.uvMapping == null) return;
        if (idx != MeshDescription.MESH_OPAQUE) return;
        try
        {
            int floorI = FindUv(atlas, MarkerFloor);
            int wallI = FindUv(atlas, MarkerWall);
            int ceilI = FindUv(atlas, MarkerCeil);
            if (floorI < 0 || wallI < 0 || ceilI < 0)
            {
                var md = MeshDescription.meshes != null && idx < MeshDescription.meshes.Length
                    ? MeshDescription.meshes[idx] : null;
                if (md == null) return;
                var diff = LoadPng(modPath, "hs_lift_floor.png");
                var wall = LoadPng(modPath, "hs_lift_wall.png");
                var ceil = LoadPng(modPath, "hs_lift_ceiling.png");
                if (diff == null || wall == null || ceil == null) return;
                var extra = new[] { diff, wall, ceil };
                var markers = new[] { MarkerFloor, MarkerWall, MarkerCeil };
                int baseIndex = atlas.uvMapping.Length;
                Grow(md, atlas, extra, markers, info, warn);
                floorI = baseIndex;
                wallI = baseIndex + 1;
                ceilI = baseIndex + 2;
            }
            Bind(floorName, (ushort)floorI);
            Bind(wallName, (ushort)wallI);
            Bind(ceilName, (ushort)ceilI);
            if (info != null) info("Paintbrush materials: floor=" + floorI + " wall=" + wallI + " ceiling=" + ceilI);
        }
        catch (Exception e)
        {
            if (warn != null) warn("Could not add cabin paints: " + e.Message);
        }
    }

    static int FindUv(TextureAtlasBlocks atlas, string marker)
    {
        var uv = atlas.uvMapping;
        for (int i = 0; i < uv.Length; i++)
            if (uv[i].textureName == marker) return i;
        return -1;
    }

    static void Bind(string paintName, ushort texId)
    {
        var list = BlockTextureData.list;
        if (list == null || string.IsNullOrEmpty(paintName)) return;
        for (int i = 0; i < list.Length; i++)
        {
            var d = list[i];
            if (d != null && d.Name == paintName) d.TextureID = texId;
        }
    }

    static Texture2D LoadPng(string modPath, string file)
    {
        var path = Path.Combine(modPath ?? "", "Resources", file);
        if (!File.Exists(path)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
        if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
        {
            UnityEngine.Object.Destroy(tex);
            return null;
        }
        tex.name = file;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        return tex;
    }

    static void Grow(MeshDescription md, TextureAtlasBlocks atlas, Texture2D[] extra, string[] markers, Action<string> info, Action<string> warn)
    {
        var oldDiff = md.TexDiffuse;
        md.TexDiffuse = GrowArray(md.TexDiffuse as Texture2DArray, extra, false, warn);
        md.TexNormal = GrowArray(md.TexNormal as Texture2DArray, extra, true, warn);
        if (md.TexSpecular != null)
            md.TexSpecular = GrowArray(md.TexSpecular as Texture2DArray, SpecularPads(extra.Length), false, warn);
        Rebind(md, oldDiff, md.TexDiffuse, atlas);
        int n = atlas.uvMapping.Length;
        var nuv = new UVRectTiling[n + extra.Length];
        Array.Copy(atlas.uvMapping, nuv, n);
        for (int k = 0; k < extra.Length; k++)
        {
            var u = new UVRectTiling();
            u.uv = new Rect(0f, 0f, 1f, 1f);
            u.blockW = 1;
            u.blockH = 1;
            u.index = n + k;
            u.textureName = markers[k];
            u.bGlobalUV = false;
            u.bSwitchUV = false;
            u.color = Color.white;
            nuv[n + k] = u;
        }
        atlas.uvMapping = nuv;
        atlas.diffuseTexture = md.TexDiffuse;
        atlas.normalTexture = md.TexNormal;
        atlas.specularTexture = md.TexSpecular;
    }

    static void Rebind(MeshDescription md, Texture oldDiff, Texture newDiff, TextureAtlasBlocks atlas)
    {
        if (md.materials == null || oldDiff == null || newDiff == null || oldDiff == newDiff) return;
        for (int i = 0; i < md.materials.Length; i++)
        {
            var m = md.materials[i];
            if (m == null) continue;
            foreach (var p in m.GetTexturePropertyNames())
                if (m.GetTexture(p) == oldDiff) m.SetTexture(p, newDiff);
        }
    }

    static Texture2D[] SpecularPads(int n)
    {
        var a = new Texture2D[n];
        for (int i = 0; i < n; i++)
        {
            var t = new Texture2D(512, 512, TextureFormat.RGBA32, true, false);
            var c = new Color(0.05f, 0.8f, 0f, 0.975f);
            var px = new Color[512 * 512];
            for (int p = 0; p < px.Length; p++) px[p] = c;
            t.SetPixels(px);
            t.Apply(true, false);
            a[i] = t;
        }
        return a;
    }

    static Texture GrowArray(Texture2DArray src, Texture2D[] extra, bool asNormal, Action<string> warn)
    {
        if (src == null || extra == null || extra.Length == 0) return src;
        var dst = new Texture2DArray(src.width, src.height, src.depth + extra.Length, src.format, src.mipmapCount > 1);
        dst.filterMode = src.filterMode;
        dst.wrapMode = src.wrapMode;
        dst.anisoLevel = src.anisoLevel;
        dst.name = src.name + "_hslift";
        int mips = src.mipmapCount;
        for (int i = 0; i < src.depth; i++)
            for (int m = 0; m < mips; m++)
                Graphics.CopyTexture(src, i, m, dst, i, m);
        for (int k = 0; k < extra.Length; k++)
        {
            var slice = Fit(extra[k], src, asNormal, warn);
            if (slice == null) continue;
            int copyMips = Math.Min(mips, slice.mipmapCount);
            for (int m = 0; m < copyMips; m++)
                Graphics.CopyTexture(slice, 0, m, dst, src.depth + k, m);
        }
        dst.Apply(false, true);
        return dst;
    }

    static Texture2D Fit(Texture2D src, Texture2DArray proto, bool asNormal, Action<string> warn)
    {
        int w = proto.width, h = proto.height;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        if (asNormal)
        {
            var px = new Color[w * h];
            var nrm = new Color(0.5f, 0.5f, 1f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = nrm;
            t.SetPixels(px);
        }
        else
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        t.Apply(true, false);
        try { t.Compress(true); }
        catch { }
        t.Apply(true, false);
        if (t.format != proto.format && warn != null)
            warn("Cabin paint format " + t.format + " vs atlas " + proto.format + " (slice may not copy)");
        return t;
    }
}
