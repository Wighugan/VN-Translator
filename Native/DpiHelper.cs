using System.Windows;

namespace VisualNovelDetect.Native;

public static class DpiHelper
{
    private const double StandardDpi = 96.0;

    /// <summary>
    /// Gets the current cursor position in physical screen coordinates.
    /// </summary>
    public static Point GetCursorPhysicalPosition()
    {
        if (Win32.GetCursorPos(out var pt))
        {
            return new Point(pt.X, pt.Y);
        }
        return new Point(0, 0);
    }

    /// <summary>
    /// Gets the DPI scale factor (e.g. 1.0 for 100%, 1.25 for 125%, 1.5 for 150%) for the monitor at the specified physical point.
    /// </summary>
    public static double GetDpiScaleForPoint(int physicalX, int physicalY)
    {
        var pt = new Win32.POINT(physicalX, physicalY);
        IntPtr hMonitor = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero)
        {
            int hr = Win32.GetDpiForMonitor(hMonitor, Win32.MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out _);
            if (hr == 0 && dpiX > 0)
            {
                return dpiX / StandardDpi;
            }
        }
        return 1.0;
    }

    /// <summary>
    /// Gets the monitor work area bounds in WPF DIPs and the DPI scale factor.
    /// </summary>
    public static Rect GetMonitorWorkAreaDip(int physicalX, int physicalY, out double dpiScale)
    {
        dpiScale = GetDpiScaleForPoint(physicalX, physicalY);
        var pt = new Win32.POINT(physicalX, physicalY);
        IntPtr hMonitor = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);

        if (hMonitor != IntPtr.Zero)
        {
            var mi = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
            if (Win32.GetMonitorInfo(hMonitor, ref mi))
            {
                double leftDip = mi.rcWork.Left / dpiScale;
                double topDip = mi.rcWork.Top / dpiScale;
                double widthDip = mi.rcWork.Width / dpiScale;
                double heightDip = mi.rcWork.Height / dpiScale;

                return new Rect(leftDip, topDip, widthDip, heightDip);
            }
        }

        // Fallback to primary screen
        double fallbackScale = dpiScale > 0 ? dpiScale : 1.0;
        return new Rect(
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
    }

    /// <summary>
    /// Converts a physical pixel rectangle to WPF DIPs.
    /// </summary>
    public static Rect PhysicalToDipRect(Rect physicalRect, double dpiScale)
    {
        if (dpiScale <= 0) dpiScale = 1.0;
        return new Rect(
            physicalRect.X / dpiScale,
            physicalRect.Y / dpiScale,
            physicalRect.Width / dpiScale,
            physicalRect.Height / dpiScale);
    }

    /// <summary>
    /// Calculates popup position for fixed/saved position mode, preventing intrusive obstruction of dialogue text.
    /// Clamps within the monitor work area.
    /// </summary>
    public static Point CalculateFixedPopupDipPosition(
        double savedLeft,
        double savedTop,
        double popupWidthDip,
        double popupHeightDip,
        Rect monitorWorkAreaDip)
    {
        const double padding = 20.0;

        // Default to a comfortable upper-right position if never saved
        if (savedLeft < 0 || savedTop < 0)
        {
            double defaultLeft = monitorWorkAreaDip.Right - popupWidthDip - padding;
            double defaultTop = monitorWorkAreaDip.Top + padding + 30.0;
            return new Point(defaultLeft, defaultTop);
        }

        double clampedLeft = Math.Clamp(savedLeft, monitorWorkAreaDip.Left, Math.Max(monitorWorkAreaDip.Left, monitorWorkAreaDip.Right - popupWidthDip));
        double clampedTop = Math.Clamp(savedTop, monitorWorkAreaDip.Top, Math.Max(monitorWorkAreaDip.Top, monitorWorkAreaDip.Bottom - popupHeightDip));
        return new Point(clampedLeft, clampedTop);
    }

    /// <summary>
    /// Calculates the optimal DIP screen coordinates (Left, Top) for the floating popup window,
    /// placing it directly below the target word/cursor (or flipping above if space is constrained)
    /// and clamping horizontally to stay safely inside the target monitor.
    /// </summary>
    public static Point CalculatePopupDipPosition(
        Rect targetWordDipRect,
        Point fallbackCursorDip,
        double popupWidthDip,
        double popupHeightDip,
        Rect monitorWorkAreaDip)
    {
        const double verticalMargin = 10.0;
        const double screenEdgePadding = 12.0;

        // Use target word rect if valid, otherwise cursor point
        bool hasWordRect = targetWordDipRect.Width > 0 && targetWordDipRect.Height > 0;
        double anchorX = hasWordRect ? (targetWordDipRect.Left + targetWordDipRect.Width / 2.0) : fallbackCursorDip.X;
        double anchorBottomY = hasWordRect ? targetWordDipRect.Bottom : fallbackCursorDip.Y + 16.0;
        double anchorTopY = hasWordRect ? targetWordDipRect.Top : fallbackCursorDip.Y - 16.0;

        // Center horizontally on anchor
        double left = anchorX - (popupWidthDip / 2.0);

        // Clamp horizontally within monitor bounds
        double minLeft = monitorWorkAreaDip.Left + screenEdgePadding;
        double maxLeft = monitorWorkAreaDip.Right - popupWidthDip - screenEdgePadding;
        if (left < minLeft) left = minLeft;
        if (left > maxLeft) left = maxLeft;

        // Try placing below target
        double top = anchorBottomY + verticalMargin;

        // If placing below exceeds the bottom of the work area, flip to above the word
        if (top + popupHeightDip > monitorWorkAreaDip.Bottom - screenEdgePadding)
        {
            double flippedTop = anchorTopY - popupHeightDip - verticalMargin;
            if (flippedTop >= monitorWorkAreaDip.Top + screenEdgePadding)
            {
                top = flippedTop;
            }
            else
            {
                // Constrain within available height
                top = Math.Max(monitorWorkAreaDip.Top + screenEdgePadding, monitorWorkAreaDip.Bottom - popupHeightDip - screenEdgePadding);
            }
        }

        return new Point(left, top);
    }
}
