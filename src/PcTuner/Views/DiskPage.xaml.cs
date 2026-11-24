using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;
using PcTuner.ViewModels;

namespace PcTuner.Views;

public partial class DiskPage : UserControl
{
    readonly List<DiskItem> _items = DiskUsage.Spots().Select(s => new DiskItem(s)).ToList();

    public DiskPage()
    {
        InitializeComponent();
        Spots.ItemsSource = _items;
    }

    async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeButton.IsEnabled = false;
        await Task.WhenAll(_items.Select(i => i.MeasureAsync()));
        Spots.ItemsSource = _items.OrderByDescending(i => i.Bytes).ToList();
        AnalyzeButton.IsEnabled = true;
        History.Write($"Espaço medido: {SizeFormat.Format(_items.Sum(i => i.Bytes))} nas pastas listadas.");
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DiskItem item })
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{item.Path}\"") { UseShellExecute = true });
    }
}
