using System.Collections.Concurrent;
using System.IO;
using ZenLoad.Models;

namespace ZenLoad.Services;

public sealed class RuleAddedEventArgs(string extension, string destination) : EventArgs
{
    public string Extension { get; } = extension;
    public string Destination { get; } = destination;
}

public sealed class ActivityEventArgs(string fileName, ActivityStatus status, string details) : EventArgs
{
    public string FileName { get; } = fileName;
    public ActivityStatus Status { get; } = status;
    public string Details { get; } = details;
}

/// <summary>Singleton service that monitors Downloads and organizes files.</summary>
public sealed class FolderMonitorService : IDisposable
{
    private static readonly Lazy<FolderMonitorService> LazyInstance = new(() => new FolderMonitorService());
    private static readonly string[] TemporaryDownloadSuffixes =
    {
        ".crdownload", // Chrome, Edge and other Chromium browsers
        ".part",       // Firefox and download managers
        ".tmp"
    };

    private readonly ConcurrentDictionary<string, byte> _filesBeingProcessed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignoredExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _promptGate = new(1, 1);
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly object _sync = new();

    private FileSystemWatcher? _watcher;
    private AppConfig? _config;
    private Func<string, Task<bool>>? _promptForNewExtension;
    private string _downloadsPath = string.Empty;
    private bool _paused;
    private bool _disposed;

    private FolderMonitorService()
    {
    }

    public static FolderMonitorService Instance => LazyInstance.Value;
    public bool IsPaused => Volatile.Read(ref _paused);

    public event EventHandler<RuleAddedEventArgs>? RuleAdded;
    public event EventHandler<ActivityEventArgs>? ActivityRecorded;
    public event EventHandler? StateChanged;

    public void Start(AppConfig config, Func<string, Task<bool>> promptForNewExtension)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stop();
        _config = config;
        _promptForNewExtension = promptForNewExtension;
        _downloadsPath = AppConfig.GetDownloadsPath();
        Directory.CreateDirectory(_downloadsPath);

        _watcher = new FileSystemWatcher(_downloadsPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            Filter = "*",
            IncludeSubdirectories = false
        };
        _watcher.Created += OnFileCreated;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;

