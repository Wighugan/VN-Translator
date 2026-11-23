using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Services.Capture;

public class ScreenCaptureService : IScreenCaptureService
{
    public Bitmap CaptureCursorRegion(Point cursorPhysical, int radiusX, int radiusY, out Rect capturePhysicalRect)
    {
        int cx = (int)Math.Round(cursorPhysical.X);
        int cy = (int)Math.Round(cursorPhysical.Y);

        int left = cx - radiusX;
        int top = cy - radiusY;
        int width = radiusX * 2;
        int height = radiusY * 2;

        // Get bounds of monitor containing cursor to prevent capturing out-of-screen area
        var pt = new Win32.POINT(cx, cy);
        IntPtr hMonitor = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero)
        {
            var mi = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
            if (Win32.GetMonitorInfo(hMonitor, ref mi))
            {
                if (left < mi.rcMonitor.Left) left = mi.rcMonitor.Left;
                if (top < mi.rcMonitor.Top) top = mi.rcMonitor.Top;
                if (left + width > mi.rcMonitor.Right) width = Math.Max(1, mi.rcMonitor.Right - left);
                if (top + height > mi.rcMonitor.Bottom) height = Math.Max(1, mi.rcMonitor.Bottom - top);
            }
        }

        capturePhysicalRect = new Rect(left, top, width, height);
        return CaptureScreenRect(left, top, width, height);
    }

    public Bitmap CaptureRegion(ScreenRegion region, out Rect capturePhysicalRect)
    {
        capturePhysicalRect = new Rect(region.X, region.Y, region.Width, region.Height);
        return CaptureScreenRect(region.X, region.Y, region.Width, region.Height);
    }

    private static Bitmap CaptureScreenRect(int x, int y, int width, int height)
    {
        if (width <= 0) width = 100;
        if (height <= 0) height = 50;

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }
}
