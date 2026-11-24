using System.Diagnostics;
using System.Text;

namespace PcTuner.Core;

/// <summary>Executa programas do Windows (powercfg, sc, PowerShell) sem abrir janela.</summary>
public static class Shell
{
    public sealed record Result(int ExitCode, string Output, string Error);

    public static Result Run(string fileName, string arguments, int timeoutMs = 120_000)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* já terminou */ }
            throw new TimeoutException($"{fileName} demorou demais para responder.");
        }

        return new Result(process.ExitCode, output.Result, error.Result);
    }

    /// <summary>Roda um script PowerShell e devolve a saída. Lança exceção se o script falhar.</summary>
    public static string PowerShell(string script)
    {
        var wrapped = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
                      "[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped));
        var result = Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}");

        if (result.ExitCode != 0)
        {
            var message = result.Error
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            throw new InvalidOperationException(message ?? $"PowerShell terminou com código {result.ExitCode}.");
        }

        return result.Output.Trim();
    }

    /// <summary>Coloca um texto entre aspas simples para uso seguro dentro de um script PowerShell.</summary>
    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
