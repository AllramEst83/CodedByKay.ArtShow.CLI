using SkiaSharp;

namespace CodedByKay.ArtShow.CLI.Imaging;

public static class ThumbnailGenerator
{
    private const int ThumbnailWidth = 600;
    private const int ThumbnailWebpQuality = 75;
    private const int OriginalWebpQuality = 90;

    /// <summary>Resizes to 600w (preserving aspect ratio, never upscaling) and encodes as WebP.</summary>
    public static byte[] CreateThumbnail(Stream sourceImage)
    {
        using var original = Decode(sourceImage);

        if (original.Width <= ThumbnailWidth)
            return Encode(original, ThumbnailWebpQuality);

        var scale = ThumbnailWidth / (float)original.Width;
        var targetHeight = (int)Math.Round(original.Height * scale);
        using var resized = original.Resize(new SKImageInfo(ThumbnailWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("Could not resize image.");
        return Encode(resized, ThumbnailWebpQuality);
    }

    /// <summary>Re-encodes as WebP at full resolution, no resize — for new (non-.webp) uploads via "Add artwork".</summary>
    public static byte[] EncodeOriginalAsWebp(Stream sourceImage)
    {
        using var original = Decode(sourceImage);
        return Encode(original, OriginalWebpQuality);
    }

    // SKBitmap.Decode(Stream) ignores the EXIF orientation tag, so photos with one set (e.g. taken
    // on a phone rather than scanned flat) come out sideways/upside down. Decode via SKCodec instead
    // so we can read EncodedOrigin and bake the correct orientation into the pixels ourselves.
    private static SKBitmap Decode(Stream sourceImage)
    {
        using var data = SKData.Create(sourceImage);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Could not decode source image.");
        var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidOperationException("Could not decode source image.");
        return ApplyExifOrientation(bitmap, codec.EncodedOrigin);
    }

    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return bitmap;

        SKBitmap oriented;
        using (bitmap)
        {
            switch (origin)
            {
                case SKEncodedOrigin.BottomRight: // 180°
                    oriented = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.RightTop: // rotate 90° CW
                    oriented = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Translate(oriented.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.LeftBottom: // rotate 90° CCW
                    oriented = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Translate(0, oriented.Height);
                        canvas.RotateDegrees(-90);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.TopRight: // mirror horizontal
                    oriented = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Scale(-1, 1, bitmap.Width / 2f, 0);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.BottomLeft: // mirror vertical
                    oriented = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.LeftTop: // transpose (mirror horizontal, then rotate 90° CW)
                    oriented = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Translate(oriented.Width, 0);
                        canvas.RotateDegrees(90);
                        canvas.Scale(-1, 1, bitmap.Width / 2f, 0);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                case SKEncodedOrigin.RightBottom: // transverse (mirror horizontal, then rotate 90° CCW)
                    oriented = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var canvas = new SKCanvas(oriented))
                    {
                        canvas.Translate(0, oriented.Height);
                        canvas.RotateDegrees(-90);
                        canvas.Scale(-1, 1, bitmap.Width / 2f, 0);
                        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unhandled EXIF origin.");
            }
        }

        return oriented;
    }

    private static byte[] Encode(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
        return data.ToArray();
    }
}
