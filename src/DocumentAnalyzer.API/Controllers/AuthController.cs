using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;

    public AuthController(
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger,
        IUserService userService)
    {
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
        _userService = userService;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration details</param>
    /// <returns>User details with JWT tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Invalid request data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            // Check if user already exists
            var existingUser = await _userService.GetUserByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new ErrorResponse { Message = "User with this email already exists" });
            }

            // Validate password strength
            var passwordValidation = ValidatePassword(request.Password);
            if (!passwordValidation.IsValid)
            {
                return BadRequest(new ErrorResponse { Message = passwordValidation.Message });
            }

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.ToLowerInvariant(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
                Role = UserRole.Viewer, // Default role
                CreatedAt = DateTime.UtcNow,
                RefreshTokens = new List<RefreshToken>()
            };

            await _userService.CreateUserAsync(user);

            // Generate tokens
            var (accessToken, refreshToken) = await GenerateTokensAsync(user);

            _logger.LogInformation("User registered successfully: {Email}", request.Email);

            return Ok(new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                Role = user.Role.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration: {Email}", request.Email);
            return StatusCode(500, new ErrorResponse { Message = "Internal server error during registration" });
        }
    }

    /// <summary>
    /// Authenticate user and return JWT tokens
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>User details with JWT tokens</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Invalid request data",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            // Find user by email
            var user = await _userService.GetUserByEmailAsync(request.Email.ToLowerInvariant());
            if (user == null)
            {
                return Unauthorized(new ErrorResponse { Message = "Invalid email or password" });
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
                return Unauthorized(new ErrorResponse { Message = "Invalid email or password" });
            }

            // Generate tokens
            var (accessToken, refreshToken) = await GenerateTokensAsync(user);

            _logger.LogInformation("User logged in successfully: {Email}", request.Email);

            return Ok(new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                Role = user.Role.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login: {Email}", request.Email);
            return StatusCode(500, new ErrorResponse { Message = "Internal server error during login" });
        }
    }

    /// <summary>
    /// Refresh JWT access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new ErrorResponse { Message = "Refresh token is required" });
            }

            var refreshToken = await _userService.GetRefreshTokenAsync(request.RefreshToken);
            if (refreshToken == null || refreshToken.ExpiresAt <= DateTime.UtcNow || refreshToken.IsRevoked)
            {
                return Unauthorized(new ErrorResponse { Message = "Invalid or expired refresh token" });
            }

            var user = await _userService.GetUserByIdAsync(refreshToken.UserId);
            if (user == null)
            {
                return Unauthorized(new ErrorResponse { Message = "User not found" });
            }

            // Revoke old refresh token
            await _userService.RevokeRefreshTokenAsync(refreshToken.Token);

            // Generate new tokens
            var (accessToken, newRefreshToken) = await GenerateTokensAsync(user);

            return Ok(new RefreshResponse
            {
                Token = accessToken,
                RefreshToken = newRefreshToken.Token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new ErrorResponse { Message = "Internal server error during token refresh" });
        }
    }

    /// <summary>
    /// Logout user and revoke refresh token
    /// </summary>
    /// <param name="request">Refresh token to revoke</param>
    /// <returns>Logout confirmation</returns>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
        [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _userService.RevokeRefreshTokenAsync(request.RefreshToken);
            }

            return Ok(new LogoutResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return Ok(new LogoutResponse { Success = true }); // Always return success for logout
        }
    }

    /// <summary>
    /// Get current user profile (requires authentication)
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetUserIdFromToken();
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new ErrorResponse { Message = "User not found" });
            }

            return Ok(new UserProfile
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile");
            return StatusCode(500, new ErrorResponse { Message = "Error retrieving user profile" });
        }
    }

    private async Task<(string accessToken, RefreshToken refreshToken)> GenerateTokensAsync(User user)
    {
        // Generate access token
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generate refresh token
        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _userService.SaveRefreshTokenAsync(refreshToken);

        return (accessToken, refreshToken);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private (bool IsValid, string Message) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password is required");

        if (password.Length < 8)
            return (false, "Password must be at least 8 characters long");

        if (!password.Any(char.IsUpper))
            return (false, "Password must contain at least one uppercase letter");

        if (!password.Any(char.IsLower))
            return (false, "Password must contain at least one lowercase letter");

        if (!password.Any(char.IsDigit))
            return (false, "Password must contain at least one digit");

        return (true, string.Empty);
    }

    private Guid GetUserIdFromToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim!);
    }
}

// DTOs and Models
        [HttpPost("register")]
public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
}

        [HttpPost("login")]
public class LoginRequest
{
    [Required]
    [EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

        [HttpPost("logout")]
public class LogoutRequest
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public class RefreshResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

        [HttpPost("logout")]
public class LogoutResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class UserProfile
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class ErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }
}

// Domain Models
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

public class RefreshToken
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}

public enum UserRole
{
    Viewer = 1,
    Editor = 2,
    Admin = 3
}

// JWT Settings
public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }
}

// Service Interface
public interface IUserService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task CreateUserAsync(User user);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task SaveRefreshTokenAsync(RefreshToken refreshToken);
    Task RevokeRefreshTokenAsync(string token);
}
