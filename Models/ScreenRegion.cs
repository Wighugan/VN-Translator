namespace VisualNovelDetect.Models;

/// <summary>
/// Screen region definition in physical pixels.
/// </summary>
public record ScreenRegion(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int px, int py)
    {
        return px >= X && px < X + Width && py >= Y && py < Y + Height;
    }
}
