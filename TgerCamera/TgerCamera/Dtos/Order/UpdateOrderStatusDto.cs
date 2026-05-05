using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho cập nhật trạng thái order
/// </summary>
public class UpdateOrderStatusDto
{
    /// <summary>
    /// Trạng thái mới: Pending, Processing, Shipped, Delivered, Cancelled
    /// </summary>
    [Required(ErrorMessage = "Status is required")]
    [RegularExpression("^(Pending|Processing|Shipped|Delivered|Cancelled)$", ErrorMessage = "Status is invalid")]
    public string Status { get; set; } = "";
}
