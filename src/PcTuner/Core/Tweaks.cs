using Microsoft.Win32;

namespace PcTuner.Core;

public enum Impact { High, Medium, Low, Space, Experiment }

/// <summary>Um ajuste que pode ser ligado e desligado. Ligado = otimização aplicada.</summary>
public abstract class Tweak
{
    public abstract string Section { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public virtual string? Warning => null;
    public abstract Impact Impact { get; }

    public virtual bool IsAvailable() => true;
    public abstract bool IsApplied();
    public abstract void Apply();
    public abstract void Revert();

    public static IReadOnlyList<Tweak> All() =>
    [
        new WifiGamingTweak(),
        new KillerBandwidthTweak(),
        new DeliveryOptimizationTweak(),
        new NetworkThrottlingTweak(),
        new HighPerformancePlanTweak(),
        new DefenderDevExclusionsTweak(),
        new HibernationTweak(),
        new ServiceTweak("DiagTrack", ServiceStart.Automatic, "Desligar telemetria do Windows",
            "Para o serviço DiagTrack, que envia dados de uso para a Microsoft em segundo plano."),
        new ServiceTweak("Killer Analytics Service", ServiceStart.Automatic, "Desligar telemetria do Killer",
            "Para o Killer Analytics Service, que só coleta estatísticas de uso da placa de rede."),
    ];
}

// ------------------------------------------------------------------ Rede

public sealed class WifiGamingTweak : Tweak
{
    // palavra-chave do driver, valor otimizado, valor padrão (numéricos: funcionam em qualquer idioma)
    static readonly (string Key, int Optimized, int Default)[] Settings =
    [
        ("RoamAggressiveness", 0, 2),        // 0 = mais baixo, 2 = média
        ("RoamingPreferredBandType", 2, 0),  // 2 = preferir 5 GHz, 0 = sem preferência
        ("*PacketCoalescing", 0, 1),         // 0 = desligado
        ("MIMOPowerSaveMode", 3, 0),         // 3 = sem SMPS, 0 = automático
    ];

    const string FindAdapter =
        "$a = Get-NetAdapter -Physical | Where-Object { $_.NdisPhysicalMedium -eq 9 } | Select-Object -First 1; " +
        "if (-not $a) { throw 'Nenhuma placa Wi-Fi encontrada.' }; ";

    static string Keys => string.Join(",", Settings.Select(s => Shell.Quote(s.Key)));

    public override string Section => "Rede";
    public override string Title => "Wi-Fi ajustado para jogos";
    public override string Description =>
        "Roaming no mínimo (a placa para de procurar outras redes no meio da partida), prefere 5 GHz, " +
        "desliga a economia de energia das antenas e a união de pacotes. Reduz os picos de ping.";
    public override string Warning => "O Wi-Fi reconecta por alguns segundos ao mudar.";
    public override Impact Impact => Impact.High;

    public override bool IsAvailable()
    {
        try { return Current().Count > 0; } catch { return false; }
    }

    Dictionary<string, int> Current()
    {
        var output = Shell.PowerShell(FindAdapter +
            $"Get-NetAdapterAdvancedProperty -Name $a.Name | Where-Object RegistryKeyword -in @({Keys}) | " +
            "ForEach-Object { $_.RegistryKeyword + '=' + ($_.RegistryValue | Select-Object -First 1) }");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(p => p.Length == 2 && int.TryParse(p[1], out _))
            .ToDictionary(p => p[0], p => int.Parse(p[1]));
    }

    public override bool IsApplied()
    {
        var current = Current();
        return current.Count > 0 && Settings.Where(s => current.ContainsKey(s.Key)).All(s => current[s.Key] == s.Optimized);
    }

    public override void Apply() => SetAll(optimized: true);
    public override void Revert() => SetAll(optimized: false);

