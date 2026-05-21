using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.Core.Entities;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<DocumentPermission> DocumentPermissions { get; set; } = new List<DocumentPermission>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public enum UserRole
{
    User = 0,
    Admin = 1,
    SuperAdmin = 2
}
