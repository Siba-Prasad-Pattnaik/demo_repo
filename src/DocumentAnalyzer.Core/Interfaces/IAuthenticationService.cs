using DocumentAnalyzer.Core.Entities;

namespace DocumentAnalyzer.Core.Interfaces
{
    public interface IAuthenticationService
    {
        Task<(bool IsValid, User? User)> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default);
        Task<(bool IsValid, Guid? UserId)> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default);
        Task<(bool IsValid, string? NewToken)> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<string> HashPasswordAsync(string password);
        Task<bool> VerifyPasswordAsync(string password, string hash);
        Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken cancellationToken = default);
    }
}
