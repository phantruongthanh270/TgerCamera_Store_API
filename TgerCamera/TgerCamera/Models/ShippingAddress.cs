using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class ShippingAddress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string City { get; set; } = null!;

    public string District { get; set; } = null!;

    public bool IsDefault { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
