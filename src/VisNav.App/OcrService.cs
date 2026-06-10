using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace VisNav.App;

/// <summary>
/// Recognizes text in a screen region using the built-in Windows OCR engine
/// (Windows.Media.Ocr) — free, offline, the same engine NVDA uses. Captures the region
/// with GDI, hands it to the OCR engine, and returns the recognized text.
/// </summary>
public sealed class OcrService
{
    private readonly OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    /// <summary>False if no OCR language pack is installed.</summary>
    public bool Available => _engine is not null;

    /// <summary>Captures the given physical-pixel screen rectangle and returns its text.</summary>
    public async Task<string> ReadRegionAsync(int x, int y, int width, int height)
    {
        if (_engine is null || width < 4 || height < 4)
            return string.Empty;

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

        return await RecognizeBitmapAsync(bmp);
    }

    /// <summary>Runs OCR on an in-memory bitmap (shared by the region reader and tests).</summary>
    public async Task<string> RecognizeBitmapAsync(Bitmap bmp)
    {
        if (_engine is null)
            return string.Empty;

        byte[] bmpBytes;
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Bmp);
            bmpBytes = ms.ToArray();
        }

        var ras = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(ras))
            {
                writer.WriteBytes(bmpBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras);
            using var software = await decoder.GetSoftwareBitmapAsync();
            using var bgra = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var result = await _engine.RecognizeAsync(bgra);
            return NormalizeLines(result);
        }
        finally
        {
            ras.Dispose();
        }
    }

    /// <summary>Joins OCR lines with spaces (a single readable passage for TTS).</summary>
    private static string NormalizeLines(OcrResult result)
    {
        if (result.Lines.Count == 0)
            return result.Text ?? string.Empty;
        return string.Join(" ", result.Lines.Select(l => l.Text));
    }
}
