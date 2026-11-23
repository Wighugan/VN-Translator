namespace VisualNovelDetect.Services.Audio;

public interface IAudioPlayerService
{
    Task PlayAudioAsync(string audioUrl);
}
