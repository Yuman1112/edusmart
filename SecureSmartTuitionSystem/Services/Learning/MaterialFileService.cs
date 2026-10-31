using System.IO.Compression;
using System.Text;
namespace SecureSmartTuitionSystem.Services.Learning;

public sealed record MaterialFile(string Name, string ContentType, byte[] Content);
public static class MaterialFileService
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };
    public static string SafeName(string name)
    {
        name = name.Replace('\\', '/').Split('/').Last();
        name = string.Concat(name.Where(c => !char.IsControl(c) && c is not '"' and not '<' and not '>' and not ':' and not '|' and not '?' and not '*')).Trim();
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext.Length > 16) return "";
        if (name.Length > 180) name = name[..Math.Min(180 - ext.Length, name.Length - ext.Length)] + ext;
        return name;
    }
    public static bool Matches(string extension, byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes) return false;
        extension = extension.ToLowerInvariant();
        if (extension == ".pdf") return bytes.AsSpan().StartsWith(Encoding.ASCII.GetBytes("%PDF-"));
        if (extension == ".png") return bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (extension is ".jpg" or ".jpeg") return bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
        var marker = extension switch { ".docx" => "word/document.xml", ".pptx" => "ppt/presentation.xml", ".xlsx" => "xl/workbook.xml", _ => null };
        if (marker == null) return false;
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            return zip.Entries.Count <= 2048 && zip.GetEntry("[Content_Types].xml") != null && zip.GetEntry(marker) != null
                && !zip.Entries.Any(x => x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException) { return false; }
        catch (IOException) { return false; }
    }
    public static async Task<(MaterialFile? File, string? Error)> ReadAsync(IFormFile file, CancellationToken token)
    {
        if (file.Length <= 0 || file.Length > MaxBytes) return (null, "Choose a non-empty file of up to 10 MB.");
        var name = SafeName(file.FileName);
        if (!Types.TryGetValue(Path.GetExtension(name), out var type)) return (null, "Use PDF, DOCX, PPTX, XLSX, PNG or JPG. Macro-enabled files are not supported.");
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (output.Length + read > MaxBytes) return (null, "The file exceeds 10 MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        var content = output.ToArray();
        if (content.Length != file.Length || !Matches(Path.GetExtension(name), content)) return (null, "The file content does not match its extension, is damaged, or contains unsupported macros.");
        return (new MaterialFile(name, type, content), null);
    }
}
