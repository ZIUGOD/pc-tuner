using System.Globalization;
using System.IO;
using PcTuner.Core;

namespace PcTuner.Tests;

public class LatencyStatsTests
{
    [Fact]
    public void Summarizes_received_samples_and_counts_losses()
    {
        var stats = LatencyStats.From([10, 12, null, 14, 40]);

        Assert.Equal(5, stats.Sent);
        Assert.Equal(4, stats.Received);
        Assert.Equal(20, stats.LossPercent);
        Assert.Equal(10, stats.Min);
        Assert.Equal(40, stats.Max);
        Assert.Equal(12, stats.Median);
        Assert.Equal(40, stats.P95);
    }

    [Fact]
    public void Jitter_is_mean_difference_between_consecutive_replies()
    {
        // diferenças: 2, 2, 26 → média 10
        var stats = LatencyStats.From([10, 12, 14, 40]);
        Assert.Equal(10, stats.Jitter, precision: 6);
    }

    [Fact]
    public void All_lost_gives_full_loss_and_zeros()
    {
        var stats = LatencyStats.From([null, null]);
        Assert.Equal(100, stats.LossPercent);
        Assert.Equal(0, stats.Median);
    }

    [Theory]
    [InlineData(20, 22, "A+")]
    [InlineData(20, 45, "A")]
    [InlineData(20, 70, "B")]
    [InlineData(20, 150, "C")]
    [InlineData(20, 380, "D")]
    [InlineData(20, 600, "F")]
    public void Bufferbloat_grade_follows_latency_increase(double idle, double loaded, string grade) =>
        Assert.Equal(grade, LatencyStats.BufferbloatGrade(idle, loaded));
}

public class SizeFormatTests
{
    static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public void Gigabytes_use_one_decimal() =>
        Assert.Equal("9,5 GB", SizeFormat.Format((long)(9.5 * 1024 * 1024 * 1024), PtBr));

    [Fact]
    public void Below_one_gigabyte_shows_megabytes() =>
        Assert.Equal("806 MB", SizeFormat.Format(806L * 1024 * 1024, PtBr));

    [Fact]
    public void Missing_folder_measures_zero() =>
        Assert.Equal(0, SizeFormat.FolderBytes(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));

    [Fact]
    public void Folder_size_sums_nested_files()
    {
        var root = Directory.CreateTempSubdirectory("pctuner-test-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "a.bin"), new byte[1000]);
            var sub = root.CreateSubdirectory("sub");
            File.WriteAllBytes(Path.Combine(sub.FullName, "b.bin"), new byte[500]);

            Assert.Equal(1500, SizeFormat.FolderBytes(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}

public class StartupApprovedTests
{
    [Fact]
    public void Missing_value_means_enabled() => Assert.True(StartupManager.IsApprovedEnabled(null));

    [Theory]
    [InlineData(0x02, true)]
    [InlineData(0x06, true)]
    [InlineData(0x03, false)]
    [InlineData(0x07, false)]
    public void First_byte_parity_decides_state(byte first, bool enabled) =>
        Assert.Equal(enabled, StartupManager.IsApprovedEnabled([first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Written_value_round_trips(bool enabled)
    {
        var data = StartupManager.ApprovedValue(enabled);
        Assert.Equal(12, data.Length);
        Assert.Equal(enabled, StartupManager.IsApprovedEnabled(data));
    }
}
