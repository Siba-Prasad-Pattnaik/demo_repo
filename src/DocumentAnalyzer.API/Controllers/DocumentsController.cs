using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace DocumentAnalyzer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ILogger<DocumentsController> _logger;
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".txt", ".rtf" };
        private const long MaxFileSize = 50 * 1024 * 1024; // 50MB
        private readonly string _uploadPath;

        public DocumentsController(ILogger<DocumentsController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

            // Ensure upload directory exists
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        // GET: api/documents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetDocuments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? filter = null)
        {
            try
            {
                // Input validation
                if (page < 1 || pageSize < 1 || pageSize > 100)
                {
                    return BadRequest("Invalid pagination parameters");
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User identification required");
                }

                // Sanitize search input
                search = SanitizeInput(search);
                filter = SanitizeInput(filter);

                // Mock implementation - replace with actual service
                var documents = new List<DocumentDto>
                {
                    new DocumentDto
                    {
                        Id = Guid.NewGuid(),
                        Name = "Sample Document",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        Size = 1024,
                        MimeType = "application/pdf",
                        Version = 1
                    }
                };

                return Ok(new { Data = documents, Page = page, PageSize = pageSize, Total = documents.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for user {UserId}", GetCurrentUserId());
                return StatusCode(500, "An error occurred while retrieving documents");
            }
        }

        // GET: api/documents/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var userId = GetCurrentUserId();

                // Check permissions
                if (!await HasDocumentPermission(id, userId, "read"))
                {
                    return Forbid("Insufficient permissions to access this document");
                }

                // Mock implementation
                var document = new DocumentDto
                {
                    Id = id,
                    Name = "Sample Document",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Size = 1024,
                    MimeType = "application/pdf",
                    Version = 1
                };

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return StatusCode(500, "An error occurred while retrieving the document");
            }
        }

        // POST: api/documents/upload
        [HttpPost("upload")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<ActionResult<DocumentDto>> UploadDocument(IFormFile file, [FromForm] DocumentMetadataDto metadata)
        {
            try
            {
                // File validation
                var validationResult = ValidateFile(file);
                if (validationResult != null)
                {
                    return BadRequest(validationResult);
                }

                // Metadata validation
                if (metadata != null)
                {
                    var metadataValidation = ValidateMetadata(metadata);
                    if (metadataValidation != null)
                    {
                        return BadRequest(metadataValidation);
                    }
                }

                var userId = GetCurrentUserId();
                var documentId = Guid.NewGuid();
                var sanitizedFileName = SanitizeFileName(file.FileName);
                var filePath = Path.Combine(_uploadPath, $"{documentId}_{sanitizedFileName}");

                // Secure file save
                await SaveFileSecurely(file, filePath);

                var document = new DocumentDto
                {
                    Id = documentId,
                    Name = sanitizedFileName,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Size = file.Length,
                    MimeType = file.ContentType,
                    Version = 1,
                    FilePath = filePath
                };

                _logger.LogInformation("Document {DocumentId} uploaded successfully by user {UserId}", documentId, userId);
                return CreatedAtAction(nameof(GetDocument), new { id = documentId }, document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, "An error occurred while uploading the document");
            }
        }

        // GET: api/documents/{id}/download
        [HttpGet("{id:guid}/download")]
        public async Task<ActionResult> DownloadDocument(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var userId = GetCurrentUserId();

                // Check permissions
                if (!await HasDocumentPermission(id, userId, "read"))
                {
                    return Forbid("Insufficient permissions to download this document");
                }

                // Mock file path - replace with actual service
                var filePath = Path.Combine(_uploadPath, $"{id}_sample.pdf");

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Document file not found");
                }

                // Validate file path to prevent path traversal
                if (!IsPathSafe(filePath))
                {
                    _logger.LogWarning("Potential path traversal attempt for document {DocumentId} by user {UserId}", id, userId);
                    return BadRequest("Invalid file path");
                }

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                var contentType = "application/octet-stream";
                var fileName = Path.GetFileName(filePath);

                return File(memory, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, "An error occurred while downloading the document");
            }
        }

        // PUT: api/documents/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<DocumentDto>> UpdateDocument(Guid id, [FromBody] UpdateDocumentDto updateDto)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = GetCurrentUserId();

                // Check permissions
                if (!await HasDocumentPermission(id, userId, "write"))
                {
                    return Forbid("Insufficient permissions to update this document");
                }

                // Sanitize inputs
                updateDto.Name = SanitizeInput(updateDto.Name);
                updateDto.Description = SanitizeInput(updateDto.Description);

                // Mock update - replace with actual service
                var updatedDocument = new DocumentDto
                {
                    Id = id,
                    Name = updateDto.Name,
                    Description = updateDto.Description,
                    ModifiedBy = userId,
                    ModifiedAt = DateTime.UtcNow,
                    Version = 2
                };

                _logger.LogInformation("Document {DocumentId} updated by user {UserId}", id, userId);
                return Ok(updatedDocument);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", id);
                return StatusCode(500, "An error occurred while updating the document");
            }
        }

        // DELETE: api/documents/{id}
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteDocument(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var userId = GetCurrentUserId();

                // Check permissions
                if (!await HasDocumentPermission(id, userId, "delete"))
                {
                    return Forbid("Insufficient permissions to delete this document");
                }

                // Mock deletion - replace with actual service
                _logger.LogInformation("Document {DocumentId} deleted by user {UserId}", id, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, "An error occurred while deleting the document");
            }
        }

        // GET: api/documents/{id}/versions
        [HttpGet("{id:guid}/versions")]
        public async Task<ActionResult<IEnumerable<DocumentVersionDto>>> GetDocumentVersions(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var userId = GetCurrentUserId();

                if (!await HasDocumentPermission(id, userId, "read"))
                {
                    return Forbid("Insufficient permissions to access document versions");
                }

                // Mock versions
                var versions = new List<DocumentVersionDto>
                {
                    new DocumentVersionDto { Id = Guid.NewGuid(), DocumentId = id, Version = 1, CreatedAt = DateTime.UtcNow.AddDays(-1), CreatedBy = userId },
                    new DocumentVersionDto { Id = Guid.NewGuid(), DocumentId = id, Version = 2, CreatedAt = DateTime.UtcNow, CreatedBy = userId }
                };

                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving versions for document {DocumentId}", id);
                return StatusCode(500, "An error occurred while retrieving document versions");
            }
        }

        // POST: api/documents/{id}/versions
        [HttpPost("{id:guid}/versions")]
        public async Task<ActionResult<DocumentVersionDto>> CreateVersion(Guid id, IFormFile file)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var validationResult = ValidateFile(file);
                if (validationResult != null)
                {
                    return BadRequest(validationResult);
                }

                var userId = GetCurrentUserId();

                if (!await HasDocumentPermission(id, userId, "write"))
                {
                    return Forbid("Insufficient permissions to create document version");
                }

                var versionId = Guid.NewGuid();
                var sanitizedFileName = SanitizeFileName(file.FileName);
                var filePath = Path.Combine(_uploadPath, $"{versionId}_{sanitizedFileName}");

                await SaveFileSecurely(file, filePath);

                var version = new DocumentVersionDto
                {
                    Id = versionId,
                    DocumentId = id,
                    Version = 3,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    FilePath = filePath
                };

                return CreatedAtAction(nameof(GetDocumentVersions), new { id }, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for document {DocumentId}", id);
                return StatusCode(500, "An error occurred while creating the document version");
            }
        }

        // GET: api/documents/{id}/permissions
        [HttpGet("{id:guid}/permissions")]
        [Authorize(Roles = "Admin,Owner")]
        public async Task<ActionResult<IEnumerable<DocumentPermissionDto>>> GetDocumentPermissions(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                var userId = GetCurrentUserId();

                if (!await HasDocumentPermission(id, userId, "admin"))
                {
                    return Forbid("Insufficient permissions to view document permissions");
                }

                // Mock permissions
                var permissions = new List<DocumentPermissionDto>
                {
                    new DocumentPermissionDto { Id = Guid.NewGuid(), DocumentId = id, UserId = userId, Permission = "admin", GrantedBy = userId, GrantedAt = DateTime.UtcNow }
                };

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permissions for document {DocumentId}", id);
                return StatusCode(500, "An error occurred while retrieving document permissions");
            }
        }

        // POST: api/documents/{id}/permissions
        [HttpPost("{id:guid}/permissions")]
        [Authorize(Roles = "Admin,Owner")]
        public async Task<ActionResult<DocumentPermissionDto>> GrantPermission(Guid id, [FromBody] GrantPermissionDto grantDto)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid document ID");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = GetCurrentUserId();

                if (!await HasDocumentPermission(id, userId, "admin"))
                {
                    return Forbid("Insufficient permissions to grant document permissions");
                }

                var permission = new DocumentPermissionDto
                {
                    Id = Guid.NewGuid(),
                    DocumentId = id,
                    UserId = grantDto.UserId,
                    Permission = grantDto.Permission,
                    GrantedBy = userId,
                    GrantedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Permission {Permission} granted to user {TargetUserId} for document {DocumentId} by {UserId}",
                    grantDto.Permission, grantDto.UserId, id, userId);

                return CreatedAtAction(nameof(GetDocumentPermissions), new { id }, permission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting permission for document {DocumentId}", id);
                return StatusCode(500, "An error occurred while granting document permission");
            }
        }

        // Private helper methods for security
        private string? ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "No file uploaded";

            if (file.Length > MaxFileSize)
                return $"File size exceeds maximum limit of {MaxFileSize / (1024 * 1024)}MB";

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                return $"File type not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";

            return null;
        }

        private string? ValidateMetadata(DocumentMetadataDto metadata)
        {
            if (!string.IsNullOrEmpty(metadata.Title) && metadata.Title.Length > 255)
                return "Title cannot exceed 255 characters";

            if (!string.IsNullOrEmpty(metadata.Description) && metadata.Description.Length > 2000)
                return "Description cannot exceed 2000 characters";

            return null;
        }

        private string SanitizeInput(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove potentially dangerous characters
            var sanitized = Regex.Replace(input, @"[<>""'%;()&+]", "");
            return sanitized.Trim();
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());

            // Additional security: remove potentially dangerous patterns
            sanitized = Regex.Replace(sanitized, @"[\.]{2,}", ".");
            sanitized = sanitized.Replace("..", "");

            return sanitized;
        }

        private bool IsPathSafe(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var basePath = Path.GetFullPath(_uploadPath);
            return fullPath.StartsWith(basePath);
        }

        private async Task SaveFileSecurely(IFormFile file, string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await file.CopyToAsync(stream);
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private async Task<bool> HasDocumentPermission(Guid documentId, string userId, string permission)
        {
            // Mock permission check - replace with actual implementation
            return !string.IsNullOrEmpty(userId);
        }
    }

    // DTOs for type safety
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public long Size { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int Version { get; set; }
        public string? FilePath { get; set; }
    }

    public class DocumentMetadataDto
    {
        [MaxLength(255)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public string[]? Tags { get; set; }
    }

    public class UpdateDocumentDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }
    }

    public class DocumentVersionDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? FilePath { get; set; }
    }

    public class DocumentPermissionDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
        public string GrantedBy { get; set; } = string.Empty;
        public DateTime GrantedAt { get; set; }
    }

    public class GrantPermissionDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(read|write|admin)$")]
        public string Permission { get; set; } = string.Empty;
    }
}
