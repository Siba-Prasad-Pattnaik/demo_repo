using Microsoft.EntityFrameworkCore;
using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Analysis> Analyses { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<DocumentTag> DocumentTags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.UploadDate);
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Analysis entity
        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Analyses)
                  .HasForeignKey(e => e.DocumentId);
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure Tag entity
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure DocumentTag many-to-many relationship
        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.HasKey(e => new { e.DocumentId, e.TagId });
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.DocumentTags)
                  .HasForeignKey(e => e.DocumentId);
            entity.HasOne(e => e.Tag)
                  .WithMany(t => t.DocumentTags)
                  .HasForeignKey(e => e.TagId);
        });
    }
}
