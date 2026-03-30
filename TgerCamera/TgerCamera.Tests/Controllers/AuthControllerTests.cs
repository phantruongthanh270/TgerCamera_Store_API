using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TgerCamera.Controllers;
using TgerCamera.Dtos.Auth;
using TgerCamera.Helpers;
using TgerCamera.Models;
using TgerCamera.Services;
using Xunit;

namespace TgerCamera.Tests.Controllers;

/// <summary>
/// Unit tests for the AuthController class.
/// Tests user registration, login, and authentication flow.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<TgerCameraContext> _mockContext;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<ICartService> _mockCartService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockContext = new Mock<TgerCameraContext>();
        _mockTokenService = new Mock<ITokenService>();
        _mockCartService = new Mock<ICartService>();
        _controller = new AuthController(_mockContext.Object, _mockTokenService.Object, _mockCartService.Object);
    }

    [Fact]
    public async Task Register_WithValidNewUser_ShouldCreateUserAndReturnSuccess()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Password = "SecurePassword123!",
            FullName = "New User"
        };

        var mockUsers = new Mock<DbSet<User>>();
        var emptyUsers = new List<User>().AsQueryable();

        mockUsers.As<IQueryable<User>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        mockUsers.As<IQueryable<User>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        mockUsers.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        mockUsers.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());

        _mockContext.Setup(c => c.Users).Returns(mockUsers.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockTokenService.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("token123");

        // Act
        var result = await _controller.Register(registerDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "existing@example.com",
            Password = "SecurePassword123!",
            FullName = "Existing User"
        };

        var existingUser = new User 
        { 
            Id = 1, 
            Email = "existing@example.com", 
            FullName = "Existing",
            PasswordHash = "hash"
        };
        
        var mockUsers = new Mock<DbSet<User>>();
        var users = new List<User> { existingUser }.AsQueryable();

        mockUsers.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
        mockUsers.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
        mockUsers.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
        mockUsers.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

        _mockContext.Setup(c => c.Users).Returns(mockUsers.Object);

        // Act
        var result = await _controller.Register(registerDto);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badResult.Value);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var password = "CorrectPassword123!";
        var loginDto = new LoginDto
        {
            Email = "user@example.com",
            Password = password
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            FullName = "Test User",
            PasswordHash = PasswordHelper.HashPassword(password),
            Role = "User"
        };

        var mockUsers = new Mock<DbSet<User>>();
        var users = new List<User> { user }.AsQueryable();

        mockUsers.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
        mockUsers.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
        mockUsers.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
        mockUsers.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

        _mockContext.Setup(c => c.Users).Returns(mockUsers.Object);
        _mockTokenService.Setup(t => t.CreateToken(user)).Returns("token123");

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        _mockTokenService.Verify(t => t.CreateToken(user), Times.Once);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "user@example.com",
            Password = "WrongPassword"
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            PasswordHash = PasswordHelper.HashPassword("CorrectPassword123!"),
            Role = "User"
        };

        var mockUsers = new Mock<DbSet<User>>();
        var users = new List<User> { user }.AsQueryable();

        mockUsers.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
        mockUsers.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
        mockUsers.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
        mockUsers.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

        _mockContext.Setup(c => c.Users).Returns(mockUsers.Object);

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "AnyPassword123!"
        };

        var mockUsers = new Mock<DbSet<User>>();
        var emptyUsers = new List<User>().AsQueryable();

        mockUsers.As<IQueryable<User>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        mockUsers.As<IQueryable<User>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        mockUsers.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        mockUsers.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());

        _mockContext.Setup(c => c.Users).Returns(mockUsers.Object);

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }
}
