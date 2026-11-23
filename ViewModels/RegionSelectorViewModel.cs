using CommunityToolkit.Mvvm.ComponentModel;
using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.ViewModels;

public partial class RegionSelectorViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public event Action? SelectionFinished;

    public RegionSelectorViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task SaveRegionAsync(int x, int y, int width, int height)
    {
        if (width > 20 && height > 20)
        {
            var s = _settingsService.CurrentSettings;
            s.DialogueRegion = new ScreenRegion(x, y, width, height);
            await _settingsService.SaveSettingsAsync(s);
        }

        SelectionFinished?.Invoke();
    }

    public void Cancel()
    {
        SelectionFinished?.Invoke();
    }
}
