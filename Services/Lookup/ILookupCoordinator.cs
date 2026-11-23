using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Lookup;

public interface ILookupCoordinator
{
    event Action<DetectedWordInfo>? LookupStarted;
    event Action<DetectedWordInfo, DictionaryResult?, TranslationResult?>? LookupCompleted;
    event Action<string>? LookupFailed;

    Task TriggerLookupAtCursorAsync();
    void CancelCurrentLookup();
}
