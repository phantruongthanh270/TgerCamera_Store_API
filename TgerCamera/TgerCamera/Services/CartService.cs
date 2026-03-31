using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Services;

public class CartService : ICartService
{
    private readonly TgerCameraContext _context;
    private readonly IDistributedCache _cache;
    private const string CART_CACHE_PREFIX = "cart:";
    private const int CACHE_EXPIRATION_HOURS = 24;

    public CartService(TgerCameraContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    #region Guest Cart - Cache Operations

    public async Task<CartDto?> GetGuestCartAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        var cacheKey = $"{CART_CACHE_PREFIX}{sessionId}";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(cachedData))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CartDto>(cachedData);
        }
        catch
        {
            // If deserialization fails, return null and let the cache expire naturally
            return null;
        }
    }

    public async Task SaveGuestCartAsync(string sessionId, CartDto cart)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentNullException(nameof(sessionId));

        var cacheKey = $"{CART_CACHE_PREFIX}{sessionId}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CACHE_EXPIRATION_HOURS)
        };

        var serialized = JsonSerializer.Serialize(cart);
        await _cache.SetStringAsync(cacheKey, serialized, options);
    }

    public async Task ClearGuestCartAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        var cacheKey = $"{CART_CACHE_PREFIX}{sessionId}";
        await _cache.RemoveAsync(cacheKey);
    }

    #endregion

    #region User Cart - Database Operations

    public async Task<CartDto?> GetUserCartAsync(int userId)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
            return null;

        return MapCartToDto(cart);
    }

    #endregion

    #region Merge Logic

    public async Task MergeGuestCartToUserAsync(int userId, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        // Step 1: Get guest cart from cache
        var guestCartDto = await GetGuestCartAsync(sessionId);
        if (guestCartDto?.Items == null || !guestCartDto.Items.Any())
            return;

        // Step 2: Get or create user cart in database
        var userCart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (userCart == null)
        {
            userCart = new Cart { UserId = userId };
            _context.Carts.Add(userCart);
            await _context.SaveChangesAsync();
        }

        // Step 3: Validate and merge items
        foreach (var guestItem in guestCartDto.Items)
        {
            // Validate product exists and is not deleted
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == guestItem.ProductId && (p.IsDeleted == null || p.IsDeleted == false));

            if (product == null)
            {
                // Skip invalid products
                continue;
            }

            // Validate stock quantity
            if (product.StockQuantity < guestItem.Quantity)
            {
                // Adjust quantity to available stock
                guestItem.Quantity = Math.Max(0, product.StockQuantity);
                if (guestItem.Quantity == 0)
                    continue; // Skip if no stock
            }

            // Check if item already exists in user's cart
            var existingItem = userCart.CartItems.FirstOrDefault(ci => ci.ProductId == guestItem.ProductId);

            if (existingItem != null)
            {
                // Add quantity, but don't exceed stock
                var newQuantity = Math.Min(
                    existingItem.Quantity + guestItem.Quantity,
                    product.StockQuantity);
                existingItem.Quantity = newQuantity;
                _context.CartItems.Update(existingItem);
            }
            else
            {
                // Add new item to user's cart
                var newItem = new CartItem
                {
                    CartId = userCart.Id,
                    ProductId = guestItem.ProductId,
                    Quantity = guestItem.Quantity
                };
                _context.CartItems.Add(newItem);
            }
        }

        await _context.SaveChangesAsync();

        // Step 4: Clear guest cart from cache
        await ClearGuestCartAsync(sessionId);
    }

    #endregion

    #region Add/Remove Item Operations

    public async Task<CartDto> AddItemToCacheOrDbAsync(string? sessionId, int? userId, int productId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0.");

        // Step 1: Validate product exists, is not deleted, and has sufficient stock
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && (p.IsDeleted == null || p.IsDeleted == false));

        if (product == null)
            throw new KeyNotFoundException("Product does not exist or has been deleted.");

        if (product.StockQuantity < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");

        // Step 2: Add to user's cart (database) if authenticated
        if (userId.HasValue && userId > 0)
        {
            var userCart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                userCart = new Cart { UserId = userId };
                _context.Carts.Add(userCart);
                await _context.SaveChangesAsync();
            }

            var existingItem = userCart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (existingItem != null)
            {
                // Ensure total doesn't exceed stock
                var newQuantity = Math.Min(existingItem.Quantity + quantity, product.StockQuantity);
                existingItem.Quantity = newQuantity;
                _context.CartItems.Update(existingItem);
            }
            else
            {
                var newItem = new CartItem
                {
                    CartId = userCart.Id,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(newItem);
            }

            await _context.SaveChangesAsync();
            return await GetUserCartAsync(userId.Value) ?? new CartDto();
        }

        // Step 3: Add to guest's cart (cache)
        if (string.IsNullOrEmpty(sessionId))
            sessionId = Guid.NewGuid().ToString();

        var guestCart = await GetGuestCartAsync(sessionId) ?? new CartDto { SessionId = sessionId };

        var guestItem = guestCart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (guestItem != null)
        {
            // Ensure total doesn't exceed stock
            guestItem.Quantity = Math.Min(guestItem.Quantity + quantity, product.StockQuantity);
        }
        else
        {
            // Assign temporary negative ID for guest cart items (not in DB yet)
            var tempId = guestCart.Items.Count > 0
                ? Math.Min(guestCart.Items.Select(i => i.Id).DefaultIfEmpty(0).Min() - 1, -1)
                : -1;

            guestCart.Items.Add(new CartItemDto
            {
                Id = tempId,
                ProductId = productId,
                Quantity = quantity,
                Product = MapProductToDto(product)
            });
        }

        await SaveGuestCartAsync(sessionId, guestCart);
        return guestCart;
    }

    public async Task RemoveItemFromCacheOrDbAsync(string? sessionId, int? userId, int cartItemId)
    {
        // For user cart: delete from database
        if (userId.HasValue && userId > 0)
        {
            var item = await _context.CartItems.FindAsync(cartItemId);
            if (item != null)
            {
                // Verify the item belongs to this user's cart
                var userCart = await _context.Carts.FirstOrDefaultAsync(c => c.Id == item.CartId && c.UserId == userId);
                if (userCart != null)
                {
                    _context.CartItems.Remove(item);
                    await _context.SaveChangesAsync();
                }
            }
            return;
        }

        // For guest cart: remove from cache
        if (!string.IsNullOrEmpty(sessionId))
        {
            var guestCart = await GetGuestCartAsync(sessionId);
            if (guestCart != null)
            {
                guestCart.Items.RemoveAll(i => i.Id == cartItemId);
                await SaveGuestCartAsync(sessionId, guestCart);
            }
        }
    }

    #endregion

    #region Helper Methods

    private CartDto MapCartToDto(Cart cart)
    {
        return new CartDto
        {
            Id = cart.Id,
            SessionId = cart.SessionId,
            UserId = cart.UserId,
            Items = cart.CartItems?.Select(ci => new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                Product = ci.Product != null ? MapProductToDto(ci.Product) : null
            }).ToList() ?? new List<CartItemDto>()
        };
    }

    private ProductDto MapProductToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            Brand = product.Brand != null ? new BrandDto { Id = product.Brand.Id, Name = product.Brand.Name } : null,
            Category = product.Category != null ? new CategoryDto { Id = product.Category.Id, Name = product.Category.Name } : null,
            Condition = product.Condition != null ? new ProductConditionDto { Id = product.Condition.Id, Name = product.Condition.Name } : null,
            MainImageUrl = product.ProductImages?.FirstOrDefault(pi => pi.IsMain == true)?.ImageUrl,
            Specifications = product.ProductSpecifications?.Select(ps => new ProductSpecificationDto
            {
                Id = ps.Id,
                Key = ps.Key,
                Value = ps.Value
            }).ToList() ?? new List<ProductSpecificationDto>()
        };
    }

    #endregion
}
