using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum HSLiftInteriorKind
{
    None,
    Floor,
    Wall,
    Ceiling
}

// Same inward-face maps on the moving clone and on a collider-less parked copy of that clone.
public static class HSLiftPaint
{
    static Texture2D floorTex, wallTex, ceilTex;
    static Material floorMat, wallMat, ceilMat;
    static bool shaderMissing;
    static bool loggedPainted;
    const string InsideChild = "hs_lift_inside";
    const float InDot = 0.35f;

    public static bool Apply(Transform model, HSLiftPinnedLooks pin, HSLiftInteriorKind kind, Vector3 inward0, Vector3 inward1)
    {
        if (model == null || kind == HSLiftInteriorKind.None) return false;
        if (MaterialFor(kind) == null) return false;
        var skin = model.gameObject.AddComponent<HSLiftInteriorSkin>();
        skin.Pin = pin;
        skin.Kind = kind;
        skin.Inward0 = inward0;
        skin.Inward1 = inward1;
        skin.Paint();
        return true;
    }

    public static int PaintAll(Transform root, HSLiftPinnedLooks pin, HSLiftInteriorKind kind, Vector3 inward0, Vector3 inward1)
    {
        var mat = MaterialFor(kind);
        if (mat == null || root == null) return 0;
        var car = pin != null ? pin.transform : null;
        int painted = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.name == InsideChild) continue;
            if (r.transform.Find(InsideChild) != null) { painted++; continue; }
            var mf = r.GetComponent<MeshFilter>();
            var src = mf != null ? mf.sharedMesh : null;
            if (src == null || src.vertexCount < 3 || !src.isReadable) continue;

