using TgerCamera.Models;

namespace TgerCamera.Services;

/// <summary>
/// Service dùng để quản lý refresh tokens trong database.
/// Xử lý việc tạo, validation, revoke và cleanup.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Tạo và lưu một refresh token mới cho user.
    /// </summary>
    Task<RefreshToken> CreateRefreshTokenAsync(int userId);

    /// <summary>
    /// Validation và lấy ra refresh token.
    /// Kiểm tra token có hợp lệ hay không (chưa hết hạn, chưa bị revoke, hash khớp).
    /// </summary>
    Task<RefreshToken?> ValidateRefreshTokenAsync(int userId, string token);

    /// <summary>
    /// Revoke một refresh token (đánh dấu đã revoke).
    /// </summary>
    Task RevokeRefreshTokenAsync(int userId, string token);

    /// <summary>
    /// Revoke toàn bộ refresh tokens của một user (logout).
    /// </summary>
    Task RevokeAllRefreshTokensAsync(int userId);

    /// <summary>
    /// Xoá các token đã hết hạn khỏi database.
    /// Nên được gọi định kỳ (ví dụ: bằng background job).
    /// </summary>
    Task CleanupExpiredTokensAsync();
}
