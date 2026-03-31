using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Order;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

/// <summary>
/// Handles order-related operations including checkout, order retrieval, and order status management.
/// Manages the complete ordering workflow from cart to order tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly ILogger<OrdersController> _logger;

    /// <summary>
    /// Initializes a new instance of the OrdersController.
    /// </summary>
    /// <param name="context">The database context for accessing order data.</param>
    /// <param name="mapper">AutoMapper instance for DTO mapping.</param>
    /// <param name="orderService">Order service for creating orders via SP.</param>
    /// <param name="cartService">Cart service for managing carts.</param>
    /// <param name="logger">Logger instance.</param>
    public OrdersController(
        TgerCameraContext context,
        IMapper mapper,
        IOrderService orderService,
        ICartService cartService,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _mapper = mapper;
        _orderService = orderService;
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// Processes checkout for authenticated users or guests, converting cart items to an order.
    /// Uses stored procedure sp_CreateOrder for transactional integrity and stock validation.
    /// Clears the cart after successful order creation.
    /// </summary>
    /// <param name="dto">The checkout request containing shipping and payment information.</param>
    /// <returns>Returns the created order checkout result with order ID and total price.</returns>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto dto)
    {
        try
        {
            // 1. Extract user info (optional for authenticated users)
            int? userId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (int.TryParse(userIdClaim, out var parsedUserId))
                    userId = parsedUserId;
            }

            // 2. Get SessionId from cookie (for guest tracking)
            string? sessionId = null;
            if (Request.Cookies.TryGetValue("SessionId", out var cookieSessionId))
                sessionId = cookieSessionId;

            // 3. Determine cart source and get items
            List<OrderItemInputDto> cartItems = new();
            int? cartId = null;

            if (userId.HasValue)
            {
                // Authenticated user - get from DB
                var userCart = await _cartService.GetUserCartAsync(userId.Value);
                if (userCart?.Items == null || userCart.Items.Count == 0)
                    return BadRequest("Cart is empty.");

                cartItems = userCart.Items
                    .Select(item => new OrderItemInputDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    })
                    .ToList();

                // Get cart ID for clearing after order
                var dbCart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.UserId == userId.Value);
                if (dbCart != null)
                    cartId = dbCart.Id;
            }
            else if (!string.IsNullOrEmpty(sessionId))
            {
                // Guest - get from cache
                var guestCart = await _cartService.GetGuestCartAsync(sessionId);
                if (guestCart?.Items == null || guestCart.Items.Count == 0)
                    return BadRequest("Cart is empty.");

                cartItems = guestCart.Items
                    .Select(item => new OrderItemInputDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    })
                    .ToList();
            }
            else
            {
                return BadRequest("No cart found. Please add items to cart first.");
            }

            // 4. Validate shipping address exists
            var addressExists = await _context.ShippingAddresses
                .AnyAsync(a => a.Id == dto.ShippingAddressId &&
                    (a.UserId == userId || a.UserId == null)); // Allow public or user's address

            if (!addressExists && userId.HasValue)
            {
                // For authenticated users, verify ownership
                addressExists = await _context.ShippingAddresses
                    .AnyAsync(a => a.Id == dto.ShippingAddressId && a.UserId == userId.Value);
            }

            if (!addressExists)
                return BadRequest("Invalid shipping address.");

            // 5. Call stored procedure to create order
            var result = await _orderService.CreateOrderAsync(
                userId,
                sessionId,
                dto.ShippingAddressId,
                dto.PaymentMethod,
                cartId,
                cartItems
            );

            // 6. Clear guest cart from cache after successful order
            if (!userId.HasValue && !string.IsNullOrEmpty(sessionId))
            {
                await _cartService.ClearGuestCartAsync(sessionId);
                Response.Cookies.Delete("SessionId");
            }

            _logger.LogInformation($"Order {result.OrderId} created successfully for userId: {userId}, orderId: {result.OrderId}");

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Validation error during checkout: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during checkout: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while processing your order. Please try again." });
        }
    }

    /// <summary>
    /// Retrieves all orders for the authenticated user.
    /// </summary>
    /// <returns>Returns a list of OrderDto for the user's orders (excluding deleted).</returns>
    [HttpGet("my-orders")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderDto>>> MyOrders()
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (!int.TryParse(userIdClaim, out var parsedUserId))
                return Unauthorized();
            userId = parsedUserId;
        }
        else
        {
            return Unauthorized();
        }

        var orders = await _context.Orders
            .Where(o => o.UserId == userId && (o.IsDeleted == null || o.IsDeleted == false))
            .Include(o => o.OrderItems)
            .ToListAsync();

        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    /// <summary>
    /// Retrieves all orders in the system. Admin only.
    /// </summary>
    /// <returns>Returns a list of all OrderDto in the system (excluding deleted).</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll()
    {
        var orders = await _context.Orders
            .Where(o => o.IsDeleted == null || o.IsDeleted == false)
            .Include(o => o.OrderItems)
            .ToListAsync();
        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    /// <summary>
    /// Retrieves a specific order by ID. Users can only view their own orders.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <returns>Returns the OrderDto if found and authorized, NotFound otherwise.</returns>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (!int.TryParse(userIdClaim, out var parsedUserId))
                return Unauthorized();
            userId = parsedUserId;

            // Check if user is admin
            var isAdmin = User.IsInRole("Admin");

            var order = await _context.Orders
                .Where(o => o.Id == id && (o.IsDeleted == null || o.IsDeleted == false))
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound();

            // Only allow user to view their own order unless they're admin
            if (!isAdmin && order.UserId != userId)
                return Forbid();

            return Ok(_mapper.Map<OrderDto>(order));
        }

        return Unauthorized();
    }

    /// <summary>
    /// Updates the status of an order. Admin only.
    /// </summary>
    /// <param name="id">The order ID to update.</param>
    /// <param name="status">The new order status (e.g., "Pending", "Processing", "Shipped", "Delivered", "Cancelled").</param>
    /// <returns>Returns NoContent on success, or NotFound if order doesn't exist.</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.Status = dto.Status;
        order.UpdatedAt = DateTime.UtcNow;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Order {id} status updated to {dto.Status}");
        return NoContent();
    }

    /// <summary>
    /// Cancels an order. User can only cancel their own pending orders.
    /// </summary>
    /// <param name="id">The order ID to cancel.</param>
    /// <returns>Returns NoContent on success.</returns>
    [HttpPut("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        // Verify ownership
        if (order.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        // Only pending orders can be cancelled
        if (order.Status != "Pending")
            return BadRequest("Only pending orders can be cancelled.");

        // Restore stock
        foreach (var item in order.OrderItems)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.StockQuantity += item.Quantity;
                _context.Products.Update(product);
            }
        }

        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Order {id} cancelled by userId {userId}");
        return NoContent();
    }
}
