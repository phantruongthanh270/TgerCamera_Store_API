using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class RentalProduct
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public decimal PricePerDay { get; set; }

    public int AvailableQuantity { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Product? Product { get; set; }

    public virtual ICollection<RentalOrderItem> RentalOrderItems { get; set; } = new List<RentalOrderItem>();
}
