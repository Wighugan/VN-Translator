using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VisualNovelDetect.Services.Capture;

public class ImagePreprocessor : IImagePreprocessor
{
    public Bitmap Preprocess(Bitmap original, bool upscale2x, bool adaptiveBinarize, out double scaleFactor)
    {
        Bitmap source = original;
        scaleFactor = 1.0;

        // 1. Optional 2x Bicubic Upscale for crisp glyphs
        if (upscale2x)
        {
            scaleFactor = 2.0;
            int targetWidth = original.Width * 2;
            int targetHeight = original.Height * 2;

            var upscaled = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(upscaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, targetWidth, targetHeight));
            }
            source = upscaled;
        }

        // If no binarization needed, return copy or upscaled
        if (!adaptiveBinarize)
        {
            return source == original ? (Bitmap)original.Clone() : source;
        }

        // 2. Grayscale & Contrast Processing / Otsu Binarization
        Bitmap processed = ApplyGrayscaleAndBinarization(source);

        if (source != original)
        {
            source.Dispose();
        }

        return processed;
    }

    private static Bitmap ApplyGrayscaleAndBinarization(Bitmap input)
    {
        int width = input.Width;
        int height = input.Height;

        var rect = new Rectangle(0, 0, width, height);
        var srcData = input.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = Math.Abs(srcData.Stride);
        int totalBytes = stride * height;
        byte[] pixelBuffer = new byte[totalBytes];
        Marshal.Copy(srcData.Scan0, pixelBuffer, 0, totalBytes);
        input.UnlockBits(srcData);

        // Convert to grayscale and build luminance histogram
        byte[] gray = new byte[width * height];
        int[] histogram = new int[256];
        long totalLuminance = 0;
        int minLum = 255;
        int maxLum = 0;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            int grayRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = rowOffset + (x * 4);
                byte b = pixelBuffer[pixelOffset];
                byte g = pixelBuffer[pixelOffset + 1];
                byte r = pixelBuffer[pixelOffset + 2];

                // Luminance: 0.299 R + 0.587 G + 0.114 B
                int lum = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                if (lum < 0) lum = 0;
                if (lum > 255) lum = 255;

                gray[grayRow + x] = (byte)lum;
                histogram[lum]++;
                totalLuminance += lum;

                if (lum < minLum) minLum = lum;
                if (lum > maxLum) maxLum = lum;
            }
        }

        int totalPixels = width * height;
        double meanLuminance = totalLuminance / (double)totalPixels;

        // Contrast stretching if range is compressed
        if (maxLum > minLum && (maxLum - minLum) < 180)
        {
            double scale = 255.0 / (maxLum - minLum);
            for (int i = 0; i < gray.Length; i++)
            {
                int stretched = (int)((gray[i] - minLum) * scale);
                gray[i] = (byte)Math.Clamp(stretched, 0, 255);
            }

            // Rebuild histogram for Otsu
            Array.Clear(histogram, 0, histogram.Length);
            for (int i = 0; i < gray.Length; i++)
            {
                histogram[gray[i]]++;
            }
        }

        // Otsu's optimal threshold calculation
        int threshold = CalculateOtsuThreshold(histogram, totalPixels);

        // Determine if text is likely bright on dark or dark on bright
        // Standard VN text boxes are semi-transparent dark with white/bright text (mean < 130).
        // Standard OCR prefers black text on white background.
        bool isBrightTextOnDark = meanLuminance < 135;

        // Build output 32bpp ARGB bitmap
        var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var dstData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] outBuffer = new byte[totalBytes];

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            int grayRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int lum = gray[grayRow + x];
                byte val;

                if (isBrightTextOnDark)
                {
                    // Foreground (bright text) becomes black (0), background becomes white (255) for optimal OCR
                    val = (lum >= threshold) ? (byte)0 : (byte)255;
                }
                else
                {
                    // Dark text on bright background -> dark stays black (0), bright becomes white (255)
                    val = (lum <= threshold) ? (byte)0 : (byte)255;
                }

                int outOffset = rowOffset + (x * 4);
                outBuffer[outOffset] = val;     // Blue
                outBuffer[outOffset + 1] = val; // Green
                outBuffer[outOffset + 2] = val; // Red
                outBuffer[outOffset + 3] = 255; // Alpha
            }
        }

        Marshal.Copy(outBuffer, 0, dstData.Scan0, totalBytes);
        output.UnlockBits(dstData);

        return output;
    }

    private static int CalculateOtsuThreshold(int[] histogram, int total)
    {
        float sum = 0;
        for (int i = 0; i < 256; i++) sum += i * histogram[i];

        float sumB = 0;
        int wB = 0;
        float varMax = 0;
        int threshold = 128;

        for (int t = 0; t < 256; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;

            int wF = total - wB;
            if (wF == 0) break;

            sumB += (float)(t * histogram[t]);

            float mB = sumB / wB;
            float mF = (sum - sumB) / wF;

            float varBetween = (float)wB * (float)wF * (mB - mF) * (mB - mF);

            if (varBetween > varMax)
            {
                varMax = varBetween;
                threshold = t;
            }
        }

        return threshold;
    }
}
