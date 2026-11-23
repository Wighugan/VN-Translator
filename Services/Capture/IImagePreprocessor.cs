using System.Drawing;

namespace VisualNovelDetect.Services.Capture;

public interface IImagePreprocessor
{
    /// <summary>
    /// Preprocesses the captured screen bitmap (optional 2x upscale, grayscale, contrast stretch, adaptive binarization)
    /// to optimize character recognition on stylized VN fonts and semi-transparent backgrounds.
    /// </summary>
    /// <param name="original">Source screen bitmap.</param>
    /// <param name="upscale2x">Whether to upscale 2x for small fonts.</param>
    /// <param name="adaptiveBinarize">Whether to apply contrast stretching and Otsu binarization.</param>
    /// <param name="scaleFactor">Output scale factor (e.g. 2.0 or 1.0) for mapping bounding boxes back.</param>
    /// <returns>Processed bitmap ready for OCR.</returns>
    Bitmap Preprocess(Bitmap original, bool upscale2x, bool adaptiveBinarize, out double scaleFactor);
}
