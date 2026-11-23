using System.Drawing;
using System.Windows;
using VisualNovelDetect.Models;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Services.Capture;

public interface IScreenCaptureService
{
    /// <summary>
    /// Captures a screen region centered around the current cursor physical position.
    /// </summary>
    Bitmap CaptureCursorRegion(Point cursorPhysical, int radiusX, int radiusY, out Rect capturePhysicalRect);

    /// <summary>
    /// Captures an explicit screen region in physical coordinates.
    /// </summary>
    Bitmap CaptureRegion(ScreenRegion region, out Rect capturePhysicalRect);
}
