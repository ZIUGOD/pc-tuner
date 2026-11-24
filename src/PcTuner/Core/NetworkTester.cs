using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PcTuner.Core;

public sealed record NetworkReport(
    string? Gateway,
    LatencyStats? GatewayIdle,
    LatencyStats InternetIdle,
    LatencyStats InternetDuringDownload,
    LatencyStats InternetDuringUpload,
    double DownloadMbps,
    double UploadMbps)
{
    public string DownloadGrade => LatencyStats.BufferbloatGrade(InternetIdle.Median, InternetDuringDownload.Median);
    public string UploadGrade => LatencyStats.BufferbloatGrade(InternetIdle.Median, InternetDuringUpload.Median);
}

/// <summary>
/// Mede ping (roteador e internet), velocidade e bufferbloat: o quanto o ping sobe
/// enquanto a conexão está ocupada com download ou upload.
/// </summary>
public static class NetworkTester
{
    const string PingTarget = "1.1.1.1";
    const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=25000000";
    const string UploadUrl = "https://speed.cloudflare.com/__up";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PcTuner/0.1");
        return client;
    }

    public static async Task<NetworkReport> RunAsync(IProgress<string> progress, CancellationToken token)
    {
        var gateway = FindGateway();

        LatencyStats? gatewayIdle = null;
        if (gateway is not null)
        {
            progress.Report($"Medindo ping até o roteador ({gateway})...");
            gatewayIdle = await PingSeries(gateway, 20, token);
        }

        progress.Report($"Medindo ping até a internet ({PingTarget})...");
        var idle = await PingSeries(PingTarget, 20, token);

        progress.Report("Medindo download e o ping com a conexão ocupada...");
        var (downloadMbps, duringDownload) = await UnderLoad(DownloadWorker, streams: 4, token);

        progress.Report("Medindo upload e o ping com a conexão ocupada...");
        var (uploadMbps, duringUpload) = await UnderLoad(UploadWorker, streams: 3, token);

        return new NetworkReport(gateway, gatewayIdle, idle, duringDownload, duringUpload, downloadMbps, uploadMbps);
    }

    static string? FindGateway() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .SelectMany(n => n.GetIPProperties().GatewayAddresses)
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))
            ?.ToString();

    static async Task<LatencyStats> PingSeries(string host, int count, CancellationToken token, int intervalMs = 200)
    {
        using var ping = new Ping();
        var samples = new List<double?>(count);
        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            var clock = Stopwatch.StartNew();
            try
            {
                var reply = await ping.SendPingAsync(host, 1000);
                samples.Add(reply.Status == IPStatus.Success ? reply.RoundtripTime : null);
            }
            catch (PingException)
            {
                samples.Add(null);
            }

            var wait = intervalMs - (int)clock.ElapsedMilliseconds;
            if (wait > 0) await Task.Delay(wait, token);
        }
        return LatencyStats.From(samples);
    }

    sealed class ByteCounter
    {
        long _value;
        public long Value => Interlocked.Read(ref _value);
        public void Add(long bytes) => Interlocked.Add(ref _value, bytes);
    }

    static async Task<(double Mbps, LatencyStats Ping)> UnderLoad(
        Func<ByteCounter, CancellationToken, Task> worker, int streams, CancellationToken token)
    {
        using var load = CancellationTokenSource.CreateLinkedTokenSource(token);
        var counter = new ByteCounter();
        var workers = Enumerable.Range(0, streams).Select(_ => Task.Run(() => worker(counter, load.Token))).ToArray();

        try
        {
            await Task.Delay(1500, token); // deixa a conexão encher antes de medir
            var startBytes = counter.Value;
            var clock = Stopwatch.StartNew();
            var ping = await PingSeries(PingTarget, 35, token);
            var seconds = clock.Elapsed.TotalSeconds;
            var mbps = (counter.Value - startBytes) * 8 / 1_000_000d / seconds;
            return (mbps, ping);
        }
        finally
        {
            load.Cancel();
            try { await Task.WhenAll(workers); } catch { /* cancelado */ }
        }
    }

    static async Task DownloadWorker(ByteCounter counter, CancellationToken token)
    {
        var buffer = new byte[81920];
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0) counter.Add(read);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                try { await Task.Delay(500, token); } catch (OperationCanceledException) { return; }
            }
        }
    }

    // Pedaços pequenos: o upload só é contado quando o servidor confirma o recebimento,
    // e com 2 MB a medição continua precisa mesmo em conexões lentas.
    static readonly byte[] UploadChunk = new byte[2_000_000];

    static async Task UploadWorker(ByteCounter counter, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var content = new ByteArrayContent(UploadChunk);
                using var response = await Http.PostAsync(UploadUrl, content, token);
                if (response.IsSuccessStatusCode) counter.Add(UploadChunk.Length);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                try { await Task.Delay(500, token); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
