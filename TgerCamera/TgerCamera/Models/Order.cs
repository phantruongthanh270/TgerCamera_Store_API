using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class Order
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? SessionId { get; set; }

    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = null!;

    public string ShippingFullName { get; set; } = null!;

    public string ShippingPhone { get; set; } = null!;

    public string ShippingAddressLine { get; set; } = null!;

    public string ShippingDistrict { get; set; } = null!;

    public string ShippingCity { get; set; } = null!;

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual User? User { get; set; }
}
