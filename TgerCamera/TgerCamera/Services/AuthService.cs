using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Dtos.Auth;
using TgerCamera.Helpers;
using TgerCamera.Models;

namespace TgerCamera.Services;

/// <summary>
/// Implementation of authentication service.
/// Clean architecture: delegates token management to ITokenService,
/// refresh token management to IRefreshTokenService,
/// and separates auth logic clearly.
/// Production-ready with comprehensive error handling and logging.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IConfiguration _config;
    private readonly TgerCameraContext _context;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly ICartService _cartService;

    public AuthService(
        IConfiguration config,
        TgerCameraContext context,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<AuthService> logger,
        ICartService cartService)
    {
        _config = config;
        _context = context;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
        _cartService = cartService;
    }

    /// <summary>
    /// Registers a new user with email and password.
    /// Returns tokens for immediate login after registration.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        _logger.LogInformation($"User registration attempt for email: {request.Email}");

        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Email and password are required.");

        // Check if email already exists
        var userExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (userExists)
        {
            _logger.LogWarning($"Registration failed: Email {request.Email} already in use");
            throw new InvalidOperationException("Email already in use.");
        }

        // Create new user
        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName,
            Phone = request.Phone,
            PasswordHash = PasswordHelper.HashPassword(request.Password),
            Role = "Customer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User registered successfully: {user.Id}");

        // Generate tokens
        var response = await BuildAuthResponseAsync(user);
        return response;
    }

    /// <summary>
    /// Authenticates user with email and password.
    /// Returns access and refresh tokens on success.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        _logger.LogInformation($"Login attempt for email: {request.Email}");

        // Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            _logger.LogWarning($"Login failed: User not found for email {request.Email}");
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Verify password
        if (string.IsNullOrEmpty(user.PasswordHash) || !PasswordHelper.VerifyPassword(user.PasswordHash, request.Password))
        {
            _logger.LogWarning($"Login failed: Invalid password for email {request.Email}");
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        _logger.LogInformation($"User logged in successfully: {user.Id}");

        // Generate tokens
        var response = await BuildAuthResponseAsync(user);
        return response;
    }

    /// <summary>
    /// Authenticates user via Google OAuth 2.0.
    /// Verifies token server-side via Google's tokeninfo endpoint.
    /// Creates user on first login, logs in existing users.
    /// </summary>
    public async Task<AuthResponse> GoogleLoginAsync(string googleIdToken)
    {
        _logger.LogInformation("Google login attempt");

        if (string.IsNullOrWhiteSpace(googleIdToken))
            throw new ArgumentException("Google ID token is required.");

        try
        {
            // Verify token via Google's endpoint (server-side, secure)
            var googlePayload = await VerifyGoogleTokenAsync(googleIdToken);

            if (googlePayload == null || string.IsNullOrWhiteSpace(googlePayload.Email))
                throw new InvalidOperationException("Unable to extract email from Google token.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == googlePayload.Email);

            if (user == null)
            {
                // Create new user from Google
                user = new User
                {
                    Email = googlePayload.Email,
                    FullName = googlePayload.Name ?? googlePayload.Email.Split('@')[0],
                    PasswordHash = string.Empty, // No password for OAuth users
                    Role = "Customer",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Google user created: {user.Id}");
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Failed to create Google user, retrying existing user lookup");
                    user = await _context.Users.FirstOrDefaultAsync(u => u.Email == googlePayload.Email);
                    if (user == null)
                    {
                        throw;
                    }

                    _logger.LogInformation($"Google user already exists after duplicate insert attempt: {user.Id}");
                }
            }
            else
            {
                _logger.LogInformation($"Google user logged in: {user.Id}");
            }

            // Generate tokens
            var response = await BuildAuthResponseAsync(user);

            await transaction.CommitAsync();
            return response;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during Google login");
            throw new InvalidOperationException("Unable to complete Google login.", ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Google login validation error: {ex.Message}");
            throw new UnauthorizedAccessException("Invalid Google token.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Google token verification failed: {ex.Message}");
            throw new UnauthorizedAccessException("Google token verification failed.", ex);
        }
    }

    /// <summary>
    /// Refreshes the access token using a valid refresh token.
    /// Issues new access and refresh tokens.
    /// </summary>
    public async Task<AuthResponse> RefreshAccessTokenAsync(int userId, string refreshToken)
    {
        _logger.LogInformation($"Token refresh attempt for user {userId}");

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.");

        // Validate refresh token
        var validToken = await _refreshTokenService.ValidateRefreshTokenAsync(userId, refreshToken);
        if (validToken == null)
        {
            _logger.LogWarning($"Token refresh failed: Invalid refresh token for user {userId}");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        // Get user
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning($"Token refresh failed: User not found {userId}");
            throw new UnauthorizedAccessException("User not found.");
        }

        // Revoke old token and create new ones
        await _refreshTokenService.RevokeRefreshTokenAsync(userId, refreshToken);

        _logger.LogInformation($"Token refreshed successfully for user {userId}");

        // Generate new tokens
        var response = await BuildAuthResponseAsync(user);
        return response;
    }

    /// <summary>
    /// Logs out user by revoking the refresh token.
    /// </summary>
    public async Task LogoutAsync(int userId, string refreshToken)
    {
        _logger.LogInformation($"Logout for user {userId}");

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _refreshTokenService.RevokeRefreshTokenAsync(userId, refreshToken);
        }

        // Optionally revoke all tokens for security
        // await _refreshTokenService.RevokeAllRefreshTokensAsync(userId);

        _logger.LogInformation($"User logged out: {userId}");
    }

    /// <summary>
    /// Builds a complete auth response with access token, refresh token, and user info.
    /// </summary>
    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        // Create access token
        var accessToken = _tokenService.CreateAccessToken(user);

        // Create and store refresh token
        var refreshTokenRecord = await _refreshTokenService.CreateRefreshTokenAsync(user.Id);

        var expirationMinutes = int.Parse(_config["Jwt:AccessTokenExpireMinutes"] ?? "15");

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenRecord.Token, // Return plain token (not hash)
            TokenType = "Bearer",
            ExpiresIn = expirationMinutes * 60, // In seconds
            User = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Role = user.Role
            }
        };
    }

    /// <summary>
    /// Gets all users with pagination.
    /// </summary>
    public async Task<GetUsersResponse> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var totalCount = await _context.Users.CountAsync();
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                Role = u.Role,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return new GetUsersResponse
        {
            Items = users,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    /// <summary>
    /// Gets a single user with detailed information.
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.ShippingAddresses)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        var defaultAddress = user.ShippingAddresses
            .FirstOrDefault(a => a.IsDefault == true)
            ?? user.ShippingAddresses.FirstOrDefault();

        string? address = null;
        if (defaultAddress != null)
        {
            address = string.Join(", ", new[]
            {
                defaultAddress.AddressLine,
                defaultAddress.District,
                defaultAddress.City
            }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role,
            Address = address,
            Status = "Active",
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <summary>
    /// Updates a user's information.
    /// Can update name, phone, address, role, and status.
    /// Email cannot be changed (immutable).
    /// </summary>
    public async Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        var user = await _context.Users
            .Include(u => u.ShippingAddresses)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        // Update user fields
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName;

        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Phone = request.Phone;

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = request.Role;

        user.UpdatedAt = DateTime.UtcNow;

        // Update address if provided
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            var defaultAddress = user.ShippingAddresses.FirstOrDefault(a => a.IsDefault == true);
            if (defaultAddress != null)
            {
                // Parse the address string (simplified - assumes "AddressLine, District, City" format)
                var parts = request.Address.Split(',').Select(p => p.Trim()).ToArray();
                defaultAddress.AddressLine = parts.Length > 0 ? parts[0] : "";
                defaultAddress.District = parts.Length > 1 ? parts[1] : "";
                defaultAddress.City = parts.Length > 2 ? parts[2] : "";
            }
            else if (user.ShippingAddresses.Count == 0)
            {
                // Create a new default address if none exists
                var parts = request.Address.Split(',').Select(p => p.Trim()).ToArray();
                var newAddress = new ShippingAddress
                {
                    UserId = userId,
                    FullName = user.FullName ?? user.Email ?? string.Empty,
                    Phone = user.Phone ?? string.Empty,
                    AddressLine = parts.Length > 0 ? parts[0] : "",
                    District = parts.Length > 1 ? parts[1] : "",
                    City = parts.Length > 2 ? parts[2] : "",
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.ShippingAddresses.Add(newAddress);
            }
        }

        await _context.SaveChangesAsync();

        // Return updated user data
        return await GetUserByIdAsync(userId);
    }

    /// <summary>
    /// Verifies Google ID token via Google's tokeninfo endpoint.
    /// This is server-side verification (most secure approach).
    /// </summary>
    private async Task<GoogleTokenPayload?> VerifyGoogleTokenAsync(string idToken)
    {
        var googleClientId = _config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId))
            throw new InvalidOperationException("Google ClientId is not configured.");

        using (var client = new HttpClient())
        {
            try
            {
                var url = $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}";
                _logger.LogInformation("Verifying Google token via tokeninfo endpoint");

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Google token verification failed with status {response.StatusCode}");
                    throw new UnauthorizedAccessException("Invalid Google token.");
                }

                var content = await response.Content.ReadAsStringAsync();

                using (var json = JsonDocument.Parse(content))
                {
                    var root = json.RootElement;

                    // Verify audience (client ID)
                    if (root.TryGetProperty("aud", out var audElement))
                    {
                        var aud = audElement.GetString();
                        if (aud != googleClientId)
                        {
                            _logger.LogWarning($"Invalid token audience: {aud}");
                            throw new UnauthorizedAccessException("Invalid token audience.");
                        }
                    }

                    // Extract claims
                    var email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
                    var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

                    if (string.IsNullOrWhiteSpace(email))
                        throw new InvalidOperationException("Email not found in Google token.");

                    return new GoogleTokenPayload { Email = email, Name = name };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Failed to parse Google token response: {ex.Message}");
                throw new UnauthorizedAccessException("Invalid Google token response format.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP error calling Google tokeninfo endpoint: {ex.Message}");
                throw new UnauthorizedAccessException("Failed to verify Google token.", ex);
            }
        }
    }
}

/// <summary>
/// Payload extracted from verified Google token.
/// </summary>
internal class GoogleTokenPayload
{
    public string? Email { get; set; }
    public string? Name { get; set; }
}
