using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Cache;

public interface IWordCacheService
{
    /// <summary>
    /// Retrieves cached dictionary and translation results for a word and target language.
    /// </summary>
    Task<(DictionaryResult? Dict, TranslationResult? Trans)> GetCachedAsync(string word, string targetLanguage);

    /// <summary>
    /// Saves dictionary and translation results into memory and SQLite persistent storage.
    /// </summary>
    Task SaveAsync(string word, string targetLanguage, DictionaryResult? dict, TranslationResult? trans);

    /// <summary>
    /// Clears all cached word entries.
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Gets the total count of cached words in the SQLite database.
    /// </summary>
    Task<int> GetCachedCountAsync();
}
