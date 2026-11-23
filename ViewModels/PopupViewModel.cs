using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using VisualNovelDetect.Services.Audio;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.ViewModels;

public partial class PopupViewModel : ObservableObject
{
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ISettingsService _settingsService;
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;

    [ObservableProperty]
    private string _word = string.Empty;

    [ObservableProperty]
    private string _headword = string.Empty;

    [ObservableProperty]
    private string _partOfSpeech = string.Empty;

    [ObservableProperty]
    private string _phoneticIpa = string.Empty;

    [ObservableProperty]
    private string? _audioUrl;

    [ObservableProperty]
    private string _translatedWord = string.Empty;

    [ObservableProperty]
    private string? _sentenceContextOriginal;

    [ObservableProperty]
    private string? _sentenceContextTranslated;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    // Idiom / Phrasal Verb Detection
    [ObservableProperty]
    private bool _hasDetectedIdiom;

    [ObservableProperty]
    private string _detectedIdiomPhrase = string.Empty;

    [ObservableProperty]
    private string _detectedIdiomMeaning = string.Empty;

    [ObservableProperty]
    private string _detectedIdiomTranslation = string.Empty;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private double _windowLeft;

    [ObservableProperty]
    private double _windowTop;

    [ObservableProperty]
    private double _popupWidth = 420;

    [ObservableProperty]
    private double _popupHeight = 420;

    [ObservableProperty]
    private double _popupOpacity = 0.96;

    [ObservableProperty]
    private string _targetLanguageDisplay = "VI";

    [ObservableProperty]
    private bool _isMoving;

    public ObservableCollection<string> Definitions { get; } = new();
    public ObservableCollection<string> ExampleSentences { get; } = new();
    public ObservableCollection<DictionarySense> Senses { get; } = new();

