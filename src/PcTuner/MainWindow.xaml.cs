using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
        var isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        VersionText.Text = $"v{version?.ToString(3)} · {(isAdmin ? "admin" : "sem admin")}";

        History.Written += message => Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = message;
            UpdateDisk();
        });

        UpdateDisk();
        NavOverview.IsChecked = true;
        SourceInitialized += (_, _) => UseDarkTitleBar();
    }

    /// <summary>Barra de título escura no Windows 10/11, combinando com o tema.</summary>
    void UseDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        int enabled = 1;
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));      // DWMWA_USE_IMMERSIVE_DARK_MODE
        int caption = 0x00080906;                                          // #060908 em COLORREF (BGR)
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));      // DWMWA_CAPTION_COLOR (Windows 11)
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

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
        DiskText.Text = $"{SizeFormat.Format(c.AvailableFreeSpace)} livres / {SizeFormat.Format(c.TotalSize)}";
        DiskBar.Foreground = (System.Windows.Media.Brush)FindResource(DiskBar.Value > 0.9 ? "Danger" : DiskBar.Value > 0.8 ? "Caution" : "Accent");
    }

    void OpenHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(History.LogFile)) History.Write("Histórico criado.");
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{History.LogFile}\"") { UseShellExecute = true });
    }
}
