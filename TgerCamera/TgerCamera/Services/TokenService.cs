using System;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TgerCamera.Models;

namespace TgerCamera.Services;

/// <summary>
/// Phần triển khai của token service để tạo JWT access token và refresh token.
/// Tuân theo clean architecture với single responsibility principle.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration config, ILogger<TokenService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Tạo JWT access token chứa user claims.
    /// Đây là short-lived token (mặc định 15 phút) cho API requests.
    /// </summary>
    public string CreateAccessToken(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogError("Cannot create access token for user {UserId} without an email", user.Id);
            throw new InvalidOperationException("User email is required to create an access token.");
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            _logger.LogError("Cannot create access token for user {UserId} without a role", user.Id);
            throw new InvalidOperationException("User role is required to create an access token.");
        }

        var jwtKey = _config["Jwt:Key"];
        var jwtIssuer = _config["Jwt:Issuer"];
        var jwtAudience = _config["Jwt:Audience"];
        var expirationMinutes = int.Parse(_config["Jwt:AccessTokenExpireMinutes"] ?? "15");

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            _logger.LogError("Jwt:Key is not configured");
            throw new InvalidOperationException("Jwt:Key is not configured.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FullName", user.FullName ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        _logger.LogInformation($"Access token created for user {user.Id}");
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Tạo refresh token (long-lived, mặc định 7 ngày).
    /// Trả về cả token gốc (cho client) và hash (để lưu trữ).
    /// </summary>
    public (string token, string hash) CreateRefreshToken()
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var token = Convert.ToBase64String(randomBytes);
        var hash = HashToken(token);

        _logger.LogInformation("Refresh token created");
        return (token, hash);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var jwtKey = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            _logger.LogError("Jwt:Key is not configured");
            throw new InvalidOperationException("Jwt:Key is not configured.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _config["Jwt:Issuer"],
            ValidAudience = _config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning(ex, "Failed to validate expired access token");
            return null;
        }
    }

    private static string HashToken(string token)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
