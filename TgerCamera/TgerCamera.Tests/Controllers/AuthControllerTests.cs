using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TgerCamera.Controllers;
using TgerCamera.Dtos.Auth;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService = new();
    private readonly Mock<ICartService> _mockCartService = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly Mock<ILogger<AuthController>> _mockLogger = new();
    private readonly Mock<TgerCameraContext> _mockContext = new();

    private AuthController CreateController()
    {
        var controller = new AuthController(
            _mockAuthService.Object,
            _mockCartService.Object,
            _mockTokenService.Object,
            _mockLogger.Object,
            _mockContext.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    [Fact]
    public async Task Register_WithValidRequest_ShouldReturnOk()
    {
        var controller = CreateController();
        var request = new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "SecurePassword123!",
            FullName = "New User",
            Phone = "+1234567890"
        };

        _mockAuthService
            .Setup(service => service.RegisterAsync(request))
            .ReturnsAsync(new AuthResponse
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                User = new UserResponse
                {
                    Id = 10,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Role = "Customer"
                }
            });

        var result = await controller.Register(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("access-token", response.AccessToken);
    }

    [Fact]
    public async Task Login_WhenServiceRejectsCredentials_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "WrongPassword123!"
        };

        _mockAuthService
            .Setup(service => service.LoginAsync(request))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid email or password."));

        var result = await controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithValidRequest_ShouldReturnOk()
    {
        var controller = CreateController();
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "CorrectPassword123!"
        };

        _mockAuthService
            .Setup(service => service.LoginAsync(request))
            .ReturnsAsync(new AuthResponse
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                User = new UserResponse
                {
                    Id = 1,
                    Email = request.Email,
                    FullName = "Test User",
                    Role = "Customer"
                }
            });

        var result = await controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithoutBearerToken_ShouldReturnUnauthorized()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.RefreshToken(new RefreshTokenRequest
        {
            RefreshToken = "refresh-token"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Logout_WithAuthenticatedUser_ShouldReturnOk()
    {
        var controller = CreateController();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "5")
                    },
                    "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var request = new RefreshTokenRequest
        {
            RefreshToken = "refresh-token"
        };

        var result = await controller.Logout(request);

        Assert.IsType<OkObjectResult>(result);
        _mockAuthService.Verify(service => service.LogoutAsync(5, "refresh-token"), Times.Once);
    }
}
