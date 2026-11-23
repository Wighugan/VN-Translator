using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Tesseract;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace VisualNovelDetect.Services.Ocr;

public class TesseractOcrService : IOcrService
{
    public string EngineName => "Tesseract OCR (Fallback)";

    private readonly string? _tessdataPath;
    private readonly WindowsMediaOcrService _fallbackService = new();

    public TesseractOcrService(string? tessdataPath = null)
    {
        _tessdataPath = tessdataPath;
    }

    public async Task<DetectedWordInfo?> DetectWordAtCursorAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        Point cursorPhysical,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default)
    {
        // If tessdata directory is not available, gracefully fallback to Windows OCR
        string tessDir = GetTessDataDirectory();
        if (!Directory.Exists(tessDir))
        {
            return await _fallbackService.DetectWordAtCursorAsync(capturedBmp, capturePhysicalRect, cursorPhysical, languageTag, scaleFactor, ct);
        }

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var allWords = ExtractAllWordsInternal(capturedBmp, capturePhysicalRect, languageTag, scaleFactor, tessDir);
            if (allWords.Count == 0) return null;

            double dpiScale = DpiHelper.GetDpiScaleForPoint((int)cursorPhysical.X, (int)cursorPhysical.Y);
            Point cursorDip = new Point(cursorPhysical.X / dpiScale, cursorPhysical.Y / dpiScale);

            DetectedWordInfo? bestWord = null;
            double minDistance = double.MaxValue;

            foreach (var word in allWords)
            {
                word.PhysicalCursorPosition = cursorPhysical;
                word.DipCursorPosition = cursorDip;

                if (word.PhysicalBoundingBox.Contains(cursorPhysical))
                {
                    word.DistanceToCursor = 0;
                    return word;
                }

                double dist = DistanceToRect(cursorPhysical, word.PhysicalBoundingBox);
                word.DistanceToCursor = dist;

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestWord = word;
                }
            }

            return bestWord;
        }, ct);
    }

    public async Task<List<DetectedWordInfo>> ExtractAllWordsAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default)
    {
        string tessDir = GetTessDataDirectory();
        if (!Directory.Exists(tessDir))
        {
            return await _fallbackService.ExtractAllWordsAsync(capturedBmp, capturePhysicalRect, languageTag, scaleFactor, ct);
        }

        return await Task.Run(() =>
        {
            return ExtractAllWordsInternal(capturedBmp, capturePhysicalRect, languageTag, scaleFactor, tessDir);
        }, ct);
    }

    private List<DetectedWordInfo> ExtractAllWordsInternal(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        string languageTag,
        double scaleFactor,
        string tessDir)
    {
        var resultList = new List<DetectedWordInfo>();

        try
        {
            string tessLang = MapToTesseractLanguage(languageTag);
            using var engine = new TesseractEngine(tessDir, tessLang, EngineMode.Default);
            using var ms = new MemoryStream();
            capturedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            using var pix = Pix.LoadFromMemory(ms.ToArray());
            using var page = engine.Process(pix);

            using var iter = page.GetIterator();
            iter.Begin();

            double dpiScale = DpiHelper.GetDpiScaleForPoint((int)capturePhysicalRect.X, (int)capturePhysicalRect.Y);

            do
            {
                string wordText = iter.GetText(PageIteratorLevel.Word);
                if (string.IsNullOrWhiteSpace(wordText)) continue;

                string cleanText = WindowsMediaOcrService.SanitizeWord(wordText);
                if (string.IsNullOrWhiteSpace(cleanText)) continue;

                if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var box))
                {
                    double physX = capturePhysicalRect.X + (box.X1 / scaleFactor);
                    double physY = capturePhysicalRect.Y + (box.Y1 / scaleFactor);
                    double physW = box.Width / scaleFactor;
                    double physH = box.Height / scaleFactor;

                    var physRect = new Rect(physX, physY, physW, physH);
                    var dipRect = DpiHelper.PhysicalToDipRect(physRect, dpiScale);

                    resultList.Add(new DetectedWordInfo
                    {
                        RawWord = wordText,
                        CleanWord = cleanText,
                        PhysicalBoundingBox = physRect,
                        DipBoundingBox = dipRect,
                        Confidence = iter.GetConfidence(PageIteratorLevel.Word)
                    });
                }
            } while (iter.Next(PageIteratorLevel.Word));
        }
        catch
        {
            // If Tesseract fails, return empty to trigger fallback
        }

        return resultList;
    }

    private string GetTessDataDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_tessdataPath) && Directory.Exists(_tessdataPath))
            return _tessdataPath;

        string appDataTess = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualNovelDetect",
            "tessdata");

        if (Directory.Exists(appDataTess))
            return appDataTess;

        string localTess = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        return localTess;
    }

    private static string MapToTesseractLanguage(string tag)
    {
        if (tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "jpn";
        if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "chi_sim";
        if (tag.StartsWith("vi", StringComparison.OrdinalIgnoreCase)) return "vie";
        return "eng";
    }

    private static double DistanceToRect(Point pt, Rect rect)
    {
        double dx = Math.Max(0, Math.Max(rect.Left - pt.X, pt.X - rect.Right));
        double dy = Math.Max(0, Math.Max(rect.Top - pt.Y, pt.Y - rect.Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
