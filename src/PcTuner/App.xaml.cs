using System.Windows;
using System.Windows.Threading;
using PcTuner.Core;

namespace PcTuner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandled;
        base.OnStartup(e);
    }

    static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Directory.CreateDirectory(History.Folder);
        File.AppendAllText(Path.Combine(History.Folder, "crash.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {e.Exception}{Environment.NewLine}");
        MessageBox.Show($"Algo deu errado:\n\n{e.Exception.Message}\n\nDetalhes em {History.Folder}\\crash.log",
            "PC Tuner", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
