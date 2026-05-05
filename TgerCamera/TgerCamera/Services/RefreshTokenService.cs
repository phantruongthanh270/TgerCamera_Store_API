using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Models;

namespace TgerCamera.Services;

/// <summary>
/// Phần triển khai của refresh token service.
/// Quản lý việc tạo, validation và revoke refresh tokens.
/// Sẵn sàng cho production với các security best practices.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly TgerCameraContext _context;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly int _refreshTokenExpiryDays;

    public RefreshTokenService(TgerCameraContext context, IConfiguration config, ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _logger = logger;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpireDays"] ?? "7");
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
    {
        _logger.LogInformation($"Creating refresh token for user {userId}");

        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var token = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(token);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Refresh token created for user {userId}");
        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(int userId, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Empty refresh token provided for validation");
            return null;
        }

        var tokenHash = HashToken(token);

        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.TokenHash == tokenHash);

        if (refreshToken == null)
        {
            _logger.LogWarning($"Refresh token not found for user {userId}");
            return null;
        }

        // Kiểm tra token còn hợp lệ hay không (chưa hết hạn, chưa bị revoke)
        if (!refreshToken.IsValid)
        {
            _logger.LogWarning($"Refresh token invalid for user {userId}: Expired={refreshToken.IsExpired}, Revoked={refreshToken.IsRevoked}");
            return null;
        }

        _logger.LogInformation($"Refresh token validated for user {userId}");
        return refreshToken;
    }

    public async Task RevokeRefreshTokenAsync(int userId, string token)
    {
        _logger.LogInformation($"Revoking refresh token for user {userId}");

        var tokenHash = HashToken(token);
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.TokenHash == tokenHash);

        if (refreshToken != null)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Refresh token revoked for user {userId}");
        }
    }

    public async Task RevokeAllRefreshTokensAsync(int userId)
    {
        _logger.LogInformation($"Revoking all refresh tokens for user {userId}");

        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        if (tokens.Count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation($"{tokens.Count} refresh tokens revoked for user {userId}");
        }
    }

    public async Task CleanupExpiredTokensAsync()
    {
        _logger.LogInformation("Starting cleanup of expired refresh tokens");

        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.IsExpired || (rt.IsRevoked && EF.Functions.DateDiffDay(rt.RevokedAt, DateTime.UtcNow) > 30))
            .ToListAsync();

        if (expiredTokens.Any())
        {
            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Cleaned up {expiredTokens.Count} expired/revoked refresh tokens");
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
