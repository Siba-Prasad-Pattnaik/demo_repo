using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Core.Services
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IDocumentAnalysisRepository _analysisRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly IAuditService _auditService;

        public AnalysisService(
            IDocumentAnalysisRepository analysisRepository,
            IDocumentRepository documentRepository,
            IAuditService auditService)
        {
            _analysisRepository = analysisRepository;
            _documentRepository = documentRepository;
            _auditService = auditService;
        }

        public async Task<DocumentAnalysis> CreateAnalysisAsync(int documentId, int userId, string analysisType)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new ArgumentException("Document not found", nameof(documentId));

            var analysis = new DocumentAnalysis
            {
                DocumentId = documentId,
                AnalysisType = analysisType,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            var result = await _analysisRepository.AddAsync(analysis);
            await _auditService.LogActionAsync(userId, "AnalysisCreated", $"Analysis created for document {documentId}");

            return result;
        }

        public async Task<DocumentAnalysis> UpdateAnalysisResultAsync(int analysisId, string result, string status)
        {
            var analysis = await _analysisRepository.GetByIdAsync(analysisId);
            if (analysis == null)
                throw new ArgumentException("Analysis not found", nameof(analysisId));

            analysis.Result = result;
            analysis.Status = status;
            analysis.CompletedDate = status == "Completed" ? DateTime.UtcNow : null;

            return await _analysisRepository.UpdateAsync(analysis);
        }

        public async Task<IEnumerable<DocumentAnalysis>> GetDocumentAnalysesAsync(int documentId)
        {
            return await _analysisRepository.GetByDocumentIdAsync(documentId);
        }

        public async Task<DocumentAnalysis> GetAnalysisByIdAsync(int analysisId)
        {
            return await _analysisRepository.GetByIdAsync(analysisId);
        }
    }
}
