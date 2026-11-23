using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.Services.Translation;

public class GoogleTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    private const string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";

    public GoogleTranslationService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;

        ConfigureDefaultHeaders();
    }

    private void ConfigureDefaultHeaders()
    {
        if (_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Remove("User-Agent");
        }
        _httpClient.DefaultRequestHeaders.Add("User-Agent", ChromeUserAgent);

        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        }
    }

    public async Task<TranslationResult?> TranslateWordAndContextAsync(
        string word,
        string? surroundingSentence,
        string targetLanguage,
        string sourceLanguage = "auto",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;

        var settings = _settingsService.CurrentSettings;
        string tl = string.IsNullOrWhiteSpace(targetLanguage) ? "vi" : targetLanguage.Trim().ToLowerInvariant();
        string sl = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage.Trim().ToLowerInvariant();

        // 1. Translate word
        var wordTask = TranslateTextAsync(word, tl, sl, ct);

        // 2. Translate context sentence in parallel if present
        Task<string?>? sentenceTask = null;
        bool shouldTranslateSentence = settings.TranslateContextSentence && !string.IsNullOrWhiteSpace(surroundingSentence);

        if (shouldTranslateSentence)
        {
            sentenceTask = TranslateTextAsync(surroundingSentence!, tl, sl, ct);
        }

        await Task.WhenAll(wordTask, sentenceTask ?? Task.FromResult<string?>(null));

        string? translatedWord = await wordTask;
        string? translatedSentence = sentenceTask != null ? await sentenceTask : null;

        // If word translation returned empty, fallback to sentence translation or original word
        if (string.IsNullOrWhiteSpace(translatedWord) && string.IsNullOrWhiteSpace(translatedSentence))
        {
            return null;
        }

        return new TranslationResult
        {
            OriginalWord = word,
            TranslatedWord = !string.IsNullOrWhiteSpace(translatedWord) ? translatedWord : word,
            SentenceContextOriginal = surroundingSentence,
            SentenceContextTranslated = translatedSentence,
            SourceLanguage = sl,
            TargetLanguage = tl
        };
    }

    public async Task<string?> TranslateTextAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "auto",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var settings = _settingsService.CurrentSettings;
        string tl = string.IsNullOrWhiteSpace(targetLanguage) ? "vi" : targetLanguage.Trim().ToLowerInvariant();
        string sl = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage.Trim().ToLowerInvariant();

        // Tier 1: Cloud API (if API Key provided)
        if (!string.IsNullOrWhiteSpace(settings.GoogleTranslateApiKey))
        {
            var cloudResult = await TranslateCloudApiAsync(text, tl, sl, settings.GoogleTranslateApiKey, ct);
            if (!string.IsNullOrWhiteSpace(cloudResult))
            {
                return cloudResult;
            }
        }

        // Tier 2: Primary Google Translate Web (dict-chrome-ex client ID)
        var primaryWebResult = await TranslateGoogleWebAsync(text, tl, sl, "dict-chrome-ex", ct);
        if (!string.IsNullOrWhiteSpace(primaryWebResult))
        {
            return primaryWebResult;
        }

        // Tier 3: Secondary Google Translate Web (gtx client ID)
        var secondaryWebResult = await TranslateGoogleWebAsync(text, tl, sl, "gtx", ct);
        if (!string.IsNullOrWhiteSpace(secondaryWebResult))
        {
            return secondaryWebResult;
        }

        // Tier 4: MyMemory API Fallback
        var myMemoryResult = await TranslateMyMemoryAsync(text, tl, sl, ct);
        if (!string.IsNullOrWhiteSpace(myMemoryResult))
        {
            return myMemoryResult;
        }

        return null;
    }

    private async Task<string?> TranslateGoogleWebAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        string clientType,
        CancellationToken ct)
    {
        try
        {
            string encodedText = Uri.EscapeDataString(text);
            string url = $"https://translate.googleapis.com/translate_a/single?client={clientType}&sl={sourceLanguage}&tl={targetLanguage}&dt=t&q={encodedText}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(ChromeUserAgent);
            req.Headers.Referrer = new Uri("https://translate.google.com/");

            using var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            string json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("["))
                return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var segments = root[0];
                if (segments.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var seg in segments.EnumerateArray())
                    {
                        if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0)
                        {
                            string? part = seg[0].GetString();
                            if (!string.IsNullOrEmpty(part))
                            {
                                sb.Append(part);
                            }
                        }
                    }
                    string result = sb.ToString().Trim();
                    return !string.IsNullOrWhiteSpace(result) ? result : null;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TranslateMyMemoryAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        CancellationToken ct)
    {
        try
        {
            string src = (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(sourceLanguage))
                ? "en"
                : sourceLanguage;

            string encoded = Uri.EscapeDataString(text);
            string url = $"https://api.mymemory.translated.net/get?q={encoded}&langpair={src}|{targetLanguage}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(ChromeUserAgent);

            using var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            string json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("responseData", out var respData) &&
                respData.TryGetProperty("translatedText", out var textElem))
            {
                string? translated = textElem.GetString();
                if (!string.IsNullOrWhiteSpace(translated) &&
                    !translated.StartsWith("'", StringComparison.Ordinal)) // MyMemory error message check
                {
                    return System.Net.WebUtility.HtmlDecode(translated.Trim());
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TranslateCloudApiAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        string apiKey,
        CancellationToken ct)
    {
        try
        {
            string url = $"https://translation.googleapis.com/language/translate/v2?key={apiKey}";

            var payload = new
            {
                q = new[] { text },
                target = targetLanguage,
                source = sourceLanguage == "auto" ? null : sourceLanguage,
                format = "text"
            };

            string jsonBody = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var resp = await _httpClient.PostAsync(url, content, ct);

            if (!resp.IsSuccessStatusCode) return null;

            string respJson = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("translations", out var transArray) &&
                transArray.ValueKind == JsonValueKind.Array &&
                transArray.GetArrayLength() > 0)
            {
                var first = transArray[0];
                if (first.TryGetProperty("translatedText", out var textElem))
                {
                    return System.Net.WebUtility.HtmlDecode(textElem.GetString() ?? string.Empty);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