    void SetAll(bool optimized)
    {
        var present = Current();
        var commands = Settings.Where(s => present.ContainsKey(s.Key)).Select(s =>
            $"Set-NetAdapterAdvancedProperty -Name $a.Name -RegistryKeyword {Shell.Quote(s.Key)} " +
            $"-RegistryValue {(optimized ? s.Optimized : s.Default)} -NoRestart; ");
        Shell.PowerShell(FindAdapter + string.Concat(commands) + "Restart-NetAdapter -Name $a.Name");
    }
}

public sealed class KillerBandwidthTweak : Tweak
{
    const string NetworkService = "Killer Network Service";
    const string BandwidthService = "KNDBWM";

    public override string Section => "Rede";
    public override string Title => "Desligar gerenciamento de banda do Killer";
    public override string Description =>
        "Para o Killer Network Service e o Dynamic Bandwidth Management. Em alguns PCs eles pioram o ping; " +
        "em outros ajudam quando alguém usa a internet ao mesmo tempo. Jogue com e sem e compare.";
    public override string Warning => "O Killer Intelligence Center deixa de funcionar enquanto estiver ligado.";
    public override Impact Impact => Impact.Experiment;

    public override bool IsAvailable() => WinServices.Exists(NetworkService);

    public override bool IsApplied() =>
        WinServices.IsDisabled(NetworkService) &&
        (!WinServices.Exists(BandwidthService) || WinServices.IsDisabled(BandwidthService));

    public override void Apply()
    {
        WinServices.Configure(NetworkService, ServiceStart.Disabled, running: false);
        if (WinServices.Exists(BandwidthService)) WinServices.Configure(BandwidthService, ServiceStart.Disabled, running: false);
    }

    public override void Revert()
    {
        WinServices.Configure(NetworkService, ServiceStart.Automatic, running: true);
        if (WinServices.Exists(BandwidthService)) WinServices.Configure(BandwidthService, ServiceStart.Manual, running: true);
    }
}

public sealed class DeliveryOptimizationTweak : Tweak
{
    const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";

    public override string Section => "Rede";
    public override string Title => "Não enviar atualizações do Windows para outros PCs";
    public override string Description =>
        "Desliga a Otimização de Entrega (P2P). O Windows baixa atualizações só da Microsoft, " +
        "sem usar o seu upload para outros computadores.";
    public override Impact Impact => Impact.Low;

    public override bool IsApplied() => Reg.Get(RegistryHive.LocalMachine, PolicyKey, "DODownloadMode") is 0;
    public override void Apply() => Reg.Set(RegistryHive.LocalMachine, PolicyKey, "DODownloadMode", 0, RegistryValueKind.DWord);
    public override void Revert() => Reg.Delete(RegistryHive.LocalMachine, PolicyKey, "DODownloadMode");
}

public sealed class NetworkThrottlingTweak : Tweak
{
    const string Key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    public override string Section => "Rede";
    public override string Title => "Remover limite de rede durante áudio e vídeo";
    public override string Description =>
        "O Windows limita pacotes de rede enquanto toca mídia (NetworkThrottlingIndex). O efeito é pequeno, " +
        "mas pode ajudar se você joga com Spotify ou Discord abertos.";
    public override Impact Impact => Impact.Low;

    public override bool IsApplied() => Reg.Get(RegistryHive.LocalMachine, Key, "NetworkThrottlingIndex") is -1;
    public override void Apply() => Reg.Set(RegistryHive.LocalMachine, Key, "NetworkThrottlingIndex", -1, RegistryValueKind.DWord);
    public override void Revert() => Reg.Set(RegistryHive.LocalMachine, Key, "NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
}

// ------------------------------------------------------------------ Sistema

public sealed class HighPerformancePlanTweak : Tweak
{
    const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public override string Section => "Sistema";
    public override string Title => "Plano de energia Alto desempenho";
    public override string Description => "Mantém o processador sem economizar energia. Desligado, volta para o plano Equilibrado.";
    public override string Warning => "Gasta mais bateria. Na tomada, deixe ligado; na bateria, desligue.";
    public override Impact Impact => Impact.Medium;

