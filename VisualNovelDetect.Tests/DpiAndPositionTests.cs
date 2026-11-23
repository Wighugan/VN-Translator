using System.Windows;
using System.Windows.Input;
using VisualNovelDetect.Models;
using VisualNovelDetect.Native;
using VisualNovelDetect.Services.Hotkey;
using Xunit;

namespace VisualNovelDetect.Tests;

public class DpiAndPositionTests
{
    [Fact]
    public void PhysicalToDipRect_ShouldScaleCorrectly()
    {
        var physical = new Rect(100, 200, 300, 150);
        double dpiScale = 1.5; // 150% scaling

        var dip = DpiHelper.PhysicalToDipRect(physical, dpiScale);

        Assert.Equal(100 / 1.5, dip.X, precision: 3);
        Assert.Equal(200 / 1.5, dip.Y, precision: 3);
        Assert.Equal(300 / 1.5, dip.Width, precision: 3);
        Assert.Equal(150 / 1.5, dip.Height, precision: 3);
    }

    [Fact]
    public void CalculatePopupDipPosition_ShouldClampInsideMonitorBounds()
    {
        var monitorWorkArea = new Rect(0, 0, 1920, 1080);
        var targetWordRect = new Rect(1850, 400, 60, 20); // Near right edge
        var fallbackCursor = new Point(1880, 410);
        double popupWidth = 380;
        double popupHeight = 250;

        var pos = DpiHelper.CalculatePopupDipPosition(
            targetWordRect,
            fallbackCursor,
            popupWidth,
            popupHeight,
            monitorWorkArea);

        // Right side of popup must not exceed monitorWorkArea.Right
        Assert.True(pos.X + popupWidth <= monitorWorkArea.Right);
        Assert.True(pos.X >= monitorWorkArea.Left);
    }

    [Fact]
    public void CalculatePopupDipPosition_ShouldFlipAboveWhenNearBottom()
    {
        var monitorWorkArea = new Rect(0, 0, 1920, 1080);
        var targetWordRect = new Rect(500, 1020, 100, 30); // Near bottom edge
        var fallbackCursor = new Point(550, 1035);
        double popupWidth = 380;
        double popupHeight = 250;

        var pos = DpiHelper.CalculatePopupDipPosition(
            targetWordRect,
            fallbackCursor,
            popupWidth,
            popupHeight,
            monitorWorkArea);

        // When target is at Y=1020 on 1080 work area, placing below would be 1050 + 250 = 1300 > 1080.
        // It must flip above targetWordRect.Top!
        Assert.True(pos.Y < targetWordRect.Top);
        Assert.True(pos.Y >= monitorWorkArea.Top);
    }

    [Fact]
    public void CalculateFixedPopupDipPosition_ShouldDefaultToComfortableUpperRight()
    {
        var monitorWorkArea = new Rect(0, 0, 1920, 1080);
        double popupWidth = 400;
        double popupHeight = 380;

        // When never saved before (-1, -1)
        var pos = DpiHelper.CalculateFixedPopupDipPosition(-1, -1, popupWidth, popupHeight, monitorWorkArea);

        // Must be in upper right quadrant (X > 1000, Y < 200)
        Assert.True(pos.X > 1400);
        Assert.True(pos.Y < 200);
        Assert.True(pos.X + popupWidth <= monitorWorkArea.Right);
    }

    [Fact]
    public void CalculateFixedPopupDipPosition_ShouldClampCustomPositionWithinWorkArea()
    {
        var monitorWorkArea = new Rect(0, 0, 1920, 1080);
        double popupWidth = 400;
        double popupHeight = 380;

        // Custom position placed too far right
        var pos = DpiHelper.CalculateFixedPopupDipPosition(1800, 950, popupWidth, popupHeight, monitorWorkArea);

        Assert.True(pos.X + popupWidth <= monitorWorkArea.Right);
        Assert.True(pos.Y + popupHeight <= monitorWorkArea.Bottom);
    }

    [Theory]
    [InlineData("D", Key.D)]
    [InlineData("F1", Key.F1)]
    [InlineData("Space", Key.Space)]
    [InlineData("0", Key.D0)]
    [InlineData("-", Key.OemMinus)]
    public void KeyStringToVk_ShouldMapKeysAccurately(string keyName, Key expectedKey)
    {
        uint vk = GlobalHookService.KeyStringToVk(keyName);
        uint expectedVk = (uint)KeyInterop.VirtualKeyFromKey(expectedKey);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void AppSettings_FormatShortcut_ShouldReturnAccurateRepresentation()
    {
        var settings = new AppSettings
        {
            HotkeyCtrl = true,
            HotkeyShift = true,
            HotkeyAlt = false,
            HotkeyKey = "D"
        };

        Assert.Equal("Ctrl + Shift + D", settings.FormatShortcut());

        settings.HotkeyShift = false;
        settings.HotkeyAlt = true;
        settings.HotkeyKey = "Q";
        Assert.Equal("Ctrl + Alt + Q", settings.FormatShortcut());
    }
}
