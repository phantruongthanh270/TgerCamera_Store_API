using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Order;

/// <summary>
/// DTO cho yêu cầu checkout
/// </summary>
public class CheckoutRequestDto
{
    /// <summary>
    /// ID địa chỉ giao hàng (cho user đã đăng nhập)
    /// </summary>
    public int? ShippingAddressId { get; set; }

    /// <summary>
    /// Phương thức thanh toán: 'COD', 'VNPAY', etc
    /// </summary>
    [Required(ErrorMessage = "Payment method is required")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Payment method must be between 2 and 50 characters")]
    public string PaymentMethod { get; set; } = "COD";

    /// <summary>
    /// SessionId (cho khách vãng lai), có thể null
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Address data cho guest checkout (khi không có ShippingAddressId)
    /// </summary>
    public ShippingAddressInfoDto? ShippingAddress { get; set; }
}

/// <summary>
/// DTO cho thông tin địa chỉ giao hàng (dùng cho guest)
/// </summary>
public class ShippingAddressInfoDto
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Phone is required")]
    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Address line is required")]
    [StringLength(255, MinimumLength = 5, ErrorMessage = "Address line must be between 5 and 255 characters")]
    public string? AddressLine { get; set; }

    [Required(ErrorMessage = "District is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "District must be between 2 and 100 characters")]
    public string? District { get; set; }

    [Required(ErrorMessage = "City is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "City must be between 2 and 100 characters")]
    public string? City { get; set; }
}
