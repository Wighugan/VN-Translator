using System.Drawing;
using System.Windows;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using VisualNovelDetect.Services.Cache;
using VisualNovelDetect.Services.Capture;
using VisualNovelDetect.Services.Dictionary;
using VisualNovelDetect.Services.Ocr;
using VisualNovelDetect.Services.Settings;
using VisualNovelDetect.Services.Translation;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Services.Lookup;

public class LookupCoordinator : ILookupCoordinator
{
    private readonly IScreenCaptureService _captureService;
    private readonly IImagePreprocessor _preprocessor;
    private readonly WindowsMediaOcrService _windowsOcrService;
    private readonly TesseractOcrService _tesseractOcrService;
    private readonly IDictionaryService _dictionaryService;
    private readonly ITranslationService _translationService;
    private readonly IWordCacheService _cacheService;
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _activeCts;
    private readonly object _lock = new();

    public event Action<DetectedWordInfo>? LookupStarted;
    public event Action<DetectedWordInfo, DictionaryResult?, TranslationResult?>? LookupCompleted;
    public event Action<string>? LookupFailed;

    public LookupCoordinator(
        IScreenCaptureService captureService,
        IImagePreprocessor preprocessor,
        WindowsMediaOcrService windowsOcrService,
        TesseractOcrService tesseractOcrService,
        IDictionaryService dictionaryService,
        ITranslationService translationService,
        IWordCacheService cacheService,
        ISettingsService settingsService)
    {
        _captureService = captureService;
        _preprocessor = preprocessor;
        _windowsOcrService = windowsOcrService;
        _tesseractOcrService = tesseractOcrService;
        _dictionaryService = dictionaryService;
        _translationService = translationService;
        _cacheService = cacheService;
        _settingsService = settingsService;
    }

    public async Task TriggerLookupAtCursorAsync()
    {
        CancellationToken ct;
        lock (_lock)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = new CancellationTokenSource();
            ct = _activeCts.Token;
        }

