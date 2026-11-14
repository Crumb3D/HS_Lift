using System;
using System.Reflection;

// 3.2 ItemInventoryData.holdingEntity/itemStack and ItemStack.itemValue are fields.
// 3.3 and 3.4 made them properties. Resolve at runtime so a 3.2-built DLL never emits
// field IL that throws MissingFieldException on 3.3 / 3.4.
public static class HSInvData
{
    const BindingFlags Inst = BindingFlags.Public | BindingFlags.Instance;
    static bool resolved;
    static MemberInfo holding;
    static MemberInfo stack;
    static MemberInfo itemValue;

    static MemberInfo Find(Type t, string name)
    {
        var p = t.GetProperty(name, Inst);
        if (p != null && p.GetGetMethod() != null) return p;
        return t.GetField(name, Inst);
    }

    static object Get(MemberInfo m, object o)
    {
        if (m == null || o == null) return null;
        var p = m as PropertyInfo;
        if (p != null) return p.GetValue(o, null);
        return ((FieldInfo)m).GetValue(o);
    }

    static void Ensure()
    {
        if (resolved) return;
        resolved = true;
        holding = Find(typeof(ItemInventoryData), "holdingEntity");
        stack = Find(typeof(ItemInventoryData), "itemStack");
        itemValue = Find(typeof(ItemStack), "itemValue");
    }

    public static EntityAlive HoldingEntity(ItemInventoryData data)
    {
        Ensure();
        return Get(holding, data) as EntityAlive;
    }

    public static ItemStack Stack(ItemInventoryData data)
    {
        Ensure();
        return Get(stack, data) as ItemStack;
    }

    public static ItemValue Val(ItemStack s)
    {
        Ensure();
        return Get(itemValue, s) as ItemValue;
    }
}
