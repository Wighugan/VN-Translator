using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Dictionary;

public interface IDictionaryService
{
    /// <summary>
    /// Looks up dictionary definition, part of speech, IPA phonetic transcription, and audio for a word.
    /// Automatically applies lemmatization if inflected forms (plurals, past tense, etc.) are looked up.
    /// </summary>
    Task<DictionaryResult?> GetDefinitionAsync(string word, CancellationToken ct = default);
}
