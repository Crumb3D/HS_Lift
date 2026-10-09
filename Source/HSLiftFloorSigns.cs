using System;
using System.Collections.Generic;
using UnityEngine;

// Vanilla wall letters/numbers (woodShapes:signLetter_g, signNumber_1, …).
// Place a G (or 1, 2, …) near each outside door (ped elevator or vehicle garage); they switch to the car's floor.
// Letters only hold one character, so writable signs showing a floor label (B2, 10, …) are kept in step too.
public static class HSLiftFloorSigns
{
    const int Reach = 3;
    static HSLiftConfigData D { get { return HSLiftConfiguration.Data; } }

    static string lastLiftId;
    static string lastSignToken;

    public static void Invalidate()
    {
        lastLiftId = null;
        lastSignToken = null;
    }

    public static void UpdateToCurrentFloor(World world)
    {
        Apply(world, HSLiftConfiguration.FloorAt(D.CurrentY));
    }

    // Going down 2→G: show 2 until the car reaches 1, then 1, then G. Same going up.
    public static void UpdatePassing(World world, float carY, int fromY, int targetY)
    {
        Apply(world, FloorPassing(carY, fromY, targetY));
    }

    static HSLiftFloor FloorPassing(float carY, int fromY, int targetY)
    {
        if (D.Floors == null || D.Floors.Count == 0) return null;
        HSLiftFloor best = null;
        if (targetY < fromY)
        {
            foreach (var f in D.Floors)
                if (f.Y + 0.05f >= carY && (best == null || f.Y < best.Y)) best = f;
        }
        else
        {
            foreach (var f in D.Floors)
                if (f.Y - 0.05f <= carY && (best == null || f.Y > best.Y)) best = f;
        }
        return best;
    }

    static void Apply(World world, HSLiftFloor floor)
    {
        if (world == null || D == null || !D.HasCar || floor == null) return;
        var text = HSLiftConfiguration.FloorSignText(floor.Name);
        if (D.ElevatorId == lastLiftId && string.Equals(lastSignToken, text, StringComparison.OrdinalIgnoreCase)) return;
        var shape = ShapeForFloor(floor.Name);

        HSLiftDoors.CollectDoors(world);
        var changes = new List<BlockChangeInfo>();
        var seen = new HashSet<Vector3i>();
        int written = ScanCabin(world, shape, text, changes, seen);
        foreach (var a in Anchors(world))
            written += ScanAround(world, a, shape, text, changes, seen);
        RefreshMoving(floor);
        lastLiftId = D.ElevatorId;
        lastSignToken = text;
        if (changes.Count > 0) world.SetBlocksRPC(changes);
        if (changes.Count > 0 || written > 0)
            HSLiftDebug.Verbose("Floor signs: " + changes.Count + " letter(s), " + written + " writable sign(s) set to " + text + " (" + HSLiftConfiguration.FloorDisplayName(floor.Name) + ")");
    }

    static IEnumerable<Vector3i> Anchors(World world)
    {
        int top = Math.Max(D.LowerY, D.HasUpper ? D.UpperY : D.LowerY) + Math.Max(1, D.DoorHeight);
        foreach (var parent in HSLiftDoors.FindDoors(world, D.LowerY, top))
        {
            yield return parent;
            foreach (var c in HSLiftDoors.DoorCells(parent, world.GetBlock(parent)))
                yield return c;
        }
        if (D.Panels == null) yield break;
        foreach (var p in D.Panels)
            yield return p.Pos;
    }

