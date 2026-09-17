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
    public static class ProjectPdfGenerator
    {
        private const int DesignWidth = 1240;
        private const int DesignHeight = 1754;
        private const int PixelWidth = 2480;
        private const int PixelHeight = 3508;
        private const int RowsPerPage = 17;

        public static void Generate(string outputPath, ProjectDocument project, decimal cashlessPercent, decimal roundTo)
        {
            if (project == null) throw new ArgumentNullException("project");
            List<ProjectPdfRow> rows = BuildRows(project);
            if (rows.Count == 0) throw new InvalidOperationException("В проекте нет изделий.");

            List<List<ProjectPdfRow>> pageRowsList = Paginate(rows);
            int pageCount = pageRowsList.Count;
            List<byte[]> pages = new List<byte[]>();
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                List<ProjectPdfRow> pageRows = pageRowsList[pageIndex];
                bool isLast = pageIndex == pageCount - 1;
                using (Bitmap page = RenderPage(project, pageRows, pageIndex + 1, pageCount, isLast, cashlessPercent, roundTo))
                {
                    pages.Add(LosslessPdfImage.EncodeRgb(page));
                }
            }
            WritePdf(outputPath, pages, PixelWidth, PixelHeight);
        }

        private static List<ProjectPdfRow> BuildRows(ProjectDocument project)
        {
            List<ProjectPdfRow> rows = new List<ProjectPdfRow>();
            foreach (ProjectRoom room in project.Rooms.Where(r => r.Items != null && r.Items.Count > 0))
            {
                rows.Add(new ProjectPdfRow { Kind = RowKind.Room, Room = room.Name });
                foreach (ProjectItem item in room.Items)
                    rows.Add(new ProjectPdfRow { Kind = RowKind.Item, Room = room.Name, Item = item.Name, Price = item.FinalWorkPrice });
                rows.Add(new ProjectPdfRow { Kind = RowKind.Subtotal, Room = room.Name, Item = "Итого по помещению", Price = room.Items.Sum(i => i.FinalWorkPrice) });
            }
            return rows;
        }

        private static List<List<ProjectPdfRow>> Paginate(IList<ProjectPdfRow> rows)
        {
            List<List<ProjectPdfRow>> pages = new List<List<ProjectPdfRow>>();
            int index = 0;
            while (index < rows.Count)
            {
                List<ProjectPdfRow> page = new List<ProjectPdfRow>();
                if (index > 0 && rows[index].Kind != RowKind.Room)
                    page.Add(new ProjectPdfRow { Kind = RowKind.Room, Room = rows[index].Room + " (продолжение)" });
                while (index < rows.Count && page.Count < RowsPerPage)
                {
                    page.Add(rows[index]);
                    index++;
                }
                pages.Add(page);
            }
            return pages;
        }

        private static Bitmap RenderPage(ProjectDocument project, IList<ProjectPdfRow> rows, int pageNumber, int pageCount, bool isLast, decimal cashlessPercent, decimal roundTo)
        {
            Bitmap page = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format24bppRgb);
            page.SetResolution(300f, 300f);
            using (Graphics g = Graphics.FromImage(page))
            using (Font titleFont = new Font("Arial", 33f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font nameFont = new Font("Arial", 24f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font sectionFont = new Font("Arial", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font bodyFont = new Font("Arial", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font bodyBold = new Font("Arial", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font smallFont = new Font("Arial", 15f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font totalFont = new Font("Arial", 25f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(29, 38, 50)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(91, 103, 119)))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(42, 111, 218)))
            using (SolidBrush pale = new SolidBrush(Color.FromArgb(237, 244, 255)))
            using (SolidBrush light = new SolidBrush(Color.FromArgb(247, 249, 251)))
            using (Pen line = new Pen(Color.FromArgb(207, 214, 223), 1.5f))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(PixelWidth / (float)DesignWidth, PixelHeight / (float)DesignHeight);

                g.FillRectangle(blue, 0, 0, DesignWidth, 24);
                g.DrawString("KB911.ru  КОММЕРЧЕСКОЕ ПРЕДЛОЖЕНИЕ", titleFont, dark, 80, 70);
                g.DrawString("Дата: " + DateTime.Now.ToString("dd.MM.yyyy"), bodyFont, muted, 980, 72);
                g.DrawString(project.Name ?? "Проект РКД", nameFont, dark, 82, 125);
                g.DrawString("Разработка рабочей конструкторской документации (РКД)", bodyFont, muted, 82, 163);
                g.DrawLine(line, 80, 207, 1160, 207);

                g.DrawString("СВОДНАЯ ВЕДОМОСТЬ", sectionFont, muted, 82, 238);
                int y = 278;
                g.FillRectangle(pale, 80, y, 1080, 46);
                g.DrawString("ПОМЕЩЕНИЕ", sectionFont, dark, 98, y + 13);
                g.DrawString("ИЗДЕЛИЕ", sectionFont, dark, 398, y + 13);
                DrawRight(g, "СТОИМОСТЬ РКД", sectionFont, dark, 1138, y + 13);
                y += 46;

                foreach (ProjectPdfRow row in rows)
                {
                    if (row.Kind == RowKind.Room)
                    {
                        g.FillRectangle(light, 80, y, 1080, 46);
                        g.DrawString(row.Room.ToUpperInvariant(), bodyBold, muted, 98, y + 12);
                        y += 46;
                    }
                    else
                    {
                        int rowHeight = 54;
                        if (row.Kind == RowKind.Subtotal) g.FillRectangle(pale, 80, y, 1080, rowHeight);
                        Font font = row.Kind == RowKind.Subtotal ? bodyBold : bodyFont;
                        if (row.Kind == RowKind.Item) g.DrawString(row.Room, smallFont, muted, 98, y + 17);
                        g.DrawString(row.Item, font, dark, 398, y + 15);
                        DrawRight(g, FormatMoney(row.Price), font, dark, 1138, y + 15);
                        g.DrawLine(line, 80, y + rowHeight, 1160, y + rowHeight);
                        y += rowHeight;
                    }
                }

                if (isLast)
                {
                    decimal items = project.Rooms.SelectMany(r => r.Items).Sum(i => i.FinalWorkPrice);
                    decimal measurement = project.IncludeMeasurement ? project.MeasurementFee : 0m;
                    decimal baseTotal = items + measurement;
                    decimal surcharge = project.CashlessPayment ? baseTotal * cashlessPercent / 100m : 0m;
                    decimal total = PricingCalculator.RoundUp(baseTotal + surcharge, roundTo);
                    y = Math.Max(y + 34, 1160);
                    g.FillRectangle(pale, 720, y, 440, project.IncludeMeasurement || project.CashlessPayment ? 190 : 128);
                    int ty = y + 18;
                    DrawTotalLine(g, "Изделия", FormatMoney(items), bodyFont, dark, 742, 1135, ty); ty += 40;
                    if (project.IncludeMeasurement) { DrawTotalLine(g, "Выезд и замер", FormatMoney(measurement), bodyFont, dark, 742, 1135, ty); ty += 40; }
                    if (project.CashlessPayment) { DrawTotalLine(g, "Безналичная оплата +" + cashlessPercent.ToString("0.#") + "%", FormatMoney(surcharge), bodyFont, dark, 742, 1135, ty); ty += 40; }
                    g.DrawLine(line, 742, ty, 1135, ty); ty += 16;
                    g.DrawString("ИТОГО", totalFont, dark, 742, ty);
                    DrawRight(g, FormatMoney(total), totalFont, blue, 1135, ty);
                }

                g.DrawString("Страница " + pageNumber + " из " + pageCount, smallFont, muted, 80, 1690);
                DrawRight(g, "KB911.RU  +7 (903) 105-99-11", smallFont, muted, 1160, 1690);
            }
            return page;
        }

        private static void DrawTotalLine(Graphics g, string label, string value, Font font, Brush brush, float left, float right, float y)
        {
            g.DrawString(label, font, brush, left, y);
            DrawRight(g, value, font, brush, right, y);
        }

        private static void DrawRight(Graphics g, string text, Font font, Brush brush, float right, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, right - size.Width, y);
        }

        private static string FormatMoney(decimal amount)
        {
            return Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", new CultureInfo("ru-RU")) + " ₽";
        }

        private static void WritePdf(string outputPath, IList<byte[]> pageImages, int pixelWidth, int pixelHeight)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            int objectCount = 2 + pageImages.Count * 3;
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long[] offsets = new long[objectCount + 1];
                WriteAscii(stream, "%PDF-1.4\n%RKD-PROJECT\n");
                offsets[1] = stream.Position;
                WriteAscii(stream, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                offsets[2] = stream.Position;
                string kids = string.Join(" ", Enumerable.Range(0, pageImages.Count).Select(i => (3 + i * 3) + " 0 R"));
                WriteAscii(stream, "2 0 obj\n<< /Type /Pages /Kids [" + kids + "] /Count " + pageImages.Count + " >>\nendobj\n");

                for (int i = 0; i < pageImages.Count; i++)
                {
                    int pageObject = 3 + i * 3;
                    int contentObject = pageObject + 1;
                    int imageObject = pageObject + 2;
                    offsets[pageObject] = stream.Position;
                    WriteAscii(stream, pageObject + " 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im0 " + imageObject + " 0 R >> >> /Contents " + contentObject + " 0 R >>\nendobj\n");
                    byte[] content = Encoding.ASCII.GetBytes("q\n595 0 0 842 0 0 cm\n/Im0 Do\nQ\n");
                    offsets[contentObject] = stream.Position;
                    WriteAscii(stream, contentObject + " 0 obj\n<< /Length " + content.Length + " >>\nstream\n");
                    stream.Write(content, 0, content.Length); WriteAscii(stream, "endstream\nendobj\n");
                    byte[] imageData = pageImages[i];
                    offsets[imageObject] = stream.Position;
                    WriteAscii(stream, imageObject + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + pixelWidth + " /Height " + pixelHeight + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length " + imageData.Length + " >>\nstream\n");
                    stream.Write(imageData, 0, imageData.Length); WriteAscii(stream, "\nendstream\nendobj\n");
                }

                long xref = stream.Position;
                WriteAscii(stream, "xref\n0 " + (objectCount + 1) + "\n0000000000 65535 f \n");
                for (int i = 1; i <= objectCount; i++) WriteAscii(stream, offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                WriteAscii(stream, "trailer\n<< /Size " + (objectCount + 1) + " /Root 1 0 R >>\nstartxref\n" + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
            }
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value); stream.Write(bytes, 0, bytes.Length);
        }

        private enum RowKind { Room, Item, Subtotal }
        private sealed class ProjectPdfRow
        {
            public RowKind Kind { get; set; }
            public string Room { get; set; }
            public string Item { get; set; }
            public decimal Price { get; set; }
        }
    }
}
