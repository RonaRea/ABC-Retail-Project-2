namespace AbcRetail.Services;

public class StorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string CustomerTable { get; set; } = "CustomerProfiles";
    public string ProductTable { get; set; } = "Products";
    public string BlobContainer { get; set; } = "product-images";
    public string QueueName { get; set; } = "order-processing";
    public string FileShare { get; set; } = "retail-logs";
    public bool UseLocalStorage { get; set; } = true;
}
