using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.Core.Entities;

public class Document
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FileExtension { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAnalyzedAt { get; set; }

    public int UploadedByUserId { get; set; }

    public string? Description { get; set; }

    public string? Tags { get; set; }

    // Navigation properties
    public virtual User UploadedByUser { get; set; } = null!;
    public virtual ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public virtual ICollection<DocumentAnalysis> Analyses { get; set; } = new List<DocumentAnalysis>();
    public virtual ICollection<DocumentPermission> Permissions { get; set; } = new List<DocumentPermission>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public enum DocumentStatus
{
    Uploaded = 0,
    Processing = 1,
    Analyzed = 2,
    Failed = 3,
    Archived = 4
}
