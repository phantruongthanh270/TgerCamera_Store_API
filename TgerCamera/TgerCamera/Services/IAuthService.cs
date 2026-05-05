using TgerCamera.Dtos;
using TgerCamera.Dtos.Auth;
using TgerCamera.Models;

namespace TgerCamera.Services;

/// <summary>
/// Service xử lý toàn bộ authentication operations.
/// Thiết kế theo clean architecture với các concern được tách riêng qua dependency injection.
/// Hỗ trợ email, Google OAuth và token refresh.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng ký user mới bằng email và password.
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Xác thực user bằng email và password.
    /// Trả về access token và refresh token.
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Xác thực user qua Google OAuth 2.0.
    /// Tạo user nếu chưa tồn tại, trả về access token và refresh token.
    /// </summary>
    Task<AuthResponse> GoogleLoginAsync(string googleIdToken);

    /// <summary>
    /// Làm mới access token bằng một refresh token hợp lệ.
    /// </summary>
    Task<AuthResponse> RefreshAccessTokenAsync(int userId, string refreshToken);

    /// <summary>
    /// Đăng xuất user bằng cách revoke refresh token.
    /// </summary>
    Task LogoutAsync(int userId, string refreshToken);

    /// <summary>
    /// Lấy toàn bộ users theo pagination. Chỉ dành cho Admin.
    /// </summary>
    Task<GetUsersResponse> GetAllUsersAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Lấy một user theo id với thông tin chi tiết. Chỉ dành cho Admin.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Cập nhật thông tin của một user. Chỉ dành cho Admin.
    /// Có thể cập nhật name, phone, address, role và status.
    /// Email không thể thay đổi (immutable).
    /// </summary>
    Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request);
}
