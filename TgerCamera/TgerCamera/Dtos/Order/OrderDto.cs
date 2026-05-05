using System;
using System.Collections.Generic;

namespace TgerCamera.Dtos.Order;

public class OrderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? SessionId { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Status { get; set; }
    public string? ShippingFullName { get; set; }
    public string? ShippingPhone { get; set; }
    public string? ShippingAddressLine { get; set; }
    public string? ShippingDistrict { get; set; }
    public string? ShippingCity { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
}
