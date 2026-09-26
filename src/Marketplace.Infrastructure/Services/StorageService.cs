using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Services;

public interface IStorageService
{
    Task<(string fileName, string contentType)> SaveProductImageAsync(Stream fileStream, string contentType, string originalName, CancellationToken ct = default);
    string GetPublicUrl(string fileName);
    Task<(Stream stream, string contentType)?> OpenReadAsync(string fileName, CancellationToken ct = default);
    Task DeleteAsync(string fileName, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly string _baseUrl;

    public StorageService(IOptions<MinioOptions> options, string baseUrl)
    {
        var value = options.Value;
        _bucket = value.Bucket;
        _baseUrl = baseUrl.TrimEnd('/');
        _client = new MinioClient()
            .WithEndpoint(value.Endpoint)
            .WithCredentials(value.AccessKey, value.SecretKey)
            .WithSSL(value.UseSsl)
            .Build();
    }

    public async Task<(string fileName, string contentType)> SaveProductImageAsync(Stream fileStream, string contentType, string originalName, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        var safeExt = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(safeExt)) safeExt = ".bin";

        var fileName = $"{Guid.NewGuid():N}{safeExt}".ToLowerInvariant();

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        var size = fileStream.CanSeek ? fileStream.Length : -1;

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(fileName)
            .WithStreamData(fileStream)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

        return (fileName, contentType);
    }

    public string GetPublicUrl(string fileName) => $"{_baseUrl}/api/images/{fileName}";

    public async Task<(Stream stream, string contentType)?> OpenReadAsync(string fileName, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        try
        {
            var stat = await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_bucket)
                .WithObject(fileName), ct);

            var ms = new MemoryStream();
            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(fileName)
                .WithCallbackStream(stream => stream.CopyTo(ms)), ct);

            ms.Position = 0;
            var contentType = string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType;
            return (ms, contentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(fileName), ct);
        }
        catch (ObjectNotFoundException)
        {
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), ct);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
        }
    }
}
