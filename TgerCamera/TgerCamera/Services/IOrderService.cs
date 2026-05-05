using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos.Order;
using TgerCamera.Models;

namespace TgerCamera.Services;

public interface IOrderService
{
    Task<OrderCheckoutResultDto> CreateOrderAsync(
        int? userId,
        string? sessionId,
        ShippingAddressInfoDto shippingAddress,
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
        ShippingAddressInfoDto shippingAddress,
        string paymentMethod,
        int? cartId,
        List<OrderItemInputDto> items)
    {
        ArgumentNullException.ThrowIfNull(shippingAddress);

        var itemsTable = new DataTable();
        itemsTable.Columns.Add("ProductId", typeof(int));
        itemsTable.Columns.Add("Quantity", typeof(int));

        foreach (var item in items)
        {
            itemsTable.Rows.Add(item.ProductId, item.Quantity);
        }

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? (object)DBNull.Value },
                new SqlParameter("@SessionId", SqlDbType.NVarChar, 100) { Value = sessionId ?? (object)DBNull.Value },
                new SqlParameter("@FullName", SqlDbType.NVarChar, 150) { Value = shippingAddress.FullName! },
                new SqlParameter("@Phone", SqlDbType.NVarChar, 20) { Value = shippingAddress.Phone! },
                new SqlParameter("@AddressLine", SqlDbType.NVarChar, 255) { Value = shippingAddress.AddressLine! },
                new SqlParameter("@District", SqlDbType.NVarChar, 100) { Value = shippingAddress.District! },
                new SqlParameter("@City", SqlDbType.NVarChar, 100) { Value = shippingAddress.City! },
                new SqlParameter("@PaymentMethod", SqlDbType.NVarChar, 50) { Value = paymentMethod },
                new SqlParameter("@CartId", SqlDbType.Int) { Value = cartId ?? (object)DBNull.Value },
                new SqlParameter("@Items", SqlDbType.Structured)
                {
                    Value = itemsTable,
                    TypeName = "dbo.OrderItemType"
                }
            };

            var results = await _context.Database.SqlQueryRaw<OrderSpResult>(
                "EXEC dbo.sp_CreateOrder @UserId, @SessionId, @FullName, @Phone, @AddressLine, @District, @City, @PaymentMethod, @CartId, @Items",
                parameters
            ).ToListAsync();

            var result = results.FirstOrDefault();

            if (result == null)
            {
                _logger.LogError("sp_CreateOrder returned null result");
                throw new InvalidOperationException("Failed to create order.");
            }

            return new OrderCheckoutResultDto
            {
                OrderId = result.OrderId,
                TotalPrice = result.TotalPrice,
                Status = "Success",
                Message = "Order created successfully"
            };
        }
        catch (SqlException ex) when (ex.Number is 50001 or 50002 or 50003)
        {
            _logger.LogWarning("Order validation failed: {Message}", ex.Message);
            throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            throw;
        }
    }
}

public class OrderSpResult
{
    public int OrderId { get; set; }
    public decimal TotalPrice { get; set; }
}
