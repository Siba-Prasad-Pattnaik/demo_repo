using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;
using DocumentAnalyzer.Core.Interfaces;
using DocumentAnalyzer.Infrastructure.Data;
using DocumentAnalyzer.Infrastructure.Storage;
using DocumentAnalyzer.Infrastructure.External;

namespace DocumentAnalyzer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Repository pattern
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // MinIO Storage
        services.Configure<MinIOConfiguration>(
            configuration.GetSection(MinIOConfiguration.SectionName));

        services.AddSingleton<IMinioClient>(provider =>
        {
            var config = configuration.GetSection(MinIOConfiguration.SectionName).Get<MinIOConfiguration>()
                ?? throw new InvalidOperationException("MinIO configuration not found");

            return new MinioClient()
                .WithEndpoint(config.Endpoint)
                .WithCredentials(config.AccessKey, config.SecretKey)
                .WithSSL(config.UseSSL)
                .Build();
        });

        services.AddScoped<IStorageService, MinIOStorageService>();

        // Redis Cache
        services.Configure<RedisConfiguration>(
            configuration.GetSection(RedisConfiguration.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var config = configuration.GetSection(RedisConfiguration.SectionName).Get<RedisConfiguration>()
                ?? throw new InvalidOperationException("Redis configuration not found");

            return ConnectionMultiplexer.Connect(config.ConnectionString);
        });

        services.AddScoped<ICacheService, RedisCacheService>();

        // Email Service
        services.Configure<EmailConfiguration>(
            configuration.GetSection(EmailConfiguration.SectionName));

        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
