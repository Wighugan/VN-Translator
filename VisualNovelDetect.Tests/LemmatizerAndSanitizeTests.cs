using VisualNovelDetect.Services.Dictionary;
using VisualNovelDetect.Services.Ocr;
using Xunit;

namespace VisualNovelDetect.Tests;

public class LemmatizerAndSanitizeTests
{
    [Theory]
    [InlineData("went", "go")]
    [InlineData("children", "child")]
    [InlineData("running", "run")]
    [InlineData("making", "make")]
    [InlineData("loved", "love")]
    [InlineData("cities", "city")]
    [InlineData("boxes", "box")]
    [InlineData("cats", "cat")]
    public void Lemmatizer_ShouldReturnValidBaseForms(string input, string expectedLemma)
    {
        var candidates = WordLemmatizer.GetCandidates(input).ToList();
        Assert.Contains(expectedLemma, candidates);
    }

    [Theory]
    [InlineData("\"adventure\"", "adventure")]
    [InlineData("“destiny”", "destiny")]
    [InlineData("‘memory’", "memory")]
    [InlineData("wait...", "wait")]
    [InlineData("Stop!", "Stop")]
    [InlineData("Really?", "Really")]
    [InlineData("「こんにちは」", "こんにちは")]
    [InlineData("player's", "player")]
    [InlineData("sister-in-law", "sister-in-law")]
    [InlineData("don't", "don't")]
    public void SanitizeWord_ShouldCleanPunctuationAccurately(string raw, string expected)
    {
        string cleaned = WindowsMediaOcrService.SanitizeWord(raw);
        Assert.Equal(expected, cleaned);
    }
}
