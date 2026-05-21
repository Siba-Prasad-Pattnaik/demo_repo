using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(int? userId, int? documentId, string action, string? details = null, AuditLevel level = AuditLevel.Info);
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(int userId, int pageSize = 50, int page = 1);
    Task<IEnumerable<AuditLog>> GetDocumentAuditLogsAsync(int documentId, int pageSize = 50, int page = 1);
    Task<IEnumerable<AuditLog>> GetSystemAuditLogsAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
