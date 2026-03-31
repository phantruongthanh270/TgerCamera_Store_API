namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho kết quả checkout từ SP
/// </summary>
public class OrderCheckoutResultDto
{
    /// <summary>
    /// ID của order vừa được tạo
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Tổng tiền đơn hàng
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Trạng thái (Success/Failed)
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Thông báo chi tiết
    /// </summary>
    public string Message { get; set; } = "";
}
