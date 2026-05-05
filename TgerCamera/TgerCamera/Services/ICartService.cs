using System.Collections.Generic;
using System.Threading.Tasks;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Services;

public interface ICartService
{
    /// <summary>
    /// Lấy hoặc khởi tạo guest cart từ cache bằng SessionId.
    /// Trả về null nếu không tìm thấy trong cache (guest chưa thêm item nào).
    /// </summary>
    Task<CartDto?> GetGuestCartAsync(string sessionId);

    /// <summary>
    /// Lưu mới hoặc cập nhật guest cart trong distributed cache với TTL 24 giờ.
    /// </summary>
    Task SaveGuestCartAsync(string sessionId, CartDto cart);

    /// <summary>
    /// Lấy cart của authenticated user từ database, bao gồm toàn bộ items.
    /// </summary>
    Task<CartDto?> GetUserCartAsync(int userId);

    /// <summary>
    /// Merge các item của guest cart từ cache vào database cart của user.
    /// Cập nhật quantity cho product đã tồn tại, thêm item mới nếu chưa có.
    /// Validation stock trước khi merge.
    /// Clear cache sau khi merge thành công.
    /// </summary>
    Task MergeGuestCartToUserAsync(int userId, string sessionId);

    /// <summary>
    /// Thêm item vào guest cart (trong cache) hoặc user cart (trong database).
    /// Validation sự tồn tại của product và stock quantity.
    /// </summary>
    Task<CartDto> AddItemToCacheOrDbAsync(string? sessionId, int? userId, int productId, int quantity);

    /// <summary>
    /// Xoá item khỏi guest cart (trong cache) hoặc user cart (trong database).
    /// </summary>
    Task RemoveItemFromCacheOrDbAsync(string? sessionId, int? userId, int cartItemId);

    /// <summary>
    /// Clear guest cart khỏi cache.
    /// </summary>
    Task ClearGuestCartAsync(string sessionId);
}
