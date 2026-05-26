namespace DocumentAnalyzer.Infrastructure.Storage;

public class MinIOConfiguration
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSSL { get; set; } = false;
    public string BucketName { get; set; } = "documents";
    public string Region { get; set; } = "us-east-1";
}
