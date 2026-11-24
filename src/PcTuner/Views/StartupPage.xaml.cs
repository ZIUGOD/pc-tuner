using System.Windows.Controls;
using PcTuner.Core;
using PcTuner.ViewModels;

namespace PcTuner.Views;

public partial class StartupPage : UserControl
{
    public StartupPage()
    {
        InitializeComponent();
        Entries.ItemsSource = StartupManager.GetEntries().Select(e => new StartupItem(e)).ToList();
    }
}
