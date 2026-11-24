namespace PcTuner.Core;

/// <summary>Resumo de uma série de pings. Amostras nulas são pacotes perdidos.</summary>
public sealed record LatencyStats(int Sent, int Received, double Min, double Median, double P95, double Max, double Jitter)
{
    public double LossPercent => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent;

    public static LatencyStats From(IReadOnlyList<double?> samples)
    {
        var received = samples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (received.Count == 0) return new LatencyStats(samples.Count, 0, 0, 0, 0, 0, 0);

        var sorted = received.OrderBy(v => v).ToList();
        double jitter = 0;
        for (var i = 1; i < received.Count; i++) jitter += Math.Abs(received[i] - received[i - 1]);
        if (received.Count > 1) jitter /= received.Count - 1;

        return new LatencyStats(
            samples.Count,
            received.Count,
            sorted[0],
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            sorted[^1],
            jitter);
    }

    /// <summary>Percentil pelo método nearest-rank sobre uma lista já ordenada.</summary>
    internal static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        var rank = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    /// <summary>
    /// Nota de bufferbloat: quanto o ping sobe quando a conexão está ocupada.
    /// Mesma escala dos testes mais conhecidos (A+ até F).
    /// </summary>
    public static string BufferbloatGrade(double idleMedian, double loadedMedian)
    {
        var increase = loadedMedian - idleMedian;
        return increase switch
        {
            < 5 => "A+",
            < 30 => "A",
            < 60 => "B",
            < 200 => "C",
            < 400 => "D",
            _ => "F",
        };
    }
}
