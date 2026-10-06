using System;
using System.IO;
using UnityEngine;

public enum HSLiftInteriorKind
{
    None,
    Floor,
    Wall,
    Ceiling
}

// Same approach as the escalator grate: do not write the shared world paint atlas.
// Moving cabin clones get a 2D material and unwrapped UVs (one tile per face).
public static class HSLiftPaint
{
    static Texture2D floorTex, wallTex, ceilTex;
    static Material floorMat, wallMat, ceilMat;
    static bool shaderMissing;
    const string MeshName = "hs_lift_interior";

    public static bool Apply(Transform model, HSLiftPinnedLooks pin, HSLiftInteriorKind kind)
    {
        if (model == null || kind == HSLiftInteriorKind.None) return false;
        if (MaterialFor(kind) == null) return false;
        var skin = model.gameObject.AddComponent<HSLiftInteriorSkin>();
        skin.Pin = pin;
        skin.Kind = kind;
        skin.Paint();
        return true;
    }

    public static int PaintAll(Transform root, HSLiftPinnedLooks pin, HSLiftInteriorKind kind)
    {
        var mat = MaterialFor(kind);
        if (mat == null || root == null) return 0;
        int painted = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r != null ? r.GetComponent<MeshFilter>() : null;
            var src = mf != null ? mf.sharedMesh : null;
            if (src == null || src.vertexCount < 3 || !src.isReadable) continue;
            if (src.name != MeshName)
            {
                var mesh = Unwrap(src);
                if (mesh == null) continue;
                mf.sharedMesh = mesh;
                if (pin != null) pin.Own(mesh);
            }
            int subs = Math.Max(1, mf.sharedMesh.subMeshCount);
            var mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.SetPropertyBlock(null);
            painted++;
        }
        return painted;
    }

    static Material MaterialFor(HSLiftInteriorKind kind)
    {
        if (kind == HSLiftInteriorKind.Floor) return Mat(ref floorMat, ref floorTex, "hs_lift_floor.png", "hs_lift_floor");
        if (kind == HSLiftInteriorKind.Wall) return Mat(ref wallMat, ref wallTex, "hs_lift_wall.png", "hs_lift_wall");
        if (kind == HSLiftInteriorKind.Ceiling) return Mat(ref ceilMat, ref ceilTex, "hs_lift_ceiling.png", "hs_lift_ceiling");
        return null;
    }

    static Material Mat(ref Material slot, ref Texture2D tex, string file, string name)
    {
        if (slot != null) return slot;
        if (shaderMissing) return null;
        if (tex == null) tex = Load(file, name);
        if (tex == null) return null;
        var sh = StepShader();
        if (sh == null)
        {
            shaderMissing = true;
            HSLiftDebug.Warn("No 2D shader for cabin interior; blocks keep the vanilla look.");
            return null;
        }
        var m = new Material(sh) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        m.name = name + "_mat";
        m.mainTexture = tex;
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        slot = m;
        return slot;
    }

    static Texture2D Load(string file, string name)
    {
        try
        {
            var root = HSLiftMod.ModPath ?? "";
            var path = Path.Combine(root, "Resources", file);
            if (!File.Exists(path))
            {
                HSLiftDebug.Warn("Cabin texture missing: " + path);
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            HSLiftDebug.Info("Loaded cabin " + name + " " + tex.width + "x" + tex.height);
            return tex;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Could not load " + file + ": " + e.Message);
            return null;
        }
    }

    static Mesh Unwrap(Mesh src)
    {
        Mesh mesh;
        try { mesh = UnityEngine.Object.Instantiate(src); }
        catch { return null; }
        if (mesh == null || mesh.vertexCount < 3) return null;
        mesh.name = MeshName;
        var verts = mesh.vertices;
        var norms = mesh.normals;
        bool haveN = norms != null && norms.Length == verts.Length;
        var uvs = new Vector2[verts.Length];
        var b = mesh.bounds;
        float dx = Mathf.Max(0.0001f, b.size.x);
        float dy = Mathf.Max(0.0001f, b.size.y);
        float dz = Mathf.Max(0.0001f, b.size.z);
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];
            var n = haveN ? norms[i] : (p - b.center);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az)
                uvs[i] = new Vector2((p.x - b.min.x) / dx, (p.z - b.min.z) / dz);
            else if (ax >= az)
                uvs[i] = new Vector2((p.z - b.min.z) / dz, (p.y - b.min.y) / dy);
            else
                uvs[i] = new Vector2((p.x - b.min.x) / dx, (p.y - b.min.y) / dy);
        }
        mesh.uv = uvs;
        var white = new Color[verts.Length];
        for (int i = 0; i < white.Length; i++) white[i] = Color.white;
        mesh.colors = white;
        if (!haveN) mesh.RecalculateNormals();
        return mesh;
    }

    static Shader StepShader()
    {
        var names = new[]
        {
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Legacy Shaders/VertexLit",
            "Unlit/Texture"
        };
        for (int i = 0; i < names.Length; i++)
        {
            var s = Shader.Find(names[i]);
            if (s != null && s.isSupported && s.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) < 0)
                return s;
        }
        return null;
    }
}

public class HSLiftInteriorSkin : MonoBehaviour
{
    public HSLiftPinnedLooks Pin;
    public HSLiftInteriorKind Kind;
    float nextCheck;

    public void Paint()
    {
        HSLiftPaint.PaintAll(transform, Pin, Kind);
    }

    void LateUpdate()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        Paint();
    }
}
