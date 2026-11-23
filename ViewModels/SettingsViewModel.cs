using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Cache;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IWordCacheService _cacheService;

    [ObservableProperty] private bool _hotkeyCtrl;
    [ObservableProperty] private bool _hotkeyShift;
    [ObservableProperty] private bool _hotkeyAlt;
    [ObservableProperty] private string _hotkeyKey = "D";
    [ObservableProperty] private string _shortcutDisplayText = "Ctrl + Shift + D";
    [ObservableProperty] private string _shortcutHintText = "Click box or 'Record' button, then press keys";
    [ObservableProperty] private bool _isRecordingShortcut;
    [ObservableProperty] private bool _enableMiddleClickTrigger;
    [ObservableProperty] private bool _isLookupEnabled;

    [ObservableProperty] private string _ocrEngineType = "WindowsOcr";
    [ObservableProperty] private string _ocrLanguageTag = "en-US";
    [ObservableProperty] private bool _enablePreprocessingUpscale;
    [ObservableProperty] private bool _enableAdaptiveBinarization;

    [ObservableProperty] private string _cambridgeApiKey = string.Empty;
    [ObservableProperty] private string _cambridgeBaseUrl = "https://dictionary.cambridge.org/api/v1";
    [ObservableProperty] private string _cambridgeDictionaryId = "british";

    [ObservableProperty] private string _googleTranslateApiKey = string.Empty;
    [ObservableProperty] private string _sourceLanguage = "en";
    [ObservableProperty] private string _targetLanguage = "vi";
    [ObservableProperty] private bool _translateContextSentence;

    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private double _popupOpacity = 0.96;
    [ObservableProperty] private int _autoCloseDelaySeconds = 6;
    [ObservableProperty] private bool _isDefaultPinned;

    [ObservableProperty] private bool _useFixedPosition = true;
    [ObservableProperty] private string _fixedPositionSummary = "Upper-right screen corner (Default)";
    [ObservableProperty] private string _dialogueRegionSummary = "None (Fullscreen/Cursor mode)";
    [ObservableProperty] private int _cachedWordsCount;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<string> AvailableEngines { get; } = new()
    {
        "WindowsOcr",
        "Tesseract"
    };

    public ObservableCollection<LanguageOption> AvailableTargetLanguages { get; } = new()
    {
        new("vi", "Vietnamese (Tiếng Việt)"),
        new("ja", "Japanese (日本語)"),
        new("zh-CN", "Chinese Simplified (简体中文)"),
        new("zh-TW", "Chinese Traditional (繁體中文)"),
        new("ko", "Korean (한국어)"),
        new("es", "Spanish (Español)"),
        new("fr", "French (Français)"),
        new("de", "German (Deutsch)"),
        new("ru", "Russian (Русский)"),
        new("en", "English")
    };

    public event Action? RequestClose;
    public event Action? RequestSelectRegion;

    public SettingsViewModel(ISettingsService settingsService, IWordCacheService cacheService)
    {
        _settingsService = settingsService;
        _cacheService = cacheService;
        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        var s = _settingsService.CurrentSettings;
        HotkeyCtrl = s.HotkeyCtrl;
        HotkeyShift = s.HotkeyShift;
        HotkeyAlt = s.HotkeyAlt;
        HotkeyKey = s.HotkeyKey;
        ShortcutDisplayText = s.FormatShortcut();
        ShortcutHintText = "Click box or 'Record' button, then press keys";
        IsRecordingShortcut = false;

        EnableMiddleClickTrigger = s.EnableMiddleClickTrigger;
        IsLookupEnabled = s.IsLookupEnabled;

        OcrEngineType = s.OcrEngineType;
        OcrLanguageTag = s.OcrLanguageTag;
        EnablePreprocessingUpscale = s.EnablePreprocessingUpscale;
        EnableAdaptiveBinarization = s.EnableAdaptiveBinarization;

        CambridgeApiKey = s.CambridgeApiKey;
        CambridgeBaseUrl = s.CambridgeBaseUrl;
        CambridgeDictionaryId = s.CambridgeDictionaryId;

        GoogleTranslateApiKey = s.GoogleTranslateApiKey;
        SourceLanguage = s.SourceLanguage;
        TargetLanguage = s.TargetLanguage;
        TranslateContextSentence = s.TranslateContextSentence;

        Theme = s.Theme;
        PopupOpacity = s.PopupOpacity;
        AutoCloseDelaySeconds = s.AutoCloseDelaySeconds;
        IsDefaultPinned = s.IsDefaultPinned;

        UseFixedPosition = s.UseFixedPosition;
        UpdateFixedPositionSummary(s);

        UpdateRegionSummary(s.DialogueRegion);
        RefreshCachedCount();
    }

    private void UpdateFixedPositionSummary(AppSettings s)
    {
        if (s.FixedPositionLeft < 0 || s.FixedPositionTop < 0)
        {
            FixedPositionSummary = "Upper-right screen corner (Default)";
        }
        else
        {
            FixedPositionSummary = $"X: {(int)s.FixedPositionLeft}, Y: {(int)s.FixedPositionTop} (Custom Dragged)";
        }
    }

    private void UpdateRegionSummary(ScreenRegion? region)
    {
        if (region == null || region.IsEmpty)
        {
            DialogueRegionSummary = "None (Dynamic cursor box mode)";
        }
        else
        {
            DialogueRegionSummary = $"X: {region.X}, Y: {region.Y}, W: {region.Width}, H: {region.Height}";
        }
    }

    [RelayCommand]
    public void ToggleShortcutRecording()
    {
        if (IsRecordingShortcut)
        {
            CancelShortcutRecording();
        }
        else
        {
            StartShortcutRecording();
        }
    }

    public void StartShortcutRecording()
    {
        IsRecordingShortcut = true;
        ShortcutDisplayText = "Press keys on keyboard...";
        ShortcutHintText = "Press any key (e.g. Ctrl+Shift+D, Alt+Q, Ctrl+Space). Esc to cancel.";
        StatusMessage = "Recording shortcut... Press your key combination.";
    }

    public void UpdatePartialShortcut(string partialText)
    {
        ShortcutDisplayText = partialText;
    }

    public void ApplyRecordedShortcut(bool ctrl, bool shift, bool alt, string key)
    {
        HotkeyCtrl = ctrl;
        HotkeyShift = shift;
        HotkeyAlt = alt;
        HotkeyKey = key;

        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (!string.IsNullOrWhiteSpace(key)) parts.Add(key);

        ShortcutDisplayText = parts.Count > 0 ? string.Join(" + ", parts) : "None";
        ShortcutHintText = "Shortcut set! Click 'Save Settings' to apply.";
        StatusMessage = $"Shortcut captured: {ShortcutDisplayText}. Click Save Settings to activate.";
        IsRecordingShortcut = false;
    }

    public void CancelShortcutRecording()
    {
        IsRecordingShortcut = false;
        var s = _settingsService.CurrentSettings;
        ShortcutDisplayText = s.FormatShortcut();
        ShortcutHintText = "Click box or 'Record' button, then press keys";
        StatusMessage = "Shortcut recording cancelled.";
    }

    public async void RefreshCachedCount()
    {
        CachedWordsCount = await _cacheService.GetCachedCountAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.CurrentSettings;
        s.HotkeyCtrl = HotkeyCtrl;
        s.HotkeyShift = HotkeyShift;
        s.HotkeyAlt = HotkeyAlt;
        s.HotkeyKey = HotkeyKey;
        s.EnableMiddleClickTrigger = EnableMiddleClickTrigger;
        s.IsLookupEnabled = IsLookupEnabled;

        s.OcrEngineType = OcrEngineType;
        s.OcrLanguageTag = OcrLanguageTag;
        s.EnablePreprocessingUpscale = EnablePreprocessingUpscale;
        s.EnableAdaptiveBinarization = EnableAdaptiveBinarization;

        s.CambridgeApiKey = CambridgeApiKey.Trim();
        s.CambridgeBaseUrl = CambridgeBaseUrl.Trim();
        s.CambridgeDictionaryId = CambridgeDictionaryId.Trim();

        s.GoogleTranslateApiKey = GoogleTranslateApiKey.Trim();
        s.SourceLanguage = SourceLanguage.Trim();
        s.TargetLanguage = TargetLanguage.Trim();
        s.TranslateContextSentence = TranslateContextSentence;

        s.Theme = Theme;
        s.PopupOpacity = PopupOpacity;
        s.AutoCloseDelaySeconds = AutoCloseDelaySeconds;
        s.IsDefaultPinned = IsDefaultPinned;

        s.UseFixedPosition = UseFixedPosition;

        await _settingsService.SaveSettingsAsync(s);
        StatusMessage = "Settings saved successfully!";
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task ResetPopupPositionAsync()
    {
        var s = _settingsService.CurrentSettings;
        s.FixedPositionLeft = -1;
        s.FixedPositionTop = -1;
        s.PopupWidth = 400;
        s.PopupHeight = 380;
        await _settingsService.SaveSettingsAsync(s);
        UpdateFixedPositionSummary(s);
        StatusMessage = "Popup position & size reset to default.";
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await _cacheService.ClearCacheAsync();
        CachedWordsCount = 0;
        StatusMessage = "Local cache cleared.";
    }

    [RelayCommand]
    private void SelectRegion()
    {
        RequestSelectRegion?.Invoke();
    }

    [RelayCommand]
    private async Task ClearRegionAsync()
    {
        var s = _settingsService.CurrentSettings;
        s.DialogueRegion = null;
        await _settingsService.SaveSettingsAsync(s);
        UpdateRegionSummary(null);
        StatusMessage = "Dialogue region cleared.";
    }
}

public record LanguageOption(string Code, string Name);
