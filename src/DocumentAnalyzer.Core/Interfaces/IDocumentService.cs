using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces;

public interface IDocumentService
{
    Task<Document> UploadDocumentAsync(string fileName, byte[] content, int userId, string? description = null);
    Task<bool> DeleteDocumentAsync(int documentId, int userId);
    Task<IEnumerable<Document>> GetUserDocumentsAsync(int userId);
    Task<Document?> GetDocumentAsync(int documentId, int userId);
    Task<bool> UpdateDocumentAsync(int documentId, int userId, string? description, string? tags);
    Task<bool> GrantPermissionAsync(int documentId, int userId, int targetUserId, PermissionType permission);
    Task<bool> RevokePermissionAsync(int documentId, int userId, int targetUserId);
    Task<IEnumerable<Document>> SearchDocumentsAsync(string searchTerm, int userId);
}
