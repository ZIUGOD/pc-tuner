using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;
using PcTuner.ViewModels;

namespace PcTuner.Views;

public partial class CleanPage : UserControl
{
    readonly List<CleanItem> _items = Cleaner.Targets().Select(t => new CleanItem(t)).ToList();

    public CleanPage()
    {
        InitializeComponent();
        Targets.ItemsSource = _items;
        Loaded += async (_, _) => await Task.WhenAll(_items.Select(i => i.MeasureAsync()));
    }

    async void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CleanItem item }) return;

        var answer = MessageBox.Show(
            $"Apagar {SizeFormat.Format(item.Bytes)} de \"{item.Title}\"?\n\nIsso não pode ser desfeito.",
            "Confirmar limpeza", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        await item.CleanAsync();
    }
}
