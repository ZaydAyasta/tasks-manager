using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Nakama.Api.Modules.Tasks.Infrastructure;

public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";
    public string Provider { get; init; } = "Local";
    public string StoragePath { get; init; } = "App_Data/attachments";
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public S3AttachmentOptions S3 { get; init; } = new();
}

public sealed class S3AttachmentOptions
{
    public string ServiceUrl { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string Region { get; init; } = "auto";
}

public interface IAttachmentStorage
{
    Task SaveAsync(string storedFileName, Stream content, CancellationToken ct, string? contentType = null);
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
    public async Task SaveAsync(string storedFileName, Stream content, CancellationToken ct, string? contentType = null) { var path = Resolve(storedFileName); Directory.CreateDirectory(root); await using var target = File.Create(path); await content.CopyToAsync(target, ct); }
    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken ct) { var path = Resolve(storedFileName); return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null); }
    public Task DeleteAsync(string storedFileName, CancellationToken ct) { var path = Resolve(storedFileName); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
}

public interface IS3ObjectStorageClient
{
    Task PutAsync(string bucketName, string key, Stream content, string? contentType, CancellationToken ct);
    Task<Stream?> GetAsync(string bucketName, string key, CancellationToken ct);
    Task DeleteAsync(string bucketName, string key, CancellationToken ct);
}

public sealed class AwsS3ObjectStorageClient(IAmazonS3 client) : IS3ObjectStorageClient
{
    public async Task PutAsync(string bucketName, string key, Stream content, string? contentType, CancellationToken ct)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        }, ct);
    }

    public async Task<Stream?> GetAsync(string bucketName, string key, CancellationToken ct)
    {
        try
        {
            var response = await client.GetObjectAsync(bucketName, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NoSuchBucket")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string bucketName, string key, CancellationToken ct)
    {
        await client.DeleteObjectAsync(bucketName, key, ct);
    }
}

public sealed class S3CompatibleAttachmentStorage(IOptions<AttachmentOptions> options, IS3ObjectStorageClient client) : IAttachmentStorage
{
    private readonly S3AttachmentOptions s3 = options.Value.S3;

    public Task SaveAsync(string storedFileName, Stream content, CancellationToken ct, string? contentType = null) =>
        client.PutAsync(s3.BucketName, AttachmentKey(storedFileName), content, contentType, ct);

    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken ct) =>
        client.GetAsync(s3.BucketName, AttachmentKey(storedFileName), ct);

    public Task DeleteAsync(string storedFileName, CancellationToken ct) =>
        client.DeleteAsync(s3.BucketName, AttachmentKey(storedFileName), ct);

    private static string AttachmentKey(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) || Path.GetFileName(storedFileName) != storedFileName)
        {
            throw new InvalidOperationException("Invalid attachment storage name.");
        }

        return storedFileName;
    }
}

public static class S3AttachmentStorageServiceCollectionExtensions
{
    public static IServiceCollection AddS3AttachmentStorage(this IServiceCollection services, AttachmentOptions options)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(options.S3.AccessKeyId, options.S3.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = options.S3.ServiceUrl,
                AuthenticationRegion = options.S3.Region,
                ForcePathStyle = true
            }));
        services.AddScoped<IS3ObjectStorageClient, AwsS3ObjectStorageClient>();
        services.AddScoped<IAttachmentStorage, S3CompatibleAttachmentStorage>();
        return services;
    }
}
