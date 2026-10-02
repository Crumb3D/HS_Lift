using System;
using System.Collections.Generic;

public static class HSLiftActivation
{
    public static bool IsTake(string commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return false;
        return EndsWithCmd(commandName, "take");
    }

    public static bool IsOpenClose(string commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return false;
        return EndsWithCmd(commandName, "open") || EndsWithCmd(commandName, "close");
    }

    public static bool IsLock(string commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return false;
        return EndsWithCmd(commandName, "lock") || EndsWithCmd(commandName, "unlock");
    }

    static bool EndsWithCmd(string name, string cmd)
    {
        return name == cmd || name.EndsWith(":" + cmd, StringComparison.Ordinal);
    }

    public static void PutOpenCloseFirst(List<BlockActivationCommand> list)
    {
        if (list == null || list.Count < 2) return;
        list.Sort((a, b) => Rank(a.text).CompareTo(Rank(b.text)));
    }

    static int Rank(string text)
    {
        if (IsOpenClose(text)) return 0;
        if (IsLock(text)) return 2;
        return 1;
    }

    public static BlockActivationCommand[] WithoutTake(BlockActivationCommand[] cmds)
    {
        if (cmds == null || cmds.Length == 0) return cmds;
        var list = new List<BlockActivationCommand>(cmds.Length);
        foreach (var c in cmds)
            if (!IsTake(c.text)) list.Add(c);
        return list.Count == cmds.Length ? cmds : list.ToArray();
    }
}
