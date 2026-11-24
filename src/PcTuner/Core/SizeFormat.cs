using System.Globalization;

namespace PcTuner.Core;

public static class SizeFormat
{
    const double Gb = 1024d * 1024 * 1024;
    const double Mb = 1024d * 1024;

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes >= Gb) return (bytes / Gb).ToString("N1", culture) + " GB";
        return (bytes / Mb).ToString("N0", culture) + " MB";
    }

    /// <summary>Soma o tamanho de todos os arquivos de uma pasta, ignorando os que não dá para ler.</summary>
    public static long FolderBytes(string path)
    {
        if (File.Exists(path)) return new FileInfo(path).Length;
        if (!Directory.Exists(path)) return 0;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        long total = 0;
        try
        {
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
            {
                try { total += file.Length; } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return total;
    }
}
