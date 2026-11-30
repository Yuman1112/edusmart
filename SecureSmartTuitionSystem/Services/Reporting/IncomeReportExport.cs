using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SecureSmartTuitionSystem.Services.Reporting;

/// <summary>Exports the same selected months used by the dashboard, without formula cells.</summary>
public static class IncomeReportExport
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static string Amount(decimal value) => value.ToString("0.00", Culture);

    public static byte[] Excel(IncomeReport report)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Part(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Part(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Part(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Income\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Part(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            using var writer = XmlWriter.Create(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = true });
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            writer.WriteStartDocument(); writer.WriteStartElement("worksheet", ns);
            writer.WriteStartElement("cols", ns);
            for (int col = 1; col <= 5; col++)
            {
                writer.WriteStartElement("col", ns); writer.WriteAttributeString("min", col.ToString(Culture));
                writer.WriteAttributeString("max", col.ToString(Culture)); writer.WriteAttributeString("width", col == 1 ? "20" : "23");
                writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement();
            }
            writer.WriteEndElement(); writer.WriteStartElement("sheetData", ns);
            void Row(int number, string[] cells, bool numeric = false)
            {
                writer.WriteStartElement("row", ns); writer.WriteAttributeString("r", number.ToString(Culture));
                for (int i = 0; i < cells.Length; i++)
                {
                    writer.WriteStartElement("c", ns); writer.WriteAttributeString("r", $"{(char)('A' + i)}{number}");
                    if (numeric && i > 0) { writer.WriteElementString("v", ns, cells[i]); }
                    else { writer.WriteAttributeString("t", "inlineStr"); writer.WriteStartElement("is", ns); writer.WriteElementString("t", ns, cells[i]); writer.WriteEndElement(); }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            Row(1, ["Month (MYT)", "Gross received (MYR)", "Tracked refunds (MYR)", "Net tracked (MYR)", "Payments"]);
            int row = 2;
            foreach (var item in report.Months)
                Row(row++, [item.Month.ToString("MM-yyyy", Culture), Amount(item.Gross), Amount(item.Refunds), Amount(item.Net), item.PaymentCount.ToString(Culture)], true);
            Row(row, ["Total", Amount(report.Gross), Amount(report.Refunds), Amount(report.Net), report.Months.Sum(x => x.PaymentCount).ToString(Culture)], true);
            Row(row + 2, ["Gross includes successful MYR payments. Net deducts completed refunds recorded in the system. Other refunds and fees are not included."]);
            Row(row + 3, ["Current month figures are month-to-date (Malaysia time)."]);
            writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndDocument();
        }
        return output.ToArray();
    }

    public static byte[] Pdf(IncomeReport report)
    {
        // Fixed ASCII content only. Build a one-page PDF with precise xref byte offsets.
        var content = new StringBuilder("BT /F1 16 Tf 42 800 Td (EduSmart - Income Report) Tj /F1 9 Tf 0 -24 Td ");
        void Line(string value) { content.Append('(').Append(value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")).Append(") Tj 0 -19 Td "); }
        Line($"{report.Months.Count} month(s) ending {report.EndMonth.ToString("MM-yyyy", Culture)} | MYT | Currency: MYR");
        Line("Month       Gross received   Tracked refunds      Net tracked     Payments");
        foreach (var item in report.Months)
            Line($"{item.Month.ToString("MM-yyyy", Culture)} {Amount(item.Gross),15} {Amount(item.Refunds),17} {Amount(item.Net),16} {item.PaymentCount,12}");
        Line($"Total   {Amount(report.Gross),15} {Amount(report.Refunds),17} {Amount(report.Net),16} {report.Months.Sum(x => x.PaymentCount),12}");
        Line("Current month figures are month-to-date (Malaysia time).");
        Line("Gross includes successful MYR payments.");
        Line("Net deducts completed refunds recorded in this system.");
        Line("Other refunds, fees and taxes are not included. This is not an accounting statement.");
        content.Append("ET");
        byte[] stream = Encoding.ASCII.GetBytes(content.ToString());
        using var pdf = new MemoryStream();
        void Write(string text) { byte[] bytes = Encoding.ASCII.GetBytes(text); pdf.Write(bytes); }
        var offsets = new List<long> { 0 };
        void Obj(int id, string body) { offsets.Add(pdf.Position); Write($"{id} 0 obj\n{body}\nendobj\n"); }
        Write("%PDF-1.4\n");
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        offsets.Add(pdf.Position); Write($"5 0 obj\n<< /Length {stream.Length} >>\nstream\n"); pdf.Write(stream); Write("\nendstream\nendobj\n");
        long xref = pdf.Position; Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(offset.ToString("D10", Culture) + " 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    private static void Part(ZipArchive archive, string name, string value)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(value);
    }
}
