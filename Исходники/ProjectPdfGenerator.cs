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

        // Версия 1.4: страницы формируются по реально занимаемой высоте,
        // а не по жёсткому количеству строк. Это позволяет обычному КП
        // (в том числе примерно до 15 изделий) оставаться на одном листе A4.
        private const int TableTop = 252;
        private const int TableHeaderHeight = 38;
        private const int RoomRowHeight = 32;
        private const int ItemRowHeight = 38;
        private const int SubtotalRowHeight = 38;
        private const int FooterTop = 1682;
        private const int BottomSafety = 26;
        private const int TotalsGap = 24;

        public static void Generate(string outputPath, ProjectDocument project, decimal cashlessPercent, decimal roundTo)
        {
            if (project == null) throw new ArgumentNullException("project");
            List<ProjectPdfRow> rows = BuildRows(project);
            if (rows.Count == 0) throw new InvalidOperationException("В проекте нет изделий.");

            int totalsHeight = GetTotalsHeight(project);
            List<List<ProjectPdfRow>> pageRowsList = Paginate(rows, totalsHeight);
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

        private static int GetTotalsHeight(ProjectDocument project)
        {
            int lines = 1; // изделия
            if (project.IncludeMeasurement) lines++;
            if (project.CashlessPayment) lines++;
            return 30 + lines * 34 + 48;
        }

        private static int GetRowHeight(ProjectPdfRow row)
        {
            if (row.Kind == RowKind.Room) return RoomRowHeight;
            if (row.Kind == RowKind.Subtotal) return SubtotalRowHeight;
            return ItemRowHeight;
        }

        private static List<List<ProjectPdfRow>> Paginate(IList<ProjectPdfRow> rows, int totalsHeight)
        {
            List<List<ProjectPdfRow>> pages = new List<List<ProjectPdfRow>>();
            int index = 0;
            int tableStart = TableTop + TableHeaderHeight;
            int normalBottom = FooterTop - BottomSafety;

            while (index < rows.Count)
            {
                List<ProjectPdfRow> page = new List<ProjectPdfRow>();
                int used = 0;

                if (index > 0 && rows[index].Kind != RowKind.Room)
                {
                    page.Add(new ProjectPdfRow { Kind = RowKind.Room, Room = rows[index].Room + " (продолжение)" });
                    used += RoomRowHeight;
                }

                while (index < rows.Count)
                {
                    int nextHeight = GetRowHeight(rows[index]);
                    int remainingAfterThis = rows.Skip(index + 1).Sum(GetRowHeight);
                    bool wouldFinish = index == rows.Count - 1;
                    int bottomLimit = wouldFinish ? normalBottom - TotalsGap - totalsHeight : normalBottom;

                    // Если все оставшиеся строки вместе с итогами помещаются на этот лист,
                    // оставляем их здесь и не создаём лишнюю страницу.
                    if (!wouldFinish && used + nextHeight + remainingAfterThis + TotalsGap + totalsHeight <= normalBottom - tableStart)
                        bottomLimit = normalBottom - TotalsGap - totalsHeight;

                    if (tableStart + used + nextHeight > bottomLimit && page.Count > 0)
                        break;

                    page.Add(rows[index]);
                    used += nextHeight;
                    index++;
                }

                pages.Add(page);
            }

            // Последняя проверка: если последняя страница получилась почти пустой,
            // пробуем перенести её строки на предыдущую, если они реально помещаются.
            if (pages.Count > 1)
            {
                List<ProjectPdfRow> last = pages[pages.Count - 1];
                List<ProjectPdfRow> previous = pages[pages.Count - 2];
                int previousHeight = previous.Sum(GetRowHeight);
                int lastHeight = last.Sum(GetRowHeight);
                int available = normalBottom - tableStart - TotalsGap - totalsHeight;
                if (previousHeight + lastHeight <= available)
                {
                    previous.AddRange(last);
                    pages.RemoveAt(pages.Count - 1);
                }
            }

            return pages;
        }

        private static Bitmap RenderPage(ProjectDocument project, IList<ProjectPdfRow> rows, int pageNumber, int pageCount, bool isLast, decimal cashlessPercent, decimal roundTo)
        {
            Bitmap page = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format24bppRgb);
            page.SetResolution(300f, 300f);
            using (Graphics g = Graphics.FromImage(page))
            using (Font titleFont = new Font("Arial", 27f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font nameFont = new Font("Arial", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font sectionFont = new Font("Arial", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font bodyFont = new Font("Arial", 15f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font bodyBold = new Font("Arial", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font smallFont = new Font("Arial", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font totalFont = new Font("Arial", 21f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(29, 38, 50)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(91, 103, 119)))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(42, 111, 218)))
            using (SolidBrush pale = new SolidBrush(Color.FromArgb(237, 244, 255)))
            using (SolidBrush light = new SolidBrush(Color.FromArgb(247, 249, 251)))
            using (Pen line = new Pen(Color.FromArgb(207, 214, 223), 1.25f))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(PixelWidth / (float)DesignWidth, PixelHeight / (float)DesignHeight);

                // Компактная шапка: оставляем максимум полезной площади ведомости.
                g.FillRectangle(blue, 0, 0, DesignWidth, 18);
                g.DrawString("KB911.ru  КОММЕРЧЕСКОЕ ПРЕДЛОЖЕНИЕ", titleFont, dark, 80, 54);
                DrawRight(g, "Дата: " + DateTime.Now.ToString("dd.MM.yyyy"), bodyFont, muted, 1160, 59);

                string projectName = string.IsNullOrWhiteSpace(project.Name) ? "Проект РКД" : project.Name.Trim();
                using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(projectName, nameFont, dark, new RectangleF(82, 105, 800, 30), sf);
                g.DrawString("Разработка рабочей конструкторской документации (РКД)", bodyFont, muted, 82, 139);
                g.DrawLine(line, 80, 181, 1160, 181);

                g.DrawString("СВОДНАЯ ВЕДОМОСТЬ", sectionFont, muted, 82, 211);
                int y = TableTop;
                g.FillRectangle(pale, 80, y, 1080, TableHeaderHeight);
                g.DrawString("ПОМЕЩЕНИЕ", sectionFont, dark, 98, y + 10);
                g.DrawString("ИЗДЕЛИЕ", sectionFont, dark, 398, y + 10);
                DrawRight(g, "СТОИМОСТЬ РКД", sectionFont, dark, 1138, y + 10);
                y += TableHeaderHeight;

                foreach (ProjectPdfRow row in rows)
                {
                    int rowHeight = GetRowHeight(row);
                    if (row.Kind == RowKind.Room)
                    {
                        g.FillRectangle(light, 80, y, 1080, rowHeight);
                        g.DrawString((row.Room ?? string.Empty).ToUpperInvariant(), bodyBold, muted, 98, y + 7);
                    }
                    else
                    {
                        if (row.Kind == RowKind.Subtotal) g.FillRectangle(pale, 80, y, 1080, rowHeight);
                        Font font = row.Kind == RowKind.Subtotal ? bodyBold : bodyFont;
                        if (row.Kind == RowKind.Item)
                        {
                            using (StringFormat roomFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                                g.DrawString(row.Room ?? string.Empty, smallFont, muted, new RectangleF(98, y + 10, 270, 20), roomFormat);
                        }
                        using (StringFormat itemFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                            g.DrawString(row.Item ?? string.Empty, font, dark, new RectangleF(398, y + 9, 520, 22), itemFormat);
                        DrawRight(g, FormatMoney(row.Price), font, dark, 1138, y + 9);
                        g.DrawLine(line, 80, y + rowHeight, 1160, y + rowHeight);
                    }
                    y += rowHeight;
                }

                if (isLast)
                {
                    decimal items = project.Rooms.SelectMany(r => r.Items).Sum(i => i.FinalWorkPrice);
                    decimal measurement = project.IncludeMeasurement ? project.MeasurementFee : 0m;
                    decimal baseTotal = items + measurement;
                    decimal surcharge = project.CashlessPayment ? baseTotal * cashlessPercent / 100m : 0m;
                    decimal total = PricingCalculator.RoundUp(baseTotal + surcharge, roundTo);

                    int totalsHeight = GetTotalsHeight(project);
                    int totalsY = y + TotalsGap;
                    int maxTotalsY = FooterTop - BottomSafety - totalsHeight;
                    totalsY = Math.Min(totalsY, maxTotalsY);

                    // Итоги не прибиваем к низу листа: они следуют сразу за таблицей,
                    // поэтому короткое КП выглядит собранно, а длинное остаётся компактным.
                    g.FillRectangle(pale, 720, totalsY, 440, totalsHeight);
                    int ty = totalsY + 16;
                    DrawTotalLine(g, "Изделия", FormatMoney(items), bodyFont, dark, 742, 1135, ty); ty += 34;
                    if (project.IncludeMeasurement)
                    {
                        DrawTotalLine(g, "Выезд и замер", FormatMoney(measurement), bodyFont, dark, 742, 1135, ty);
                        ty += 34;
                    }
                    if (project.CashlessPayment)
                    {
                        DrawTotalLine(g, "Безналичная оплата +" + cashlessPercent.ToString("0.#") + "%", FormatMoney(surcharge), bodyFont, dark, 742, 1135, ty);
                        ty += 34;
                    }
                    g.DrawLine(line, 742, ty, 1135, ty);
                    ty += 12;
                    g.DrawString("ИТОГО", totalFont, dark, 742, ty);
                    DrawRight(g, FormatMoney(total), totalFont, blue, 1135, ty);
                }

                if (pageCount > 1)
                    g.DrawString("Страница " + pageNumber + " из " + pageCount, smallFont, muted, 80, FooterTop);
                DrawRight(g, "KB911.RU  +7 (903) 105-99-11", smallFont, muted, 1160, FooterTop);
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
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
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
