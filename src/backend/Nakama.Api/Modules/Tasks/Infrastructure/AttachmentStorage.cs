using Microsoft.Extensions.Options;

namespace Nakama.Api.Modules.Tasks.Infrastructure;

public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";
    public string StoragePath { get; init; } = "App_Data/attachments";
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}

public interface IAttachmentStorage
{
    Task SaveAsync(string storedFileName, Stream content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken ct);
    Task DeleteAsync(string storedFileName, CancellationToken ct);
}

public sealed class LocalAttachmentStorage(IOptions<AttachmentOptions> options, IWebHostEnvironment environment) : IAttachmentStorage
{
    private readonly string root = Path.GetFullPath(Path.IsPathRooted(options.Value.StoragePath) ? options.Value.StoragePath : Path.Combine(environment.ContentRootPath, options.Value.StoragePath));
    private string Resolve(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) || Path.GetFileName(storedFileName) != storedFileName) throw new InvalidOperationException("Invalid attachment storage name.");
        var path = Path.GetFullPath(Path.Combine(root, storedFileName));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid attachment storage path.");
        return path;
    }
    public async Task SaveAsync(string storedFileName, Stream content, CancellationToken ct) { var path = Resolve(storedFileName); Directory.CreateDirectory(root); await using var target = File.Create(path); await content.CopyToAsync(target, ct); }
    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken ct) { var path = Resolve(storedFileName); return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null); }
    public Task DeleteAsync(string storedFileName, CancellationToken ct) { var path = Resolve(storedFileName); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
}
