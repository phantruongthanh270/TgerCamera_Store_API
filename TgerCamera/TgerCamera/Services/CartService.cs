using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Models;

namespace TgerCamera.Services;

public class CartService : ICartService
{
    private readonly TgerCameraContext _context;

    public CartService(TgerCameraContext context)
    {
        _context = context;
    }

    public async Task<Cart> GetOrCreateCartBySessionAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) sessionId = System.Guid.NewGuid().ToString();

        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (cart != null) return cart;

        cart = new Cart { SessionId = sessionId };
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();
        return cart;
    }

    public async Task MergeCartAsync(int userId, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        var guestCart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.UserId == null);

        if (guestCart == null) return;

        var userCart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (userCart == null)
        {
            guestCart.UserId = userId;
            _context.Carts.Update(guestCart);
        }
        else
        {
            // merge items: sum quantities for same product
            foreach (var item in guestCart.CartItems.ToList())
            {
                var existing = userCart.CartItems.FirstOrDefault(ci => ci.ProductId == item.ProductId);
                if (existing != null)
                {
                    existing.Quantity += item.Quantity;
                    _context.CartItems.Update(existing);
                    _context.CartItems.Remove(item);
                }
                else
                {
                    item.CartId = userCart.Id;
                    userCart.CartItems.Add(item);
                    _context.CartItems.Update(item);
                }
            }
            _context.Carts.Remove(guestCart);
            _context.Carts.Update(userCart);
        }

        await _context.SaveChangesAsync();
    }
}
