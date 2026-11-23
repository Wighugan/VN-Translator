using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;

namespace VisualNovelDetect.Services.Audio;

public class AudioPlayerService : IAudioPlayerService
{
    private readonly HttpClient _httpClient;
    private MediaPlayer? _mediaPlayer;

    public AudioPlayerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task PlayAudioAsync(string audioUrl)
    {
        if (string.IsNullOrWhiteSpace(audioUrl)) return;

        try
        {
            // If it's a web URL, check if valid
            if (audioUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                audioUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Play directly or via temporary local file
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mediaPlayer?.Close();
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(audioUrl));
                    _mediaPlayer.Play();
                });
            }
            else if (File.Exists(audioUrl))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mediaPlayer?.Close();
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(audioUrl));
                    _mediaPlayer.Play();
                });
            }
        }
        catch
        {
            // Ignore playback failure
        }
    }
}
