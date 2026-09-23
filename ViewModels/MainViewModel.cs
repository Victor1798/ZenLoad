using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    private bool _isPaused;
    private string _routeValidationMessage = string.Empty;

    public MainViewModel(
        AppConfig config,
        FolderMonitorService monitorService,
        IFolderPickerService folderPickerService)
    {
        _config = config;
        _monitorService = monitorService;
        _folderPickerService = folderPickerService;
        _startWithWindows = StartupManager.IsEnabled;
        _isPaused = monitorService.IsPaused;
        Rules = new ObservableCollection<RuleEntry>();
        RecentActivity = new ObservableCollection<ActivityEntry>();
        LoadRules();

        SaveCommand = new RelayCommand(_ => Save());
        ResetCommand = new RelayCommand(_ => Reset());
        AddCommand = new RelayCommand(_ => AddRule());
        RemoveCommand = new RelayCommand(_ => RemoveSelectedRule(), _ => SelectedRule is not null);
        BrowseCommand = new RelayCommand(BrowseFolder);
        TogglePauseCommand = new RelayCommand(_ => _monitorService.SetPaused(!_monitorService.IsPaused));

        _monitorService.RuleAdded += OnRuleAdded;
        _monitorService.ActivityRecorded += OnActivityRecorded;
        _monitorService.StateChanged += OnStateChanged;
    }

    public ObservableCollection<RuleEntry> Rules { get; }
    public ObservableCollection<ActivityEntry> RecentActivity { get; }

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

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (_isPaused == value)
            {
                return;
            }

            _isPaused = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PauseButtonText));
        }
    }

    public string PauseButtonText => IsPaused ? "Reanudar" : "Pausar";

    public string RouteValidationMessage
    {
        get => _routeValidationMessage;
        private set
        {
            if (_routeValidationMessage == value)
            {
                return;
            }

            _routeValidationMessage = value;
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
    public RelayCommand TogglePauseCommand { get; }

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

        var disabledExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in Rules.Where(rule => !rule.IsEnabled))
        {
            foreach (var extensionValue in rule.Extension.Split(
                         new[] { ',', ';', ' ', '\r', '\n', '\t' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                disabledExtensions.Add(AppConfig.NormalizeExtension(extensionValue));
            }
        }

        if (rules.Count == 0)
        {
            StatusMessage = "Debes conservar al menos una extensión válida.";
            return;
        }

        _config.ReplaceRules(rules, disabledExtensions);
        _config.Save();
        _monitorService.UpdateRules(_config);
        LoadRules();
        var activeRuleCount = rules.Count - disabledExtensions.Count;
        StatusMessage = $"Guardado: {activeRuleCount} reglas activas de {rules.Count}.";
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
        foreach (var destinationGroup in _config.ExtensionRules
                     .OrderBy(pair => pair.Key)
                     .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var enabledGroup in destinationGroup.GroupBy(pair =>
                         !_config.DisabledExtensions.Contains(pair.Key)))
            {
                var extensions = string.Join(", ", enabledGroup.Select(pair => pair.Key));
                Rules.Add(new RuleEntry(extensions, destinationGroup.Key, enabledGroup.Key));
            }
        }

        RefreshRouteValidation();
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
                rule.IsEnabled
                && string.Equals(rule.Destination, e.Destination, StringComparison.OrdinalIgnoreCase));

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

    private void OnActivityRecorded(object? sender, ActivityEventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            RecentActivity.Insert(0, new ActivityEntry(DateTime.Now, e.FileName, e.Status, e.Details));
            while (RecentActivity.Count > 100)
            {
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
            }

            if (e.Status == ActivityStatus.Error)
            {
                StatusMessage = e.Details;
            }
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() => IsPaused = _monitorService.IsPaused);
    }

    private void RefreshRouteValidation()
    {
        var missingRoutes = Rules
            .Where(rule => rule.IsEnabled && !string.IsNullOrWhiteSpace(rule.Destination))
            .Select(rule => Path.IsPathRooted(rule.Destination)
                ? rule.Destination
                : Path.Combine(AppConfig.GetDownloadsPath(), rule.Destination))
            .Where(destination => !Directory.Exists(destination))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        RouteValidationMessage = missingRoutes.Length == 0
            ? string.Empty
            : $"Aviso: estas carpetas no existen todavía y se crearán al mover un archivo: {string.Join(", ", missingRoutes)}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
