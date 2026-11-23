namespace VisualNovelDetect.Services.Dictionary;

public static class WordLemmatizer
{
    private static readonly Dictionary<string, string> IrregularForms = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common irregular verbs
        ["went"] = "go", ["gone"] = "go", ["goes"] = "go",
        ["was"] = "be", ["were"] = "be", ["been"] = "be", ["am"] = "be", ["is"] = "be", ["are"] = "be",
        ["had"] = "have", ["has"] = "have",
        ["did"] = "do", ["done"] = "do", ["does"] = "do",
        ["saw"] = "see", ["seen"] = "see",
        ["came"] = "come",
        ["made"] = "make",
        ["took"] = "take", ["taken"] = "take",
        ["got"] = "get", ["gotten"] = "get",
        ["knew"] = "know", ["known"] = "know",
        ["thought"] = "think",
        ["found"] = "find",
        ["gave"] = "give", ["given"] = "give",
        ["told"] = "tell",
        ["felt"] = "feel",
        ["became"] = "become",
        ["left"] = "leave",
        ["put"] = "put",
        ["brought"] = "bring",
        ["began"] = "begin", ["begun"] = "begin",
        ["kept"] = "keep",
        ["held"] = "hold",
        ["wrote"] = "write", ["written"] = "write",
        ["stood"] = "stand",
        ["heard"] = "hear",
        ["let"] = "let",
        ["meant"] = "mean",
        ["set"] = "set",
        ["met"] = "meet",
        ["ran"] = "run",
        ["paid"] = "pay",
        ["sat"] = "sit",
        ["spoke"] = "speak", ["spoken"] = "speak",
        ["lay"] = "lie", ["lain"] = "lie",
        ["led"] = "lead",
        ["read"] = "read",
        ["grew"] = "grow", ["grown"] = "grow",
        ["lost"] = "lose",
        ["fell"] = "fall", ["fallen"] = "fall",
        ["sent"] = "send",
        ["built"] = "build",
        ["understood"] = "understand",
        ["drew"] = "draw", ["drawn"] = "draw",
        ["broke"] = "break", ["broken"] = "break",
        ["spent"] = "spend",
        ["cut"] = "cut",
        ["rose"] = "rise", ["risen"] = "rise",
        ["drove"] = "drive", ["driven"] = "drive",
        ["bought"] = "buy",
        ["wore"] = "wear", ["worn"] = "wear",
        ["chose"] = "choose", ["chosen"] = "choose",
        ["shook"] = "shake", ["shaken"] = "shake",
        ["won"] = "win",
        ["sang"] = "sing", ["sung"] = "sing",
        ["swam"] = "swim", ["swum"] = "swim",
        ["caught"] = "catch",
        ["fought"] = "fight",
        ["taught"] = "teach",
        ["slept"] = "sleep",

        // Common irregular plurals
        ["children"] = "child",
        ["men"] = "man",
        ["women"] = "woman",
        ["people"] = "person",
        ["teeth"] = "tooth",
        ["feet"] = "foot",
        ["mice"] = "mouse",
        ["geese"] = "goose",
        ["oxen"] = "ox",
        ["lives"] = "life",
        ["wives"] = "wife",
        ["halves"] = "half",
        ["leaves"] = "leaf",
        ["knives"] = "knife"
    };

    /// <summary>
    /// Generates potential base lemma candidates for an inflected word in order of likelihood.
    /// </summary>
    public static IEnumerable<string> GetCandidates(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length <= 2)
            yield break;

        string lower = word.ToLowerInvariant();

        // 1. Direct irregular check
        if (IrregularForms.TryGetValue(lower, out var irregularBase))
        {
            yield return irregularBase;
        }

        // 2. Regular rules for suffixes
        // -ing
        if (lower.EndsWith("ing") && lower.Length > 4)
        {
            string stem = lower[..^3];
            // e.g. making -> make
            yield return stem + "e";
            // e.g. walking -> walk
            yield return stem;
            // e.g. running -> run, stopping -> stop
            if (stem.Length > 2 && stem[^1] == stem[^2])
            {
                yield return stem[..^1];
            }
        }

        // -ed
        if (lower.EndsWith("ed") && lower.Length > 3)
        {
            string stem = lower[..^2];
            // e.g. loved -> love
            yield return stem + "e";
            // e.g. walked -> walk
            yield return stem;
            // e.g. studied -> study
            if (lower.EndsWith("ied") && lower.Length > 4)
            {
                yield return lower[..^3] + "y";
            }
            // e.g. stopped -> stop
            if (stem.Length > 2 && stem[^1] == stem[^2])
            {
                yield return stem[..^1];
            }
        }

        // -ies -> -y
        if (lower.EndsWith("ies") && lower.Length > 4)
        {
            yield return lower[..^3] + "y";
        }

        // -es
        if (lower.EndsWith("es") && lower.Length > 3)
        {
            yield return lower[..^2];
            yield return lower[..^1]; // e.g. changes -> change
        }

        // -s
        if (lower.EndsWith("s") && !lower.EndsWith("ss") && lower.Length > 2)
        {
            yield return lower[..^1];
        }

        // -ly
        if (lower.EndsWith("ly") && lower.Length > 4)
        {
            yield return lower[..^2];
            if (lower.EndsWith("ily") && lower.Length > 5)
            {
                yield return lower[..^3] + "y";
            }
        }
    }
}
