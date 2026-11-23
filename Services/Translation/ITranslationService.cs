using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Translation;

public interface ITranslationService
{
    /// <summary>
    /// Translates the target word and optionally the surrounding sentence dialogue context.
    /// </summary>
    Task<TranslationResult?> TranslateWordAndContextAsync(
        string word,
        string? surroundingSentence,
        string targetLanguage,
        string sourceLanguage = "auto",
        CancellationToken ct = default);

    /// <summary>
    /// Translates arbitrary text (e.g. detected idiom phrase).
    /// </summary>
    Task<string?> TranslateTextAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "auto",
        CancellationToken ct = default);
}
