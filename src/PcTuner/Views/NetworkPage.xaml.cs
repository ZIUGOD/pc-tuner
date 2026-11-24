using System.Text;
using System.Windows;
using System.Windows.Controls;
using PcTuner.Core;

namespace PcTuner.Views;

public partial class NetworkPage : UserControl
{
    public NetworkPage() => InitializeComponent();

    async void Start_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        Results.Visibility = Visibility.Collapsed;
        var progress = new Progress<string>(text => ProgressText.Text = text);

        try
        {
            var report = await NetworkTester.RunAsync(progress, CancellationToken.None);
            Show(report);
            ProgressText.Text = $"Teste concluído às {DateTime.Now:HH:mm}.";
            History.Write($"Teste de rede: {report.DownloadMbps:N0} Mbps down, {report.UploadMbps:N0} Mbps up, " +
                          $"ping {report.InternetIdle.Median:N0} ms, bufferbloat {report.DownloadGrade}/{report.UploadGrade}.");
        }
        catch (Exception ex)
        {
            ProgressText.Text = $"O teste falhou: {ex.Message}";
        }
        finally
        {
            StartButton.IsEnabled = true;
            StartButton.Content = "Testar de novo";
        }
    }

    static string Ms(double value) => $"{value:N0} ms";

    void Show(NetworkReport r)
    {
        DownloadValue.Text = $"{r.DownloadMbps:N0} Mbps";
        DownloadDetail.Text = "4 conexões em paralelo, servidor da Cloudflare.";
        UploadValue.Text = $"{r.UploadMbps:N0} Mbps";
        UploadDetail.Text = "3 conexões em paralelo.";

        PingValue.Text = Ms(r.InternetIdle.Median);
        PingDetail.Text = $"Até 1.1.1.1. Mín {Ms(r.InternetIdle.Min)}, pior 5% {Ms(r.InternetIdle.P95)}, " +
                          $"jitter {Ms(r.InternetIdle.Jitter)}, perda {r.InternetIdle.LossPercent:N0}%.";

        DownGradeValue.Text = r.DownloadGrade;
        DownGradeDetail.Text = $"Ping sobe para {Ms(r.InternetDuringDownload.Median)} (pior 5%: {Ms(r.InternetDuringDownload.P95)}).";
        UpGradeValue.Text = r.UploadGrade;
        UpGradeDetail.Text = $"Ping sobe para {Ms(r.InternetDuringUpload.Median)} (pior 5%: {Ms(r.InternetDuringUpload.P95)}).";

        if (r.GatewayIdle is { } gw)
        {
            GatewayValue.Text = Ms(gw.Median);
            GatewayDetail.Text = $"{r.Gateway}. Pior 5% {Ms(gw.P95)}, perda {gw.LossPercent:N0}%.";
        }
        else
        {
            GatewayValue.Text = "—";
            GatewayDetail.Text = "Roteador não encontrado.";
        }

        Verdict.Text = Explain(r);
        Results.Visibility = Visibility.Visible;
    }

    static string Explain(NetworkReport r)
    {
        var text = new StringBuilder();

        if (r.GatewayIdle is { } gw && (gw.P95 > 20 || gw.LossPercent > 0))
            text.AppendLine("• O caminho até o roteador oscila. Se estiver no Wi-Fi, chegue mais perto, use 5 GHz ou passe um cabo.");
        else if (r.GatewayIdle is not null)
            text.AppendLine("• A conexão até o roteador está estável.");

        if (r.InternetIdle.LossPercent > 0)
            text.AppendLine("• Houve perda de pacotes até a internet. Se continuar acontecendo, vale falar com a operadora.");

        var worst = new[] { r.DownloadGrade, r.UploadGrade }.Max(g => g switch { "A+" => 0, "A" => 1, "B" => 2, "C" => 3, "D" => 4, _ => 5 });
        text.AppendLine(worst switch
        {
            <= 1 => "• Bufferbloat baixo: dá para jogar mesmo com alguém usando a internet.",
            2 => "• Bufferbloat moderado: downloads pesados de outras pessoas podem dar picos de lag.",
            _ => "• Bufferbloat alto: quando alguém baixa ou envia algo, o ping sobe muito. Um roteador com SQM (ou QoS bem configurado) resolve; ligar o PC por cabo também ajuda.",
        });

        return text.ToString().TrimEnd();
    }
}
