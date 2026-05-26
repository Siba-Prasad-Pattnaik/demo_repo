using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentAnalyzer.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync();

            // Apply pending migrations
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                await context.Database.MigrateAsync();
                logger.LogInformation("Applied pending migrations");
            }

            // Seed initial data if needed
            await SeedDataAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database");
            throw;
        }
    }

    private static async Task SeedDataAsync(ApplicationDbContext context, ILogger logger)
    {
        // Add default tags if they don't exist
        if (!await context.Tags.AnyAsync())
        {
            var defaultTags = new[]
            {
                "Contract",
                "Invoice",
                "Report",
                "Legal",
                "Financial",
                "Technical",
                "Marketing",
                "HR"
            };

            foreach (var tagName in defaultTags)
            {
                context.Tags.Add(new Core.Entities.Tag { Name = tagName });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default tags");
        }
    }
}
