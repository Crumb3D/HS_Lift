using UnityEngine;

// Smooth kinematic travel of the car copy. Positions are world block Y; Unity positions follow Origin shifts.
public class HSLiftMovement
{
    public GameObject Root;
    public float CurY;
    public int TargetY;

    Rigidbody rb;

    public void Begin(GameObject root, int startY, int targetY)
    {
        Root = root;
        rb = root.GetComponent<Rigidbody>();
        CurY = startY;
        TargetY = targetY;
        root.transform.position = HSLiftCar.UnityPos(CurY);
        Physics.SyncTransforms();
    }

    // Returns true on arrival.
    public bool Step(float dt, float speed)
    {
        CurY = Mathf.MoveTowards(CurY, TargetY, speed * dt);
        Apply();
        return Mathf.Approximately(CurY, TargetY);
    }

    public void Apply()
    {
        if (Root == null) return;
        var p = HSLiftCar.UnityPos(CurY);
        if (rb != null) rb.MovePosition(p);
        else Root.transform.position = p;
    }

    public int Direction { get { return TargetY > CurY ? 1 : (TargetY < CurY ? -1 : 0); } }

    // Nearest whole row already travelled through (always clear, the car just swept it).
    public int SettleY(int fromY)
    {
        int y = Direction > 0 ? Mathf.FloorToInt(CurY + 0.001f) : Mathf.CeilToInt(CurY - 0.001f);
        if (fromY <= TargetY) return Mathf.Clamp(y, fromY, TargetY);
        return Mathf.Clamp(y, TargetY, fromY);
    }

    public void Destroy()
    {
        if (Root != null) Object.Destroy(Root);
        Root = null;
        rb = null;
    }
}
