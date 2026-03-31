namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho cập nhật trạng thái order
/// </summary>
public class UpdateOrderStatusDto
{
    /// <summary>
    /// Trạng thái mới: Pending, Processing, Shipped, Delivered, Cancelled
    /// </summary>
    public string Status { get; set; } = "";
}
