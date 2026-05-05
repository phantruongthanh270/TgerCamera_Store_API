using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Order;
using TgerCamera.Models;
using TgerCamera.Services;

namespace TgerCamera.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly ILogger<OrdersController> _logger;

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

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto dto)
    {
        try
        {
            int? userId = GetCurrentUserId();
            string? sessionId = dto.SessionId;

            if (string.IsNullOrWhiteSpace(sessionId) && Request.Cookies.TryGetValue("SessionId", out var cookieSessionId))
            {
                sessionId = cookieSessionId;
            }

            if (userId.HasValue)
            {
                sessionId = null;
            }

            List<OrderItemInputDto> cartItems = new();
            int? cartId = null;

            if (userId.HasValue)
            {
                var userCart = await _cartService.GetUserCartAsync(userId.Value);
                if (userCart?.Items == null || userCart.Items.Count == 0)
                {
                    return BadRequest(new { message = "Cart is empty." });
                }

                cartItems = userCart.Items
                    .Select(item => new OrderItemInputDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    })
                    .ToList();

                cartId = await _context.Carts
                    .Where(c => c.UserId == userId.Value)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();
            }
            else if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var guestCart = await _cartService.GetGuestCartAsync(sessionId);
                if (guestCart?.Items == null || guestCart.Items.Count == 0)
                {
                    return BadRequest(new { message = "Cart is empty." });
                }

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
                return BadRequest(new { message = "No cart found." });
            }

            var shippingAddress = await ResolveShippingAddressAsync(dto, userId);
            if (!IsValidShippingAddress(shippingAddress))
            {
                return BadRequest(new { message = "Shipping address is required." });
            }

            var result = await _orderService.CreateOrderAsync(
                userId,
                sessionId,
                shippingAddress!,
                dto.PaymentMethod.Trim(),
                cartId,
                cartItems);

            if (!userId.HasValue && !string.IsNullOrWhiteSpace(sessionId))
            {
                await _cartService.ClearGuestCartAsync(sessionId);
                Response.Cookies.Delete("SessionId");
            }

            _logger.LogInformation("Order {OrderId} created for userId {UserId}", result.OrderId, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout validation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout failed");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while processing your order. Please try again." });
        }
    }

    [HttpGet("my-orders")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderDto>>> MyOrders()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId && (o.IsDeleted == null || o.IsDeleted == false))
            .Include(o => o.OrderItems)
            .ToListAsync();

        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll()
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.IsDeleted == null || o.IsDeleted == false)
            .Include(o => o.OrderItems)
            .ToListAsync();

        return Ok(_mapper.Map<IEnumerable<OrderDto>>(orders));
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole("Admin");

        var order = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == id && (o.IsDeleted == null || o.IsDeleted == false))
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound();
        }

        if (!isAdmin && order.UserId != userId)
        {
            return Forbid();
        }

        return Ok(_mapper.Map<OrderDto>(order));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        order.Status = dto.Status.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} status updated to {Status}", id, dto.Status);
        return NoContent();
    }

    [HttpPut("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (order.UserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (order.Status != "Pending")
        {
            return BadRequest("Only pending orders can be cancelled.");
        }

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

        _logger.LogInformation("Order {OrderId} cancelled by userId {UserId}", id, userId);
        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        return int.TryParse(userIdClaim, out var parsedUserId)
            ? parsedUserId
            : null;
    }

    private async Task<ShippingAddressInfoDto?> ResolveShippingAddressAsync(CheckoutRequestDto dto, int? userId)
    {
        if (dto.ShippingAddressId.HasValue)
        {
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("Saved shipping address requires an authenticated user.");
            }

            var savedAddress = await _context.ShippingAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == dto.ShippingAddressId.Value && a.UserId == userId.Value);

            if (savedAddress == null)
            {
                throw new InvalidOperationException("Shipping address is invalid.");
            }

            return new ShippingAddressInfoDto
            {
                FullName = savedAddress.FullName,
                Phone = savedAddress.Phone,
                AddressLine = savedAddress.AddressLine,
                District = savedAddress.District,
                City = savedAddress.City
            };
        }

        if (dto.ShippingAddress == null)
        {
            return null;
        }

        return new ShippingAddressInfoDto
        {
            FullName = dto.ShippingAddress.FullName,
            Phone = dto.ShippingAddress.Phone,
            AddressLine = dto.ShippingAddress.AddressLine,
            District = dto.ShippingAddress.District,
            City = dto.ShippingAddress.City
        };
    }

    private static bool IsValidShippingAddress(ShippingAddressInfoDto? shippingAddress)
    {
        return shippingAddress != null
            && !string.IsNullOrWhiteSpace(shippingAddress.FullName)
            && !string.IsNullOrWhiteSpace(shippingAddress.Phone)
            && !string.IsNullOrWhiteSpace(shippingAddress.AddressLine)
            && !string.IsNullOrWhiteSpace(shippingAddress.District)
            && !string.IsNullOrWhiteSpace(shippingAddress.City);
    }
}