        // Process files that were already present before ZenLoad started.
        _ = ScanExistingFilesAsync();
    }

    public void UpdateRules(AppConfig config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _config = config;
        }
    }

    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsPaused == paused)
        {
            return;
        }

        Volatile.Write(ref _paused, paused);
        RecordActivity(string.Empty, ActivityStatus.Info, paused
            ? "Organización pausada."
            : "Organización reanudada.");
        StateChanged?.Invoke(this, EventArgs.Empty);

        if (!paused)
        {
            _ = ScanExistingFilesAsync();
        }
    }

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileCreated;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e) => QueueFile(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e) => QueueFile(e.FullPath);

    private void QueueFile(string filePath)
    {
        if (IsPaused
            || Directory.Exists(filePath)
            || IsTemporaryDownload(filePath)
            || !_filesBeingProcessed.TryAdd(filePath, 0))
        {
            return;
        }

        _ = ProcessFileAsync(filePath);
    }

    private async Task ScanExistingFilesAsync()
    {
        if (IsPaused)
        {
            return;
        }

        await _scanGate.WaitAsync();
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(_downloadsPath, "*", SearchOption.TopDirectoryOnly))
            {
                QueueFile(filePath);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordActivity(string.Empty, ActivityStatus.Error, $"No se pudo leer Descargas: {ex.Message}");
        }
        catch (IOException ex)
        {
            RecordActivity(string.Empty, ActivityStatus.Error, $"No se pudo escanear Descargas: {ex.Message}");
        }
        finally
        {
            _scanGate.Release();
        }
    }

    private async Task ProcessFileAsync(string filePath)
    {
        try
        {
            if (IsPaused || !await WaitUntilFileIsReadyAsync(filePath))
            {
                return;
            }

            var extension = GetCompoundExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                RecordActivity(filePath, ActivityStatus.Ignored, "El archivo no tiene una extensión reconocida.");
                return;
            }

            var rule = GetRule(extension);
            string? destination;

            if (!rule.Found)
            {
                destination = await AskForNewExtensionAsync(extension);
            }
            else if (!rule.Enabled)
            {
                RecordActivity(filePath, ActivityStatus.Ignored, $"La regla para {extension} está desactivada.");
                return;
            }
            else
            {
                destination = rule.Destination;
            }

            if (string.IsNullOrWhiteSpace(destination) || IsDownloadsRoot(destination))
            {
                RecordActivity(filePath, ActivityStatus.Ignored, "La regla deja el archivo en Descargas.");
                return;
            }

            try
            {
                var moveResult = MoveFile(filePath, destination);
                var folderNote = moveResult.FolderWasCreated ? " Carpeta creada automáticamente." : string.Empty;
                RecordActivity(filePath, ActivityStatus.Moved, $"Movido a {moveResult.TargetPath}.{folderNote}");
            }
            catch (UnauthorizedAccessException ex)
            {
                RecordActivity(filePath, ActivityStatus.Error, $"Sin permisos para mover el archivo: {ex.Message}");
            }
            catch (IOException ex)
            {
                RecordActivity(filePath, ActivityStatus.Error, $"No se pudo mover el archivo: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                RecordActivity(filePath, ActivityStatus.Error, $"La ruta de destino no es válida: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                RecordActivity(filePath, ActivityStatus.Error, $"La ruta de destino no es compatible: {ex.Message}");
            }
        }
        finally
        {
            _filesBeingProcessed.TryRemove(filePath, out _);
        }
    }

    private async Task<string?> AskForNewExtensionAsync(string extension)
    {
        await _promptGate.WaitAsync();
        try
        {
            var existingRule = GetRule(extension);
            if (existingRule.Found)
            {
                return existingRule.Enabled ? existingRule.Destination : null;
            }

            if (_ignoredExtensions.Contains(extension) || _promptForNewExtension is null || _config is null)
            {
                RecordActivity(string.Empty, ActivityStatus.Ignored, $"Extensión desconocida ignorada: {extension}.");
                return null;
            }

            var accepted = await _promptForNewExtension(extension);
            if (!accepted)
            {
                _ignoredExtensions.Add(extension);
                RecordActivity(string.Empty, ActivityStatus.Ignored, $"El usuario rechazó la extensión {extension}.");
                return null;
            }

            var destination = Path.Combine(_downloadsPath, extension);
            Directory.CreateDirectory(destination);
            _config.ExtensionRules[extension] = destination;
            _config.DisabledExtensions.Remove(extension);
            _config.Save();
            RuleAdded?.Invoke(this, new RuleAddedEventArgs(extension, destination));
            return destination;
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordActivity(string.Empty, ActivityStatus.Error, $"No se pudo crear la carpeta para {extension}: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            RecordActivity(string.Empty, ActivityStatus.Error, $"No se pudo crear la carpeta para {extension}: {ex.Message}");
            return null;
        }
        finally
        {
            _promptGate.Release();
        }
    }

    private RuleMatch GetRule(string extension)
    {
        lock (_sync)
        {
            if (_config?.ExtensionRules.TryGetValue(extension, out var destination) != true)
            {
                return new RuleMatch(false, null, false);
            }

            var disabled = _config.DisabledExtensions.Contains(extension);
            return new RuleMatch(true, destination, !disabled);
        }
    }

    private MoveResult MoveFile(string filePath, string configuredDestination)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("El archivo ya no existe.", filePath);
        }

        var destination = Path.IsPathRooted(configuredDestination)
            ? configuredDestination
            : Path.Combine(_downloadsPath, configuredDestination);

        if (IsDownloadsRoot(destination))
        {
            throw new IOException("La carpeta de destino es la misma que Descargas.");
        }

        var folderWasCreated = !Directory.Exists(destination);
        Directory.CreateDirectory(destination);
        var targetPath = GetAvailableTargetPath(destination, Path.GetFileName(filePath));
        File.Move(filePath, targetPath);
        return new MoveResult(targetPath, folderWasCreated);
    }

    private static string GetAvailableTargetPath(string directory, string fileName)
    {
        var target = Path.Combine(directory, fileName);
        if (!File.Exists(target))
        {
            return target;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        do
        {
            target = Path.Combine(directory, $"{baseName} ({counter++}){extension}");
        } while (File.Exists(target));

        return target;
    }

    private async Task<bool> WaitUntilFileIsReadyAsync(string filePath)
    {
        long previousLength = -1;
        var stableChecks = 0;

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var length = stream.Length;
                stableChecks = length == previousLength ? stableChecks + 1 : 0;
                previousLength = length;

                if (stableChecks >= 2)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                stableChecks = 0;
            }
            catch (UnauthorizedAccessException)
            {
                stableChecks = 0;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private bool IsDownloadsRoot(string destination)
    {
        return string.Equals(
            Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(_downloadsPath).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCompoundExtension(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        return fileName.EndsWith(".tar.gz", StringComparison.Ordinal)
            ? ".tar.gz"
            : AppConfig.NormalizeExtension(Path.GetExtension(fileName));
    }

    private static bool IsTemporaryDownload(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return TemporaryDownloadSuffixes.Any(suffix =>
            fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private void RecordActivity(string filePath, ActivityStatus status, string details)
    {
        ActivityRecorded?.Invoke(
            this,
            new ActivityEventArgs(Path.GetFileName(filePath), status, details));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _promptGate.Dispose();
        _scanGate.Dispose();
        _disposed = true;
    }

    private readonly record struct RuleMatch(bool Found, string? Destination, bool Enabled);
    private readonly record struct MoveResult(string TargetPath, bool FolderWasCreated);
}
