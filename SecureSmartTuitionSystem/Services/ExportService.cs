using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SecureSmartTuitionSystem.Services;

public class ExportService : IExportService
{
    public byte[] ExportToPdf(ExportTable table)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Header().Text(table.Title).FontSize(18).Bold();

                page.Content().Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in table.Columns) columns.RelativeColumn();
                    });

                    t.Header(header =>
                    {
                        foreach (var col in table.Columns)
                        {
                            header.Cell().Border(1).Padding(4).Text(col.Header).Bold();
                        }
                    });

                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            t.Cell().Border(1).Padding(4).Text(cell);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ");
                    x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportToExcel(ExportTable table)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(table.Title.Length > 31 ? table.Title[..31] : table.Title);

        for (var c = 0; c < table.Columns.Count; c++)
        {
            worksheet.Cell(1, c + 1).Value = table.Columns[c].Header;
            worksheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Rows[r].Count; c++)
            {
                worksheet.Cell(r + 2, c + 1).Value = table.Rows[r][c];
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
