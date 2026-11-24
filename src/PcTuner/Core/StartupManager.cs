using Microsoft.Win32;

namespace PcTuner.Core;

public enum StartupLocation { RegistryRun, StartupFolder }

public sealed record StartupEntry(string Name, string Command, StartupLocation Location);

/// <summary>
/// Liga e desliga programas da inicialização do mesmo jeito que o Gerenciador de Tarefas:
/// pelo valor StartupApproved, sem apagar a entrada original.
/// </summary>
public static class StartupManager
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public static IReadOnlyList<StartupEntry> GetEntries()
    {
        var entries = Reg.Values(RegistryHive.CurrentUser, RunKey)
            .Select(v => new StartupEntry(v.Name, v.Value?.ToString() ?? "", StartupLocation.RegistryRun))
            .ToList();

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(folder))
        {
            entries.AddRange(Directory.EnumerateFiles(folder)
                .Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                .Select(f => new StartupEntry(Path.GetFileName(f), f, StartupLocation.StartupFolder)));
        }

        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static string ApprovedKey(StartupEntry entry) =>
        entry.Location == StartupLocation.RegistryRun ? ApprovedRun : ApprovedFolder;

    public static bool IsEnabled(StartupEntry entry) =>
        IsApprovedEnabled(Reg.Get(RegistryHive.CurrentUser, ApprovedKey(entry), entry.Name) as byte[]);

    public static void SetEnabled(StartupEntry entry, bool enabled) =>
        Reg.Set(RegistryHive.CurrentUser, ApprovedKey(entry), entry.Name, ApprovedValue(enabled), RegistryValueKind.Binary);

    /// <summary>Sem valor = ligado. Primeiro byte par (02, 06) = ligado; ímpar (03, 07) = desligado.</summary>
    internal static bool IsApprovedEnabled(byte[]? data) => data is null || data.Length == 0 || (data[0] & 1) == 0;

    internal static byte[] ApprovedValue(bool enabled)
    {
        var data = new byte[12];
        data[0] = enabled ? (byte)2 : (byte)3;
        return data;
    }
}
