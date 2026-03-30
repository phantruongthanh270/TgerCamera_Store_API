using System;
using System.Collections.Generic;

namespace TgerCamera.Models;

public partial class ProductSpecification
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public virtual Product? Product { get; set; }
}
