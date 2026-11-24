using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;

namespace PcTuner.Views;

public partial class OverviewPage : UserControl
{
    sealed record DriveRow(string Title, string Detail, double Used, string BarColor);

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => Show(await Task.Run(SystemInfo.Read));
    }

    void Show(SystemSnapshot s)
    {
        Subtitle.Text = $"{s.Model}  ·  {s.Os}";

        CpuValue.Text = s.Cpu;
        CpuDetail.Text = $"{s.Cores} núcleos, {s.Threads} threads";

        var used = s.RamTotal - s.RamFree;
        RamValue.Text = $"{SizeFormat.Format(used)} em uso";
        RamBar.Value = s.RamTotal == 0 ? 0 : (double)used / s.RamTotal;
        RamDetail.Text = $"de {SizeFormat.Format(s.RamTotal)} ({SizeFormat.Format(s.RamFree)} livres)";

        UptimeValue.Text = s.Uptime.TotalDays >= 1
            ? $"{(int)s.Uptime.TotalDays} d {s.Uptime.Hours} h"
            : $"{s.Uptime.Hours} h {s.Uptime.Minutes} min";
        BatteryDetail.Text = s.BatteryPercent is { } battery ? $"Bateria: {battery}%" : "Sem bateria";

        GpuValue.Text = s.Gpus.Count == 0 ? "Não encontrada" : string.Join(Environment.NewLine, s.Gpus);

        Drives.ItemsSource = s.Drives.Select(d => new DriveRow(
            string.IsNullOrWhiteSpace(d.Label) ? d.Name : $"{d.Name}  {d.Label}",
            $"{SizeFormat.Format(d.Free)} livres de {SizeFormat.Format(d.Total)}",
            d.UsedFraction,
            d.UsedFraction > 0.9 ? "#DC2626" : d.UsedFraction > 0.8 ? "#D97706" : "#2563EB")).ToList();

        var alerts = new List<string>();
        foreach (var d in s.Drives.Where(d => d.UsedFraction > 0.9))
            alerts.Add($"O disco {d.Name} está com só {SizeFormat.Format(d.Free)} livres. Disco quase cheio deixa o PC lento: veja a tela Espaço em disco.");
        if (s.Uptime.TotalDays > 7)
            alerts.Add($"O PC está ligado há {(int)s.Uptime.TotalDays} dias sem reiniciar. Reiniciar de vez em quando libera memória e aplica atualizações.");

        if (alerts.Count > 0)
        {
            AlertText.Text = string.Join(Environment.NewLine, alerts);
            AlertBox.Visibility = Visibility.Visible;
        }
    }
}
