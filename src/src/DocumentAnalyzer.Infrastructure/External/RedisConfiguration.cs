namespace DocumentAnalyzer.Infrastructure.External;

public class RedisConfiguration
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "DocumentAnalyzer";
    public int Database { get; set; } = 0;
    public int DefaultExpirationMinutes { get; set; } = 60;
}
