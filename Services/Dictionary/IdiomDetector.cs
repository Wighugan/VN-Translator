using System.Text.RegularExpressions;
using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Dictionary;

public class IdiomEntry
{
    public string CanonicalPhrase { get; set; } = string.Empty;
    public Regex Pattern { get; set; } = null!;
    public string Meaning { get; set; } = string.Empty;
    public string DefaultVietnamese { get; set; } = string.Empty;
    public HashSet<string> Keywords { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IdiomEntry(string canonical, string regexPattern, string meaning, string defaultVi, params string[] keywords)
    {
        CanonicalPhrase = canonical;
        Pattern = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Meaning = meaning;
        DefaultVietnamese = defaultVi;
        foreach (var kw in keywords)
        {
            Keywords.Add(kw.ToLowerInvariant());
        }
    }
}

public class IdiomDetector
{
    private static readonly List<IdiomEntry> Idioms = new();

    static IdiomDetector()
    {
        InitializeDatabase();
    }

    public static DetectedIdiomInfo? DetectIdiom(string? sentence, string targetWord, string? headword = null)
    {
        if (string.IsNullOrWhiteSpace(sentence) || string.IsNullOrWhiteSpace(targetWord))
            return null;

        string wordNorm = targetWord.Trim().ToLowerInvariant();
        string headNorm = (headword ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var entry in Idioms)
        {
            bool matchesKeyword = entry.Keywords.Contains(wordNorm) ||
                                 (!string.IsNullOrEmpty(headNorm) && entry.Keywords.Contains(headNorm));

            if (!matchesKeyword) continue;

            var match = entry.Pattern.Match(sentence);
            if (match.Success)
            {
                return new DetectedIdiomInfo
                {
                    Phrase = match.Value.Trim(),
                    Meaning = entry.Meaning,
                    Translation = entry.DefaultVietnamese
                };
            }
        }

        return null;
    }

    private static void Register(string canonical, string regexPattern, string meaning, string defaultVi, params string[] keywords)
    {
        Idioms.Add(new IdiomEntry(canonical, regexPattern, meaning, defaultVi, keywords));
    }

