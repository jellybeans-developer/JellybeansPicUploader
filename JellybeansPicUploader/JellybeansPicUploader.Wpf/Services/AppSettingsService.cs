using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JellybeansPicUploader.Models;

namespace JellybeansPicUploader.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string SettingsFilePath { get; }

    public AppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootDirectory = Path.Combine(appData, "JellybeansPicUploader");
        SettingsFilePath = Path.Combine(rootDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json).ConfigureAwait(false);
    }
}

