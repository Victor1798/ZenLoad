using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZenLoad.Models;

/// <summary>
/// Persistent application configuration. Keys are normalized extensions and values
/// are absolute destination folders (or folders relative to Downloads).
/// </summary>
public sealed class AppConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public Dictionary<string, string> ExtensionRules { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> DisabledExtensions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public static string ConfigDirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZenLoad");

    [JsonIgnore]
    public static string ConfigFilePath => Path.Combine(ConfigDirectoryPath, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

                if (config is not null)
                {
                    config.NormalizeRules();
                    return config;
                }
            }
        }
        catch (JsonException)
        {
            // A damaged configuration should not prevent the application from starting.
        }
        catch (IOException)
        {
            // The defaults below let the application continue if the file is unavailable.
        }

        return CreateDefault();
    }

    public static AppConfig CreateDefault()
    {
        var downloads = GetDownloadsPath();
        var config = new AppConfig
        {
            ExtensionRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".jpeg"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".png"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".gif"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".webp"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".svg"] = GetKnownFolder(Environment.SpecialFolder.MyPictures, downloads, "Imágenes"),
                [".pdf"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".docx"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".doc"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".xlsx"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".txt"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".pptx"] = GetKnownFolder(Environment.SpecialFolder.MyDocuments, downloads, "Documentos"),
                [".zip"] = Path.Combine(downloads, "Archivos"),
                [".rar"] = Path.Combine(downloads, "Archivos"),
                [".7z"] = Path.Combine(downloads, "Archivos"),
                [".tar.gz"] = Path.Combine(downloads, "Archivos"),
                [".exe"] = Path.Combine(downloads, "Programas"),
                [".msi"] = Path.Combine(downloads, "Programas"),
                [".mp3"] = GetKnownFolder(Environment.SpecialFolder.MyMusic, downloads, "Música"),
                [".mp4"] = GetKnownFolder(Environment.SpecialFolder.MyVideos, downloads, "Videos"),
                [".mkv"] = GetKnownFolder(Environment.SpecialFolder.MyVideos, downloads, "Videos"),
                [".wav"] = GetKnownFolder(Environment.SpecialFolder.MyMusic, downloads, "Música")
            }
        };

        config.NormalizeRules();
        return config;
    }

    public void ReplaceRules(
        IDictionary<string, string> rules,
        IEnumerable<string>? disabledExtensions = null)
    {
        ExtensionRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (extension, destination) in rules)
        {
            var normalizedExtension = NormalizeExtension(extension);
            if (!string.IsNullOrWhiteSpace(normalizedExtension) && !string.IsNullOrWhiteSpace(destination))
            {
                ExtensionRules[normalizedExtension] = destination.Trim();
            }
        }

        DisabledExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in disabledExtensions ?? [])
        {
            var normalizedExtension = NormalizeExtension(extension);
            if (ExtensionRules.ContainsKey(normalizedExtension))
            {
                DisabledExtensions.Add(normalizedExtension);
            }
        }
    }

    /// <summary>Resets known rules to Downloads, which effectively disables moving.</summary>
    public void ResetToDownloads()
    {
        var downloads = GetDownloadsPath();
        var resetRules = ExtensionRules.Keys
            .Concat(CreateDefault().ExtensionRules.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(extension => extension, _ => downloads, StringComparer.OrdinalIgnoreCase);

        ReplaceRules(resetRules);
    }

    public void Save()
    {
        NormalizeRules();
        Directory.CreateDirectory(ConfigDirectoryPath);

        var temporaryPath = ConfigFilePath + ".tmp";
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, ConfigFilePath, overwrite: true);
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim().ToLowerInvariant();
        return value.StartsWith('.') ? value : $".{value}";
    }

    public static string GetDownloadsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads");

    private void NormalizeRules()
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (extension, destination) in ExtensionRules ?? new())
        {
            var normalizedExtension = NormalizeExtension(extension);
            if (!string.IsNullOrWhiteSpace(normalizedExtension) && !string.IsNullOrWhiteSpace(destination))
            {
                normalized[normalizedExtension] = destination.Trim();
            }
        }

        ExtensionRules = normalized;
        DisabledExtensions = new HashSet<string>(
            (DisabledExtensions ?? new())
                .Select(NormalizeExtension)
                .Where(extension => normalized.ContainsKey(extension)),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string GetKnownFolder(
        Environment.SpecialFolder folder,
        string downloads,
        string fallbackFolderName)
    {
        var knownFolder = Environment.GetFolderPath(folder);
        return string.IsNullOrWhiteSpace(knownFolder)
            ? Path.Combine(downloads, fallbackFolderName)
            : knownFolder;
    }
}
