using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;
using PcTuner.ViewModels;

namespace PcTuner.Views;

public partial class TweaksPage : UserControl
{
    public TweaksPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (Sections.ItemsSource is not null) return;

            var candidates = Tweak.All();
            var checks = candidates.Select(t => Task.Run(() =>
            {
                try { return t.IsAvailable() ? t : null; } catch { return null; }
            }));
            var available = (await Task.WhenAll(checks)).OfType<Tweak>().ToList();

            var items = available.Select(t => new TweakItem(t)).ToList();
            Sections.ItemsSource = items
                .GroupBy(i => i.Section)
                .Select(g => new TweakSection(g.Key.ToUpperInvariant(), g.ToList()))
                .ToList();
            Loading.Visibility = Visibility.Collapsed;

            await Task.WhenAll(items.Select(i => i.RefreshAsync()));
        };
    }
}
