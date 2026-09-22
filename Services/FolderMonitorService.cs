using System.Collections.Concurrent;
using System.IO;
using ZenLoad.Models;

namespace ZenLoad.Services;

public sealed class RuleAddedEventArgs(string extension, string destination) : EventArgs
{
    public string Extension { get; } = extension;
    public string Destination { get; } = destination;
}

/// <summary>
/// Singleton service responsible for monitoring Downloads and organizing files.
/// </summary>
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
    private readonly object _sync = new();

    private FileSystemWatcher? _watcher;
    private AppConfig? _config;
    private Func<string, Task<bool>>? _promptForNewExtension;
    private string _downloadsPath = string.Empty;
    private bool _disposed;

    private FolderMonitorService()
    {
    }

    public static FolderMonitorService Instance => LazyInstance.Value;

    public bool IsRunning => _watcher?.EnableRaisingEvents == true;

    public event EventHandler<RuleAddedEventArgs>? RuleAdded;

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
    }

    public void UpdateRules(AppConfig config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _config = config;
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

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        QueueFile(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Browsers usually rename the temporary download to its final name
        // instead of raising a new Created event for the completed file.
        QueueFile(e.FullPath);
    }

    private void QueueFile(string filePath)
    {
        if (Directory.Exists(filePath)
            || IsTemporaryDownload(filePath)
            || !_filesBeingProcessed.TryAdd(filePath, 0))
        {
            return;
        }

        _ = ProcessFileAsync(filePath);
    }

    private async Task ProcessFileAsync(string filePath)
    {
        try
        {
            if (!await WaitUntilFileIsReadyAsync(filePath))
            {
                return;
            }

            var extension = GetCompoundExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return;
            }

            var destination = GetRule(extension);
            if (destination is null)
            {
                destination = await AskForNewExtensionAsync(extension);
            }

            if (string.IsNullOrWhiteSpace(destination) || IsDownloadsRoot(destination))
            {
                return;
            }

            MoveFile(filePath, destination);
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
            if (existingRule is not null)
            {
                return existingRule;
            }

            if (_ignoredExtensions.Contains(extension) || _promptForNewExtension is null || _config is null)
            {
                return null;
            }

            var accepted = await _promptForNewExtension(extension);
            if (!accepted)
            {
                _ignoredExtensions.Add(extension);
                return null;
            }

            var destination = Path.Combine(_downloadsPath, extension);
            Directory.CreateDirectory(destination);
            _config.ExtensionRules[extension] = destination;
            _config.Save();
            RuleAdded?.Invoke(this, new RuleAddedEventArgs(extension, destination));
            return destination;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            _promptGate.Release();
        }
    }

    private string? GetRule(string extension)
    {
        lock (_sync)
        {
            return _config?.ExtensionRules.TryGetValue(extension, out var destination) == true
                ? destination
                : null;
        }
    }

    private void MoveFile(string filePath, string configuredDestination)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var destination = Path.IsPathRooted(configuredDestination)
            ? configuredDestination
            : Path.Combine(_downloadsPath, configuredDestination);

        if (IsDownloadsRoot(destination))
        {
            return;
        }

        Directory.CreateDirectory(destination);
        var targetPath = GetAvailableTargetPath(destination, Path.GetFileName(filePath));
        File.Move(filePath, targetPath);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _promptGate.Dispose();
        _disposed = true;
    }
}
