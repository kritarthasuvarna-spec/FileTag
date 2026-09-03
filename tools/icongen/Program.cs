using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Renders the "Bleeding Drop" mark (brand doc: radial-gradient circle +
// soft blurred trailing ellipse beneath it) at each required size, then
// packs them into a single multi-resolution .ico.

const int Canvas = 256; // render everything at high res, downscale for smaller sizes
int[] sizes = { 16, 32, 48, 128, 256 };

Bitmap RenderAt(int canvas)
{
    var bmp = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

    float s = canvas / 96f; // design grid is 96x96

    // Bleed: soft radial falloff approximating the blurred ellipse.
    float bx = 40 * s, by = 78 * s, brx = 34 * s, bry = 14 * s;
    using (var path = new GraphicsPath())
    {
        path.AddEllipse(bx - brx, by - bry, brx * 2, bry * 2);
        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(90, 0x4F, 0x8E, 0xF7),
            SurroundColors = new[] { Color.FromArgb(0, 0x4F, 0x8E, 0xF7) },
            FocusScales = new PointF(0.3f, 0.3f),
        };
        g.FillPath(pgb, path);
    }

    // Primary drop: radial gradient, light blue center to brand Ink blue edge.
    float cx = 48 * s, cy = 44 * s, r = 30 * s;
    using (var path = new GraphicsPath())
    {
        path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(255, 0x8F, 0xB5, 0xFA),
            SurroundColors = new[] { Color.FromArgb(255, 0x4F, 0x8E, 0xF7) },
            CenterPoint = new PointF(cx - r * 0.24f, cy - r * 0.32f),
            FocusScales = new PointF(0.15f, 0.15f),
        };
        g.FillPath(pgb, path);
    }

    return bmp;
}

string outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

var rendered = new Dictionary<int, Bitmap>();
using var master = RenderAt(Canvas);
foreach (int size in sizes)
{
    Bitmap bmp;
    if (size == Canvas) bmp = (Bitmap)master.Clone();
    else
    {
        bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(master, 0, 0, size, size);
    }
    rendered[size] = bmp;
    bmp.Save(Path.Combine(outDir, $"footnote_{size}.png"), ImageFormat.Png);
}

// Pack into a single .ico. Small sizes (<=48) use raw 32bpp DIB data — some
// icon loaders (including the one behind Icon.ExtractAssociatedIcon, used
// for the tray icon) render a PNG-compressed small frame distorted. Large
// sizes (128/256) use PNG, which every Vista+ loader handles fine and which
// keeps the file size sane.
byte[] ToDib(Bitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    // BITMAPINFOHEADER — height is doubled because ICO DIBs include an AND mask.
    bw.Write(40);           // biSize
    bw.Write(w);            // biWidth
    bw.Write(h * 2);        // biHeight (XOR + AND)
    bw.Write((short)1);     // biPlanes
    bw.Write((short)32);    // biBitCount
    bw.Write(0);            // biCompression: BI_RGB
    bw.Write(w * h * 4);    // biSizeImage
    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

    // XOR (color+alpha) plane, bottom-up, BGRA byte order.
    for (int y = h - 1; y >= 0; y--)
        for (int x = 0; x < w; x++)
        {
            var p = bmp.GetPixel(x, y);
            bw.Write(p.B); bw.Write(p.G); bw.Write(p.R); bw.Write(p.A);
        }

    // AND mask: all zero (fully opaque via alpha channel above is enough
    // on modern Windows, but the mask must still be present and padded).
    int maskRowBytes = ((w + 31) / 32) * 4;
    for (int i = 0; i < maskRowBytes * h; i++) bw.Write((byte)0);

    return ms.ToArray();
}

using (var fs = new FileStream(Path.Combine(outDir, "footnote.ico"), FileMode.Create))
using (var bw = new BinaryWriter(fs))
{
    var frames = new List<byte[]>();
    foreach (int size in sizes)
    {
        frames.Add(size <= 48
            ? ToDib(rendered[size])
            : PngBytes(rendered[size]));
    }

    bw.Write((short)0);   // reserved
    bw.Write((short)1);   // type: icon
    bw.Write((short)sizes.Length);

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int size = sizes[i];
        byte b = size >= 256 ? (byte)0 : (byte)size;
        bw.Write(b);              // width (0 = 256)
        bw.Write(b);              // height
        bw.Write((byte)0);        // color palette
        bw.Write((byte)0);        // reserved
        bw.Write((short)1);       // color planes
        bw.Write((short)32);      // bits per pixel
        bw.Write(frames[i].Length);
        bw.Write(offset);
        offset += frames[i].Length;
    }
    foreach (var data in frames) bw.Write(data);
}

byte[] PngBytes(Bitmap bmp)
{
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

Console.WriteLine("done: " + Path.Combine(outDir, "footnote.ico"));
