using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Services.Tutor;

public sealed class TutorExportService(ApplicationDbContext db)
{
    public async Task<List<TutorExportRowViewModel>> GetRecordsAsync(
        TutorExportFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var records = await db.TutorProfiles.AsNoTracking()
            .OrderBy(p => p.ApplicationUser.UserName)
            .Select(p => new TutorExportRowViewModel
            {
                TutorId = p.ApplicationUser.UserName ?? "",
                FullName = p.FullName,
                Email = p.ApplicationUser.Email ?? "",
                PhoneNumber = p.ApplicationUser.PhoneNumber ?? "",
                Qualification = p.Qualification,
                AreaOfExpertise = p.AreaOfExpertise ?? "",
                TeachingExperience = p.TeachingExperience ?? "",
                IsActive = p.IsActive && p.ApplicationUser.IsActive,
                SetupPending = p.ApplicationUser.MustChangePassword,
                CreatedAtUtc = p.CreatedAt,
                UpdatedAtUtc = p.UpdatedAt,
                TeachingSubjects = p.TeachingSubjects.Select(s => new TutorExportSubjectViewModel
                { Stage = s.EducationStage, Level = s.AcademicLevel, Code = s.SubjectCode }).ToList()
            }).ToListAsync(cancellationToken);
        var filtered = records.Where(row => Matches(row, filter)).ToList();
        foreach (var row in filtered)
        {
            row.TeachingSummary = string.Join(" | ", row.TeachingSubjects
                .GroupBy(s => new { s.Stage, s.Level })
                .OrderBy(g => g.Key.Stage == "Primary" ? 0 : 1)
                .ThenBy(g => int.TryParse(g.Key.Level.Split(' ').LastOrDefault(), out int n) ? n : int.MaxValue)
                .Select(g => $"{g.Key.Stage} - {g.Key.Level}: " + string.Join(", ", g
                    .OrderBy(s => Array.IndexOf(TeachingSubjectCatalog.SubjectDisplayOrder, s.Code))
                    .Select(s => TeachingSubjectCatalog.GetSubjectDisplayName(s.Code)).Distinct())));
        }
        return filtered;
    }

    public static bool Matches(TutorExportRowViewModel row, TutorExportFilterViewModel filter)
    {
        string search = filter.Search?.Trim().ToLowerInvariant() ?? "";
        string haystack = $"{row.TutorId} {row.FullName} {row.Email} {row.AreaOfExpertise}".ToLowerInvariant();
        return haystack.Contains(search)
            && (string.IsNullOrEmpty(filter.Status) || (row.IsActive ? "active" : "inactive") == filter.Status)
            && (string.IsNullOrEmpty(filter.Setup) || (row.SetupPending ? "pending" : "completed") == filter.Setup);
    }

    public static TutorExportViewModel Preview(
        TutorExportFilterViewModel filter, List<TutorExportRowViewModel> rows) => new()
        {
            Search = filter.Search?.Trim(),
            Status = filter.Status,
            Setup = filter.Setup,
            Format = filter.Format,
            TotalRecords = rows.Count,
            PreviewRows = rows.Take(100).ToList()
        };

    private static readonly string[] Headers =
    {
        "Tutor ID", "Full Name", "Current Email", "Phone Number", "Qualification",
        "Area of Expertise", "Teaching Experience", "Teaching Subjects & Levels",
        "Status", "Account Setup", "Created At (MYT)", "Updated At (MYT)"
    };

    private static string Myt(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        .AddHours(8).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string[] Cells(TutorExportRowViewModel row) =>
    [
        row.TutorId, row.FullName, row.Email, row.PhoneNumber, row.Qualification,
        row.AreaOfExpertise, row.TeachingExperience, row.TeachingSummary,
        row.IsActive ? "Active" : "Inactive", row.SetupPending ? "Pending" : "Completed",
        Myt(row.CreatedAtUtc), row.UpdatedAtUtc.HasValue ? Myt(row.UpdatedAtUtc.Value) : ""
    ];

    public static string CsvCell(string? text)
    {
        text ??= "";
        string trimmed = text.TrimStart();
        if ((trimmed.Length > 0 && "=+-@".Contains(trimmed[0]))
            || text.StartsWith('\t') || text.StartsWith('\r') || text.StartsWith('\n'))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public static byte[] Csv(IReadOnlyList<TutorExportRowViewModel> records)
    {
        var content = new StringBuilder();
        content.AppendLine(string.Join(",", Headers.Select(CsvCell)));
        foreach (var row in records) content.AppendLine(string.Join(",", Cells(row).Select(CsvCell)));
        var utf8 = new UTF8Encoding(true);
        return utf8.GetPreamble().Concat(utf8.GetBytes(content.ToString())).ToArray();
    }

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

    public static byte[] Excel(IReadOnlyList<TutorExportRowViewModel> records)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            TextPart(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            TextPart(zip, "_rels/.rels", """<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            TextPart(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="utf-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets><sheet name="Tutor Records" sheetId="1" r:id="rId1"/></sheets></workbook>""");
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
