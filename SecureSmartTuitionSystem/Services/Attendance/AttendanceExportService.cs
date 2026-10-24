using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SecureSmartTuitionSystem.ViewModels.Attendance;
namespace SecureSmartTuitionSystem.Services.Attendance;
public static class AttendanceExportService
{
    private static readonly string[] Headers = ["Date", "Time (MYT)", "Class Code", "Class Name", "Tutor", "Student ID", "Student Name", "Attendance", "Lesson Type", "Remark", "Schedule Changed", "Timezone"];
    private static string[] Cells(AttendanceReportRow row) => [row.Date.ToString("yyyy-MM-dd"), row.Start.ToString("HH:mm") + "-" + row.End.ToString("HH:mm"), row.ClassCode, row.ClassName, row.Tutor, row.StudentNumber, row.StudentName, row.Status?.ToString() ?? "Unrecorded", row.Replacement ? "Replacement" : "Regular", row.Remark ?? "", row.ScheduleChanged ? "Yes - check before reporting totals" : "No", "MYT (UTC+8)"];
    public static string CleanXmlText(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (XmlConvert.IsXmlChar(c)) result.Append(c);
            else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            { result.Append(c).Append(text[++i]); }
        }
        return result.ToString();
    }

    public static byte[] Excel(IReadOnlyList<AttendanceReportRow> records)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            TextPart(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            TextPart(zip, "_rels/.rels", """<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            TextPart(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="utf-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets><sheet name="Attendance Records" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            TextPart(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            TextPart(zip, "xl/styles.xml", """<?xml version="1.0" encoding="utf-8"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF008899"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using var xml = XmlWriter.Create(entry.Open(), new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = true });
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            xml.WriteStartDocument(); xml.WriteStartElement("worksheet", ns);
            xml.WriteStartElement("dimension", ns); xml.WriteAttributeString("ref", $"A1:L{records.Count + 1}"); xml.WriteEndElement();
            xml.WriteStartElement("sheetViews", ns); xml.WriteStartElement("sheetView", ns); xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", ns); xml.WriteAttributeString("ySplit", "1"); xml.WriteAttributeString("topLeftCell", "A2"); xml.WriteAttributeString("activePane", "bottomLeft"); xml.WriteAttributeString("state", "frozen"); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
            xml.WriteStartElement("cols", ns);
            int[] widths = [14, 26, 34, 20, 30, 26, 42, 70, 14, 18, 23, 23];
            for (int i = 0; i < widths.Length; i++)
            {
                xml.WriteStartElement("col", ns); xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("customWidth", "1"); xml.WriteEndElement();
            }
            xml.WriteEndElement(); xml.WriteStartElement("sheetData", ns);
            void WriteRow(int number, string[] values, bool header)
            {
                xml.WriteStartElement("row", ns); xml.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < values.Length; i++)
                {
                    xml.WriteStartElement("c", ns); xml.WriteAttributeString("r", $"{(char)('A' + i)}{number}"); xml.WriteAttributeString("t", "inlineStr"); if (header) xml.WriteAttributeString("s", "1");
                    xml.WriteStartElement("is", ns); xml.WriteStartElement("t", ns); xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); xml.WriteString(CleanXmlText(values[i])); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            WriteRow(1, Headers, true);
            for (int i = 0; i < records.Count; i++) WriteRow(i + 2, Cells(records[i]), false);
            xml.WriteEndElement(); xml.WriteStartElement("autoFilter", ns); xml.WriteAttributeString("ref", $"A1:L{records.Count + 1}"); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument();
        }
        return stream.ToArray();
    }

    private static void TextPart(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}