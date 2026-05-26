using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> CanReadDocumentAsync(int userId, int documentId);
        Task<bool> CanWriteDocumentAsync(int userId, int documentId);
        Task<bool> CanDeleteDocumentAsync(int userId, int documentId);
        Task<bool> CanShareDocumentAsync(int userId, int documentId);
        Task<DocumentPermission> GrantPermissionAsync(int documentId, int userId, string permissionType);
        Task RevokePermissionAsync(int documentId, int userId, string permissionType);
        Task<IEnumerable<DocumentPermission>> GetDocumentPermissionsAsync(int documentId);
        Task<IEnumerable<DocumentPermission>> GetUserPermissionsAsync(int userId);
    }
}
