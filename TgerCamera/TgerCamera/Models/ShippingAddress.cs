using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class ShippingAddress
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public bool? IsDefault { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User? User { get; set; }
}