            Mesh outside;
            Mesh inside;
            if (!SplitInside(src, r.transform, car, inward0, inward1, MeshTag(kind), out outside, out inside))
                continue;
            if (outside != null)
            {
                mf.sharedMesh = outside;
                if (pin != null) pin.Own(outside);
            }
            if (inside != null)
            {
                var go = new GameObject(InsideChild);
                go.transform.SetParent(r.transform, false);
                var imf = go.AddComponent<MeshFilter>();
                imf.sharedMesh = inside;
                var ir = go.AddComponent<MeshRenderer>();
                ir.sharedMaterial = mat;
                ir.SetPropertyBlock(null);
                if (pin != null) pin.Own(inside);
                if (!loggedPainted)
                {
                    loggedPainted = true;
                    HSLiftDebug.Info("Cabin " + kind + " on inside faces only");
                }
            }
            painted++;
        }
        return painted;
    }

    public static string Describe(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            var m = mf != null ? mf.sharedMesh : null;
            sb.Append(r.GetType().Name).Append(" '").Append(r.name).Append("' mesh=")
              .Append(m == null ? "none" : m.name + " v" + m.vertexCount + (m.isReadable ? "" : " unreadable"))
              .Append(" mat=").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "none").Append("; ");
        }
        return sb.Length == 0 ? "no renderers" : sb.ToString();
    }

    static string MeshTag(HSLiftInteriorKind kind)
    {
        if (kind == HSLiftInteriorKind.Wall) return "wall";
        if (kind == HSLiftInteriorKind.Ceiling) return "ceiling";
        return "floor";
    }

    static bool SplitInside(Mesh src, Transform model, Transform car, Vector3 in0, Vector3 in1, string tag, out Mesh outside, out Mesh inside)
    {
        outside = null;
        inside = null;
        var verts = src.vertices;
        var norms = src.normals;
        var uvs = src.uv;
        var cols = src.colors;
        bool haveN = norms != null && norms.Length == verts.Length;
        bool haveUv = uvs != null && uvs.Length == verts.Length;
        bool haveC = cols != null && cols.Length == verts.Length;
        var b = src.bounds;
        float dx = Mathf.Max(0.0001f, b.size.x);
        float dy = Mathf.Max(0.0001f, b.size.y);
        float dz = Mathf.Max(0.0001f, b.size.z);

        var outV = new List<Vector3>();
        var outN = new List<Vector3>();
        var outUv = new List<Vector2>();
        var outC = new List<Color>();
        var outI = new List<int>();
        var inV = new List<Vector3>();
        var inN = new List<Vector3>();
        var inUv = new List<Vector2>();
        var inC = new List<Color>();
        var inI = new List<int>();

        int subs = Math.Max(1, src.subMeshCount);
        for (int s = 0; s < subs; s++)
        {
            var tris = src.GetTriangles(s);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                if (i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length) continue;
                var fn = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
                if (fn.sqrMagnitude < 1e-12f && haveN)
                    fn = norms[i0] + norms[i1] + norms[i2];
                bool inner = FacesInside(fn, model, car, in0, in1);
                if (inner)
                    PushTri(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, i1, i2, true, inV, inN, inUv, inC, inI);
                else
                    PushTri(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, i1, i2, false, outV, outN, outUv, outC, outI);
            }
        }

        if (outI.Count >= 3)
        {
            outside = MakeMesh("hs_lift_out_" + tag, outV, outN, outUv, outC, outI);
            if (!haveN) outside.RecalculateNormals();
        }
        if (inI.Count >= 3)
        {
            inside = MakeMesh("hs_lift_in_" + tag, inV, inN, inUv, inC, inI);
            if (!haveN) inside.RecalculateNormals();
        }
        return outside != null || inside != null;
    }

    static bool FacesInside(Vector3 localFace, Transform model, Transform car, Vector3 in0, Vector3 in1)
    {
        var n = model != null ? model.TransformDirection(localFace) : localFace;
        if (car != null) n = car.InverseTransformDirection(n);
        if (n.sqrMagnitude < 1e-10f) return false;
        n.Normalize();
        if (in0.sqrMagnitude > 0.01f && Vector3.Dot(n, in0.normalized) > InDot) return true;
        if (in1.sqrMagnitude > 0.01f && Vector3.Dot(n, in1.normalized) > InDot) return true;
        return false;
    }

    static void PushTri(Vector3[] verts, Vector3[] norms, Vector2[] uvs, Color[] cols,
        bool haveN, bool haveUv, bool haveC, Bounds b, float dx, float dy, float dz,
        int i0, int i1, int i2, bool inner,
        List<Vector3> dv, List<Vector3> dn, List<Vector2> du, List<Color> dc, List<int> di)
    {
        int baseI = dv.Count;
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i0, inner, dv, dn, du, dc);
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i1, inner, dv, dn, du, dc);
        PushVert(verts, norms, uvs, cols, haveN, haveUv, haveC, b, dx, dy, dz, i2, inner, dv, dn, du, dc);
        di.Add(baseI);
        di.Add(baseI + 1);
        di.Add(baseI + 2);
    }

    static void PushVert(Vector3[] verts, Vector3[] norms, Vector2[] uvs, Color[] cols,
        bool haveN, bool haveUv, bool haveC, Bounds b, float dx, float dy, float dz,
        int i, bool inner,
        List<Vector3> dv, List<Vector3> dn, List<Vector2> du, List<Color> dc)
    {
        var p = verts[i];
        dv.Add(p);
        var n = haveN ? norms[i] : (p - b.center);
        dn.Add(n);
        if (inner)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az)
                du.Add(new Vector2((p.x - b.min.x) / dx, (p.z - b.min.z) / dz));
            else if (ax >= az)
                du.Add(new Vector2((p.z - b.min.z) / dz, (p.y - b.min.y) / dy));
            else
                du.Add(new Vector2((p.x - b.min.x) / dx, (p.y - b.min.y) / dy));
            dc.Add(Color.white);
        }
        else
        {
            du.Add(haveUv ? uvs[i] : Vector2.zero);
            dc.Add(haveC ? cols[i] : Color.white);
        }
    }

    static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Color> c, List<int> tris)
    {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetColors(c);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
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
        m.renderQueue = 3000;
        slot = m;
        HSLiftDebug.Info("Cabin material " + name + " " + sh.name);
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

    static Shader StepShader()
    {
        var names = new[]
        {
            "Unlit/Texture",
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Legacy Shaders/VertexLit"
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
    public Vector3 Inward0;
    public Vector3 Inward1;
    float nextCheck;
    float born = -1f;
    bool reported;
    static int reports;

    public void Paint()
    {
        if (born < 0f) born = Time.time;
        int n = HSLiftPaint.PaintAll(transform, Pin, Kind, Inward0, Inward1);
        if (n == 0 && !reported && Time.time - born > 3f)
        {
            reported = true;
            if (reports++ < 3)
                HSLiftDebug.Warn("Cabin clone has nothing to paint (" + Kind + "): " + HSLiftPaint.Describe(transform));
        }
    }

    void LateUpdate()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        Paint();
    }
}