    private static void InitializeDatabase()
    {
        // Core Visual Novel & Common English Idioms
        Register("kick the bucket",
            @"\b(?:kick(?:ed|ing|s)?)\s+(?:the\s+)?bucket\b",
            "To die; pass away.",
            "Qua đời, chết, ngỏm",
            "kick", "kicked", "kicking", "kicks", "bucket");

        Register("bite the bullet",
            @"\b(?:bite|bit|bitten|biting|bites)\s+the\s+bullet\b",
            "To face a difficult or unpleasant situation with courage and fortitude.",
            "Cắn răng chịu đựng, ngậm đắng nuốt cay",
            "bite", "bit", "bitten", "biting", "bites", "bullet");

        Register("take for granted",
            @"\b(?:take|took|taken|taking|takes)\s+(?:\w+\s+)?for\s+granted\b",
            "To fail to properly appreciate someone or something, assuming they will always be there.",
            "Coi là điều hiển nhiên, không biết trân trọng",
            "take", "took", "taken", "taking", "takes", "granted");

        Register("piece of cake",
            @"\b(?:piece\s+of\s+cake)\b",
            "Something that is very easy and simple to achieve or do.",
            "Dễ như ăn cháo, chuyện nhỏ",
            "piece", "cake");

        Register("break a leg",
            @"\b(?:break|broke|broken|breaking|breaks)\s+a\s+leg\b",
            "A phrase wishing good luck, especially before a performance or challenge.",
            "Chúc may mắn (trước khi biểu diễn/hành động)",
            "break", "broke", "broken", "breaking", "breaks", "leg");

        Register("at all costs",
            @"\bat\s+all\s+costs\b",
            "Regardless of the effort, difficulty, sacrifice, or consequences involved.",
            "Bằng mọi giá, bất chấp tất cả",
            "cost", "costs");

        Register("call it a day",
            @"\b(?:call|called|calling|calls)\s+it\s+a\s+day\b",
            "To stop what you are doing for the rest of the day or time.",
            "Nghỉ tay, kết thúc công việc hôm nay",
            "call", "called", "calling", "calls", "day");

        Register("make up one's mind",
            @"\b(?:make|made|making|makes)\s+up\s+(?:one's|my|your|his|her|our|their|someone's)?\s*mind\b",
            "To make a firm decision or resolve to do something.",
            "Quyết định, hạ quyết tâm",
            "mind", "make", "made", "making", "makes");

        Register("change one's mind",
            @"\b(?:change|changed|changing|changes)\s+(?:one's|my|your|his|her|our|their)?\s*mind\b",
            "To change a decision or opinion previously held.",
            "Đổi ý, thay đổi quyết định",
            "mind", "change", "changed", "changing", "changes");

        Register("on cloud nine",
            @"\b(?:on\s+cloud\s+nine)\b",
            "Extremely happy, euphoric, and overjoyed.",
            "Trên chín tầng mây, cực kỳ hạnh phúc",
            "cloud", "nine");

        Register("spill the beans",
            @"\b(?:spill|spilled|spilt|spilling|spills)\s+the\s+beans\b",
            "To reveal secret, private, or confidential information carelessly or prematurely.",
            "Tiết lộ bí mật, làm lộ chuyện",
            "spill", "spilled", "spilt", "spilling", "spills", "beans", "bean");

        Register("by the way",
            @"\bby\s+the\s+way\b",
            "Incidentally; used to introduce a related or new thought into conversation.",
            "Nhân tiện, tiện thể mà nói",
            "way");

        Register("look forward to",
            @"\b(?:look|looked|looking|looks)\s+forward\s+to\b",
            "To feel pleased and excited about something that is going to happen.",
            "Mong chờ, trông đợi",
            "forward", "look", "looked", "looking", "looks");

        Register("give up",
            @"\b(?:give|gave|given|giving|gives)\s+up\b",
            "To surrender, stop making an effort, or admit defeat.",
            "Bỏ cuộc, từ bỏ, đầu hàng",
            "give", "gave", "given", "giving", "gives");

        Register("run out of",
            @"\b(?:run|ran|running|runs)\s+out\s+of\b",
            "To finish, use up, or exhaust the available supply of something.",
            "Cạn kiệt, hết sạch (thời gian, tiền bạc, năng lượng)",
            "run", "ran", "running", "runs");

        Register("get over",
            @"\b(?:get|got|gotten|getting|gets)\s+over\b",
            "To recover from an illness, loss, heartbreak, or unpleasant emotional shock.",
            "Vượt qua, nguôi ngoai",
            "get", "got", "gotten", "getting", "gets");

        Register("keep an eye on",
            @"\b(?:keep|kept|keeping|keeps)\s+an\s+eye\s+on\b",
            "To watch or monitor someone or something closely and attentively.",
            "Để mắt tới, trông chừng, theo dõi",
            "eye", "keep", "kept", "keeping", "keeps");

        Register("under the weather",
            @"\bunder\s+the\s+weather\b",
            "Feeling slightly sick, unwell, or low in energy.",
            "Mệt mỏi, khó ở, không khỏe",
            "weather");

        Register("beat around the bush",
            @"\b(?:beat|beating|beats)\s+around\s+the\s+bush\b",
            "To avoid discussing what is important directly, speaking vaguely instead.",
            "Nói vòng vo tam quốc, tránh né vấn đề chính",
            "bush", "beat", "beating", "beats");

        Register("cut corners",
            @"\b(?:cut|cutting|cuts)\s+corners\b",
            "To do something in the easiest, quickest, or cheapest way, sacrificing quality.",
            "Đốt cháy giai đoạn, làm ẩu, bớt xén",
            "corner", "corners", "cut", "cutting", "cuts");

        Register("hit the nail on the head",
            @"\b(?:hit|hitting|hits)\s+the\s+nail\s+on\s+the\s+head\b",
            "To describe exactly what is causing a situation or answer with absolute precision.",
            "Nói trúng tim đen, nói chuẩn xác 100%",
            "nail", "hit", "hitting", "hits");

        Register("once in a blue moon",
            @"\bonce\s+in\s+a\s+blue\s+moon\b",
            "Extremely rarely; almost never.",
            "Hiếm khi, năm thì mười họa",
            "moon", "blue");

        Register("burn the midnight oil",
            @"\b(?:burn|burned|burnt|burning|burns)\s+the\s+midnight\s+oil\b",
            "To work or study late into the night.",
            "Thức khuya làm việc/học tập, cày đêm",
            "midnight", "oil", "burn", "burned", "burnt", "burning");

        Register("see eye to eye",
            @"\b(?:see|saw|seen|seeing|sees)\s+eye\s+to\s+eye\b",
            "To agree fully and have the same opinion as someone else.",
            "Đồng tình, có cùng quan điểm, hợp cạ",
            "eye", "see", "saw", "seen", "seeing", "sees");

        Register("speak of the devil",
            @"\bspeak\s+of\s+the\s+devil\b",
            "Said when a person appears just as people are talking about them.",
            "Vừa nhắc Tào Tháo, Tào Tháo đến ngay",
            "devil", "speak", "spoke");

        Register("through thick and thin",
            @"\bthrough\s+thick\s+and\s+thin\b",
            "In spite of all difficulties, hardships, and unfavorable circumstances.",
            "Vượt qua muôn trùng sóng gió, chia ngọt sẻ bùi",
            "thick", "thin");

        Register("throw in the towel",
            @"\b(?:throw|threw|thrown|throwing|throws)\s+in\s+the\s+towel\b",
            "To acknowledge defeat and abandon a struggle.",
            "Nhận thua, tung cờ trắng, bỏ cuộc",
            "towel", "throw", "threw", "thrown", "throwing", "throws");

        Register("turn a blind eye",
            @"\b(?:turn|turned|turning|turns)\s+a\s+blind\s+eye\b",
            "To deliberately ignore or overlook something that you know is wrong.",
            "Nhắm mắt làm ngơ, làm như không thấy",
            "blind", "eye", "turn", "turned", "turning", "turns");

        Register("up in the air",
            @"\bup\s+in\s+the\s+air\b",
            "Uncertain, unresolved, or still undecided.",
            "Chưa chắc chắn, còn lơ lửng, chưa chốt",
            "air");

        Register("wrap one's head around",
            @"\b(?:wrap|wrapped|wrapping|wraps)\s+(?:one's|my|your|his|her|our|their)?\s*head\s+around\b",
            "To succeed in understanding something complicated or difficult.",
            "Hiểu được, tiêu hóa được (một điều khó tin/phức tạp)",
            "head", "wrap", "wrapped", "wrapping", "wraps");

        Register("face the music",
            @"\b(?:face|faced|facing|faces)\s+the\s+music\b",
            "To accept criticism, unpleasant consequences, or punishment for one's actions.",
            "Chấp nhận đối mặt với hậu quả, chịu trận",
            "music", "face", "faced", "facing", "faces");

        Register("cost an arm and a leg",
            @"\b(?:cost|costs|costing)\s+an\s+arm\s+and\s+a\s+leg\b",
            "To be extremely expensive or exorbitant.",
            "Đắt cắt cổ, tốn kém gia tài",
            "arm", "leg", "cost", "costs");

        Register("cry over spilled milk",
            @"\b(?:cry|cried|crying|cries)\s+over\s+(?:spilled|spilt)\s+milk\b",
            "To waste time being upset over past misfortunes that cannot be undone.",
            "Tiếc rẻ chuyện đã rồi, khóc cũng chẳng ích gì",
            "milk", "spilled", "spilt", "cry", "cried", "crying");

        Register("leave no stone unturned",
            @"\b(?:leave|left|leaving|leaves)\s+no\s+stone\s+unturned\b",
            "To do everything conceivable to achieve a goal or find something.",
            "Tìm kiếm khắp hang cùng ngõ hẻm, làm đủ mọi cách",
            "stone", "unturned", "leave", "left");

        Register("ring a bell",
            @"\b(?:ring|rang|rung|ringing|rings)\s+a\s+bell\b",
            "To sound vaguely familiar, recalling a forgotten memory.",
            "Nghe quen quen, gợi nhớ ký ức",
            "bell", "ring", "rang", "rung", "ringing", "rings");

        Register("in the blink of an eye",
            @"\bin\s+the\s+blink\s+of\s+an\s+eye\b",
            "Extremely rapidly; almost instantaneously.",
            "Trong chớp mắt, thoáng chốc",
            "blink", "eye");

        Register("out of the blue",
            @"\bout\s+of\s+the\s+blue\b",
            "Completely unexpectedly without prior hint or warning.",
            "Bất thình lình, từ trên trời rơi xuống",
            "blue");

        Register("pull someone's leg",
            @"\b(?:pull|pulled|pulling|pulls)\s+(?:\w+\s+)?leg\b",
            "To tease or deceive someone playfully as a joke.",
            "Trêu chọc, đùa vui, chọc ghẹo",
            "leg", "pull", "pulled", "pulling", "pulls");

        Register("lost in thought",
            @"\blost\s+in\s+thought\b",
            "Deeply preoccupied with one's own thinking and oblivious to surroundings.",
            "Chìm đắm trong suy nghĩ, trầm ngâm",
            "thought", "lost");

        Register("fall in love",
            @"\b(?:fall|fell|fallen|falling|falls)\s+in\s+love\b",
            "To begin to experience deep romantic attachment and love for someone.",
            "Rơi vào lưới tình, đem lòng yêu",
            "love", "fall", "fell", "fallen", "falling", "falls");

        Register("make sense",
            @"\b(?:make|made|making|makes)\s+sense\b",
            "To be intelligible, reasonable, or logical.",
            "Có lý, hợp lý, dễ hiểu",
            "sense", "make", "made", "making", "makes");

        Register("figure out",
            @"\b(?:figure|figured|figuring|figures)\s+out\b",
            "To discover, calculate, or finally understand the solution to a problem.",
            "Tìm ra, luận ra, hiểu ra",
            "figure", "figured", "figuring", "figures");

        Register("come up with",
            @"\b(?:come|came|coming|comes)\s+up\s+with\b",
            "To produce, suggest, or think of an idea, plan, or excuse.",
            "Nghĩ ra, nảy ra (ý tưởng, kế hoạch)",
            "come", "came", "coming", "comes");

        Register("hold on",
            @"\b(?:hold|held|holding|holds)\s+on\b",
            "To wait for a short time; or to maintain one's grip/position.",
            "Đợi một chút; hoặc bám chặt",
            "hold", "held", "holding", "holds");

        Register("find out",
            @"\b(?:find|found|finding|finds)\s+out\b",
            "To discover or learn a piece of information or truth.",
            "Tìm ra, phát hiện ra sự thật",
            "find", "found", "finding", "finds");

        Register("point of view",
            @"\bpoint\s+of\s+view\b",
            "A specific perspective, standpoint, or opinion.",
            "Góc nhìn, quan điểm",
            "point", "view");

        Register("as a matter of fact",
            @"\bas\s+a\s+matter\s+of\s+fact\b",
            "In actual reality; actually.",
            "Trên thực tế, thật ra mà nói",
            "matter", "fact");

        Register("at first sight",
            @"\bat\s+first\s+sight\b",
            "When seen or considered for the very first time.",
            "Từ cái nhìn đầu tiên, thoạt nhìn",
            "sight");

        Register("bear in mind",
            @"\b(?:bear|bore|borne|bearing|bears)\s+in\s+mind\b",
            "To remember and take into active consideration.",
            "Ghi nhớ, khắc cốt ghi tâm",
            "mind", "bear", "bore", "bearing");

        Register("spill the tea",
            @"\b(?:spill|spilled|spilt|spilling|spills)\s+the\s+tea\b",
            "To share gossip, scandal, or spicy private details.",
            "Hóng hớt / buôn chuyện drama",
            "tea", "spill", "spilled", "spilt", "spilling");

        Register("cross the line",
            @"\b(?:cross|crossed|crossing|crosses)\s+the\s+line\b",
            "To go beyond acceptable or polite limits of behavior.",
            "Vượt quá giới hạn, đi quá đà",
            "line", "cross", "crossed", "crossing", "crosses");

        Register("catch red-handed",
            @"\b(?:catch|caught|catching|catches)\s+(?:\w+\s+)?red[- ]handed\b",
            "To discover someone in the exact middle of committing a wrongful act.",
            "Bắt quả tang tại trận",
            "handed", "catch", "caught", "catching");

        Register("take a rain check",
            @"\b(?:take|took|taken|taking|takes)\s+a\s+rain\s+check\b",
            "To decline an offer or invitation now with the promise of accepting later.",
            "Hẹn lần khác, để dịp sau nhé",
            "check", "rain", "take", "took", "taken");
    }
}
