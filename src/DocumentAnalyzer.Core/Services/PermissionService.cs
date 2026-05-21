using DocumentAnalyzer.Core.Entities;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Core.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IDocumentPermissionRepository _permissionRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;

        public PermissionService(
            IDocumentPermissionRepository permissionRepository,
            IDocumentRepository documentRepository,
            IUserRepository userRepository,
            IAuditService auditService)
        {
            _permissionRepository = permissionRepository;
            _documentRepository = documentRepository;
            _userRepository = userRepository;
            _auditService = auditService;
        }

        public async Task<bool> CanReadDocumentAsync(int userId, int documentId)
        {
            // Owner can always read
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document?.CreatedBy == userId)
                return true;

            // Check explicit permissions
            var permission = await _permissionRepository.GetPermissionAsync(documentId, userId, "Read");
            return permission != null;
        }

        public async Task<bool> CanWriteDocumentAsync(int userId, int documentId)
        {
            // Owner can always write
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document?.CreatedBy == userId)
                return true;

            // Check explicit permissions
            var permission = await _permissionRepository.GetPermissionAsync(documentId, userId, "Write");
            return permission != null;
        }

        public async Task<bool> CanDeleteDocumentAsync(int userId, int documentId)
        {
            // Only owner can delete
            var document = await _documentRepository.GetByIdAsync(documentId);
            return document?.CreatedBy == userId;
        }

        public async Task<bool> CanShareDocumentAsync(int userId, int documentId)
        {
            // Owner can always share
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document?.CreatedBy == userId)
                return true;

            // Check explicit share permissions
            var permission = await _permissionRepository.GetPermissionAsync(documentId, userId, "Share");
            return permission != null;
        }

        public async Task<DocumentPermission> GrantPermissionAsync(int documentId, int userId, string permissionType)
        {
            var permission = new DocumentPermission
            {
                DocumentId = documentId,
                UserId = userId,
                PermissionType = permissionType,
                GrantedDate = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _permissionRepository.AddAsync(permission);
            await _auditService.LogActionAsync(userId, "PermissionGranted",
                $"Permission {permissionType} granted for document {documentId} to user {userId}");

            return result;
        }

        public async Task RevokePermissionAsync(int documentId, int userId, string permissionType)
        {
            var permission = await _permissionRepository.GetPermissionAsync(documentId, userId, permissionType);
            if (permission != null)
            {
                permission.IsActive = false;
                await _permissionRepository.UpdateAsync(permission);
                await _auditService.LogActionAsync(userId, "PermissionRevoked",
                    $"Permission {permissionType} revoked for document {documentId} from user {userId}");
            }
        }

        public async Task<IEnumerable<DocumentPermission>> GetDocumentPermissionsAsync(int documentId)
        {
            return await _permissionRepository.GetByDocumentIdAsync(documentId);
        }

        public async Task<IEnumerable<DocumentPermission>> GetUserPermissionsAsync(int userId)
        {
            return await _permissionRepository.GetByUserIdAsync(userId);
        }
    }
}
