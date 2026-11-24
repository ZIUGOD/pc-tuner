using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PcTuner.Core;

namespace PcTuner.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Raise(params string[] names)
    {
        foreach (var name in names) Raise(name);
    }
}

public sealed record TweakSection(string Name, IReadOnlyList<TweakItem> Items);

public sealed class TweakItem(Tweak tweak) : ObservableObject
{
    bool _isOn;
    bool _busy = true;
    bool _loaded;

    public string Section => tweak.Section;
    public string Title => tweak.Title;
    public string Description => tweak.Description;
    public bool HasWarning => tweak.Warning is not null;
    public string WarningText => " " + tweak.Warning; // ícone de aviso do Nerd Fonts

    public string BadgeText => tweak.Impact switch
    {
        Impact.High => "[impacto:alto]",
        Impact.Medium => "[impacto:médio]",
        Impact.Low => "[impacto:baixo]",
        Impact.Space => "[libera espaço]",
        _ => "[experimental]",
    };

    // Fundo quase transparente + texto colorido: destaque discreto no tema escuro.
    public string BadgeBackground => tweak.Impact switch
    {
        Impact.High => "#12301F",
        Impact.Medium => "#0F2A36",
        Impact.Low => "#161E1B",
        Impact.Space => "#251C36",
        _ => "#33280F",
    };

    public string BadgeForeground => tweak.Impact switch
    {
        Impact.High => "#39D98A",
        Impact.Medium => "#5CC8E8",
        Impact.Low => "#7E8F88",
        Impact.Space => "#B794F6",
        _ => "#E5B454",
    };

    public bool IsIdle => !_busy;

    public string StateText => !_loaded ? "lendo..." : _busy ? "aplicando..." : _isOn ? "Ligado" : "Desligado";

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_busy || value == _isOn) return;
            _ = ChangeAsync(value);
        }
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        Raise(nameof(IsIdle), nameof(StateText));
    }

    public async Task RefreshAsync()
    {
        SetBusy(true);
        try { _isOn = await Task.Run(tweak.IsApplied); }
        catch { _isOn = false; }
        _loaded = true;
        Raise(nameof(IsOn));
        SetBusy(false);
    }

    async Task ChangeAsync(bool on)
    {
        SetBusy(true);
        _isOn = on;
        Raise(nameof(IsOn));

        var label = $"{(on ? "Ligar" : "Desligar")} '{Title}'";
        try
        {
            await Task.Run(on ? new Action(tweak.Apply) : tweak.Revert);
            History.Write($"{label}: concluído.");
        }
        catch (Exception ex)
        {
            History.Write($"{label}: falhou ({ex.Message})");
            MessageBox.Show($"Não deu certo:\n\n{ex.Message}", "PC Tuner", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await RefreshAsync();
    }
}

public sealed class StartupItem(StartupEntry entry) : ObservableObject
{
    bool _isOn = StartupManager.IsEnabled(entry);

    public string Name => entry.Name;
    public string Command => entry.Command.Length > 120 ? entry.Command[..117] + "..." : entry.Command;
    public string StateText => _isOn ? "Abre com o Windows" : "Não abre";

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (value == _isOn) return;
            var label = $"Inicialização {(value ? "ligada" : "desligada")} para '{Name}'";
            try
            {
                StartupManager.SetEnabled(entry, value);
                History.Write($"{label}: concluído.");
            }
            catch (Exception ex)
            {
                History.Write($"{label}: falhou ({ex.Message})");
                MessageBox.Show($"Não deu certo:\n\n{ex.Message}", "PC Tuner", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _isOn = StartupManager.IsEnabled(entry);
            Raise(nameof(IsOn), nameof(StateText));
        }
    }
}

public sealed class CleanItem(CleanTarget target) : ObservableObject
{
    long _bytes = -1;
    bool _busy;

    public CleanTarget Target => target;
    public string Title => target.Title;
    public string Description => target.Description;
    public long Bytes => _bytes;
    public string SizeText => _busy ? "trabalhando..." : _bytes < 0 ? "calculando..." : SizeFormat.Format(_bytes);
    public bool CanClean => !_busy && _bytes >= 1024 * 1024; // abaixo de 1 MB não vale a limpeza

    public async Task MeasureAsync()
    {
        _busy = true;
        Raise(nameof(SizeText), nameof(CanClean));
        _bytes = await Task.Run(() => Cleaner.Measure(target));
        _busy = false;
        Raise(nameof(SizeText), nameof(CanClean), nameof(Bytes));
    }

    public async Task CleanAsync()
    {
        _busy = true;
        Raise(nameof(SizeText), nameof(CanClean));
        var label = $"Limpeza '{Title}' ({SizeFormat.Format(_bytes)})";
        try
        {
            await Task.Run(() => Cleaner.Clean(target));
            History.Write($"{label}: concluído.");
        }
        catch (Exception ex)
        {
            History.Write($"{label}: falhou ({ex.Message})");
        }
        await MeasureAsync();
    }
}

public sealed class DiskItem(DiskSpot spot) : ObservableObject
{
    string _sizeText = "—";

    public string Title => spot.Title;
    public string Path => spot.Path;
    public string Advice => spot.Advice;
    public long Bytes { get; private set; }

    public string SizeText
    {
        get => _sizeText;
        private set { _sizeText = value; Raise(); }
    }

    public async Task MeasureAsync()
    {
        SizeText = "medindo...";
        Bytes = await Task.Run(() => SizeFormat.FolderBytes(spot.Path));
        SizeText = SizeFormat.Format(Bytes);
    }
}
