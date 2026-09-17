using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconBuilder
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    public static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Укажите папку Assets.");
        string assets = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(assets);

        List<byte[]> pngs = new List<byte[]>();
        foreach (int size in Sizes)
        {
            using (Bitmap bitmap = DrawIcon(size))
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                byte[] bytes = stream.ToArray();
                pngs.Add(bytes);
                File.WriteAllBytes(Path.Combine(assets, "digital-ruble-" + size + ".png"), bytes);
            }
        }

        using (Bitmap master = DrawIcon(1024)) master.Save(Path.Combine(assets, "digital-ruble-icon.png"), ImageFormat.Png);
        WriteIco(Path.Combine(assets, "digital-ruble.ico"), pngs);
        CreatePreview(Path.Combine(assets, "digital-ruble-preview.png"));
    }

    private static Bitmap DrawIcon(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96f, 96f);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (SolidBrush black = new SolidBrush(Color.Black))
        using (GraphicsPath ring = new GraphicsPath(FillMode.Alternate))
        using (GraphicsPath glyph = new GraphicsPath())
        using (FontFamily family = new FontFamily("Arial"))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = size <= 24 ? SmoothingMode.AntiAlias : SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            float margin = Math.Max(1f, size * 0.035f);
            float thickness = Math.Max(2f, size * 0.082f);
            RectangleF outer = new RectangleF(margin, margin, size - margin * 2f, size - margin * 2f);
            RectangleF inner = new RectangleF(margin + thickness, margin + thickness, size - (margin + thickness) * 2f, size - (margin + thickness) * 2f);
            ring.AddEllipse(outer);
            ring.AddEllipse(inner);
            g.FillPath(black, ring);

            float emSize = size * 0.58f;
            glyph.AddString("₽", family, (int)FontStyle.Bold, emSize, new PointF(0, 0), StringFormat.GenericTypographic);
            RectangleF bounds = glyph.GetBounds();
            using (Matrix transform = new Matrix())
            {
                float targetCenterX = size * 0.50f;
                float targetCenterY = size * 0.515f;
                transform.Translate(targetCenterX - (bounds.Left + bounds.Width / 2f), targetCenterY - (bounds.Top + bounds.Height / 2f));
                glyph.Transform(transform);
            }
            g.FillPath(black, glyph);
        }
        return bitmap;
    }

    private static void WriteIco(string path, IList<byte[]> images)
    {
        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);
            int offset = 6 + images.Count * 16;
            for (int i = 0; i < images.Count; i++)
            {
                int size = Sizes[i];
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)images[i].Length);
                writer.Write((uint)offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
    }

    private static void CreatePreview(string path)
    {
        using (Bitmap preview = new Bitmap(860, 310, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(preview))
        using (Font label = new Font("Segoe UI", 11f))
        using (SolidBrush text = new SolidBrush(Color.FromArgb(45, 52, 62)))
        {
            g.Clear(Color.White);
            g.FillRectangle(new SolidBrush(Color.FromArgb(239, 242, 246)), 0, 0, 860, 154);
            g.FillRectangle(new SolidBrush(Color.FromArgb(32, 39, 49)), 0, 154, 860, 156);
            int x = 28;
            foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64, 128 })
            {
                using (Bitmap icon = DrawIcon(size))
                {
                    g.DrawImageUnscaled(icon, x + (128 - size) / 2, 14 + (128 - size) / 2);
                    g.DrawImageUnscaled(icon, x + (128 - size) / 2, 168 + (128 - size) / 2);
                }
                g.DrawString(size + " px", label, text, x + 42, 136);
                x += 100;
            }
            preview.Save(path, ImageFormat.Png);
        }
    }
}
