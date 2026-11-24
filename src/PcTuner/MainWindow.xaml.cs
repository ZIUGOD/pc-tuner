using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;
using PcTuner.Views;

namespace PcTuner;

public partial class MainWindow : Window
{
    readonly Dictionary<string, UserControl> _pages = new();

    public MainWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"versão {version?.ToString(3)}";

        History.Written += message => Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = message;
            UpdateDisk();
        });

        UpdateDisk();
        NavOverview.IsChecked = true;
    }

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost is null || sender is not RadioButton { Tag: string key }) return;

        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "tweaks" => new TweaksPage(),
                "network" => new NetworkPage(),
                "startup" => new StartupPage(),
                "clean" => new CleanPage(),
                "disk" => new DiskPage(),
                _ => new OverviewPage(),
            };
            _pages[key] = page;
        }

        PageHost.Content = page;
    }

    void UpdateDisk()
    {
        var c = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
        if (c is null) return;
        DiskBar.Value = 1 - (double)c.AvailableFreeSpace / c.TotalSize;
        DiskText.Text = $"{SizeFormat.Format(c.AvailableFreeSpace)} livres de {SizeFormat.Format(c.TotalSize)}";
    }

    void OpenHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(History.LogFile)) History.Write("Histórico criado.");
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{History.LogFile}\"") { UseShellExecute = true });
    }
}
