namespace SecureSmartTuitionSystem.Services;

public record ExportColumn(string Header);
public record ExportTable(string Title, List<ExportColumn> Columns, List<List<string>> Rows);

/// <summary>Report Export use case (Table 4.26): PDF via QuestPDF, Excel via ClosedXML.</summary>
public interface IExportService
{
    byte[] ExportToPdf(ExportTable table);
    byte[] ExportToExcel(ExportTable table);
}
