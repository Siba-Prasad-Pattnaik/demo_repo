using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.Core.Entities;

public class DocumentVersion
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int VersionNumber { get; set; }

    [Required]
    [MaxLength(255)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CreatedByUserId { get; set; }

    public string? ChangeNotes { get; set; }

    public bool IsCurrentVersion { get; set; } = false;

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
