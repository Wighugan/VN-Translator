namespace VisualNovelDetect.Models;

/// <summary>
/// Represents the result of a translation lookup.
/// </summary>
public class TranslationResult
{
    public string OriginalWord { get; set; } = string.Empty;
    public string TranslatedWord { get; set; } = string.Empty;
    public string? SentenceContextOriginal { get; set; }
    public string? SentenceContextTranslated { get; set; }
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "vi";

    public bool HasSentenceTranslation => !string.IsNullOrWhiteSpace(SentenceContextTranslated);
}
