namespace TgerCamera.Dtos.Order;

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
