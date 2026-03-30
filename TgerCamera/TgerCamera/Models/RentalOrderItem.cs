using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class RentalOrderItem
{
    public int Id { get; set; }

    public int RentalOrderId { get; set; }

    public int RentalProductId { get; set; }

    public decimal? PricePerDay { get; set; }

    public int? Quantity { get; set; }

    public virtual RentalOrder? RentalOrder { get; set; }

    public virtual RentalProduct? RentalProduct { get; set; }
}
