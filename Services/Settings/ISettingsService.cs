using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Settings;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    event Action<AppSettings>? SettingsChanged;
    Task LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
