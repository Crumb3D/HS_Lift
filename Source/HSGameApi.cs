using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// Compiled against 3.2. 3.2 and 3.3 IL stay in different methods so the 3.2
// signatures are never JIT'd on 3.3 (and the 3.3-only emit path is never JIT'd on 3.2).
public static class HSGameApi
{
    static readonly Dictionary<string, Type> emitted = new Dictionary<string, Type>();
    static ModuleBuilder emitMod;

    public static Transform CloneBlockModel(ItemClass ic, World world, BlockValue bv, Vector3 worldPos, Transform parent, TextureFullArray tex)
    {
        if (ic == null) return null;
        if (HSGameVersion.Is33)
            return CloneBlockModel33(world, bv, worldPos, parent, tex);
        return CloneBlockModel32(ic, world, bv, worldPos, parent, tex);
    }

    static Transform CloneBlockModel32(ItemClass ic, World world, BlockValue bv, Vector3 worldPos, Transform parent, TextureFullArray tex)
    {
        return ic.CloneModel(world, bv.ToItemValue(), worldPos, parent, _textureFullArray: tex);
    }

    static Transform CloneBlockModel33(World world, BlockValue bv, Vector3 worldPos, Transform parent, TextureFullArray tex)
    {
        var t = typeof(ItemClass).Assembly.GetType("ItemClassBlock");
        if (t == null) return null;
        MethodInfo create = null;
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != "CreateMesh") continue;
            if (methods[i].GetParameters().Length < 7) continue;
            create = methods[i];
            break;
        }
        if (create == null) return null;
        var args = create.GetParameters();
        var call = new object[args.Length];
        call[0] = null;
        call[1] = world;
        call[2] = Unrotated(bv);
        call[3] = null;
        call[4] = worldPos;
        call[5] = parent;
        if (args.Length > 6)
            call[6] = Enum.ToObject(args[6].ParameterType, (int)BlockShape.MeshPurpose.World);
        if (args.Length > 7)
            call[7] = tex;
        return create.Invoke(null, call) as Transform;
    }

    // Same block the 3.2 CloneModel(ItemValue) path builds: type only, so the model comes back
    // unrotated and the caller applies the placed rotation once.
    static BlockValue Unrotated(BlockValue bv)
    {
        var flat = new BlockValue { type = bv.type };
        var block = flat.Block;
        if (block != null && block.SelectAlternates) flat = block.GetAltBlockValue(0);
        return flat;
    }

    public static Type NetPackageType32(string packageName, Type core)
    {
        Type existing;
        if (emitted.TryGetValue(packageName, out existing))
            return existing;
        var t = EmitConcrete(packageName, core, true);
        emitted[packageName] = t;
        return t;
    }

    public static Type NetPackageType33(string packageName, Type coreWithoutGetLength)
    {
        Type existing;
        if (emitted.TryGetValue(packageName, out existing))
            return existing;
        var t = EmitConcrete(packageName, coreWithoutGetLength, false);
        emitted[packageName] = t;
        return t;
    }

    static Type EmitConcrete(string name, Type core, bool withGetLength)
    {
        if (emitMod == null)
        {
            var an = new AssemblyName("HSLiftGameApiNet");
            var asm = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            emitMod = asm.DefineDynamicModule("HSLiftGameApiNet");
        }
        var tb = emitMod.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, core);
        var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = ctor.GetILGenerator();
        var baseCtor = core.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (baseCtor == null)
            baseCtor = typeof(NetPackage).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
        if (withGetLength) EmitGetLength(tb, core);
        return tb.CreateType();
    }

    static void EmitGetLength(TypeBuilder tb, Type core)
    {
        var baseMethod = typeof(NetPackage).GetMethod("GetLength", BindingFlags.Instance | BindingFlags.Public);
        if (baseMethod == null) return;
        var mb = tb.DefineMethod("GetLength",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot,
            typeof(int), Type.EmptyTypes);
        var il = mb.GetILGenerator();
        var n = il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldc_I4, 64);
        il.Emit(OpCodes.Stloc, n);
        AddStringLength(il, n, core.GetField("text", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        AddStringLength(il, n, core.GetField("liftId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
        il.Emit(OpCodes.Ldloc, n);
        il.Emit(OpCodes.Ret);
        tb.DefineMethodOverride(mb, baseMethod);
    }

    static void AddStringLength(ILGenerator il, LocalBuilder n, FieldInfo field)
    {
        if (field == null) return;
        var skip = il.DefineLabel();
        var length = typeof(string).GetProperty("Length").GetGetMethod();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Brfalse_S, skip);
        il.Emit(OpCodes.Ldloc, n);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Callvirt, length);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, n);
        il.MarkLabel(skip);
    }

    public static NetPackage GetNetPackage(Type packageType)
    {
        var methods = typeof(NetPackageManager).GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            var mi = methods[i];
            if (mi.Name != "GetPackage" || !mi.IsGenericMethodDefinition) continue;
            if (mi.GetParameters().Length != 0) continue;
            return (NetPackage)mi.MakeGenericMethod(packageType).Invoke(null, null);
        }
        return (NetPackage)Activator.CreateInstance(packageType);
    }
}
