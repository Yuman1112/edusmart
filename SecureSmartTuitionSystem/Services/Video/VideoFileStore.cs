using System.Buffers.Binary;
using System.Text;
namespace SecureSmartTuitionSystem.Services.Video;

// Content is never served by StaticFiles; every range request goes through the controller.
public sealed class VideoFileStore
{
    public const long MaxBytes = 209715200;
    public const long RequestBytes = 220200960;
    private readonly string root;
    public VideoFileStore(IWebHostEnvironment env, IConfiguration config)
    {
        var configured = config["VideoStorage:Root"];
        if (!string.IsNullOrWhiteSpace(configured)) root = Path.GetFullPath(configured, env.ContentRootPath);
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrWhiteSpace(home)) throw new IOException("Persistent video storage is not configured.");
            root = Path.Combine(home, "data", "edusmart-videos");
        }
        else root = Path.Combine(env.ContentRootPath, "App_Data", "VideoVault");
        root = Path.GetFullPath(root);
        var publicRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
        if (root.Equals(publicRoot, StringComparison.OrdinalIgnoreCase) || root.StartsWith(publicRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Video storage must be outside the public web root.");
    }
    public static bool ValidKey(string key) => key.Length == 36 && key.EndsWith(".mp4", StringComparison.Ordinal) && Guid.TryParseExact(key[..32], "N", out _);
    private string FilePath(string key) => ValidKey(key) ? Path.Combine(root, key) : throw new IOException("Invalid video key.");
    public FileStream? Open(string key)
    {
        var path = FilePath(key);
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.RandomAccess); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    public void Remove(string key) { try { File.Delete(FilePath(key)); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    public async Task<(string Key, long Size, double Duration)> StoreAsync(IFormFile upload, CancellationToken ct, string? currentVideoKey = null)
    {
        if (upload.Length <= 0 || upload.Length > MaxBytes || !string.Equals(Path.GetExtension(upload.FileName), ".mp4", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose one MP4 video, up to 200 MB.");
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + ".mp4";
        var path = FilePath(key);
        try
        {
            long length = 0;
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            await using (var source = upload.OpenReadStream())
            {
                var buffer = new byte[65536]; int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    length += read;
                    if (length > MaxBytes) throw new InvalidDataException("The video exceeds 200 MB.");
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            if (length != upload.Length) throw new InvalidDataException("The upload was incomplete. Select the video again.");
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var duration = InspectMp4(file);
            if (currentVideoKey != null)
            {
                await using var current = Open(currentVideoKey);
                if (current != null && current.Length == length)
                {
                    file.Position = 0;
                    if (await SameContentAsync(file, current, ct))
                        throw new InvalidDataException("This video is already saved for this lesson. Select a different MP4 or remove the selection to save other changes.");
                }
            }
            return (key, length, duration);
        }
        catch { Remove(key); throw; }
    }
    private static async Task<bool> SameContentAsync(Stream first, Stream second, CancellationToken ct)
    {
        var firstBuffer = new byte[65536];
        var secondBuffer = new byte[firstBuffer.Length];
        var remaining = first.Length;
        while (remaining > 0)
        {
            var count = (int)Math.Min(remaining, firstBuffer.Length);
            await first.ReadExactlyAsync(firstBuffer.AsMemory(0, count), ct);
            await second.ReadExactlyAsync(secondBuffer.AsMemory(0, count), ct);
            if (!firstBuffer.AsSpan(0, count).SequenceEqual(secondBuffer.AsSpan(0, count))) return false;
            remaining -= count;
        }
        return true;
    }

    // Check ISO BMFF boundaries, MP4 file type, movie duration, media data and a video track.
    // This is structural validation, not malware scanning or transcoding.
    public static double InspectMp4(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 40) throw new InvalidDataException("This is not a readable MP4 video.");
        bool ftyp = false, mdat = false, video = false; double duration = 0; int boxes = 0;
        void Scan(long start, long end, int depth)
        {
            long pos = start;
            Span<byte> header = stackalloc byte[16];
            Span<byte> handler = stackalloc byte[4];
            while (pos < end)
            {
                if (++boxes > 50000 || end - pos < 8) throw new InvalidDataException("The MP4 structure is invalid.");
                stream.Position = pos; stream.ReadExactly(header[..8]);
                long size = BinaryPrimitives.ReadUInt32BigEndian(header[..4]); var kind = Encoding.ASCII.GetString(header.Slice(4, 4)); int head = 8;
                if (size == 1) { stream.ReadExactly(header[8..]); var large = BinaryPrimitives.ReadUInt64BigEndian(header[8..]); if (large > long.MaxValue) throw new InvalidDataException("Invalid MP4 box."); size = (long)large; head = 16; }
                if (size == 0) size = end - pos;
                if (size < head || size > end - pos) throw new InvalidDataException("The MP4 upload is damaged or incomplete.");
                long payload = pos + head, bytes = size - head;
                if (depth == 0 && pos == 0 && kind != "ftyp") throw new InvalidDataException("Choose an MP4 video exported for web playback.");
                if (depth == 0 && kind == "ftyp") { if (bytes < 8) throw new InvalidDataException("Invalid MP4 file type."); ftyp = true; }
                if (depth == 0 && kind == "mdat" && bytes > 0) mdat = true;
                if (kind == "mvhd" && depth == 1)
                {
                    if (bytes < 20) throw new InvalidDataException("MP4 duration is missing.");
                    stream.Position = payload; var data = new byte[(int)Math.Min(32, bytes)]; stream.ReadExactly(data);
                    uint scale; ulong ticks;
                    if (data[0] == 0) { scale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12, 4)); ticks = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4)); }
                    else if (data[0] == 1 && data.Length >= 32) { scale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4)); ticks = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(24, 8)); }
                    else throw new InvalidDataException("Unsupported MP4 duration format.");
                    if (scale > 0) duration = (double)ticks / scale;
                }
                if (kind == "hdlr" && depth == 3 && bytes >= 12) { stream.Position = payload + 8; stream.ReadExactly(handler); if (Encoding.ASCII.GetString(handler) == "vide") video = true; }
                if ((depth == 0 && kind == "moov") || (depth == 1 && kind == "trak") || (depth == 2 && kind == "mdia")) Scan(payload, pos + size, depth + 1);
                pos += size;
            }
        }
        Scan(0, stream.Length, 0);
        if (!ftyp || !mdat || !video || !double.IsFinite(duration) || duration <= 0 || duration > 28800)
            throw new InvalidDataException("Choose a complete MP4 video with a valid duration (up to 8 hours). Export as H.264 video with AAC audio for browser playback.");
        return duration;
    }
}
