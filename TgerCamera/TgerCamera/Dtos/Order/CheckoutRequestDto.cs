namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho yêu cầu checkout
/// </summary>
public class CheckoutRequestDto
{
    /// <summary>
    /// ID địa chỉ giao hàng
    /// </summary>
    public int ShippingAddressId { get; set; }

    /// <summary>
    /// Phương thức thanh toán: 'COD', 'VNPAY', etc
    /// </summary>
    public string PaymentMethod { get; set; } = "COD";

    /// <summary>
    /// SessionId (cho khách vãng lai), có thể null
    /// </summary>
    public string? SessionId { get; set; }
}
