namespace Marketplace.Infrastructure.Services;

public class MinioOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "marketplace";
    public bool UseSsl { get; set; } = false;
}
