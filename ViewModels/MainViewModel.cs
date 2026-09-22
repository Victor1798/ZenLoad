using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZenLoad.Models;
using ZenLoad.Services;

namespace ZenLoad.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private readonly FolderMonitorService _monitorService;
    private readonly IFolderPickerService _folderPickerService;
    private string _statusMessage = "Listo para organizar tus Descargas.";
    private RuleEntry? _selectedRule;
    private bool _startWithWindows;

    public MainViewModel(
        AppConfig config,
        FolderMonitorService monitorService,
        IFolderPickerService folderPickerService)
    {
        _config = config;
        _monitorService = monitorService;
        _folderPickerService = folderPickerService;
        _startWithWindows = StartupManager.IsEnabled;
        Rules = new ObservableCollection<RuleEntry>();
        LoadRules();

        SaveCommand = new RelayCommand(_ => Save());
        ResetCommand = new RelayCommand(_ => Reset());
        AddCommand = new RelayCommand(_ => AddRule());
        RemoveCommand = new RelayCommand(_ => RemoveSelectedRule(), _ => SelectedRule is not null);
        BrowseCommand = new RelayCommand(BrowseFolder);

        _monitorService.RuleAdded += OnRuleAdded;
    }

    public ObservableCollection<RuleEntry> Rules { get; }

    public AppConfig Config => _config;

    public RuleEntry? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (ReferenceEquals(_selectedRule, value))
            {
                return;
            }

            _selectedRule = value;
            OnPropertyChanged();
            RemoveCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows == value)
            {
                return;
            }

            _startWithWindows = value;
            StartupManager.SetEnabled(value);
            StatusMessage = value
                ? "ZenLoad se iniciará oculto con Windows."
                : "Inicio automático desactivado.";
            OnPropertyChanged();
        }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand BrowseCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Save()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in Rules)
        {
            var destination = rule.Destination?.Trim();

            if (string.IsNullOrWhiteSpace(rule.Extension) || string.IsNullOrWhiteSpace(destination))
            {
                StatusMessage = "Cada regla debe tener una o más extensiones y una carpeta de destino.";
                return;
            }

            var extensions = rule.Extension.Split(
                new[] { ',', ';', ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var extensionValue in extensions)
            {
                var extension = AppConfig.NormalizeExtension(extensionValue);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    continue;
                }

                if (rules.ContainsKey(extension))
                {
                    StatusMessage = $"La extensión {extension} aparece más de una vez.";
                    return;
                }

                rules[extension] = destination;
            }
        }

        _config.ReplaceRules(rules);
        _config.Save();
        _monitorService.UpdateRules(_config);
        LoadRules();
        StatusMessage = $"Guardado: {rules.Count} reglas activas.";
    }

    private void Reset()
    {
        _config.ResetToDownloads();
        _config.Save();
        _monitorService.UpdateRules(_config);
        LoadRules();
        StatusMessage = "Reglas restablecidas: los archivos permanecerán en Descargas.";
    }

    private void AddRule()
    {
        Rules.Add(new RuleEntry(".nueva", AppConfig.GetDownloadsPath()));
        SelectedRule = Rules[^1];
        StatusMessage = "Nueva regla añadida. Edita sus valores y pulsa Guardar.";
    }

    private void RemoveSelectedRule()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Rules.Remove(SelectedRule);
        SelectedRule = null;
    }

    private void BrowseFolder(object? parameter)
    {
        if (parameter is not RuleEntry rule)
        {
            return;
        }

        var selectedPath = _folderPickerService.PickFolder(rule.Destination);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        rule.Destination = selectedPath;
        StatusMessage = $"Carpeta seleccionada para {rule.Extension}. Pulsa Guardar para conservarla.";
    }

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var group in _config.ExtensionRules
                     .OrderBy(pair => pair.Key)
                     .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase))
        {
            var extensions = string.Join(", ", group.Select(pair => pair.Key));
            Rules.Add(new RuleEntry(extensions, group.Key));
        }
    }

    private void OnRuleAdded(object? sender, RuleAddedEventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            var existingRule = Rules.FirstOrDefault(rule =>
                string.Equals(rule.Destination, e.Destination, StringComparison.OrdinalIgnoreCase));

            if (existingRule is not null)
            {
                var existingExtensions = existingRule.Extension
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (!existingExtensions.Any(extension =>
                        string.Equals(extension, e.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    existingRule.Extension = $"{existingRule.Extension}, {e.Extension}";
                }
            }
            else
            {
                Rules.Add(new RuleEntry(e.Extension, e.Destination));
            }

            StatusMessage = $"Nueva regla creada para {e.Extension}.";
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
