using System.Management;

namespace PcTuner.Core;

public sealed record DriveSnapshot(string Name, string Label, long Free, long Total)
{
    public double UsedFraction => Total == 0 ? 0 : 1 - (double)Free / Total;
}

public sealed record SystemSnapshot(
    string Model,
    string Os,
    string Cpu,
    int Cores,
    int Threads,
    long RamTotal,
    long RamFree,
    IReadOnlyList<string> Gpus,
    IReadOnlyList<DriveSnapshot> Drives,
    bool HasBattery,
    int? BatteryPercent,
    TimeSpan Uptime);

/// <summary>Lê as informações básicas do PC pelo WMI.</summary>
public static class SystemInfo
{
    public static SystemSnapshot Read()
    {
        var os = First("SELECT Caption, BuildNumber, TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
        var computer = First("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
        var cpu = First("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        var gpus = All("SELECT Name, DriverVersion FROM Win32_VideoController")
            .Select(g => $"{Text(g, "Name")} (driver {Text(g, "DriverVersion")})")
            .ToList();
        var battery = All("SELECT EstimatedChargeRemaining FROM Win32_Battery").FirstOrDefault();

        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DriveSnapshot(d.Name.TrimEnd('\\'), d.VolumeLabel, d.AvailableFreeSpace, d.TotalSize))
            .ToList();

        return new SystemSnapshot(
            Model: $"{Text(computer, "Manufacturer")} {Text(computer, "Model")}".Trim(),
            Os: $"{Text(os, "Caption")} (build {Text(os, "BuildNumber")})",
            Cpu: Text(cpu, "Name"),
            Cores: Number(cpu, "NumberOfCores"),
            Threads: Number(cpu, "NumberOfLogicalProcessors"),
            RamTotal: Number64(os, "TotalVisibleMemorySize") * 1024,
            RamFree: Number64(os, "FreePhysicalMemory") * 1024,
            Gpus: gpus,
            Drives: drives,
            HasBattery: battery is not null,
            // alguns notebooks não informam a carga; nesse caso o valor vem vazio, não zero
            BatteryPercent: battery?["EstimatedChargeRemaining"] is { } charge ? Convert.ToInt32(charge) : null,
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    static List<ManagementBaseObject> All(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return searcher.Get().Cast<ManagementBaseObject>().ToList();
        }
        catch (ManagementException) { return []; }
    }

    static ManagementBaseObject? First(string query) => All(query).FirstOrDefault();

    static string Text(ManagementBaseObject? o, string name) => o?[name]?.ToString()?.Trim() ?? "?";
    static int Number(ManagementBaseObject? o, string name) => o?[name] is { } v ? Convert.ToInt32(v) : 0;
    static long Number64(ManagementBaseObject? o, string name) => o?[name] is { } v ? Convert.ToInt64(v) : 0;
}
