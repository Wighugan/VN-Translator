using System.IO;
using System.Text.Json;
using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Settings;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public AppSettings CurrentSettings => _currentSettings;
    public event Action<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualNovelDetect");
        Directory.CreateDirectory(appData);
        _settingsFilePath = Path.Combine(appData, "settings.json");
        _currentSettings = LoadSettingsInternal();
    }

    public Task LoadSettingsAsync()
    {
        _currentSettings = LoadSettingsInternal();
        SettingsChanged?.Invoke(_currentSettings);
        return Task.CompletedTask;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        _currentSettings = settings;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch
        {
            // Ignore write errors
        }

        SettingsChanged?.Invoke(_currentSettings);
    }

    private AppSettings LoadSettingsInternal()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fall back to default
        }

        return new AppSettings();
    }
}
