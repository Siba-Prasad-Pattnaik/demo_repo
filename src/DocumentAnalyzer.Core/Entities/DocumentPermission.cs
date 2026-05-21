namespace DocumentAnalyzer.Core.Entities;

public class DocumentPermission
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int UserId { get; set; }

    public PermissionType Permission { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public int GrantedByUserId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual User GrantedByUser { get; set; } = null!;
}

public enum PermissionType
{
    Read = 1,
    Write = 2,
    Delete = 4,
    Share = 8,
    Admin = 15 // All permissions
}
