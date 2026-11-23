using System.Windows;

namespace VisualNovelDetect.Models;

/// <summary>
/// Information about a word detected on screen by the OCR engine.
/// </summary>
public class DetectedWordInfo
{
    public string RawWord { get; set; } = string.Empty;
    public string CleanWord { get; set; } = string.Empty;
    public Rect PhysicalBoundingBox { get; set; }
    public Rect DipBoundingBox { get; set; }
    public Point PhysicalCursorPosition { get; set; }
    public Point DipCursorPosition { get; set; }
    public string SurroundingSentence { get; set; } = string.Empty;
    public double DistanceToCursor { get; set; }
    public double Confidence { get; set; } = 1.0;
}
