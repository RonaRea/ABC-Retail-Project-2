using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class OrderMessage
{
    [Required, Display(Name = "Order number")]
    public string OrderNumber { get; set; } = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}";

    [Required, Display(Name = "Customer email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, Display(Name = "Product")]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    public string Status { get; set; } = "Processing order";
}
