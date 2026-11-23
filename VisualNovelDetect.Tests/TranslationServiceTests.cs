using System.Net.Http;
using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Settings;
using VisualNovelDetect.Services.Translation;
using Xunit;

namespace VisualNovelDetect.Tests;

public class TranslationServiceTests
{
    [Fact]
    public async Task GoogleTranslationService_ShouldTranslateWordSuccessfully()
    {
        var settingsService = new FakeSettingsService
        {
            CurrentSettings = new AppSettings
            {
                TargetLanguage = "vi",
                SourceLanguage = "auto"
            }
        };

        using var httpClient = new HttpClient();
        var service = new GoogleTranslationService(httpClient, settingsService);

        var result = await service.TranslateTextAsync("adventure", "vi", "en");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result));
        // Expect "cuộc phiêu lưu" or similar Vietnamese translation
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GoogleTranslationService_ShouldTranslateWordAndContextSuccessfully()
    {
        var settingsService = new FakeSettingsService
        {
            CurrentSettings = new AppSettings
            {
                TargetLanguage = "vi",
                SourceLanguage = "auto",
                TranslateContextSentence = true
            }
        };

        using var httpClient = new HttpClient();
        var service = new GoogleTranslationService(httpClient, settingsService);

        var result = await service.TranslateWordAndContextAsync(
            "sword",
            "He drew his legendary sword from the stone.",
            "vi",
            "en");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedWord));
        Assert.False(string.IsNullOrWhiteSpace(result.SentenceContextTranslated));
    }
}