    public bool HasDefinitions => Definitions.Count > 0 || Senses.Count > 0;
    public bool HasSenses => Senses.Count > 0;
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioUrl);
    public bool HasTranslation => !string.IsNullOrWhiteSpace(TranslatedWord);
    public bool HasSentenceTranslation => !string.IsNullOrWhiteSpace(SentenceContextTranslated);
    public bool HasPartOfSpeech => !string.IsNullOrWhiteSpace(PartOfSpeech);
    public bool HasPhoneticIpa => !string.IsNullOrWhiteSpace(PhoneticIpa);

    // Visual helper properties for Pin button
    public string PinButtonText => IsPinned ? "📌 Pinned" : "📌 Pin";
    public string PinButtonBackground => IsPinned ? "#0284C7" : "#1E293B";
    public string PinButtonBorder => IsPinned ? "#38BDF8" : "#334155";
    public string PinButtonForeground => IsPinned ? "#FFFFFF" : "#94A3B8";

    public event Action? RequestPositionUpdate;

    public PopupViewModel(IAudioPlayerService audioPlayer, ISettingsService settingsService)
    {
        _audioPlayer = audioPlayer;
        _settingsService = settingsService;
        var s = _settingsService.CurrentSettings;
        _popupOpacity = s.PopupOpacity;
        _isPinned = s.IsDefaultPinned;
        _popupWidth = s.PopupWidth > 0 ? s.PopupWidth : 420;
        _popupHeight = s.PopupHeight > 0 ? s.PopupHeight : 420;

        _settingsService.SettingsChanged += OnSettingsChanged;

        InitAutoCloseTimer();
    }

    private void OnSettingsChanged(AppSettings s)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsPinned = s.IsDefaultPinned;
            PopupOpacity = s.PopupOpacity;
            if (IsPinned)
            {
                _autoCloseTimer?.Stop();
            }
        });
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(PinButtonText));
        OnPropertyChanged(nameof(PinButtonBackground));
        OnPropertyChanged(nameof(PinButtonBorder));
        OnPropertyChanged(nameof(PinButtonForeground));
    }

    private void InitAutoCloseTimer()
    {
        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer();
        _autoCloseTimer.Tick += (s, e) =>
        {
            if (!IsPinned && IsVisible && !IsMoving)
            {
                Hide();
            }
        };
    }

    public void PrepareForLookup(DetectedWordInfo wordInfo)
    {
        var s = _settingsService.CurrentSettings;

        // If user configured default pin, enforce pinned state
        if (s.IsDefaultPinned)
        {
            IsPinned = true;
        }

        ResetAutoCloseTimer();

        Word = wordInfo.CleanWord;
        Headword = wordInfo.CleanWord;
        PartOfSpeech = string.Empty;
        PhoneticIpa = string.Empty;
        AudioUrl = null;
        TranslatedWord = string.Empty;
        SentenceContextOriginal = wordInfo.SurroundingSentence;
        SentenceContextTranslated = null;
        ErrorMessage = string.Empty;
        HasError = false;
        IsLoading = true;

        HasDetectedIdiom = false;
        DetectedIdiomPhrase = string.Empty;
        DetectedIdiomMeaning = string.Empty;
        DetectedIdiomTranslation = string.Empty;

        Definitions.Clear();
        ExampleSentences.Clear();
        Senses.Clear();

        TargetLanguageDisplay = s.TargetLanguage.ToUpperInvariant();
        PopupOpacity = s.PopupOpacity;
        if (s.PopupWidth > 0) PopupWidth = s.PopupWidth;
        if (s.PopupHeight > 0) PopupHeight = s.PopupHeight;

        // Position window: either saved fixed position (comfortable, never covers dialogue) or near word
        CalculateAndSetPosition(wordInfo);

        IsVisible = true;
        OnPropertyChanged(nameof(HasDefinitions));
        OnPropertyChanged(nameof(HasSenses));
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasTranslation));
        OnPropertyChanged(nameof(HasSentenceTranslation));
        OnPropertyChanged(nameof(HasPartOfSpeech));
        OnPropertyChanged(nameof(HasPhoneticIpa));
    }

    public void PopulateResults(DetectedWordInfo wordInfo, DictionaryResult? dict, TranslationResult? trans)
    {
        IsLoading = false;

        if (dict != null)
        {
            if (!string.IsNullOrWhiteSpace(dict.Headword))
                Headword = dict.Headword;
            PartOfSpeech = dict.PartOfSpeech;
            PhoneticIpa = dict.PhoneticIpa;
            AudioUrl = dict.AudioUrl;

            Definitions.Clear();
            foreach (var d in dict.Definitions)
                Definitions.Add(d);

            ExampleSentences.Clear();
            foreach (var ex in dict.ExampleSentences)
                ExampleSentences.Add(ex);

            Senses.Clear();
            foreach (var sense in dict.Senses)
                Senses.Add(sense);

            if (dict.DetectedIdiom != null && !string.IsNullOrWhiteSpace(dict.DetectedIdiom.Phrase))
            {
                HasDetectedIdiom = true;
                DetectedIdiomPhrase = dict.DetectedIdiom.Phrase;
                DetectedIdiomMeaning = dict.DetectedIdiom.Meaning;
                DetectedIdiomTranslation = dict.DetectedIdiom.Translation;
            }
        }

        if (trans != null)
        {
            TranslatedWord = trans.TranslatedWord;
            SentenceContextTranslated = trans.SentenceContextTranslated;
        }

        if (!HasDefinitions && !HasTranslation && !HasDetectedIdiom)
        {
            HasError = true;
            ErrorMessage = $"No dictionary entry or translation found for \"{Word}\".";
        }

        OnPropertyChanged(nameof(HasDefinitions));
        OnPropertyChanged(nameof(HasSenses));
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(HasTranslation));
        OnPropertyChanged(nameof(HasSentenceTranslation));
        OnPropertyChanged(nameof(HasPartOfSpeech));
        OnPropertyChanged(nameof(HasPhoneticIpa));

        ResetAutoCloseTimer();
    }

    public void ShowError(string message)
    {
        IsLoading = false;
        HasError = true;
        ErrorMessage = message;
        ResetAutoCloseTimer();
    }

    private void CalculateAndSetPosition(DetectedWordInfo wordInfo)
    {
        var settings = _settingsService.CurrentSettings;
        var workArea = DpiHelper.GetMonitorWorkAreaDip(
            (int)wordInfo.PhysicalCursorPosition.X,
            (int)wordInfo.PhysicalCursorPosition.Y,
            out _);

        Point pos;
        if (settings.UseFixedPosition)
        {
            // Fixed position mode: spawn at user's saved/preferred comfortable spot so it never blocks the word!
            pos = DpiHelper.CalculateFixedPopupDipPosition(
                settings.FixedPositionLeft,
                settings.FixedPositionTop,
                PopupWidth,
                PopupHeight,
                workArea);
        }
        else
        {
            // Dynamic mode: place near target word/cursor
            pos = DpiHelper.CalculatePopupDipPosition(
                wordInfo.DipBoundingBox,
                wordInfo.DipCursorPosition,
                PopupWidth,
                PopupHeight,
                workArea);
        }

        WindowLeft = pos.X;
        WindowTop = pos.Y;

        RequestPositionUpdate?.Invoke();
    }

    public void UpdatePosition(double newLeft, double newTop)
    {
        WindowLeft = newLeft;
        WindowTop = newTop;

        // Persist to settings
        var s = _settingsService.CurrentSettings;
        s.FixedPositionLeft = newLeft;
        s.FixedPositionTop = newTop;
        s.UseFixedPosition = true; // Automatically lock to moved position
        _ = _settingsService.SaveSettingsAsync(s);
    }

    public void UpdateSize(double newWidth, double newHeight)
    {
        PopupWidth = newWidth;
        PopupHeight = newHeight;

        var s = _settingsService.CurrentSettings;
        s.PopupWidth = newWidth;
        s.PopupHeight = newHeight;
        _ = _settingsService.SaveSettingsAsync(s);
    }

    public void ResetAutoCloseTimer()
    {
        if (_autoCloseTimer == null) return;
        _autoCloseTimer.Stop();

        // If pinned or delay is 0, do not start timer!
        if (IsPinned || _settingsService.CurrentSettings.AutoCloseDelaySeconds <= 0)
        {
            return;
        }

        _autoCloseTimer.Interval = TimeSpan.FromSeconds(_settingsService.CurrentSettings.AutoCloseDelaySeconds);
        _autoCloseTimer.Start();
    }

    public void PauseAutoCloseTimer()
    {
        _autoCloseTimer?.Stop();
    }

    [RelayCommand]
    private async Task PlayAudioAsync()
    {
        if (!string.IsNullOrWhiteSpace(AudioUrl))
        {
            await _audioPlayer.PlayAudioAsync(AudioUrl);
        }
    }

    [RelayCommand]
    private void CopyWord()
    {
        try
        {
            Clipboard.SetText(Word);
        }
        catch { }
    }

    [RelayCommand]
    private void CopyDefinition()
    {
        try
        {
            var text = $"{Word} [{PartOfSpeech}] {PhoneticIpa}\n" +
                       $"Translation: {TranslatedWord}\n";

            if (HasDetectedIdiom)
            {
                text += $"\nIdiom: {DetectedIdiomPhrase}\nMeaning: {DetectedIdiomMeaning}\nTranslation: {DetectedIdiomTranslation}\n";
            }

            text += string.Join("\n• ", Definitions);
            Clipboard.SetText(text);
        }
        catch { }
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
        if (IsPinned)
        {
            _autoCloseTimer?.Stop();
        }
        else
        {
            ResetAutoCloseTimer();
        }

        // Persist pin state to user settings
        var s = _settingsService.CurrentSettings;
        s.IsDefaultPinned = IsPinned;
        _ = _settingsService.SaveSettingsAsync(s);
    }

    [RelayCommand]
    public void Hide()
    {
        _autoCloseTimer?.Stop();
        IsVisible = false;
    }
}
