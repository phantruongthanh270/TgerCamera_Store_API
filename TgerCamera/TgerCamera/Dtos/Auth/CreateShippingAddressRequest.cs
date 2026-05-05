using System.ComponentModel.DataAnnotations;

namespace TgerCamera.Dtos.Auth;

/// <summary>
/// DTO cho yêu cầu tạo địa chỉ giao hàng
/// </summary>
public class CreateShippingAddressRequest
{
    /// <summary>
    /// Họ tên người nhận
    /// </summary>
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số điện thoại
    /// </summary>
    [Required(ErrorMessage = "Phone is required")]
    [Phone(ErrorMessage = "Phone must be a valid phone number")]
    [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ chi tiết
    /// </summary>
    [Required(ErrorMessage = "Address line is required")]
    [StringLength(255, MinimumLength = 5, ErrorMessage = "Address line must be between 5 and 255 characters")]
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>
    /// Quận/Huyện
    /// </summary>
    [Required(ErrorMessage = "District is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "District must be between 2 and 100 characters")]
    public string District { get; set; } = string.Empty;

    /// <summary>
    /// Tỉnh/Thành phố
    /// </summary>
    [Required(ErrorMessage = "City is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "City must be between 2 and 100 characters")]
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Đặt làm địa chỉ mặc định
    /// </summary>
    public bool IsDefault { get; set; } = false;
}