        try
        {
            var settings = _settingsService.CurrentSettings;
            if (!settings.IsLookupEnabled) return;

            Point cursorPhysical = DpiHelper.GetCursorPhysicalPosition();

            // 1. Capture screen region (expanded radius captures full dialogue sentence)
            Bitmap capturedBmp;
            Rect capturePhysicalRect;

            if (settings.DialogueRegion != null && !settings.DialogueRegion.IsEmpty &&
                settings.DialogueRegion.Contains((int)cursorPhysical.X, (int)cursorPhysical.Y))
            {
                capturedBmp = _captureService.CaptureRegion(settings.DialogueRegion, out capturePhysicalRect);
            }
            else
            {
                capturedBmp = _captureService.CaptureCursorRegion(
                    cursorPhysical,
                    settings.CaptureRadiusX,
                    settings.CaptureRadiusY,
                    out capturePhysicalRect);
            }

            // 2. Preprocess captured image
            double scaleFactor;
            Bitmap preprocessedBmp;
            using (capturedBmp)
            {
                ct.ThrowIfCancellationRequested();
                preprocessedBmp = _preprocessor.Preprocess(
                    capturedBmp,
                    settings.EnablePreprocessingUpscale,
                    settings.EnableAdaptiveBinarization,
                    out scaleFactor);
            }

            // 3. OCR Word Detection
            IOcrService ocrService = settings.OcrEngineType.Equals("Tesseract", StringComparison.OrdinalIgnoreCase)
                ? _tesseractOcrService
                : _windowsOcrService;

            DetectedWordInfo? detectedWord;
            using (preprocessedBmp)
            {
                ct.ThrowIfCancellationRequested();
                detectedWord = await ocrService.DetectWordAtCursorAsync(
                    preprocessedBmp,
                    capturePhysicalRect,
                    cursorPhysical,
                    settings.OcrLanguageTag,
                    scaleFactor,
                    ct);
            }

            if (detectedWord == null || string.IsNullOrWhiteSpace(detectedWord.CleanWord))
            {
                LookupFailed?.Invoke("No word detected under cursor.");
                return;
            }

            // Notify UI that a word was detected and lookup has commenced
            LookupStarted?.Invoke(detectedWord);

            // Check for Idiom / Phrasal Verb in surrounding sentence
            var detectedIdiom = IdiomDetector.DetectIdiom(
                detectedWord.SurroundingSentence,
                detectedWord.CleanWord,
                detectedWord.CleanWord);

            Task<string?>? idiomTransTask = null;
            if (detectedIdiom != null && !string.IsNullOrWhiteSpace(detectedIdiom.Phrase))
            {
                idiomTransTask = _translationService.TranslateTextAsync(
                    detectedIdiom.Phrase,
                    settings.TargetLanguage,
                    settings.SourceLanguage,
                    ct);
            }

            // 4. Cache Check
            var (cachedDict, cachedTrans) = await _cacheService.GetCachedAsync(detectedWord.CleanWord, settings.TargetLanguage);
            DictionaryResult? dictResult = cachedDict;
            TranslationResult? transResult = cachedTrans;

            // Invalidate empty/incomplete cached translation
            if (transResult != null && string.IsNullOrWhiteSpace(transResult.TranslatedWord))
            {
                transResult = null;
            }

            // 5. Parallel Lookup for missing parts
            var tasksToWait = new List<Task>();

            Task<DictionaryResult?>? dictTask = null;
            if (dictResult == null)
            {
                dictTask = _dictionaryService.GetDefinitionAsync(detectedWord.CleanWord, ct);
                tasksToWait.Add(dictTask);
            }

            Task<TranslationResult?>? transTask = null;
            if (transResult == null ||
                (settings.TranslateContextSentence &&
                 string.IsNullOrWhiteSpace(transResult.SentenceContextTranslated) &&
                 !string.IsNullOrWhiteSpace(detectedWord.SurroundingSentence)))
            {
                transTask = _translationService.TranslateWordAndContextAsync(
                    detectedWord.CleanWord,
                    detectedWord.SurroundingSentence,
                    settings.TargetLanguage,
                    settings.SourceLanguage,
                    ct);
                tasksToWait.Add(transTask);
            }

            if (idiomTransTask != null)
            {
                tasksToWait.Add(idiomTransTask);
            }

            if (tasksToWait.Count > 0)
            {
                await Task.WhenAll(tasksToWait);
                ct.ThrowIfCancellationRequested();
            }

            if (dictTask != null)
                dictResult = await dictTask;

            if (transTask != null)
                transResult = await transTask;

            if (idiomTransTask != null)
            {
                try
                {
                    string? liveTrans = await idiomTransTask;
                    if (!string.IsNullOrWhiteSpace(liveTrans) &&
                        !liveTrans.Equals(detectedIdiom!.Phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        detectedIdiom.Translation = liveTrans;
                    }
                }
                catch
                {
                    // Fallback to built-in translation
                }
            }

            // Attach detected idiom
            if (detectedIdiom != null)
            {
                if (dictResult == null)
                {
                    dictResult = new DictionaryResult
                    {
                        Word = detectedWord.CleanWord,
                        Headword = detectedIdiom.Phrase,
                        PartOfSpeech = "idiom",
                        Source = "Idiom / Phrase Detection"
                    };
                    dictResult.Definitions.Add(detectedIdiom.Meaning);
                }

                dictResult.DetectedIdiom = detectedIdiom;
            }

            // 6. Save to cache
            if (dictResult != null || transResult != null)
            {
                await _cacheService.SaveAsync(detectedWord.CleanWord, settings.TargetLanguage, dictResult, transResult);
            }

            ct.ThrowIfCancellationRequested();
            LookupCompleted?.Invoke(detectedWord, dictResult, transResult);
        }
        catch (OperationCanceledException)
        {
            // Ignored on new lookup debounce
        }
        catch (Exception ex)
        {
            LookupFailed?.Invoke($"Lookup failed: {ex.Message}");
        }
    }

    public void CancelCurrentLookup()
    {
        lock (_lock)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }
}
