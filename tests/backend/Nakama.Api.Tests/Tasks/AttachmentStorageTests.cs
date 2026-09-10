using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Nakama.Api.Modules.Tasks;
using Nakama.Api.Modules.Tasks.Infrastructure;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

public sealed class AttachmentStorageTests
{
    [Theory]
    [InlineData("Local", typeof(LocalAttachmentStorage))]
    [InlineData("S3", typeof(S3CompatibleAttachmentStorage))]
    public void Provider_selects_the_expected_attachment_storage(string provider, Type expectedStorageType)
    {
        var services = new ServiceCollection();

        services.AddTasksModule(new AttachmentOptions { Provider = provider });

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAttachmentStorage));
        Assert.Equal(expectedStorageType, registration.ImplementationType);
    }

    [Fact]
    public async Task S3_storage_uses_the_physical_attachment_name_as_the_object_key()
    {
        var client = new FakeS3ObjectStorageClient();
        var storage = CreateS3Storage(client);
        await using var input = new MemoryStream("attachment"u8.ToArray());

        await storage.SaveAsync("c0ffee.pdf", input, CancellationToken.None, "application/pdf");

        Assert.Equal("nakama-attachments", client.BucketName);
        Assert.Equal("c0ffee.pdf", client.Key);
        Assert.Equal("application/pdf", client.ContentType);
        Assert.Equal("attachment", client.Content);
    }

    [Fact]
    public async Task S3_storage_returns_the_authenticated_object_stream()
    {
        var client = new FakeS3ObjectStorageClient { DownloadContent = "download" };
        var storage = CreateS3Storage(client);

        await using var stream = await storage.OpenReadAsync("c0ffee.pdf", CancellationToken.None);
        using var reader = new StreamReader(stream!);

        Assert.Equal("download", await reader.ReadToEndAsync(CancellationToken.None));
        Assert.Equal("c0ffee.pdf", client.Key);
    }

    [Fact]
    public async Task S3_storage_deletes_only_the_requested_object_key()
    {
        var client = new FakeS3ObjectStorageClient();
        var storage = CreateS3Storage(client);

        await storage.DeleteAsync("c0ffee.pdf", CancellationToken.None);

        Assert.Equal("nakama-attachments", client.BucketName);
        Assert.Equal("c0ffee.pdf", client.Key);
    }

    [Fact]
    public async Task Local_storage_continues_to_save_open_and_delete_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nakama-attachment-tests-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new AttachmentOptions { StoragePath = root });
            var storage = new LocalAttachmentStorage(options, new TestWebHostEnvironment());
            await using var input = new MemoryStream("local"u8.ToArray());
            await storage.SaveAsync("c0ffee.txt", input, CancellationToken.None, "text/plain");

            await using (var output = await storage.OpenReadAsync("c0ffee.txt", CancellationToken.None))
            {
                using var reader = new StreamReader(output!);
                Assert.Equal("local", await reader.ReadToEndAsync(CancellationToken.None));
            }

            await storage.DeleteAsync("c0ffee.txt", CancellationToken.None);
            Assert.Null(await storage.OpenReadAsync("c0ffee.txt", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static S3CompatibleAttachmentStorage CreateS3Storage(FakeS3ObjectStorageClient client) =>
        new(Options.Create(new AttachmentOptions
        {
            Provider = "S3",
            S3 = new S3AttachmentOptions { BucketName = "nakama-attachments" }
        }), client);

    private sealed class FakeS3ObjectStorageClient : IS3ObjectStorageClient
    {
        public string? BucketName { get; private set; }
        public string? Key { get; private set; }
        public string? ContentType { get; private set; }
        public string? Content { get; private set; }
        public string? DownloadContent { get; init; }

        public async Task PutAsync(string bucketName, string key, Stream content, string? contentType, CancellationToken ct)
        {
            BucketName = bucketName;
            Key = key;
            ContentType = contentType;
            using var reader = new StreamReader(content, leaveOpen: true);
            Content = await reader.ReadToEndAsync(ct);
        }

        public Task<Stream?> GetAsync(string bucketName, string key, CancellationToken ct)
        {
            BucketName = bucketName;
            Key = key;
            return Task.FromResult<Stream?>(DownloadContent is null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(DownloadContent)));
        }

        public Task DeleteAsync(string bucketName, string key, CancellationToken ct)
        {
            BucketName = bucketName;
            Key = key;
            return Task.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Nakama.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
