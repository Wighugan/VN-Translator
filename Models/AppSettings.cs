using System.Text.Json.Serialization;

namespace VisualNovelDetect.Models;

public class AppSettings
{
    // Activation & Hotkey
    public bool HotkeyCtrl { get; set; } = true;
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyAlt { get; set; } = false;
    public string HotkeyKey { get; set; } = "D";
    public bool EnableMiddleClickTrigger { get; set; } = true;
    public bool IsLookupEnabled { get; set; } = true;

    // Capture & Region
    public ScreenRegion? DialogueRegion { get; set; }
    public int CaptureRadiusX { get; set; } = 500; // Expanded to capture full dialogue box width
    public int CaptureRadiusY { get; set; } = 90;  // Expanded to capture 2-3 lines of dialogue

    // OCR Settings
    public string OcrEngineType { get; set; } = "WindowsOcr"; // "WindowsOcr" or "Tesseract"
    public string OcrLanguageTag { get; set; } = "en-US";
    public bool EnablePreprocessingUpscale { get; set; } = true;
    public bool EnableAdaptiveBinarization { get; set; } = true;
    public string? TesseractDataPath { get; set; }

    // Dictionary Settings (Cambridge)
    public string CambridgeApiKey { get; set; } = string.Empty;
    public string CambridgeBaseUrl { get; set; } = "https://dictionary.cambridge.org/api/v1";
    public string CambridgeDictionaryId { get; set; } = "british";

    // Translation Settings (Google Translate)
    public string GoogleTranslateApiKey { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "vi"; // e.g. Vietnamese, Japanese, etc.
    public bool TranslateContextSentence { get; set; } = true;

    // UI & Appearance
    public string Theme { get; set; } = "Dark"; // "Dark" or "Light"
    public double PopupOpacity { get; set; } = 0.96;
    public int AutoCloseDelaySeconds { get; set; } = 6;
    public bool IsDefaultPinned { get; set; } = false;
    public double PopupWidth { get; set; } = 400;
    public double PopupHeight { get; set; } = 380;
    public double PopupMaxHeight { get; set; } = 600;

    // Positioning
    public bool UseFixedPosition { get; set; } = true;
    public double FixedPositionLeft { get; set; } = -1;
    public double FixedPositionTop { get; set; } = -1;

    public string FormatShortcut()
    {
        var parts = new List<string>();
        if (HotkeyCtrl) parts.Add("Ctrl");
        if (HotkeyAlt) parts.Add("Alt");
        if (HotkeyShift) parts.Add("Shift");
        if (!string.IsNullOrWhiteSpace(HotkeyKey)) parts.Add(HotkeyKey);
        return parts.Count > 0 ? string.Join(" + ", parts) : "None";
    }
}
