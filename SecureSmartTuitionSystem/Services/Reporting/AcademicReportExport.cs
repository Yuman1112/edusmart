using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SecureSmartTuitionSystem.ViewModels.Reporting;
using SecureSmartTuitionSystem.Services.Attendance;
namespace SecureSmartTuitionSystem.Services.Reporting;
public static class AcademicReportExport
{
    private static string N(decimal? x)=>x?.ToString("0.00",CultureInfo.InvariantCulture)??"N/A";
    public static List<string[]> Rows(AcademicReport r)
    {
        List<string[]> rows=[["EduSmart - Academic report",r.Filter.Year.ToString(CultureInfo.InvariantCulture)],
            ["Subject filter",r.Filter.SubjectId?.ToString()??"All","Class filter",r.Filter.ClassId?.ToString()??"All"],
            ["Published/opened active assessments; fixed publication roster; released grades only; MYT deadlines."],
            ["Equal-weight assessment percentages. Blank grade averages mean no released marks."],
            ["Student performance summary"],["Student ID","Name","Assessments","Submitted","Released grades","Average (%)"]];
        rows.AddRange(r.Students.Select(x=>new[]{x.StudentNumber,x.StudentName,x.Expected.ToString(),x.Submitted.ToString(),x.Graded.ToString(),N(x.Average)}));
        rows.Add(["Assignment submission rate"]);rows.Add(["ID","Assignment","Class code","Class name","Subject","Due (MYT)","Expected","Submitted","On time","Late","Missing","Rate (%)"]);
        rows.AddRange(r.Assignments.Select(x=>new[]{x.Id.ToString(),x.Title,x.ClassCode,x.ClassName,x.Subject,x.Due.ToString("yyyy-MM-dd HH:mm"),x.Expected.ToString(),x.Submitted.ToString(),x.OnTime.ToString(),x.Late.ToString(),x.Missing.ToString(),N(x.Rate)}));
        rows.Add(["Missing counts overdue unsubmitted work only. Rate is submitted / fixed roster; N/A means no roster."]);
        return rows;
    }
    public static byte[] Excel(AcademicReport r)
    {
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            void Part(string name,string text){using var w=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));w.Write(text);}
            Part("[Content_Types].xml","""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Part("_rels/.rels","""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part("xl/workbook.xml","""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Academic report" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Part("xl/_rels/workbook.xml.rels","""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            using var writer=XmlWriter.Create(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(),new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=true});
            const string ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            writer.WriteStartElement("worksheet",ns);writer.WriteStartElement("cols",ns);
            for(int i=1;i<=12;i++){writer.WriteStartElement("col",ns);writer.WriteAttributeString("min",i.ToString());writer.WriteAttributeString("max",i.ToString());writer.WriteAttributeString("width",i is 2 or 4?"45":"23");writer.WriteAttributeString("customWidth","1");writer.WriteEndElement();}writer.WriteEndElement();
            writer.WriteStartElement("sheetData",ns);
            foreach(var row in Rows(r)){writer.WriteStartElement("row",ns);foreach(var cell in row){writer.WriteStartElement("c",ns);writer.WriteAttributeString("t","inlineStr");writer.WriteStartElement("is",ns);writer.WriteElementString("t",ns,AttendanceExportService.CleanXmlText(cell));writer.WriteEndElement();writer.WriteEndElement();}writer.WriteEndElement();}
            writer.WriteEndElement();writer.WriteEndElement();
        }
        return output.ToArray();
    }
    // A printer-friendly, paginated view preserves Unicode with the browser's fonts.
    // Actual PDF downloads below use a standard Courier font and explicitly escape non-ASCII.
    public static byte[] Pdf(AcademicReport r)
    {
        string Ascii(string value)=>string.Concat(value.Select(c=>c is >= ' ' and <= '~'?c.ToString():"\\u"+((int)c).ToString("X4")));
        var lines=new List<string>{"EduSmart Academic Report - "+r.Filter.Year,"Non-ASCII names are written as Unicode escape codes. Excel preserves original text."};
        foreach(var row in Rows(r))
        {
            var line=Ascii(string.Join(" | ",row));
            for(int i=0;i<line.Length;i+=105)lines.Add(line.Substring(i,Math.Min(105,line.Length-i)));
            lines.Add("");
        }
        var pages=lines.Chunk(46).ToArray();var objects=new List<byte[]>();
        byte[] B(string s)=>Encoding.ASCII.GetBytes(s);
        objects.Add(B("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(B($"<< /Type /Pages /Kids [{string.Join(" ",Enumerable.Range(0,pages.Length).Select(i=>$"{4+i*2} 0 R"))}] /Count {pages.Length} >>"));
        objects.Add(B("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));
        for(int i=0;i<pages.Length;i++)
        {
            objects.Add(B($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R >> >> /Contents {5+i*2} 0 R >>"));
            var content=new StringBuilder("BT /F1 9 Tf 30 570 Td ");
            foreach(var line in pages[i].Append($"Page {i+1} / {pages.Length}"))content.Append('(').Append(line.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)")).Append(") Tj 0 -11 Td ");
            content.Append("ET");var bytes=B(content.ToString());objects.Add(B($"<< /Length {bytes.Length} >>\nstream\n"+content+"\nendstream"));
        }
        using var output=new MemoryStream();void Write(string s)=>output.Write(B(s));Write("%PDF-1.4\n");var offsets=new List<long>{0};
        for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);Write($"{i+1} 0 obj\n");output.Write(objects[i]);Write("\nendobj\n");}
        var xref=output.Position;Write($"xref\n0 {objects.Count+1}\n0000000000 65535 f \n");foreach(var offset in offsets.Skip(1))Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");return output.ToArray();
    }
}
