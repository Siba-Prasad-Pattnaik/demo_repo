using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace DocumentAnalyzer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ILogger<DocumentsController> _logger;
        private readonly string _uploadPath;

        public DocumentsController(ILogger<DocumentsController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _uploadPath = configuration["FileStorage:UploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            Directory.CreateDirectory(_uploadPath);
        }

        // GET: api/documents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetDocuments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? category = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving documents for user {UserId}, page {Page}, pageSize {PageSize}", userId, page, pageSize);

                // Mock implementation - replace with actual service call
                var documents = GetMockDocuments()
                    .Where(d => string.IsNullOrEmpty(searchTerm) || d.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Where(d => string.IsNullOrEmpty(category) || d.Category == category)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize);

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/documents/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                _logger.LogInformation("Retrieving document {DocumentId} for user {UserId}", id, userId);

                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);
                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                // Check permissions
                if (!HasDocumentPermission(document, userId, "read"))
                {
                    return Forbid("Insufficient permissions to access this document");
                }

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/documents/upload
        [HttpPost("upload")]
        public async Task<ActionResult<DocumentDto>> UploadDocument([FromForm] DocumentUploadRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest("No file provided");
                }

                var userId = GetCurrentUserId();
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest($"File type {fileExtension} is not supported");
                }

                if (request.File.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    return BadRequest("File size exceeds 50MB limit");
                }

                var documentId = Guid.NewGuid();
                var fileName = $"{documentId}{fileExtension}";
                var filePath = Path.Combine(_uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var document = new DocumentDto
                {
                    Id = documentId,
                    Title = request.Title ?? Path.GetFileNameWithoutExtension(request.File.FileName),
                    Description = request.Description,
                    Category = request.Category ?? "General",
                    FileName = request.File.FileName,
                    FilePath = filePath,
                    FileSize = request.File.Length,
                    ContentType = request.File.ContentType,
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow,
                    Version = 1,
                    Status = "Active",
                    Permissions = new List<DocumentPermissionDto>
                    {
                        new DocumentPermissionDto { UserId = userId, Permission = "owner" }
                    }
                };

                _logger.LogInformation("Document {DocumentId} uploaded successfully by user {UserId}", documentId, userId);
                return CreatedAtAction(nameof(GetDocument), new { id = documentId }, document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/documents/{id}/download
        [HttpGet("{id}/download")]
        public async Task<ActionResult> DownloadDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "read"))
                {
                    return Forbid("Insufficient permissions to download this document");
                }

                if (!System.IO.File.Exists(document.FilePath))
                {
                    return NotFound("File not found on disk");
                }

                var memory = new MemoryStream();
                using (var stream = new FileStream(document.FilePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                _logger.LogInformation("Document {DocumentId} downloaded by user {UserId}", id, userId);
                return File(memory, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/documents/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<DocumentDto>> UpdateDocument(Guid id, [FromBody] DocumentUpdateRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "write"))
                {
                    return Forbid("Insufficient permissions to update this document");
                }

                // Update document metadata
                document.Title = request.Title ?? document.Title;
                document.Description = request.Description ?? document.Description;
                document.Category = request.Category ?? document.Category;
                document.ModifiedAt = DateTime.UtcNow;

                _logger.LogInformation("Document {DocumentId} updated by user {UserId}", id, userId);
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/documents/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "delete"))
                {
                    return Forbid("Insufficient permissions to delete this document");
                }

                // Soft delete
                document.Status = "Deleted";
                document.ModifiedAt = DateTime.UtcNow;

                _logger.LogInformation("Document {DocumentId} deleted by user {UserId}", id, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/documents/{id}/versions
        [HttpGet("{id}/versions")]
        public async Task<ActionResult<IEnumerable<DocumentVersionDto>>> GetDocumentVersions(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "read"))
                {
                    return Forbid("Insufficient permissions to access document versions");
                }

                var versions = GetMockVersions(id);
                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving versions for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/documents/{id}/versions
        [HttpPost("{id}/versions")]
        public async Task<ActionResult<DocumentVersionDto>> CreateDocumentVersion(Guid id, [FromForm] DocumentVersionRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest("No file provided for new version");
                }

                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "write"))
                {
                    return Forbid("Insufficient permissions to create document version");
                }

                var versionId = Guid.NewGuid();
                var fileName = $"{versionId}{Path.GetExtension(request.File.FileName)}";
                var filePath = Path.Combine(_uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var version = new DocumentVersionDto
                {
                    Id = versionId,
                    DocumentId = id,
                    Version = document.Version + 1,
                    FilePath = filePath,
                    FileSize = request.File.Length,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Comments = request.Comments
                };

                // Update document version
                document.Version = version.Version;
                document.FilePath = filePath;
                document.FileSize = request.File.Length;
                document.ModifiedAt = DateTime.UtcNow;

                _logger.LogInformation("New version {Version} created for document {DocumentId} by user {UserId}", version.Version, id, userId);
                return CreatedAtAction(nameof(GetDocumentVersions), new { id }, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/documents/{id}/permissions
        [HttpGet("{id}/permissions")]
        public async Task<ActionResult<IEnumerable<DocumentPermissionDto>>> GetDocumentPermissions(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "owner"))
                {
                    return Forbid("Only document owners can view permissions");
                }

                return Ok(document.Permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permissions for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/documents/{id}/permissions
        [HttpPost("{id}/permissions")]
        public async Task<ActionResult<DocumentPermissionDto>> GrantDocumentPermission(Guid id, [FromBody] DocumentPermissionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "owner"))
                {
                    return Forbid("Only document owners can grant permissions");
                }

                var permission = new DocumentPermissionDto
                {
                    UserId = request.UserId,
                    Permission = request.Permission,
                    GrantedBy = userId,
                    GrantedAt = DateTime.UtcNow
                };

                document.Permissions.Add(permission);

                _logger.LogInformation("Permission {Permission} granted to user {TargetUserId} for document {DocumentId} by {UserId}",
                    request.Permission, request.UserId, id, userId);
                return CreatedAtAction(nameof(GetDocumentPermissions), new { id }, permission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting permission for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/documents/{id}/permissions/{userId}
        [HttpDelete("{id}/permissions/{targetUserId}")]
        public async Task<ActionResult> RevokeDocumentPermission(Guid id, string targetUserId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "owner"))
                {
                    return Forbid("Only document owners can revoke permissions");
                }

                var permission = document.Permissions.FirstOrDefault(p => p.UserId == targetUserId);
                if (permission == null)
                {
                    return NotFound("Permission not found");
                }

                if (permission.Permission == "owner")
                {
                    return BadRequest("Cannot revoke owner permission");
                }

                document.Permissions.Remove(permission);

                _logger.LogInformation("Permission revoked for user {TargetUserId} on document {DocumentId} by {UserId}",
                    targetUserId, id, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking permission for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/documents/{id}/metadata
        [HttpGet("{id}/metadata")]
        public async Task<ActionResult<DocumentMetadataDto>> GetDocumentMetadata(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var document = GetMockDocuments().FirstOrDefault(d => d.Id == id);

                if (document == null)
                {
                    return NotFound($"Document with ID {id} not found");
                }

                if (!HasDocumentPermission(document, userId, "read"))
                {
                    return Forbid("Insufficient permissions to access document metadata");
                }

                var metadata = new DocumentMetadataDto
                {
                    Id = document.Id,
                    Title = document.Title,
                    Description = document.Description,
                    Category = document.Category,
                    FileSize = document.FileSize,
                    ContentType = document.ContentType,
                    Version = document.Version,
                    Status = document.Status,
                    UploadedBy = document.UploadedBy,
                    UploadedAt = document.UploadedAt,
                    ModifiedAt = document.ModifiedAt,
                    Tags = document.Tags ?? new List<string>(),
                    Properties = document.Properties ?? new Dictionary<string, string>()
                };

                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metadata for document {DocumentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        }

        private bool HasDocumentPermission(DocumentDto document, string userId, string requiredPermission)
        {
            var userPermission = document.Permissions.FirstOrDefault(p => p.UserId == userId);
            if (userPermission == null) return false;

            return requiredPermission.ToLower() switch
            {
                "read" => true,
                "write" => userPermission.Permission is "write" or "owner",
                "delete" => userPermission.Permission is "owner",
                "owner" => userPermission.Permission is "owner",
                _ => false
            };
        }

        private List<DocumentDto> GetMockDocuments()
        {
            // Mock data - replace with actual data service
            return new List<DocumentDto>
            {
                new DocumentDto
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Title = "Sample Document",
                    Description = "A sample document for testing",
                    Category = "General",
                    FileName = "sample.pdf",
                    FilePath = "/uploads/sample.pdf",
                    FileSize = 1024000,
                    ContentType = "application/pdf",
                    UploadedBy = "user1",
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    Version = 1,
                    Status = "Active",
                    Permissions = new List<DocumentPermissionDto>
                    {
                        new DocumentPermissionDto { UserId = "user1", Permission = "owner" }
                    }
                }
            };
        }

        private List<DocumentVersionDto> GetMockVersions(Guid documentId)
        {
            return new List<DocumentVersionDto>
            {
                new DocumentVersionDto
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    Version = 1,
                    FilePath = "/uploads/sample_v1.pdf",
                    FileSize = 1024000,
                    CreatedBy = "user1",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    Comments = "Initial version"
                }
            };
        }
    }

    // DTOs
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public List<DocumentPermissionDto> Permissions { get; set; } = new();
    }

    public class DocumentUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class DocumentUpdateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }

    public class DocumentVersionDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public int Version { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Comments { get; set; }
    }

    public class DocumentVersionRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? Comments { get; set; }
    }

    public class DocumentPermissionDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty; // read, write, owner
        public string? GrantedBy { get; set; }
        public DateTime? GrantedAt { get; set; }
    }

    public class DocumentPermissionRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
    }

    public class DocumentMetadataDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
    }
}
