using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetByUserIdAsync(int userId);
    Task<Document?> GetByHashAsync(string hash);
    Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status);
    Task<IEnumerable<Document>> SearchAsync(string searchTerm, int userId);
    Task<bool> HasPermissionAsync(int documentId, int userId, PermissionType permission);
}
