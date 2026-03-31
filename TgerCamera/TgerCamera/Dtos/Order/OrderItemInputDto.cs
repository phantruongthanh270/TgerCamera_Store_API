namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho item trong bảng OrderItemType (Table-valued parameter cho SP)
/// </summary>
public class OrderItemInputDto
{
    /// <summary>
    /// ID của sản phẩm
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Số lượng mua
    /// </summary>
    public int Quantity { get; set; }
}
