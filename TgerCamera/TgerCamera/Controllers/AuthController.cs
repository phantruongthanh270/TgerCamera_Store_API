using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Auth;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

/// <summary>
/// Xử lý các thao tác user authentication.
/// Hỗ trợ email/password, Google OAuth và các endpoint refresh token.
/// Sẵn sàng cho production với clean architecture và xử lý lỗi đầy đủ.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICartService _cartService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly TgerCameraContext _context;

    public AuthController(
        IAuthService authService,
        ICartService cartService,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        TgerCameraContext context)
    {
        _authService = authService;
        _cartService = cartService;
        _tokenService = tokenService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Đăng ký user mới bằng email và password.
    /// Trả về access token, refresh token và thông tin user.
    /// Tự động merge guest cart nếu tồn tại cookie SessionId.
    /// </summary>
    /// <returns>AuthResponse chứa tokens và dữ liệu user</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            _logger.LogInformation($"Registration endpoint: {request.Email}");

            var response = await _authService.RegisterAsync(request);

            // Merge guest cart nếu có
            await MergeGuestCartAsync(response.User!.Id);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Registration validation error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Registration argument error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Registration error: {ex.Message}");
            return StatusCode(500, new { message = "Registration failed" });
        }
    }

    /// <summary>
    /// Xác thực user bằng email và password.
    /// Trả về access token (short-lived) và refresh token (long-lived).
    /// Tự động merge guest cart nếu tồn tại cookie SessionId.
    /// </summary>
    /// <returns>AuthResponse chứa tokens và dữ liệu user</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation($"Login endpoint: {request.Email}");

            var response = await _authService.LoginAsync(request);

            // Merge guest cart nếu có
            await MergeGuestCartAsync(response.User!.Id);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Login authentication error: {ex.Message}");
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Login argument error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            return StatusCode(500, new { message = "Login failed" });
        }
    }

    /// <summary>
    /// Xác thực user qua Google OAuth 2.0.
    /// Tạo user mới ở lần đăng nhập đầu tiên, đăng nhập cho user đã tồn tại.
    /// Trả về access token và refresh token.
    /// Token được verify ở phía server (cách tiếp cận bảo mật).
    /// </summary>
    /// <returns>AuthResponse chứa tokens và dữ liệu user</returns>
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.IdToken))
                return BadRequest(new { message = "Google ID token is required" });

            _logger.LogInformation("Google login endpoint");

            var response = await _authService.GoogleLoginAsync(request.IdToken);

            // Merge guest cart nếu có
            await MergeGuestCartAsync(response.User!.Id);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Google login authentication error: {ex.Message}");
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Google login validation error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Google login argument error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Google login error: {ex.Message}");
            return StatusCode(500, new { message = "Google login failed" });
        }
    }

    /// <summary>
    /// Làm mới access token bằng một refresh token hợp lệ.
    /// Cấp mới access token và refresh token.
    /// Yêu cầu một access token hợp lệ đã được ký trong Authorization header.
    /// Access token có thể đã hết hạn.
    /// </summary>
    /// <returns>AuthResponse chứa bộ token mới</returns>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var accessToken = ReadBearerToken();
            var principal = string.IsNullOrWhiteSpace(accessToken)
                ? null
                : _tokenService.GetPrincipalFromExpiredAccessToken(accessToken);

            if (!TryGetUserId(principal, out var userId))
            {
                _logger.LogWarning("Refresh token: Unable to extract user ID from bearer token");
                return Unauthorized(new { message = "Invalid token claims" });
            }

            _logger.LogInformation($"Refresh token endpoint: User {userId}");

            var response = await _authService.RefreshAccessTokenAsync(userId, request.RefreshToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Refresh token authentication error: {ex.Message}");
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Refresh token argument error: {ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Refresh token error: {ex.Message}");
            return StatusCode(500, new { message = "Token refresh failed" });
        }
    }

    /// <summary>
    /// Đăng xuất user bằng cách revoke refresh token.
    /// Yêu cầu authentication (access token hợp lệ).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (!TryGetUserId(User, out var userId))
            {
                _logger.LogWarning("Logout: Unable to extract user ID from claims");
                return Unauthorized(new { message = "Invalid token claims" });
            }

            _logger.LogInformation($"Logout endpoint: User {userId}");

            await _authService.LogoutAsync(userId, request.RefreshToken);

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Logout error: {ex.Message}");
            return StatusCode(500, new { message = "Logout failed" });
        }
    }

    /// <summary>
    /// Helper method để merge guest cart với user cart nếu tồn tại cookie hoặc header SessionId.
    /// Xoá cookie SessionId sau khi merge.
    /// </summary>
    private async Task MergeGuestCartAsync(int userId)
    {
        string? sessionId = null;

        if (Request.Cookies.TryGetValue("SessionId", out var sid) && !string.IsNullOrEmpty(sid))
        {
            sessionId = sid;
        }
        else if (Request.Headers.TryGetValue("X-Session-Id", out var sessionIdHeader) && !string.IsNullOrEmpty(sessionIdHeader))
        {
            sessionId = sessionIdHeader.ToString();
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                await _cartService.MergeGuestCartToUserAsync(userId, sessionId);
                Response.Cookies.Delete("SessionId");
                _logger.LogInformation($"Guest cart merged for user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Guest cart merge failed: {ex.Message}");
                // Không throw - vẫn cho phép auth thành công ngay cả khi merge cart thất bại
            }
        }
    }

    /// <summary>
    /// TẠM THỜI: nâng user lên role Admin. Hãy xoá trong production!
    /// </summary>
    [HttpPost("promote-to-admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PromoteToAdmin([FromBody] PromoteToAdminRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            user.Role = "Admin";
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {user.Id} promoted to Admin");
            return Ok(new { message = $"User {user.Email} promoted to Admin successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Promote to admin error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to promote user" });
        }
    }

    /// <summary>
    /// Lấy toàn bộ users. Chỉ dành cho Admin.
    /// Trả về danh sách users theo pagination cùng thông tin cơ bản.
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation($"Get all users endpoint: page {page}, pageSize {pageSize}");
            var response = await _authService.GetAllUsersAsync(page, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get all users error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to retrieve users" });
        }
    }

    /// <summary>
    /// Lấy một user theo id. Chỉ dành cho Admin.
    /// </summary>
    [HttpGet("users/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserById(int id)
    {
        try
        {
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get user by id error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to retrieve user" });
        }
    }

    /// <summary>
    /// Cập nhật một user theo id. Chỉ dành cho Admin.
    /// Có thể cập nhật name, phone, address, role và status.
    /// Email không thể thay đổi (immutable).
    /// </summary>
    [HttpPut("users/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required" });

            _logger.LogInformation($"Update user endpoint: User {id}");

            var updatedUser = await _authService.UpdateUserAsync(id, request);
            if (updatedUser == null)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "User updated successfully", user = updatedUser });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update user error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to update user" });
        }
    }

    /// <summary>
    /// Tạo mới một shipping address cho authenticated user hiện tại.
    /// </summary>
    [HttpPost("shipping-address")]
    [Authorize]
    public async Task<IActionResult> CreateShippingAddress([FromBody] CreateShippingAddressRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            _logger.LogInformation($"Create shipping address for user {userId}");

            // Nếu address này được đặt là mặc định, bỏ mặc định ở các address khác
            if (request.IsDefault)
            {
                var existingDefaults = await _context.ShippingAddresses
                    .Where(sa => sa.UserId == userId && sa.IsDefault == true)
                    .ToListAsync();

                foreach (var addr in existingDefaults)
                {
                    addr.IsDefault = false;
                }
            }

            var shippingAddress = new ShippingAddress
            {
                UserId = userId,
                FullName = request.FullName,
                Phone = request.Phone,
                AddressLine = request.AddressLine,
                District = request.District,
                City = request.City,
                IsDefault = request.IsDefault,
                CreatedAt = DateTime.UtcNow
            };

            _context.ShippingAddresses.Add(shippingAddress);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Shipping address created with ID {shippingAddress.Id} for user {userId}");

            return Ok(new
            {
                id = shippingAddress.Id,
                fullName = shippingAddress.FullName,
                phone = shippingAddress.Phone,
                addressLine = shippingAddress.AddressLine,
                district = shippingAddress.District,
                city = shippingAddress.City,
                isDefault = shippingAddress.IsDefault
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Create shipping address error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to create shipping address" });
        }
    }

    /// <summary>
    /// Lấy thông tin của authenticated user hiện tại.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            _logger.LogInformation($"Get current user endpoint for user {userId}");

            var user = await _context.Users
                .Include(u => u.ShippingAddresses)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var defaultAddress = user.ShippingAddresses
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                fullName = defaultAddress?.FullName ?? user.FullName,
                phone = defaultAddress?.Phone ?? user.Phone,
                role = user.Role,
                createdAt = user.CreatedAt,
                shippingAddressId = defaultAddress?.Id,
                address = defaultAddress?.AddressLine,
                addressLine = defaultAddress?.AddressLine,
                district = defaultAddress?.District,
                city = defaultAddress?.City,
                isDefaultShippingAddress = defaultAddress?.IsDefault ?? false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get current user error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to get user information" });
        }
    }

    /// <summary>
    /// Cập nhật thông tin profile của authenticated user hiện tại.
    /// </summary>
    [HttpPut("update-profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            _logger.LogInformation($"Update profile endpoint for user {userId}");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Cập nhật các field nếu được truyền vào
            if (!string.IsNullOrEmpty(request.FullName))
            {
                user.FullName = request.FullName;
            }

            if (!string.IsNullOrEmpty(request.Phone))
            {
                user.Phone = request.Phone;
            }

            // Các address fields được quản lý thông qua entity ShippingAddress

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Profile updated for user {userId}");

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                phone = user.Phone,
                role = user.Role,
                updatedAt = user.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update profile error: {ex.Message}");
            return StatusCode(500, new { message = "Failed to update profile" });
        }
    }

    private string? ReadBearerToken()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return null;
        }

        var headerValue = authorizationHeader.ToString();
        const string bearerPrefix = "Bearer ";
        return headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? headerValue[bearerPrefix.Length..].Trim()
            : null;
    }

    private static bool TryGetUserId(ClaimsPrincipal? principal, out int userId)
    {
        var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? principal?.FindFirst("sub")?.Value;

        return int.TryParse(userIdClaim, out userId);
    }
}
