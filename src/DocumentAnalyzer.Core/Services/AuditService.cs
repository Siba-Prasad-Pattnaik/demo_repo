using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Core.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUserRepository _userRepository;

        public AuditService(IAuditLogRepository auditLogRepository, IUserRepository userRepository)
        {
            _auditLogRepository = auditLogRepository;
            _userRepository = userRepository;
        }

        public async Task LogActionAsync(int userId, string action, string details, int? entityId = null, string entityType = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetCurrentIpAddress()
            };

            await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<IEnumerable<AuditLog>> GetUserActivityAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            return await _auditLogRepository.GetByUserIdAsync(userId, fromDate, toDate);
        }

        public async Task<IEnumerable<AuditLog>> GetEntityAuditTrailAsync(string entityType, int entityId)
        {
            return await _auditLogRepository.GetByEntityAsync(entityType, entityId);
        }

        public async Task<IEnumerable<AuditLog>> GetSystemAuditLogsAsync(DateTime fromDate, DateTime toDate, string action = null)
        {
            return await _auditLogRepository.GetByDateRangeAsync(fromDate, toDate, action);
        }

        private string GetCurrentIpAddress()
        {
            // This would be populated by the API layer in a real implementation
            return "Unknown";
        }
    }
}
