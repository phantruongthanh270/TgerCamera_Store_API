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

    public int? ShippingAddressId { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ShippingAddress? ShippingAddress { get; set; }

    public virtual User? User { get; set; }
}
