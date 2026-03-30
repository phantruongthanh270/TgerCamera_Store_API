namespace TgerCamera.Dtos;

public class RentalProductDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal PricePerDay { get; set; }
    public int AvailableQuantity { get; set; }
}
