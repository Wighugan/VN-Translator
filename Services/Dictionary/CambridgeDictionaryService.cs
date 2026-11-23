using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualNovelDetect.Models;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.Services.Dictionary;

public class CambridgeDictionaryService : IDictionaryService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    public CambridgeDictionaryService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;

        // Ensure user-agent is present for public dictionary endpoints
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VisualNovelDetect/1.0 (Windows NT 10.0; Win64; x64)");
        }
    }

    public async Task<DictionaryResult?> GetDefinitionAsync(string word, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;

        string cleanWord = word.Trim();

        // 1. Try exact word / phrase
        var result = await QueryDictionaryAsync(cleanWord, ct);
        if (result != null && result.HasDefinitions)
        {
            return result;
        }

        // 2. If single word and not found, try lemmatization candidates (e.g. running -> run, cats -> cat, loved -> love)
        if (!cleanWord.Contains(' '))
        {
            foreach (var candidate in WordLemmatizer.GetCandidates(cleanWord))
            {
                ct.ThrowIfCancellationRequested();
                var lemmaResult = await QueryDictionaryAsync(candidate, ct);
                if (lemmaResult != null && lemmaResult.HasDefinitions)
                {
                    lemmaResult.Word = cleanWord;
                    return lemmaResult;
                }
            }
        }

        return result;
    }

    private async Task<DictionaryResult?> QueryDictionaryAsync(string word, CancellationToken ct)
    {
        var settings = _settingsService.CurrentSettings;

        // A. If Cambridge API Key is provided, use official Cambridge API
        if (!string.IsNullOrWhiteSpace(settings.CambridgeApiKey))
        {
            var apiResult = await QueryOfficialCambridgeApiAsync(word, settings, ct);
            if (apiResult != null && apiResult.HasDefinitions)
            {
                return apiResult;
            }
        }

        // B. Primary zero-config fallback: Free Dictionary API (rich structured Oxford/Cambridge definitions)
        var freeResult = await QueryFreeDictionaryApiAsync(word, ct);
        if (freeResult != null && freeResult.HasDefinitions)
        {
            return freeResult;
        }

        // C. Secondary fallback: Scrape Cambridge Dictionary web entry
        var webResult = await ScrapeCambridgeWebAsync(word, ct);
        if (webResult != null && webResult.HasDefinitions)
        {
            return webResult;
        }

        return null;
    }

    private async Task<DictionaryResult?> QueryOfficialCambridgeApiAsync(string word, AppSettings settings, CancellationToken ct)
    {
        try
        {
            string slug = Uri.EscapeDataString(word.ToLowerInvariant().Replace(' ', '-'));
            string url = $"{settings.CambridgeBaseUrl.TrimEnd('/')}/dictionaries/{settings.CambridgeDictionaryId}/entries/{slug}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("accessKey", settings.CambridgeApiKey);

            using var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            string json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new DictionaryResult
            {
                Word = word,
                Headword = word,
                Source = "Cambridge Dictionary API"
            };

            // Extract definitions from Cambridge API format
            if (root.TryGetProperty("entry", out var entryElem))
            {
                if (entryElem.TryGetProperty("pos", out var posElem))
                    result.PartOfSpeech = posElem.GetString() ?? string.Empty;

                if (entryElem.TryGetProperty("pronunciations", out var pronArray) && pronArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pron in pronArray.EnumerateArray())
                    {
                        if (pron.TryGetProperty("ipa", out var ipaElem))
                            result.PhoneticIpa = $"/{ipaElem.GetString()}/";
                        if (pron.TryGetProperty("audio", out var audioElem))
                            result.AudioUrl = audioElem.GetString();
                        if (!string.IsNullOrEmpty(result.PhoneticIpa)) break;
                    }
                }

                if (entryElem.TryGetProperty("definitions", out var defArray) && defArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var def in defArray.EnumerateArray())
                    {
                        if (result.Definitions.Count >= 8) break;
                        if (def.TryGetProperty("definition", out var textElem))
                        {
                            string d = textElem.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(d))
                            {
                                result.Definitions.Add(d);
                                result.Senses.Add(new DictionarySense
                                {
                                    PartOfSpeech = result.PartOfSpeech,
                                    Definition = d
                                });
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<DictionaryResult?> QueryFreeDictionaryApiAsync(string word, CancellationToken ct)
    {
        try
        {
            string url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{Uri.EscapeDataString(word.ToLowerInvariant())}";
            using var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            string json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return null;

            var entry = root[0];
            var result = new DictionaryResult
            {
                Word = word,
                Headword = entry.TryGetProperty("word", out var wElem) ? (wElem.GetString() ?? word) : word,
                Source = "Cambridge / Lexicon"
            };

            // Extract Phonetics
            if (entry.TryGetProperty("phonetics", out var phonetics) && phonetics.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in phonetics.EnumerateArray())
                {
                    if (string.IsNullOrEmpty(result.PhoneticIpa) && p.TryGetProperty("text", out var tElem))
                    {
                        result.PhoneticIpa = tElem.GetString() ?? string.Empty;
                    }
                    if (string.IsNullOrEmpty(result.AudioUrl) && p.TryGetProperty("audio", out var aElem))
                    {
                        string? audio = aElem.GetString();
                        if (!string.IsNullOrWhiteSpace(audio) && audio.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        {
                            result.AudioUrl = audio;
                        }
                    }
                }
            }

            // Extract Meanings, POS, Definitions, and Examples with rich senses
            if (entry.TryGetProperty("meanings", out var meanings) && meanings.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in meanings.EnumerateArray())
                {
                    string pos = m.TryGetProperty("partOfSpeech", out var posElem) ? (posElem.GetString() ?? string.Empty) : string.Empty;
                    if (string.IsNullOrEmpty(result.PartOfSpeech) && !string.IsNullOrEmpty(pos))
                    {
                        result.PartOfSpeech = pos;
                    }

                    if (m.TryGetProperty("definitions", out var defs) && defs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in defs.EnumerateArray())
                        {
                            if (result.Definitions.Count >= 8) break;

                            if (d.TryGetProperty("definition", out var defText))
                            {
                                string defStr = defText.GetString() ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(defStr))
                                {
                                    var sense = new DictionarySense
                                    {
                                        PartOfSpeech = pos,
                                        Definition = defStr
                                    };

                                    if (d.TryGetProperty("example", out var exText))
                                    {
                                        string exStr = exText.GetString() ?? string.Empty;
                                        if (!string.IsNullOrWhiteSpace(exStr))
                                        {
                                            sense.Examples.Add(exStr);
                                            if (result.ExampleSentences.Count < 4)
                                            {
                                                result.ExampleSentences.Add(exStr);
                                            }
                                        }
                                    }

                                    result.Definitions.Add(defStr);
                                    result.Senses.Add(sense);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<DictionaryResult?> ScrapeCambridgeWebAsync(string word, CancellationToken ct)
    {
        try
        {
            string slug = Uri.EscapeDataString(word.Trim().ToLowerInvariant().Replace(' ', '-'));
            string url = $"https://dictionary.cambridge.org/dictionary/english/{slug}";
            using var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            string html = await resp.Content.ReadAsStringAsync(ct);

            var result = new DictionaryResult
            {
                Word = word,
                Headword = word,
                Source = "Cambridge Dictionary"
            };

            // Part of speech: <span class="pos dpos" ...>noun</span>
            var posMatch = Regex.Match(html, @"<span class=""pos dpos""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
            if (posMatch.Success)
            {
                result.PartOfSpeech = StripHtml(posMatch.Groups[1].Value);
            }

            // IPA: <span class="ipa dipa[^"]*">/([^<]+)/</span>
            var ipaMatch = Regex.Match(html, @"<span class=""ipa dipa[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
            if (ipaMatch.Success)
            {
                result.PhoneticIpa = $"/{StripHtml(ipaMatch.Groups[1].Value)}/";
            }

            // Audio: <source type="audio/mpeg" src="([^"]+)"
            var audioMatch = Regex.Match(html, @"<source type=""audio/mpeg""\s+src=""([^""]+)""", RegexOptions.IgnoreCase);
            if (audioMatch.Success)
            {
                string audioPath = audioMatch.Groups[1].Value;
                if (!audioPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    audioPath = "https://dictionary.cambridge.org" + audioPath;
                }
                result.AudioUrl = audioPath;
            }

            // Rich Definition blocks in Cambridge: <div class="def-block ddef_block"[^>]*>...</div>
            // Parse individual def-blocks to extract CEFR, grammar tags, definition text, and examples
            var blockMatches = Regex.Matches(html, @"<div class=""def-block ddef_block[^""]*""[^>]*>(.*?)</div>\s*(?=<div class=""def-block|<div class=""phrase-block|</section>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (blockMatches.Count > 0)
            {
                foreach (Match bm in blockMatches)
                {
                    if (result.Definitions.Count >= 8) break;
                    string blockHtml = bm.Groups[1].Value;

                    // Definition text
                    var defMatch = Regex.Match(blockHtml, @"<div class=""def ddef_d db""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (!defMatch.Success) continue;

                    string defText = StripHtml(defMatch.Groups[1].Value).TrimEnd(':', ' ');
                    if (string.IsNullOrWhiteSpace(defText)) continue;

                    var sense = new DictionarySense
                    {
                        PartOfSpeech = result.PartOfSpeech,
                        Definition = defText
                    };

                    // CEFR level: <span class="epp-xref dxref">B2</span>
                    var cefrMatch = Regex.Match(blockHtml, @"<span class=""epp-xref dxref""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    if (cefrMatch.Success)
                    {
                        sense.CefrLevel = StripHtml(cefrMatch.Groups[1].Value);
                    }

                    // Grammar tag: <span class="gram dgram"[^>]*>(.*?)</span>
                    var gramMatch = Regex.Match(blockHtml, @"<span class=""gram dgram""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    if (gramMatch.Success)
                    {
                        sense.Grammar = StripHtml(gramMatch.Groups[1].Value);
                    }

                    // Examples: <span class="eg deg"[^>]*>(.*?)</span>
                    var egMatches = Regex.Matches(blockHtml, @"<span class=""eg deg""[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    foreach (Match em in egMatches)
                    {
                        string eg = StripHtml(em.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(eg))
                        {
                            sense.Examples.Add(eg);
                            if (result.ExampleSentences.Count < 4)
                            {
                                result.ExampleSentences.Add(eg);
                            }
                        }
                    }

                    result.Definitions.Add(defText);
                    result.Senses.Add(sense);
                }
            }
            else
            {
                // Fallback: simple def match
                var defMatches = Regex.Matches(html, @"<div class=""def ddef_d db""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase);
                foreach (Match m in defMatches)
                {
                    if (result.Definitions.Count >= 8) break;
                    string defText = StripHtml(m.Groups[1].Value).TrimEnd(':', ' ');
                    if (!string.IsNullOrWhiteSpace(defText))
                    {
                        result.Definitions.Add(defText);
                        result.Senses.Add(new DictionarySense
                        {
                            PartOfSpeech = result.PartOfSpeech,
                            Definition = defText
                        });
                    }
                }
            }

            return result.HasDefinitions ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input, "<.*?>", string.Empty).Replace("&nbsp;", " ").Trim();
    }
}
