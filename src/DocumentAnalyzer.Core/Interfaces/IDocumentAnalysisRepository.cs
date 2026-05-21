using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.ValueObjects;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IDocumentAnalysisRepository
    {
        Task<DocumentAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentAnalysis>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentAnalysis>> GetByTypeAsync(AnalysisType analysisType, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentAnalysis>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<DocumentAnalysis?> GetLatestByDocumentAndTypeAsync(Guid documentId, AnalysisType analysisType, CancellationToken cancellationToken = default);
        Task<DocumentAnalysis> CreateAsync(DocumentAnalysis analysis, CancellationToken cancellationToken = default);
        Task<DocumentAnalysis> UpdateAsync(DocumentAnalysis analysis, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<DocumentAnalysis>> GetPendingAnalysesAsync(CancellationToken cancellationToken = default);
    }
}
