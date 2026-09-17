using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RkdEstimator
{
    public static class PdfEstimateGenerator
    {
        private const int DesignWidth = 1240;
        private const int DesignHeight = 1754;
        private const int PixelWidth = 2480;
        private const int PixelHeight = 3508;

        public static void Generate(string outputPath, string calculationName, IList<Image> sketches, decimal workPrice, decimal measurementAmount)
        {
            if (sketches == null) sketches = new List<Image>();
            if (sketches.Count > 3) throw new ArgumentException("В PDF можно добавить не более трёх эскизов.");
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Не указан путь к PDF.");

            using (Bitmap page = RenderPage(calculationName, sketches, workPrice, measurementAmount))
            {
                WritePdf(outputPath, LosslessPdfImage.EncodeRgb(page), page.Width, page.Height);
            }
        }

        private static Bitmap RenderPage(string calculationName, IList<Image> sketches, decimal workPrice, decimal measurementAmount)
        {
            Bitmap page = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format24bppRgb);
            page.SetResolution(300f, 300f);
            using (Graphics g = Graphics.FromImage(page))
            using (Font titleFont = new Font("Arial", 33f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font subtitleFont = new Font("Arial", 17f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font nameFont = new Font("Arial", 22f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font sectionFont = new Font("Arial", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font bodyFont = new Font("Arial", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font priceFont = new Font("Arial", 38f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font totalFont = new Font("Arial", 23f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush dark = new SolidBrush(Color.FromArgb(30, 38, 50)))
            using (Brush muted = new SolidBrush(Color.FromArgb(91, 100, 114)))
            using (Brush blue = new SolidBrush(Color.FromArgb(38, 108, 225)))
            using (Brush paleBlue = new SolidBrush(Color.FromArgb(238, 244, 255)))
            using (Pen line = new Pen(Color.FromArgb(214, 219, 227), 2f))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform((float)PixelWidth / DesignWidth, (float)PixelHeight / DesignHeight);

                g.FillRectangle(blue, 0, 0, DesignWidth, 24);
                g.DrawString("KB911.ru  КОММЕРЧЕСКОЕ ПРЕДЛОЖЕНИЕ", titleFont, dark, 80, 96);
                string safeName = string.IsNullOrWhiteSpace(calculationName) ? "Новый расчёт" : calculationName.Trim();
                using (StringFormat nameFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(safeName, nameFont, dark, new RectangleF(82, 145, 760, 34), nameFormat);
                g.DrawString("Рабочая конструкторская документация", subtitleFont, muted, 82, 181);
                string date = "Дата: " + DateTime.Now.ToString("dd.MM.yyyy", new CultureInfo("ru-RU"));
                SizeF dateSize = g.MeasureString(date, subtitleFont);
                g.DrawString(date, subtitleFont, muted, DesignWidth - 80 - dateSize.Width, 96);
                g.DrawLine(line, 80, 226, DesignWidth - 80, 226);

                int priceTitleY;
                if (sketches.Count > 0)
                {
                    g.DrawString(sketches.Count == 1 ? "ЭСКИЗ ИЗДЕЛИЯ" : "ЭСКИЗЫ ИЗДЕЛИЯ", sectionFont, muted, 80, 256);
                    List<Rectangle> sketchRects = CalculateSketchRects(sketches, new Rectangle(80, 302, DesignWidth - 160, 825), 18);
                    for (int i = 0; i < sketches.Count; i++)
                    {
                        Rectangle frame = sketchRects[i];
                        g.DrawRectangle(line, frame);
                        DrawImageContained(g, sketches[i], new Rectangle(frame.X + 5, frame.Y + 5, frame.Width - 10, frame.Height - 10));
                    }
                    int sketchesBottom = sketchRects.Max(r => r.Bottom);
                    priceTitleY = Math.Min(1290, sketchesBottom + 58);
                }
                else
                {
                    priceTitleY = 286;
                }
                g.DrawString("СТОИМОСТЬ РАБОТ", sectionFont, muted, 80, priceTitleY);
                bool hasMeasurement = measurementAmount > 0m;
                Rectangle priceBlock = new Rectangle(80, priceTitleY + 45, DesignWidth - 160, hasMeasurement ? 270 : 220);
                g.FillRectangle(paleBlue, priceBlock);
                g.DrawString("Разработка рабочей конструкторской документации (РКД)", bodyFont, dark, priceBlock.X + 34, priceBlock.Y + 28);
                g.DrawString("(без учёта выездов на объект и замеров)", subtitleFont, muted, priceBlock.X + 35, priceBlock.Y + 62);
                string workPriceText = FormatMoney(workPrice);
                SizeF workPriceSize = g.MeasureString(workPriceText, priceFont);
                g.DrawString(workPriceText, priceFont, blue, priceBlock.Right - 34 - workPriceSize.Width, priceBlock.Y + 42);
                g.DrawLine(line, priceBlock.X + 34, priceBlock.Y + 112, priceBlock.Right - 34, priceBlock.Y + 112);

                int totalY;
                if (hasMeasurement)
                {
                    g.DrawString("Выезд специалиста на объект и замер", bodyFont, dark, priceBlock.X + 34, priceBlock.Y + 137);
                    string measurementText = FormatMoney(measurementAmount);
                    SizeF measurementSize = g.MeasureString(measurementText, totalFont);
                    g.DrawString(measurementText, totalFont, dark, priceBlock.Right - 34 - measurementSize.Width, priceBlock.Y + 135);
                    g.DrawLine(line, priceBlock.X + 34, priceBlock.Y + 184, priceBlock.Right - 34, priceBlock.Y + 184);
                    totalY = priceBlock.Y + 211;
                }
                else
                {
                    totalY = priceBlock.Y + 146;
                }

                decimal total = workPrice + Math.Max(0m, measurementAmount);
                string totalText = FormatMoney(total);
                g.DrawString("ИТОГО", totalFont, dark, priceBlock.X + 34, totalY);
                SizeF totalSize = g.MeasureString(totalText, totalFont);
                g.DrawString(totalText, totalFont, dark, priceBlock.Right - 34 - totalSize.Width, totalY);

                string footer = "KB911.RU  +7 (903) 105-99-11";
                SizeF footerSize = g.MeasureString(footer, subtitleFont);
                g.DrawString(footer, subtitleFont, muted, DesignWidth - 80 - footerSize.Width, 1690);
            }
            return page;
        }

        private static List<Rectangle> CalculateSketchRects(IList<Image> images, Rectangle area, int gap)
        {
            List<Rectangle> row = BuildRowLayout(images, area, gap);
            List<Rectangle> column = BuildColumnLayout(images, area, gap);
            double rowArea = row.Sum(r => (double)r.Width * r.Height);
            double columnArea = column.Sum(r => (double)r.Width * r.Height);
            return rowArea >= columnArea ? row : column;
        }

        private static List<Rectangle> BuildRowLayout(IList<Image> images, Rectangle area, int gap)
        {
            int slotWidth = (area.Width - gap * (images.Count - 1)) / images.Count;
            List<Rectangle> result = new List<Rectangle>();
            for (int i = 0; i < images.Count; i++)
            {
                Rectangle slot = new Rectangle(area.X + i * (slotWidth + gap), area.Y, slotWidth, area.Height);
                Rectangle fitted = FitRectangle(images[i], slot);
                fitted.Y = area.Y;
                result.Add(fitted);
            }
            return result;
        }

        private static List<Rectangle> BuildColumnLayout(IList<Image> images, Rectangle area, int gap)
        {
            int slotHeight = (area.Height - gap * (images.Count - 1)) / images.Count;
            List<Rectangle> result = new List<Rectangle>();
            for (int i = 0; i < images.Count; i++)
            {
                Rectangle slot = new Rectangle(area.X, area.Y + i * (slotHeight + gap), area.Width, slotHeight);
                Rectangle fitted = FitRectangle(images[i], slot);
                fitted.Y = slot.Y;
                result.Add(fitted);
            }
            return result;
        }

        private static Rectangle FitRectangle(Image image, Rectangle bounds)
        {
            double ratio = Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
            int width = Math.Max(40, (int)Math.Round(image.Width * ratio));
            int height = Math.Max(40, (int)Math.Round(image.Height * ratio));
            return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        }

        private static void DrawImageContained(Graphics g, Image image, Rectangle bounds)
        {
            double ratio = Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * ratio));
            int height = Math.Max(1, (int)Math.Round(image.Height * ratio));
            int x = bounds.X + (bounds.Width - width) / 2;
            int y = bounds.Y + (bounds.Height - height) / 2;
            g.DrawImage(image, new Rectangle(x, y, width, height));
        }

        private static string FormatMoney(decimal amount)
        {
            return Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", new CultureInfo("ru-RU")) + " ₽";
        }

        private static void WritePdf(string outputPath, byte[] imageData, int pixelWidth, int pixelHeight)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                List<long> offsets = new List<long> { 0L };
                WriteAscii(stream, "%PDF-1.4\n%RKD-PDF\n");

                offsets.Add(stream.Position);
                WriteAscii(stream, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                offsets.Add(stream.Position);
                WriteAscii(stream, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

                offsets.Add(stream.Position);
                WriteAscii(stream, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");

                byte[] content = Encoding.ASCII.GetBytes("q\n595 0 0 842 0 0 cm\n/Im0 Do\nQ\n");
                offsets.Add(stream.Position);
                WriteAscii(stream, "4 0 obj\n<< /Length " + content.Length + " >>\nstream\n");
                stream.Write(content, 0, content.Length);
                WriteAscii(stream, "endstream\nendobj\n");

                offsets.Add(stream.Position);
                WriteAscii(stream, "5 0 obj\n<< /Type /XObject /Subtype /Image /Width " + pixelWidth + " /Height " + pixelHeight + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length " + imageData.Length + " >>\nstream\n");
                stream.Write(imageData, 0, imageData.Length);
                WriteAscii(stream, "\nendstream\nendobj\n");

                long xref = stream.Position;
                WriteAscii(stream, "xref\n0 6\n0000000000 65535 f \n");
                for (int i = 1; i <= 5; i++)
                    WriteAscii(stream, offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                WriteAscii(stream, "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
            }
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
