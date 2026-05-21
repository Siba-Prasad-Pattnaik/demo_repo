using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IDocumentVersionRepository
    {
        Task<DocumentVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentVersion>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<DocumentVersion?> GetLatestVersionAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<DocumentVersion?> GetVersionByNumberAsync(Guid documentId, int versionNumber, CancellationToken cancellationToken = default);
        Task<DocumentVersion> CreateAsync(DocumentVersion version, CancellationToken cancellationToken = default);
        Task<DocumentVersion> UpdateAsync(DocumentVersion version, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<int> GetNextVersionNumberAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<long> GetTotalSizeByDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
