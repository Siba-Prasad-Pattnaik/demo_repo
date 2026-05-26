using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public AuditLevel Level { get; set; } = AuditLevel.Info;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Document? Document { get; set; }
}

public enum AuditLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
