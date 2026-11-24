using System.Runtime.InteropServices;

namespace PcTuner.Core;

/// <summary>Um grupo de cache ou arquivos temporários que pode ser apagado com segurança.</summary>
public sealed record CleanTarget(
    string Title,
    string Description,
    string[] Paths,
    int MinAgeDays = 0,
    string[]? StopServices = null,
    bool IsRecycleBin = false);

public static class Cleaner
{
    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static IReadOnlyList<CleanTarget> Targets() =>
    [
        new("Arquivos temporários do usuário", "Pasta %TEMP%. Só apaga o que tem mais de 1 dia, para não mexer em nada em uso.",
            [Path.GetTempPath()], MinAgeDays: 1),
        new("Arquivos temporários do Windows", @"Pasta C:\Windows\Temp. Só apaga o que tem mais de 1 dia.",
            [Path.Combine(Windows, "Temp")], MinAgeDays: 1),
        new("Downloads do Windows Update", "Instaladores de atualizações já aplicadas. O Windows baixa de novo se precisar.",
            [Path.Combine(Windows, "SoftwareDistribution", "Download")], StopServices: ["wuauserv", "bits"]),
        new("Cache do pip (Python)", "Pacotes baixados pelo pip. Ele baixa de novo quando precisar.",
            [Path.Combine(Local, "pip", "cache")]),
        new("Relatórios de erro e dumps", "Arquivos de falha de programas, usados só para diagnóstico.",
            [Path.Combine(Local, "CrashDumps"),
             Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"),
             Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue")]),
        new("Lixeira", "Esvazia a Lixeira de todos os discos.",
            DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => Path.Combine(d.RootDirectory.FullName, "$Recycle.Bin")).ToArray(),
            IsRecycleBin: true),
    ];

    static IEnumerable<FileSystemInfo> Items(CleanTarget target)
    {
        var cutoff = DateTime.Now.AddDays(-target.MinAgeDays);
        foreach (var path in target.Paths)
        {
            if (!Directory.Exists(path)) continue;
            IEnumerable<FileSystemInfo> children;
            try
            {
                children = new DirectoryInfo(path)
                    .EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })
                    .ToList();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var item in children)
            {
                if (target.MinAgeDays == 0 || item.LastWriteTime < cutoff) yield return item;
            }
        }
    }

    public static long Measure(CleanTarget target) =>
        Items(target).Sum(i => i is FileInfo f ? SafeLength(f) : SizeFormat.FolderBytes(i.FullName));

    static long SafeLength(FileInfo file)
    {
        try { return file.Length; } catch { return 0; }
    }

    public static void Clean(CleanTarget target)
    {
        if (target.IsRecycleBin)
        {
            // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
            SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            return;
        }

        foreach (var service in target.StopServices ?? []) Shell.Run("sc.exe", $"stop {service}");
        try
        {
            foreach (var item in Items(target).ToList())
            {
                try
                {
                    if (item is DirectoryInfo dir) dir.Delete(recursive: true);
                    else
                    {
                        item.Attributes = FileAttributes.Normal;
                        item.Delete();
                    }
                }
                catch (IOException) { /* em uso: fica para a próxima */ }
                catch (UnauthorizedAccessException) { }
            }
        }
        finally
        {
            foreach (var service in target.StopServices ?? []) Shell.Run("sc.exe", $"start {service}");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