    public override bool IsAvailable() => Shell.Run("powercfg", "/list").Output.Contains(HighPerformance, StringComparison.OrdinalIgnoreCase);
    public override bool IsApplied() => Shell.Run("powercfg", "/getactivescheme").Output.Contains(HighPerformance, StringComparison.OrdinalIgnoreCase);
    public override void Apply() => Activate(HighPerformance);
    public override void Revert() => Activate(Balanced);

    static void Activate(string guid)
    {
        if (Shell.Run("powercfg", $"/setactive {guid}").ExitCode != 0)
            throw new InvalidOperationException("O powercfg não conseguiu trocar o plano de energia.");
    }
}

public sealed class DefenderDevExclusionsTweak : Tweak
{
    static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    readonly string[] _folders = new[]
    {
        Path.Combine(Home, ".gradle"),
        Path.Combine(Home, ".android"),
        Path.Combine(Local, "Android", "Sdk"),
        Path.Combine(Home, "AndroidStudioProjects"),
    }.Where(Directory.Exists).ToArray();

    public override string Section => "Sistema";
    public override string Title => "Builds Android mais rápidos (exclusões no Defender)";
    public override string Description =>
        "O antivírus deixa de escanear o Gradle, o SDK, os emuladores e os projetos a cada build. Pastas: " +
        string.Join(", ", _folders);
    public override string Warning => "Essas pastas ficam sem verificação do antivírus. O resto do PC continua protegido.";
    public override Impact Impact => Impact.High;

    string FolderList => "@(" + string.Join(",", _folders.Select(Shell.Quote)) + ")";

    public override bool IsAvailable() => _folders.Length > 0;

    public override bool IsApplied()
    {
        var excluded = Shell.PowerShell("(Get-MpPreference).ExclusionPath")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _folders.All(f => excluded.Contains(f, StringComparer.OrdinalIgnoreCase));
    }

    public override void Apply() => Shell.PowerShell($"Add-MpPreference -ExclusionPath {FolderList}");
    public override void Revert() => Shell.PowerShell($"Remove-MpPreference -ExclusionPath {FolderList}");
}

public sealed class HibernationTweak : Tweak
{
    static string HiberSize()
    {
        try
        {
            var file = new DirectoryInfo(@"C:\")
                .EnumerateFiles("hiberfil.sys", new EnumerationOptions { AttributesToSkip = 0 })
                .FirstOrDefault();
            return file is null ? "alguns GB" : SizeFormat.Format(file.Length);
        }
        catch { return "alguns GB"; }
    }

    readonly string _size = HiberSize();

    public override string Section => "Sistema";
    public override string Title => $"Desligar hibernação (libera {_size} no C:)";
    public override string Description => "Apaga o hiberfil.sys. Suspender continua funcionando normalmente.";
    public override string Warning =>
        "Também desliga a Inicialização rápida. Se a bateria acabar com o PC suspenso, ele desliga em vez de hibernar, " +
        "e o que não estiver salvo se perde.";
    public override Impact Impact => Impact.Space;

    public override bool IsApplied() =>
        Reg.Get(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") is 0;

    public override void Apply() => Run("off");
    public override void Revert() => Run("on");

    static void Run(string state)
    {
        if (Shell.Run("powercfg", $"/hibernate {state}").ExitCode != 0)
            throw new InvalidOperationException("O powercfg não conseguiu mudar a hibernação.");
    }
}

/// <summary>Desliga um serviço; ao reverter, volta ao tipo de inicialização padrão.</summary>
public sealed class ServiceTweak(string service, ServiceStart defaultStart, string title, string description) : Tweak
{
    public override string Section => "Sistema";
    public override string Title => title;
    public override string Description => description;
    public override Impact Impact => Impact.Low;

    public override bool IsAvailable() => WinServices.Exists(service);
    public override bool IsApplied() => WinServices.IsDisabled(service);
    public override void Apply() => WinServices.Configure(service, ServiceStart.Disabled, running: false);
    public override void Revert() => WinServices.Configure(service, defaultStart, running: true);
}
