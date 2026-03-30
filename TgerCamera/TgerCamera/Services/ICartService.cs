using System.Threading.Tasks;
using TgerCamera.Models;

namespace TgerCamera.Services;

public interface ICartService
{
    Task<Cart> GetOrCreateCartBySessionAsync(string sessionId);
    Task MergeCartAsync(int userId, string sessionId);
}
