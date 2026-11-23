using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualNovelDetect.Services.Cache;
using VisualNovelDetect.Services.Lookup;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILookupCoordinator _lookupCoordinator;
    private readonly IWordCacheService _cacheService;

    [ObservableProperty] private bool _isLookupEnabled;
    [ObservableProperty] private string _tooltipText = "Visual Novel Detect";

    public event Action? RequestOpenSettings;
    public event Action? RequestSelectRegion;
    public event Action? RequestExit;

    public TrayViewModel(
        ISettingsService settingsService,
        ILookupCoordinator lookupCoordinator,
        IWordCacheService cacheService)
    {
        _settingsService = settingsService;
        _lookupCoordinator = lookupCoordinator;
        _cacheService = cacheService;

        _isLookupEnabled = _settingsService.CurrentSettings.IsLookupEnabled;
        UpdateTooltip();
        _settingsService.SettingsChanged += s =>
        {
            IsLookupEnabled = s.IsLookupEnabled;
            UpdateTooltip();
        };
    }

    private void UpdateTooltip()
    {
        var s = _settingsService.CurrentSettings;
        string hotkey = $"{(s.HotkeyCtrl ? "Ctrl+" : "")}{(s.HotkeyShift ? "Shift+" : "")}{(s.HotkeyAlt ? "Alt+" : "")}{s.HotkeyKey}";
        string status = s.IsLookupEnabled ? "Active" : "Disabled";
        TooltipText = $"Visual Novel Detect ({status} - {hotkey})";
    }

    [RelayCommand]
    private async Task ToggleLookupEnabledAsync()
    {
        var s = _settingsService.CurrentSettings;
        s.IsLookupEnabled = !s.IsLookupEnabled;
        await _settingsService.SaveSettingsAsync(s);
        IsLookupEnabled = s.IsLookupEnabled;
        UpdateTooltip();
    }

    [RelayCommand]
    private async Task LookupNowAsync()
    {
        await _lookupCoordinator.TriggerLookupAtCursorAsync();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        RequestOpenSettings?.Invoke();
    }

    [RelayCommand]
    private void SelectDialogueRegion()
    {
        RequestSelectRegion?.Invoke();
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await _cacheService.ClearCacheAsync();
        MessageBox.Show("Word cache cleared successfully.", "Visual Novel Detect", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Exit()
    {
        RequestExit?.Invoke();
    }
}
