using System.Collections.Generic;
using System.Threading.Tasks;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Services;

public interface ICartService
{
    /// <summary>
    /// Gets or creates a guest cart from cache using SessionId.
    /// Returns null if not found in cache (guest hasn't added items yet).
    /// </summary>
    Task<CartDto?> GetGuestCartAsync(string sessionId);

    /// <summary>
    /// Saves or updates a guest cart in distributed cache with 24-hour TTL.
    /// </summary>
    Task SaveGuestCartAsync(string sessionId, CartDto cart);

    /// <summary>
    /// Gets the authenticated user's cart from database, including all items.
    /// </summary>
    Task<CartDto?> GetUserCartAsync(int userId);

    /// <summary>
    /// Merges guest cart items from cache into user's database cart.
    /// Updates quantities for existing products, adds new ones.
    /// Validates stock before merging.
    /// Clears cache after successful merge.
    /// </summary>
    Task MergeGuestCartToUserAsync(int userId, string sessionId);

    /// <summary>
    /// Adds an item to guest cart (in cache) or user cart (in database).
    /// Validates product existence and stock quantity.
    /// </summary>
    Task<CartDto> AddItemToCacheOrDbAsync(string? sessionId, int? userId, int productId, int quantity);

    /// <summary>
    /// Removes an item from guest cart (in cache) or user cart (in database).
    /// </summary>
    Task RemoveItemFromCacheOrDbAsync(string? sessionId, int? userId, int cartItemId);

    /// <summary>
    /// Clears guest cart from cache.
    /// </summary>
    Task ClearGuestCartAsync(string sessionId);
}
