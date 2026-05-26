using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace DocumentAnalyzer.Core.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAuditService _auditService;

    public DocumentService(IDocumentRepository documentRepository, IAuditService auditService)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Document> UploadDocumentAsync(string fileName, byte[] content, int userId, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        if (content == null || content.Length == 0)
            throw new ArgumentException("File content cannot be empty", nameof(content));

        var hash = ComputeFileHash(content);
        var existingDoc = await _documentRepository.GetByHashAsync(hash);

        if (existingDoc != null)
            throw new InvalidOperationException("Document with same content already exists");

        var document = new Document
        {
            FileName = fileName,
            FilePath = GenerateFilePath(fileName),
            FileExtension = Path.GetExtension(fileName),
            FileSize = content.Length,
            ContentType = GetContentType(fileName),
            FileHash = hash,
            UploadedByUserId = userId,
            Description = description,
            Status = DocumentStatus.Uploaded
        };

        var result = await _documentRepository.AddAsync(document);
        await _auditService.LogAsync(userId, result.Id, "DocumentUploaded", $"Uploaded: {fileName}");

        return result;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, int userId)
    {
        var hasPermission = await _documentRepository.HasPermissionAsync(documentId, userId, PermissionType.Delete);
        if (!hasPermission)
            return false;

        await _documentRepository.DeleteAsync(documentId);
        await _auditService.LogAsync(userId, documentId, "DocumentDeleted", "Document deleted");

        return true;
    }

    public async Task<IEnumerable<Document>> GetUserDocumentsAsync(int userId)
    {
        return await _documentRepository.GetByUserIdAsync(userId);
    }

    public async Task<Document?> GetDocumentAsync(int documentId, int userId)
    {
        var hasPermission = await _documentRepository.HasPermissionAsync(documentId, userId, PermissionType.Read);
        if (!hasPermission)
            return null;

        return await _documentRepository.GetByIdAsync(documentId);
    }

    public async Task<bool> UpdateDocumentAsync(int documentId, int userId, string? description, string? tags)
    {
        var hasPermission = await _documentRepository.HasPermissionAsync(documentId, userId, PermissionType.Write);
        if (!hasPermission)
            return false;

        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
            return false;

        document.Description = description;
        document.Tags = tags;

        await _documentRepository.UpdateAsync(document);
        await _auditService.LogAsync(userId, documentId, "DocumentUpdated", "Document metadata updated");

        return true;
    }

    public async Task<bool> GrantPermissionAsync(int documentId, int userId, int targetUserId, PermissionType permission)
    {
        var hasPermission = await _documentRepository.HasPermissionAsync(documentId, userId, PermissionType.Admin);
        if (!hasPermission)
            return false;

        // Implementation would involve permission repository
        await _auditService.LogAsync(userId, documentId, "PermissionGranted", $"Granted {permission} to user {targetUserId}");

        return true;
    }

    public async Task<bool> RevokePermissionAsync(int documentId, int userId, int targetUserId)
    {
        var hasPermission = await _documentRepository.HasPermissionAsync(documentId, userId, PermissionType.Admin);
        if (!hasPermission)
            return false;

        // Implementation would involve permission repository
        await _auditService.LogAsync(userId, documentId, "PermissionRevoked", $"Revoked permissions from user {targetUserId}");

        return true;
    }

    public async Task<IEnumerable<Document>> SearchDocumentsAsync(string searchTerm, int userId)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<Document>();

        return await _documentRepository.SearchAsync(searchTerm, userId);
    }

    private static string ComputeFileHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToHexString(hash);
    }

    private static string GenerateFilePath(string fileName)
    {
        var directory = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        return Path.Combine(directory, uniqueFileName);
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
