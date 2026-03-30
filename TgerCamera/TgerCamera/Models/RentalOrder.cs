using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class RentalOrder
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? SessionId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Status { get; set; }

    public decimal? TotalPrice { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<RentalOrderItem> RentalOrderItems { get; set; } = new List<RentalOrderItem>();

    public virtual User? User { get; set; }
}
