namespace VisualNovelDetect.Services.Hotkey;

public interface IGlobalHookService : IDisposable
{
    event Action? TriggerActivated;

    void Start(IntPtr windowHandle);
    void Stop();
    void UpdateHotkey();
}