    static int ScanAround(World world, Vector3i center, string shape, string text, List<BlockChangeInfo> changes, HashSet<Vector3i> seen)
    {
        int written = 0;
        int yHi = Math.Max(2, D.DoorHeight);
        for (int dy = -1; dy <= yHi; dy++)
        for (int dx = -Reach; dx <= Reach; dx++)
        for (int dz = -Reach; dz <= Reach; dz++)
        {
            var pos = new Vector3i(center.x + dx, center.y + dy, center.z + dz);
            if (!seen.Add(pos)) continue;
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            // The sign above this door, not the one above the lift next door.
            if (HSLiftDoors.LiftBeside(pos) != D) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild) continue;
            if (TryWriteSign(world, pos, text, false)) { written++; continue; }
            if (shape == null) continue;
            string name = bv.Block.GetBlockName();
            char glyph;
            if (!TryGlyph(name, out glyph) || !IsThisLiftFloorGlyph(glyph)) continue;
            var next = ReplaceShape(name, shape);
            if (next == null || string.Equals(next, name, StringComparison.OrdinalIgnoreCase)) continue;
            var nv = Block.GetBlockValue(next);
            if (nv.isair || nv.type == 0) continue;
            nv.rotation = bv.rotation;
            nv.damage = bv.damage;
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) continue;
            int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
            changes.Add(new BlockChangeInfo(pos, nv, chunk.GetDensity(lx, ly, lz), chunk.GetTextureFullArray(lx, ly, lz)));
        }
        return written;
    }

    // A sign inside the cabin shows the floor the car is on, or the floor it is passing.
    static int ScanCabin(World world, string shape, string text, List<BlockChangeInfo> changes, HashSet<Vector3i> seen)
    {
        if (!D.HasCar) return 0;
        int written = 0;
        int h = Math.Max(1, D.SizeY);
        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < D.SizeX; dx++)
        for (int dz = 0; dz < D.SizeZ; dz++)
        {
            var pos = new Vector3i(D.MinX + dx, D.CurrentY + dy, D.MinZ + dz);
            if (!seen.Add(pos)) continue;
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair || bv.ischild) continue;
            if (TryWriteSign(world, pos, text, true)) { written++; continue; }
            if (shape == null) continue;
            char glyph;
            if (!TryGlyph(bv.Block.GetBlockName(), out glyph) || !IsThisLiftFloorGlyph(glyph)) continue;
            var next = ReplaceShape(bv.Block.GetBlockName(), shape);
            if (next == null || string.Equals(next, bv.Block.GetBlockName(), StringComparison.OrdinalIgnoreCase)) continue;
            var nv = Block.GetBlockValue(next);
            if (nv.isair || nv.type == 0) continue;
            nv.rotation = bv.rotation;
            nv.damage = bv.damage;
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) continue;
            int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
            changes.Add(new BlockChangeInfo(pos, nv, chunk.GetDensity(lx, ly, lz), chunk.GetTextureFullArray(lx, ly, lz)));
        }
        return written;
    }

    // Vanilla writable signs (Wood Sign 1x1 etc.) only become floor displays once a player has written
    // one of this lift's floor labels on them (G, B2, 10 ...); any other text is left alone.
    // Outside a door they read "Floor: <landing>" / "Lift: <car>". Inside the cabin, just the car's floor.
    static bool TryWriteSign(World world, Vector3i pos, string carText, bool cabin)
    {
        var te = world.GetTileEntity(pos) as TileEntityComposite;
        if (te == null) return false;
        var sign = te.GetFeature<ITileEntitySignable>();
        if (sign == null) return false;
        var current = sign.GetAuthoredText();
        var now = current != null && current.Text != null ? current.Text.Trim() : "";
        if (!IsThisLiftFloorText(now) && !IsDisplayText(now)) return false;
        string text;
        if (cabin) text = carText;
        else
        {
            var landing = LandingFor(pos.y);
            if (landing == null) return false;
            text = FloorWord + HSLiftConfiguration.FloorSignText(landing.Name) + "\n" + LiftWord + carText;
        }
        if (string.Equals(now, text, StringComparison.Ordinal)) return false;
        try
        {
            sign.SetText(text, true, current.Author);
            return true;
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Floor sign at " + pos + " not updated: " + e.Message);
            return false;
        }
    }

    const string FloorWord = "Floor: ";
    const string LiftWord = "Lift: ";

    static bool IsDisplayText(string text)
    {
        return text.StartsWith(FloorWord.Trim(), StringComparison.OrdinalIgnoreCase)
            && text.IndexOf(LiftWord.Trim(), StringComparison.OrdinalIgnoreCase) > 0;
    }

    // The landing a sign belongs to: the highest floor at or below it, within one door height.
    static HSLiftFloor LandingFor(int signY)
    {
        HSLiftFloor best = null;
        foreach (var f in D.Floors)
            if (f.Y <= signY && signY <= f.Y + D.DoorHeight && (best == null || f.Y > best.Y)) best = f;
        return best;
    }

    static bool IsThisLiftFloorText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var f in D.Floors)
            if (string.Equals(HSLiftConfiguration.FloorSignText(f.Name), text, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static bool IsThisLiftFloorGlyph(char glyph)
    {
        foreach (var f in D.Floors)
        {
            if (string.IsNullOrEmpty(f.Name)) continue;
            var stored = f.Name.Trim();
            if (char.ToUpperInvariant(stored[0]) == char.ToUpperInvariant(glyph)) return true;
            var shown = HSLiftConfiguration.FloorSignToken(stored);
            if (!string.IsNullOrEmpty(shown) && char.ToUpperInvariant(shown[0]) == char.ToUpperInvariant(glyph)) return true;
        }
        return false;
    }

    static string ShapeForFloor(string name)
    {
        var token = HSLiftConfiguration.FloorSignToken(name);
        if (string.IsNullOrEmpty(token)) return null;
        char ch = token[0];
        if (char.IsDigit(ch)) return "signNumber_" + ch;
        if (char.IsLetter(ch)) return "signLetter_" + char.ToLowerInvariant(ch);
        return null;
    }

    static bool TryGlyph(string blockName, out char glyph)
    {
        glyph = '\0';
        if (string.IsNullOrEmpty(blockName)) return false;
        var n = blockName;
        int c = n.LastIndexOf(':');
        if (c >= 0) n = n.Substring(c + 1);
        if (n.StartsWith("signLetter_", StringComparison.OrdinalIgnoreCase) && n.Length == 12)
        {
            glyph = n[11];
            return char.IsLetter(glyph);
        }
        if (n.StartsWith("signNumber_", StringComparison.OrdinalIgnoreCase) && n.Length == 12)
        {
            glyph = n[11];
            return char.IsDigit(glyph);
        }
        return false;
    }

    static string ReplaceShape(string blockName, string shape)
    {
        int c = blockName.LastIndexOf(':');
        if (c < 0) return shape;
        return blockName.Substring(0, c + 1) + shape;
    }

    static GameObject movingRoot;
    static List<HSLiftCell> movingCells;

    // Captured blocks, before the moving copy is built. Letters change shape. Writable floor signs take the label.
    public static void PrepareRide(List<HSLiftCell> cells, HSLiftFloor floor)
    {
        if (cells == null || floor == null) return;
        var label = HSLiftConfiguration.FloorSignText(floor.Name);
        var shape = ShapeForFloor(floor.Name);
        foreach (var c in cells)
        {
            if (c == null || c.Bv.Block == null) continue;
            char glyph;
            if (shape != null && TryGlyph(c.Bv.Block.GetBlockName(), out glyph) && IsThisLiftFloorGlyph(glyph))
            {
                var next = ReplaceShape(c.Bv.Block.GetBlockName(), shape);
                if (next != null && !string.Equals(next, c.Bv.Block.GetBlockName(), StringComparison.OrdinalIgnoreCase))
                {
                    var nv = Block.GetBlockValue(next);
                    if (!nv.isair && nv.type != 0)
                    {
                        nv.rotation = c.Bv.rotation;
                        nv.damage = c.Bv.damage;
                        c.Bv = nv;
                    }
                }
            }
            WriteCellSign(c, label);
        }
    }

    public static void Watch(GameObject root, List<HSLiftCell> cells)
    {
        movingRoot = root;
        movingCells = cells;
    }

    public static void Unwatch()
    {
        movingRoot = null;
        movingCells = null;
    }

    // The cloned sign has no tile-entity text, so the floor is written onto its text meshes.
    public static void StampModel(GameObject holder, Transform model, HSLiftCell c, int baseY)
    {
        if (holder == null || model == null || c == null || c.Bv.Block == null) return;
        var meshes = model.GetComponentsInChildren<TextMesh>(true);
        bool letter = false;
        char glyph;
        if (TryGlyph(c.Bv.Block.GetBlockName(), out glyph) && IsThisLiftFloorGlyph(glyph)) letter = true;
        var authored = SignTextAt(c, baseY);
        bool writable = meshes != null && meshes.Length > 0 && (IsThisLiftFloorText(authored) || IsDisplayText(authored));
        if (!letter && !writable) return;
        var mark = holder.GetComponent<HSLiftCabinSign>();
        if (mark == null) mark = holder.AddComponent<HSLiftCabinSign>();
        mark.Dx = c.Dx;
        mark.Dy = c.Dy;
        mark.Dz = c.Dz;
        mark.Letter = letter && !writable;
        if (!writable) return;
        var floor = HSLiftConfiguration.FloorAt(baseY);
        PaintMeshes(meshes, floor != null ? HSLiftConfiguration.FloorSignText(floor.Name) : authored);
    }

    static void RefreshMoving(HSLiftFloor floor)
    {
        if (movingRoot == null || movingCells == null || floor == null) return;
        var label = HSLiftConfiguration.FloorSignText(floor.Name);
        var shape = ShapeForFloor(floor.Name);
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        foreach (var mark in movingRoot.GetComponentsInChildren<HSLiftCabinSign>(true))
        {
            if (mark == null) continue;
            HSLiftCell cell = null;
            foreach (var c in movingCells)
                if (c != null && c.Dx == mark.Dx && c.Dy == mark.Dy && c.Dz == mark.Dz) { cell = c; break; }
            if (cell == null) continue;
            var meshes = mark.GetComponentsInChildren<TextMesh>(true);
            if (meshes != null && meshes.Length > 0)
            {
                PaintMeshes(meshes, label);
                WriteCellSign(cell, label);
                continue;
            }
            if (!mark.Letter || shape == null || cell.Bv.Block == null) continue;
            var name = cell.Bv.Block.GetBlockName();
            if (name != null && name.EndsWith(shape, StringComparison.OrdinalIgnoreCase)) continue;
            var next = ReplaceShape(name, shape);
            var nv = Block.GetBlockValue(next);
            if (nv.isair || nv.type == 0) continue;
            nv.rotation = cell.Bv.rotation;
            nv.damage = cell.Bv.damage;
            cell.Bv = nv;
            for (int i = mark.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(mark.transform.GetChild(i).gameObject);
            var ic = ItemClass.GetForId(nv.ToItemType());
            var worldPos = new Vector3(D.MinX + cell.Dx, movingRoot.transform.position.y + Origin.position.y + cell.Dy, D.MinZ + cell.Dz);
            var model = HSGameApi.CloneBlockModel(ic, world, nv, worldPos, mark.transform, cell.Tex);
            if (model == null) continue;
            model.localPosition = Vector3.zero;
            model.localRotation = nv.Block.shape.GetRotation(nv);
            foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        }
    }

    static void WriteCellSign(HSLiftCell c, string label)
    {
        var te = c.Te as TileEntityComposite;
        if (te == null || string.IsNullOrEmpty(label)) return;
        var sign = te.GetFeature<ITileEntitySignable>();
        if (sign == null) return;
        var now = SignText(c);
        if (!IsThisLiftFloorText(now) && !IsDisplayText(now)) return;
        if (string.Equals(now, label, StringComparison.Ordinal)) return;
        try
        {
            var authored = sign.GetAuthoredText();
            sign.SetText(label, false, authored != null ? authored.Author : null);
        }
        catch (Exception e)
        {
            HSLiftDebug.Warn("Cabin sign text failed: " + e.Message);
        }
    }

    static bool CellSignIsFloor(HSLiftCell c)
    {
        var now = SignText(c);
        return IsThisLiftFloorText(now) || IsDisplayText(now);
    }

    static string SignText(HSLiftCell c)
    {
        var te = c != null ? c.Te as TileEntityComposite : null;
        var sign = te != null ? te.GetFeature<ITileEntitySignable>() : null;
        var authored = sign != null ? sign.GetAuthoredText() : null;
        return authored != null && authored.Text != null ? authored.Text.Trim() : "";
    }

    static string SignTextAt(HSLiftCell c, int baseY)
    {
        var fromCell = SignText(c);
        if (!string.IsNullOrEmpty(fromCell)) return fromCell;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || D == null || c == null) return "";
        var pos = new Vector3i(D.MinX + c.Dx, baseY + c.Dy, D.MinZ + c.Dz);
        var te = world.GetTileEntity(pos) as TileEntityComposite;
        var sign = te != null ? te.GetFeature<ITileEntitySignable>() : null;
        var authored = sign != null ? sign.GetAuthoredText() : null;
        return authored != null && authored.Text != null ? authored.Text.Trim() : "";
    }

    static void PaintMeshes(TextMesh[] meshes, string label)
    {
        if (meshes == null || string.IsNullOrEmpty(label)) return;
        for (int i = 0; i < meshes.Length; i++)
        {
            var m = meshes[i];
            if (m == null) continue;
            var smart = m.GetComponent<SmartTextMesh>();
            if (smart != null) smart.enabled = false;
            if (m.fontSize < 1) m.fontSize = 48;
            if (m.characterSize < 0.001f) m.characterSize = 0.02f;
            m.text = label;
        }
    }
}

public class HSLiftCabinSign : MonoBehaviour
{
    public int Dx, Dy, Dz;
    public bool Letter;
}
