using Microsoft.Win32;

namespace PcTuner.Core;

public enum ServiceStart
{
    Automatic = 2,
    Manual = 3,
    Disabled = 4,
}

/// <summary>Lê o tipo de inicialização pelo registro e altera serviços pelo sc.exe.</summary>
public static class WinServices
{
    const string ServicesKey = @"SYSTEM\CurrentControlSet\Services\";

    public static bool Exists(string name) => Reg.KeyExists(RegistryHive.LocalMachine, ServicesKey + name);

    public static ServiceStart? GetStart(string name) =>
        Reg.Get(RegistryHive.LocalMachine, ServicesKey + name, "Start") is int value ? (ServiceStart)value : null;

    public static bool IsDisabled(string name) => GetStart(name) == ServiceStart.Disabled;

    public static void Configure(string name, ServiceStart start, bool running)
    {
        if (!Exists(name)) throw new InvalidOperationException($"Serviço '{name}' não encontrado.");

        var mode = start switch
        {
            ServiceStart.Automatic => "auto",
            ServiceStart.Manual => "demand",
            _ => "disabled",
        };

        var result = Shell.Run("sc.exe", $"config \"{name}\" start= {mode}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Não foi possível configurar o serviço '{name}' (código {result.ExitCode}).");

        // start/stop devolvem erro quando o serviço já está no estado pedido; isso não é falha.
        Shell.Run("sc.exe", $"{(running ? "start" : "stop")} \"{name}\"");
    }
}
