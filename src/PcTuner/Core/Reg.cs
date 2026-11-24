using Microsoft.Win32;

namespace PcTuner.Core;

/// <summary>Atalhos para ler e gravar valores no registro (visão 64 bits).</summary>
public static class Reg
{
    static RegistryKey Root(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    public static object? Get(RegistryHive hive, string path, string name)
    {
        using var root = Root(hive);
        using var key = root.OpenSubKey(path);
        return key?.GetValue(name);
    }

    public static bool KeyExists(RegistryHive hive, string path)
    {
        using var root = Root(hive);
        using var key = root.OpenSubKey(path);
        return key is not null;
    }

    public static void Set(RegistryHive hive, string path, string name, object value, RegistryValueKind kind)
    {
        using var root = Root(hive);
        using var key = root.CreateSubKey(path, writable: true);
        key.SetValue(name, value, kind);
    }

    public static void Delete(RegistryHive hive, string path, string name)
    {
        using var root = Root(hive);
        using var key = root.OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static IReadOnlyList<(string Name, object? Value)> Values(RegistryHive hive, string path)
    {
        using var root = Root(hive);
        using var key = root.OpenSubKey(path);
        if (key is null) return [];
        return key.GetValueNames().Select(n => (n, key.GetValue(n))).ToList();
    }
}
