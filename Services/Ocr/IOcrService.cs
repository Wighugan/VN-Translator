using System.Drawing;
using System.Windows;
using VisualNovelDetect.Models;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Services.Ocr;

public interface IOcrService
{
    string EngineName { get; }

    /// <summary>
    /// Detects the target word under or closest to the cursor within the captured screen image.
    /// </summary>
    Task<DetectedWordInfo?> DetectWordAtCursorAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        Point cursorPhysical,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts all words and their bounding boxes from the captured bitmap.
    /// </summary>
    Task<List<DetectedWordInfo>> ExtractAllWordsAsync(
        Bitmap capturedBmp,
        Rect capturePhysicalRect,
        string languageTag,
        double scaleFactor = 1.0,
        CancellationToken ct = default);
}
