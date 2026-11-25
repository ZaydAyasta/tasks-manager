using System.IO.Compression;

namespace Nakama.Api.Modules.Tasks.Features;

internal static class AttachmentFormat
{
    public static async Task<bool> MatchesAsync(Stream stream, string extension, CancellationToken ct)
    {
        if (extension == ".txt") return true;

        var header = new byte[8];
        var bytesRead = await stream.ReadAtLeastAsync(header, header.Length, false, ct);
        stream.Position = 0;
        return extension switch
        {
            ".pdf" => header.AsSpan(0, bytesRead).StartsWith("%PDF-"u8),
            ".png" => header.AsSpan(0, bytesRead).StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => header.AsSpan(0, bytesRead).StartsWith(new byte[] { 255, 216, 255 }),
            ".docx" => IsOfficePackage(stream, "word/document.xml"),
            ".xlsx" => IsOfficePackage(stream, "xl/workbook.xml"),
            _ => false
        };
    }

    private static bool IsOfficePackage(Stream stream, string mainEntry)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return archive.GetEntry("[Content_Types].xml") is not null && archive.GetEntry(mainEntry) is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
