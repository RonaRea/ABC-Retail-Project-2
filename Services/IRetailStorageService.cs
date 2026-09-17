using AbcRetail.Models;

namespace AbcRetail.Services;

public interface IRetailStorageService
{
    string Mode { get; }
    Task SeedAsync();
    Task<IReadOnlyList<CustomerProfile>> GetCustomersAsync();
    Task<IReadOnlyList<ProductItem>> GetProductsAsync();
    Task<IReadOnlyList<string>> GetBlobImagesAsync();
    Task<IReadOnlyList<string>> GetQueueMessagesAsync();
    Task<IReadOnlyList<string>> GetLogFilesAsync();
    Task<string?> ProcessNextQueueMessageAsync();
    Task<IReadOnlyList<string>> GetLogEntriesAsync(int maxEntries = 10);
    Task AddCustomerAsync(CustomerProfile customer);
    Task AddProductAsync(ProductItem product);
    Task<string> UploadImageAsync(IFormFile image);
    Task QueueOrderAsync(OrderMessage order);
    Task WriteLogAsync(string message);
}
