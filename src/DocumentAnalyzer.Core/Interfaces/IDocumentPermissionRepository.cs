using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.ValueObjects;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IDocumentPermissionRepository
    {
        Task<DocumentPermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentPermission>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentPermission>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<DocumentPermission?> GetByDocumentAndUserAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentPermission>> GetByPermissionLevelAsync(PermissionLevel permissionLevel, CancellationToken cancellationToken = default);
        Task<DocumentPermission> CreateAsync(DocumentPermission permission, CancellationToken cancellationToken = default);
        Task<DocumentPermission> UpdateAsync(DocumentPermission permission, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> DeleteByDocumentAndUserAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> HasPermissionAsync(Guid documentId, Guid userId, PermissionLevel requiredLevel, CancellationToken cancellationToken = default);
    }
}
