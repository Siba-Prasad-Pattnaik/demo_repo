using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ExistsAsync(string username, string email);
    Task<IEnumerable<User>> GetActiveUsersAsync();
}
