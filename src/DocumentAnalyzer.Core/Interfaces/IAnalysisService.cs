using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces;

public interface IAnalysisService
{
    Task<DocumentAnalysis> StartAnalysisAsync(int documentId, AnalysisType analysisType, int userId);
    Task<DocumentAnalysis?> GetAnalysisAsync(int analysisId, int userId);
    Task<IEnumerable<DocumentAnalysis>> GetDocumentAnalysesAsync(int documentId, int userId);
    Task<bool> CancelAnalysisAsync(int analysisId, int userId);
    Task ProcessPendingAnalysesAsync();
}
