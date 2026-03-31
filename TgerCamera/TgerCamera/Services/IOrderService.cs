using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Order;
using TgerCamera.Models;
using System.Data;

namespace TgerCamera.Services;

/// <summary>
/// Service cho Order operations - gọi Stored Procedures
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Tạo order thông qua stored procedure sp_CreateOrder
    /// </summary>
    Task<OrderCheckoutResultDto> CreateOrderAsync(
        int? userId,
        string? sessionId,
        int shippingAddressId,
        string paymentMethod,
        int? cartId,
        List<OrderItemInputDto> items);
}

public class OrderService : IOrderService
{
    private readonly TgerCameraContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(TgerCameraContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderCheckoutResultDto> CreateOrderAsync(
        int? userId,
        string? sessionId,
        int shippingAddressId,
        string paymentMethod,
        int? cartId,
        List<OrderItemInputDto> items)
    {
        try
        {
            // 1. Chuẩn bị bảng cho Table-Valued Parameter
            var itemsTable = new DataTable();
            itemsTable.Columns.Add("ProductId", typeof(int));
            itemsTable.Columns.Add("Quantity", typeof(int));

            foreach (var item in items)
            {
                itemsTable.Rows.Add(item.ProductId, item.Quantity);
            }

            // 2. Chuẩn bị parameters cho SP
            var parameters = new[]
            {
                new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? (object)DBNull.Value },
                new SqlParameter("@SessionId", SqlDbType.NVarChar, 100) { Value = sessionId ?? (object)DBNull.Value },
                new SqlParameter("@ShippingAddressId", SqlDbType.Int) { Value = shippingAddressId },
                new SqlParameter("@PaymentMethod", SqlDbType.NVarChar, 50) { Value = paymentMethod },
                new SqlParameter("@CartId", SqlDbType.Int) { Value = cartId ?? (object)DBNull.Value },
                new SqlParameter("@Items", SqlDbType.Structured)
                {
                    Value = itemsTable,
                    TypeName = "dbo.OrderItemType"
                }
            };

            // 3. Thực thi SP và lấy kết quả
            var result = await _context.Database.SqlQueryRaw<OrderSpResult>(
                "EXEC dbo.sp_CreateOrder @UserId, @SessionId, @ShippingAddressId, @PaymentMethod, @CartId, @Items",
                parameters
            ).FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogError("sp_CreateOrder returned null result");
                throw new Exception("Failed to create order");
            }

            return new OrderCheckoutResultDto
            {
                OrderId = result.OrderId,
                TotalPrice = result.TotalPrice,
                Status = "Success",
                Message = "Order created successfully"
            };
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            // Custom error từ SP - Insufficient stock
            _logger.LogWarning($"Stock validation failed: {ex.Message}");
            throw new InvalidOperationException(ex.Message);
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            // Custom error từ SP - Empty items
            _logger.LogWarning($"Order validation failed: {ex.Message}");
            throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating order: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Model cho kết quả trả về từ SP
/// </summary>
public class OrderSpResult
{
    public int OrderId { get; set; }
    public decimal TotalPrice { get; set; }
}
