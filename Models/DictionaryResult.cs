namespace VisualNovelDetect.Models;

/// <summary>
/// Represents the result of a dictionary lookup.
/// </summary>
public class DictionaryResult
{
    public string Word { get; set; } = string.Empty;
    public string Headword { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string PhoneticIpa { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public List<string> Definitions { get; set; } = new();
    public List<string> ExampleSentences { get; set; } = new();
    public List<DictionarySense> Senses { get; set; } = new();
    public string Source { get; set; } = "Cambridge Dictionary";

    // Idiom / Phrasal verb detection
    public DetectedIdiomInfo? DetectedIdiom { get; set; }

    public bool HasDefinitions => Definitions.Count > 0 || Senses.Count > 0;
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioUrl);
    public bool HasExamples => ExampleSentences.Count > 0;
    public bool HasIdiom => DetectedIdiom != null && !string.IsNullOrWhiteSpace(DetectedIdiom.Phrase);
    public bool HasSenses => Senses.Count > 0;
}

public class DictionarySense
{
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Grammar { get; set; } = string.Empty; // e.g. [ C ], [ U ], [ T ], [ I ]
    public string CefrLevel { get; set; } = string.Empty; // e.g. B1, B2, C1, C2
    public string Definition { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();

    public bool HasPartOfSpeech => !string.IsNullOrWhiteSpace(PartOfSpeech);
    public bool HasGrammar => !string.IsNullOrWhiteSpace(Grammar);
    public bool HasCefr => !string.IsNullOrWhiteSpace(CefrLevel);
    public bool HasExamples => Examples.Count > 0;
}

public class DetectedIdiomInfo
{
    public string Phrase { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string? Example { get; set; }

    public bool HasTranslation => !string.IsNullOrWhiteSpace(Translation);
    public bool HasMeaning => !string.IsNullOrWhiteSpace(Meaning);
}
