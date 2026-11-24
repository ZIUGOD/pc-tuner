namespace PcTuner.Core;

/// <summary>Histórico de tudo que o app muda, em %LOCALAPPDATA%\PcTuner\history.log.</summary>
public static class History
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcTuner");

    public static string LogFile { get; } = Path.Combine(Folder, "history.log");

    static readonly object Gate = new();

    public static event Action<string>? Written;

    public static void Write(string message)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        Written?.Invoke(message);
    }
}
