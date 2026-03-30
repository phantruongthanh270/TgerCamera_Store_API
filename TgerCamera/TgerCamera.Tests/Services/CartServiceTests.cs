using Microsoft.EntityFrameworkCore;
using Moq;
using TgerCamera.Models;
using TgerCamera.Services;
using Xunit;

namespace TgerCamera.Tests.Services;

/// <summary>
/// Unit tests for the CartService class.
/// Tests cart retrieval, creation, and cart merging functionality.
/// </summary>
public class CartServiceTests
{
    private readonly Mock<TgerCameraContext> _mockContext;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        _mockContext = new Mock<TgerCameraContext>();
        _cartService = new CartService(_mockContext.Object);
    }

    [Fact]
    public async Task GetOrCreateCartBySessionAsync_WithValidSessionId_ShouldReturnCart()
    {
        // Arrange
        string sessionId = "test-session-123";
        var mockDbSet = new Mock<DbSet<Cart>>();
        var carts = new List<Cart>
        {
            new Cart
            {
                Id = 1,
                SessionId = sessionId,
                CartItems = new List<CartItem>()
            }
        }.AsQueryable();

        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Provider).Returns(carts.Provider);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Expression).Returns(carts.Expression);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.ElementType).Returns(carts.ElementType);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.GetEnumerator()).Returns(carts.GetEnumerator());

        _mockContext.Setup(c => c.Carts).Returns(mockDbSet.Object);

        // Act
        var result = await _cartService.GetOrCreateCartBySessionAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetOrCreateCartBySessionAsync_WithNoExistingCart_ShouldCreateNew()
    {
        // Arrange
        string sessionId = "new-session-456";
        var mockDbSet = new Mock<DbSet<Cart>>();
        var emptyCarts = new List<Cart>().AsQueryable();

        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Provider).Returns(emptyCarts.Provider);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Expression).Returns(emptyCarts.Expression);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.ElementType).Returns(emptyCarts.ElementType);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.GetEnumerator()).Returns(emptyCarts.GetEnumerator());

        _mockContext.Setup(c => c.Carts).Returns(mockDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _cartService.GetOrCreateCartBySessionAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeCartAsync_ShouldMergeGuestCartToUserCart()
    {
        // Arrange
        int userId = 1;
        string sessionId = "guest-session-789";

        var guestCart = new Cart
        {
            Id = 1,
            SessionId = sessionId,
            CartItems = new List<CartItem>
            {
                new CartItem { ProductId = 1, Quantity = 2 }
            }
        };

        var userCart = new Cart
        {
            Id = 2,
            UserId = userId,
            CartItems = new List<CartItem>()
        };

        var mockDbSet = new Mock<DbSet<Cart>>();
        var carts = new List<Cart> { guestCart, userCart }.AsQueryable();

        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Provider).Returns(carts.Provider);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.Expression).Returns(carts.Expression);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.ElementType).Returns(carts.ElementType);
        mockDbSet.As<IQueryable<Cart>>().Setup(m => m.GetEnumerator()).Returns(carts.GetEnumerator());

        _mockContext.Setup(c => c.Carts).Returns(mockDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await _cartService.MergeCartAsync(userId, sessionId);

        // Assert
        // Verify cart merge was called
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
