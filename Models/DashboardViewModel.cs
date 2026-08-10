namespace AbcRetail.Models;

public class DashboardViewModel
{
    public IReadOnlyList<CustomerProfile> Customers { get; set; } = [];
    public IReadOnlyList<ProductItem> Products { get; set; } = [];
    public IReadOnlyList<string> BlobImages { get; set; } = [];
    public IReadOnlyList<string> QueueMessages { get; set; } = [];
    public IReadOnlyList<string> LogFiles { get; set; } = [];
    public string StorageMode { get; set; } = "Local development";
    public CustomerProfile NewCustomer { get; set; } = new();
    public ProductItem NewProduct { get; set; } = new();
    public OrderMessage NewOrder { get; set; } = new();
}
