using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Cache;
using Xunit;

namespace VisualNovelDetect.Tests;

public class CacheServiceTests
{
    [Fact]
    public async Task SqliteWordCacheService_ShouldSaveAndRetrieveAsync()
    {
        var cacheService = new SqliteWordCacheService();
        await cacheService.ClearCacheAsync();

        string testWord = "ephemeral";
        string targetLang = "vi";

        var dict = new DictionaryResult
        {
            Word = testWord,
            Headword = testWord,
            PartOfSpeech = "adjective",
            PhoneticIpa = "/ɪˈfem.ər.əl/",
            Definitions = new List<string> { "lasting for only a short time" },
            ExampleSentences = new List<string> { "Fame in the visual novel world is ephemeral." }
        };

        var trans = new TranslationResult
        {
            OriginalWord = testWord,
            TranslatedWord = "phù du / chóng tàn",
            TargetLanguage = targetLang
        };

        // Save
        await cacheService.SaveAsync(testWord, targetLang, dict, trans);

        // Retrieve
        var (cachedDict, cachedTrans) = await cacheService.GetCachedAsync(testWord, targetLang);

        Assert.NotNull(cachedDict);
        Assert.Equal(testWord, cachedDict.Word);
        Assert.Equal("adjective", cachedDict.PartOfSpeech);
        Assert.Single(cachedDict.Definitions);

        Assert.NotNull(cachedTrans);
        Assert.Equal("phù du / chóng tàn", cachedTrans.TranslatedWord);

        int count = await cacheService.GetCachedCountAsync();
        Assert.True(count >= 1);
    }
}
