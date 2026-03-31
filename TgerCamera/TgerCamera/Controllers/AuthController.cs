using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Auth;
using TgerCamera.Helpers;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles user authentication operations including registration and login.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly ITokenService _tokenService;
    private readonly ICartService _cartService;

    /// <summary>
    /// Initializes a new instance of the AuthController.
    /// </summary>
    /// <param name="context">The database context for accessing user data.</param>
    /// <param name="tokenService">Service for creating JWT tokens.</param>
    /// <param name="cartService">Service for managing shopping carts.</param>
    public AuthController(TgerCameraContext context, ITokenService tokenService, ICartService cartService)
    {
        _context = context;
        _tokenService = tokenService;
        _cartService = cartService;
    }

    /// <summary>
    /// Registers a new user account with the provided credentials.
    /// Automatically merges guest cart (from SessionId cookie) to user cart if a guest session exists.
    /// </summary>
    /// <param name="dto">The registration data containing email, password, and full name.</param>
    /// <returns>Returns an AuthResultDto with JWT token and expiration time on success, or BadRequest if validation fails.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Email and password are required.");

        var exists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists) return BadRequest("Email already in use.");

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = PasswordHelper.HashPassword(dto.Password),
            FullName = dto.FullName,
            Role = "Customer"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);

        // Merge guest cart from cache to user's database cart
        var sessionId = Request.Cookies.TryGetValue("SessionId", out var sid) ? sid : null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            await _cartService.MergeGuestCartToUserAsync(user.Id, sessionId);
            // Clear the SessionId cookie
            Response.Cookies.Delete("SessionId");
        }

        return Ok(new AuthResultDto { Token = token, ExpiresAt = System.DateTime.UtcNow.AddMinutes(60).ToString("o") });
    }

    /// <summary>
    /// Authenticates a user with email and password credentials.
    /// Automatically merges guest cart (from SessionId cookie) to user cart if a guest session exists.
    /// </summary>
    /// <param name="dto">The login credentials containing email and password.</param>
    /// <returns>Returns an AuthResultDto with JWT token and expiration time on success, or Unauthorized if credentials are invalid.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Email and password are required.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null) return Unauthorized("Invalid email or password.");

        if (!PasswordHelper.VerifyPassword(user.PasswordHash, dto.Password)) return Unauthorized();

        var token = _tokenService.CreateToken(user);

        // Merge guest cart from cache to user's database cart
        var sessionId = Request.Cookies.TryGetValue("SessionId", out var sid2) ? sid2 : null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            await _cartService.MergeGuestCartToUserAsync(user.Id, sessionId);
            // Clear the SessionId cookie
            Response.Cookies.Delete("SessionId");
        }

        return Ok(new AuthResultDto { Token = token, ExpiresAt = System.DateTime.UtcNow.AddMinutes(60).ToString("o") });
    }
}
