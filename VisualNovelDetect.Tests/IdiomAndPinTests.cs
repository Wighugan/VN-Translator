using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Audio;
using VisualNovelDetect.Services.Dictionary;
using VisualNovelDetect.Services.Settings;
using VisualNovelDetect.ViewModels;
using Xunit;

namespace VisualNovelDetect.Tests;

public class FakeAudioPlayer : IAudioPlayerService
{
    public Task PlayAudioAsync(string url) => Task.CompletedTask;
    public void Stop() { }
}

public class FakeSettingsService : ISettingsService
{
    public AppSettings CurrentSettings { get; set; } = new();
    public event Action<AppSettings>? SettingsChanged;
    public Task LoadSettingsAsync() => Task.CompletedTask;
    public Task SaveSettingsAsync(AppSettings settings)
    {
        CurrentSettings = settings;
        SettingsChanged?.Invoke(CurrentSettings);
        return Task.CompletedTask;
    }
}

public class IdiomAndPinTests
{
    [Theory]
    [InlineData("He finally kicked the bucket last night.", "bucket", "kicked the bucket", "qua đời, chết, ngỏm")]
    [InlineData("I think he kicked the bucket.", "kicked", "kicked the bucket", "qua đời, chết, ngỏm")]
    [InlineData("Don't worry, the exam was a piece of cake!", "cake", "piece of cake", "dễ như ăn cháo, chuyện nhỏ")]
    [InlineData("She always took him for granted in that relationship.", "granted", "took him for granted", "coi là điều hiển nhiên, không biết trân trọng")]
    [InlineData("We should just bite the bullet and do it.", "bullet", "bite the bullet", "cắn răng chịu đựng, ngậm đắng nuốt cay")]
    [InlineData("Let's call it a day, everyone.", "day", "call it a day", "nghỉ tay, kết thúc công việc hôm nay")]
    [InlineData("Break a leg out there tonight!", "leg", "break a leg", "chúc may mắn")]
    [InlineData("We must protect the artifact at all costs.", "costs", "at all costs", "bằng mọi giá, bất chấp tất cả")]
    [InlineData("I finally made up my mind to leave.", "mind", "made up my mind", "quyết định, hạ quyết tâm")]
    [InlineData("She was on cloud nine after receiving the letter.", "cloud", "on cloud nine", "trên chín tầng mây, cực kỳ hạnh phúc")]
    [InlineData("Don't you dare spill the beans!", "beans", "spill the beans", "tiết lộ bí mật, làm lộ chuyện")]
    public void IdiomDetector_ShouldDetectIdiomsInSentence(string sentence, string targetWord, string expectedPhrase, string expectedViSnippet)
    {
        var result = IdiomDetector.DetectIdiom(sentence, targetWord);

        Assert.NotNull(result);
        Assert.Contains(expectedPhrase.ToLowerInvariant(), result.Phrase.ToLowerInvariant());
        Assert.False(string.IsNullOrWhiteSpace(result.Meaning));
        Assert.Contains(expectedViSnippet.ToLowerInvariant(), result.Translation.ToLowerInvariant());
    }

    [Theory]
    [InlineData("He filled the wooden bucket with cold water.", "bucket")]
    [InlineData("She baked a delicious chocolate cake for the party.", "cake")]
    [InlineData("He injured his left leg during football practice.", "leg")]
    [InlineData("The dark cloud moved slowly across the sky.", "cloud")]
    [InlineData("He planted green beans in the garden.", "beans")]
    public void IdiomDetector_ShouldNotTriggerOnLiteralNonIdiomSentences(string sentence, string targetWord)
    {
        var result = IdiomDetector.DetectIdiom(sentence, targetWord);

        Assert.Null(result);
    }

    [Fact]
    public void PopupViewModel_PinStatusProperties_ReflectIsPinnedCorrectly()
    {
        var fakeAudio = new FakeAudioPlayer();
        var fakeSettings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings { IsDefaultPinned = false }
        };

        var vm = new PopupViewModel(fakeAudio, fakeSettings);

        Assert.False(vm.IsPinned);
        Assert.Equal("📌 Pin", vm.PinButtonText);
        Assert.Equal("#1E293B", vm.PinButtonBackground);

        // Toggle pin
        vm.TogglePinCommand.Execute(null);

        Assert.True(vm.IsPinned);
        Assert.Equal("📌 Pinned", vm.PinButtonText);
        Assert.Equal("#0284C7", vm.PinButtonBackground);
        Assert.True(fakeSettings.CurrentSettings.IsDefaultPinned);
    }

    [Fact]
    public void PopupViewModel_PrepareForLookup_RespectsDefaultPinnedSetting()
    {
        var fakeAudio = new FakeAudioPlayer();
        var fakeSettings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings { IsDefaultPinned = true }
        };

        var vm = new PopupViewModel(fakeAudio, fakeSettings);

        var wordInfo = new DetectedWordInfo
        {
            CleanWord = "adventure",
            SurroundingSentence = "An unexpected adventure awaits."
        };

        vm.PrepareForLookup(wordInfo);

        Assert.True(vm.IsPinned);
        Assert.Equal("📌 Pinned", vm.PinButtonText);
    }

    [Fact]
    public void PopupViewModel_PopulateResults_AttachesDetectedIdiom()
    {
        var fakeAudio = new FakeAudioPlayer();
        var fakeSettings = new FakeSettingsService
        {
            CurrentSettings = new AppSettings()
        };

        var vm = new PopupViewModel(fakeAudio, fakeSettings);

        var wordInfo = new DetectedWordInfo { CleanWord = "bucket" };
        var dictResult = new DictionaryResult
        {
            Word = "bucket",
            DetectedIdiom = new DetectedIdiomInfo
            {
                Phrase = "kicked the bucket",
                Meaning = "To die; pass away.",
                Translation = "Qua đời, chết"
            }
        };

        vm.PopulateResults(wordInfo, dictResult, null);

        Assert.True(vm.HasDetectedIdiom);
        Assert.Equal("kicked the bucket", vm.DetectedIdiomPhrase);
        Assert.Equal("To die; pass away.", vm.DetectedIdiomMeaning);
        Assert.Equal("Qua đời, chết", vm.DetectedIdiomTranslation);
    }
}
