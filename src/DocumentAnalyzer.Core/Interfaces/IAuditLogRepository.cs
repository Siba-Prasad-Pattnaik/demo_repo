using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetByActionAsync(string action, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);
        Task<AuditLog> CreateAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<int> GetCountAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    }
}
