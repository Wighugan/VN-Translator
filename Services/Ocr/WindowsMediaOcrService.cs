using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace VisualNovelDetect.Services.Ocr;

public class WindowsMediaOcrService : IOcrService
{
    public string EngineName => "Windows OCR (Native)";

    private readonly Dictionary<string, OcrEngine> _cachedEngines = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _engineLock = new();

    public async Task<DetectedWordInfo?> DetectWordAtCursorAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        Point cursorPhysical,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default)
    {
        var allWords = await ExtractAllWordsAsync(capturedBmp, capturePhysicalRect, languageTag, scaleFactor, ct);
        if (allWords.Count == 0) return null;

        double dpiScale = DpiHelper.GetDpiScaleForPoint((int)cursorPhysical.X, (int)cursorPhysical.Y);
        Point cursorDip = new Point(cursorPhysical.X / dpiScale, cursorPhysical.Y / dpiScale);

        DetectedWordInfo? bestWord = null;
        double minDistance = double.MaxValue;

        foreach (var word in allWords)
        {
            word.PhysicalCursorPosition = cursorPhysical;
            word.DipCursorPosition = cursorDip;

            // Check if cursor is directly inside the physical bounding box
            if (word.PhysicalBoundingBox.Contains(cursorPhysical))
            {
                word.DistanceToCursor = 0;
                return word; // Exact hit under cursor!
            }

            // Otherwise calculate Euclidean distance from cursor to closest point on the bounding box
            double dist = DistanceToRect(cursorPhysical, word.PhysicalBoundingBox);
            word.DistanceToCursor = dist;

            if (dist < minDistance)
            {
                minDistance = dist;
                bestWord = word;
            }
        }

        // Only accept if within a reasonable distance (e.g. 80 physical pixels from word)
        if (bestWord != null && minDistance <= 80.0)
        {
            return bestWord;
        }

        return bestWord; // Fallback to nearest detected word
    }

    public async Task<List<DetectedWordInfo>> ExtractAllWordsAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default)
    {
        var resultList = new List<DetectedWordInfo>();
        if (capturedBmp.Width <= 0 || capturedBmp.Height <= 0) return resultList;

        var engine = GetOrCreateOcrEngine(languageTag);
        if (engine == null) return resultList;

        using var softwareBitmap = await ConvertBitmapToSoftwareBitmapAsync(capturedBmp);
        if (softwareBitmap == null) return resultList;

        ct.ThrowIfCancellationRequested();
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);
        if (ocrResult == null || ocrResult.Lines == null) return resultList;

        double dpiScale = DpiHelper.GetDpiScaleForPoint((int)capturePhysicalRect.X, (int)capturePhysicalRect.Y);

        // Reconstruct full dialogue utterance across lines so idioms and sentences that wrap are intact
        var allLineTexts = ocrResult.Lines
            .Select(l => l.Text?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        string fullDialogueText = string.Join(" ", allLineTexts);

        foreach (var line in ocrResult.Lines)
        {
            string lineText = line.Text ?? string.Empty;

            foreach (var word in line.Words)
            {
                string rawText = word.Text ?? string.Empty;
                string cleanText = SanitizeWord(rawText);
                if (string.IsNullOrWhiteSpace(cleanText)) continue;

                // Map bounding rect from captured bitmap coordinates (accounting for 2x upscale if applied)
                double physX = capturePhysicalRect.X + (word.BoundingRect.X / scaleFactor);
                double physY = capturePhysicalRect.Y + (word.BoundingRect.Y / scaleFactor);
                double physW = word.BoundingRect.Width / scaleFactor;
                double physH = word.BoundingRect.Height / scaleFactor;

                var physRect = new Rect(physX, physY, physW, physH);
                var dipRect = DpiHelper.PhysicalToDipRect(physRect, dpiScale);

                resultList.Add(new DetectedWordInfo
                {
                    RawWord = rawText,
                    CleanWord = cleanText,
                    PhysicalBoundingBox = physRect,
                    DipBoundingBox = dipRect,
                    SurroundingSentence = !string.IsNullOrWhiteSpace(fullDialogueText) ? fullDialogueText : lineText
                });
            }
        }

        return resultList;
    }

    private OcrEngine? GetOrCreateOcrEngine(string languageTag)
    {
        lock (_engineLock)
        {
            if (_cachedEngines.TryGetValue(languageTag, out var cached))
                return cached;

            OcrEngine? engine = null;
            try
            {
                var lang = new Language(languageTag);
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    engine = OcrEngine.TryCreateFromLanguage(lang);
                }
            }
            catch
            {
                // Ignore and fallback
            }

            if (engine == null)
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }

            if (engine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
            {
                engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
            }

            if (engine != null)
            {
                _cachedEngines[languageTag] = engine;
            }

            return engine;
        }
    }

    private static async Task<SoftwareBitmap?> ConvertBitmapToSoftwareBitmapAsync(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var ras = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        var decoder = await BitmapDecoder.CreateAsync(ras);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }

    public static string SanitizeWord(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Custom characters commonly encountered in visual novel dialogues
        char[] trimChars =
        {
            ' ', '\t', '\r', '\n',
            '"', '\'', '`', '‘', '’', '“', '”',
            '(', ')', '[', ']', '{', '}', '<', '>', '«', '»',
            '「', '」', '『', '』', '【', '】', '（', '）',
            ',', '.', ';', ':', '!', '?', '¿', '¡',
            '…', '~', '-', '—', '_', '*', '^', '/', '\\', '|',
            '、', '。', '！', '？', '：', '；'
        };

        string trimmed = input.Trim(trimChars);

        // If ends with possessive 's (e.g. "player's"), remove 's
        if (trimmed.EndsWith("'s", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
        {
            trimmed = trimmed[..^2];
        }

        // If ends with ’s
        if (trimmed.EndsWith("’s", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
        {
            trimmed = trimmed[..^2];
        }

        return trimmed.Trim(trimChars);
    }

    private static double DistanceToRect(Point pt, Rect rect)
    {
        double dx = Math.Max(0, Math.Max(rect.Left - pt.X, pt.X - rect.Right));
        double dy = Math.Max(0, Math.Max(rect.Top - pt.Y, pt.Y - rect.Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
