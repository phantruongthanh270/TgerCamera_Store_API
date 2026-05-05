using TgerCamera.Models;
using System.Security.Claims;

namespace TgerCamera.Services;

/// <summary>
/// Service dùng để tạo và quản lý JWT tokens cùng refresh tokens.
/// Tách riêng logic tạo token theo đúng single responsibility.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Tạo JWT access token cho user được cung cấp.
    /// </summary>
    string CreateAccessToken(User user);

    /// <summary>
    /// Tạo refresh token cho user được cung cấp.
    /// Trả về token gốc (trước khi hash) để có thể trả lại cho client.
    /// </summary>
    (string token, string hash) CreateRefreshToken();

    /// <summary>
    /// Validation chữ ký của access token nhưng bỏ qua expiration để refresh flow
    /// có thể trích xuất claims an toàn từ một expired token.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredAccessToken(string accessToken);
}
