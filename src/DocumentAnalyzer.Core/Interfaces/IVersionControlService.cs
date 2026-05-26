using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IVersionControlService
    {
        Task<DocumentVersion> CreateVersionAsync(Guid documentId, string filePath, long size, Guid createdById, string? changes = null, CancellationToken cancellationToken = default);
        Task<(bool IsValid, string[] Differences)> CompareVersionsAsync(Guid version1Id, Guid version2Id, CancellationToken cancellationToken = default);
        Task<(bool IsValid, string[] Errors)> ValidateVersionAsync(DocumentVersion version, CancellationToken cancellationToken = default);
        Task<DocumentVersion?> GetLatestVersionAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentVersion>> GetVersionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<bool> CanCreateVersionAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
        Task<DocumentVersion?> RollbackToVersionAsync(Guid documentId, int versionNumber, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteVersionAsync(Guid versionId, Guid userId, CancellationToken cancellationToken = default);
        Task<long> CalculateVersionSizeImpactAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
