using System.Collections.Concurrent;
using Marketplace.Infrastructure.Services;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class InMemoryStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, StoredFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public async Task<(string fileName, string contentType)> SaveProductImageAsync(Stream fileStream, string contentType, string originalName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);

        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var fileName = $"{Guid.NewGuid():N}{extension}".ToLowerInvariant();
        _files[fileName] = new StoredFile(ms.ToArray(), contentType);

        return (fileName, contentType);
    }

    public string GetPublicUrl(string fileName) => $"https://api.test/api/images/{fileName}";

    public Task<(Stream stream, string contentType)?> OpenReadAsync(string fileName, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(fileName, out var file))
        {
            return Task.FromResult<(Stream stream, string contentType)?>(null);
        }

        Stream stream = new MemoryStream(file.Bytes, writable: false);
        return Task.FromResult<(Stream stream, string contentType)?>(new(stream, file.ContentType));
    }

    public Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        _files.TryRemove(fileName, out _);
        return Task.CompletedTask;
    }

    private sealed record StoredFile(byte[] Bytes, string ContentType);
}
