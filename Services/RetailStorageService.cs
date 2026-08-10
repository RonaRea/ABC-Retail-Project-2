using AbcRetail.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AbcRetail.Services;

public class RetailStorageService : IRetailStorageService
{
    private readonly StorageOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public RetailStorageService(IOptions<StorageOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public string Mode => UseAzure ? "Azure Storage Services" : "Local development storage";

    private bool UseAzure => !_options.UseLocalStorage && !string.IsNullOrWhiteSpace(_options.ConnectionString);
    private string LocalDataPath => Path.Combine(_environment.ContentRootPath, "App_Data");
    private string LocalBlobPath => Path.Combine(_environment.WebRootPath, "uploads");

    public async Task SeedAsync()
    {
        if (UseAzure)
        {
            await EnsureAzureAsync();
            await SeedProductImagesAsync();
            if ((await GetCustomersAsync()).Count >= 5 && (await GetProductsAsync()).Count >= 5)
            {
                return;
            }
        }
        else
        {
            Directory.CreateDirectory(LocalDataPath);
            Directory.CreateDirectory(LocalBlobPath);
            await SeedProductImagesAsync();
            if (File.Exists(Path.Combine(LocalDataPath, "customers.json")) &&
                File.Exists(Path.Combine(LocalDataPath, "products.json")))
            {
                return;
            }
        }

        var customers = new[]
        {
            new CustomerProfile { FullName = "Ava Naidoo", Email = "ava.naidoo@gmail.com", City = "Durban", LoyaltyTier = "Gold" },
            new CustomerProfile { FullName = "Thabo Mokoena", Email = "thabo.mokoena@gmail.com", City = "Johannesburg", LoyaltyTier = "Silver" },
            new CustomerProfile { FullName = "Mia Jacobs", Email = "mia.jacobs@gmail.com", City = "Cape Town", LoyaltyTier = "Standard" },
            new CustomerProfile { FullName = "Liam Smith", Email = "liam.smith@gmail.com", City = "Pretoria", LoyaltyTier = "Gold" },
            new CustomerProfile { FullName = "Zara Khan", Email = "zara.khan@gmail.com", City = "Gqeberha", LoyaltyTier = "Silver" }
        };

        var productImageUrls = await SeedProductImagesAsync();

        var products = new[]
        {
            new ProductItem { Name = "Wireless Headphones", Category = "Electronics", Price = 899.99, Stock = 42, ImageUrl = productImageUrls[0] },
            new ProductItem { Name = "Cotton Hoodie", Category = "Clothing", Price = 459.00, Stock = 65, ImageUrl = productImageUrls[1] },
            new ProductItem { Name = "Desk Lamp", Category = "Home", Price = 299.50, Stock = 31, ImageUrl = productImageUrls[2] },
            new ProductItem { Name = "Running Shoes", Category = "Sports", Price = 1199.00, Stock = 18, ImageUrl = productImageUrls[3] },
            new ProductItem { Name = "Travel Mug", Category = "Kitchen", Price = 149.99, Stock = 94, ImageUrl = productImageUrls[4] }
        };

        foreach (var customer in customers)
        {
            await AddCustomerAsync(customer);
        }

        foreach (var product in products)
        {
            await AddProductAsync(product);
        }

        await QueueOrderAsync(new OrderMessage { CustomerEmail = customers[0].Email, ProductName = products[0].Name, Quantity = 1 });
        await WriteLogAsync("Seeded ABC Retail demo records for assessment screenshots.");
    }

    public async Task<IReadOnlyList<CustomerProfile>> GetCustomersAsync()
    {
        if (!UseAzure)
        {
            return await ReadLocalAsync<CustomerProfile>("customers.json");
        }

        var table = new TableClient(_options.ConnectionString, _options.CustomerTable);
        var rows = new List<CustomerProfile>();
        await foreach (var customer in table.QueryAsync<CustomerProfile>())
        {
            rows.Add(customer);
        }
        return rows.OrderBy(customer => customer.FullName).ToList();
    }

    public async Task<IReadOnlyList<ProductItem>> GetProductsAsync()
    {
        if (!UseAzure)
        {
            return await ReadLocalAsync<ProductItem>("products.json");
        }

        var table = new TableClient(_options.ConnectionString, _options.ProductTable);
        var rows = new List<ProductItem>();
        await foreach (var product in table.QueryAsync<ProductItem>())
        {
            rows.Add(product);
        }
        return rows.OrderBy(product => product.Name).ToList();
    }

    public async Task<IReadOnlyList<string>> GetBlobImagesAsync()
    {
        if (!UseAzure)
        {
            Directory.CreateDirectory(LocalBlobPath);
            return Directory.GetFiles(LocalBlobPath).Select(file => $"/uploads/{Path.GetFileName(file)}").ToList();
        }

        var container = new BlobContainerClient(_options.ConnectionString, _options.BlobContainer);
        var images = new List<string>();
        await foreach (var blob in container.GetBlobsAsync())
        {
            images.Add(container.GetBlobClient(blob.Name).Uri.ToString());
        }
        return images;
    }

    public async Task<IReadOnlyList<string>> GetQueueMessagesAsync()
    {
        if (!UseAzure)
        {
            return await ReadLocalAsync<string>("queue.json");
        }

        var queue = new QueueClient(_options.ConnectionString, _options.QueueName);
        var messages = await queue.PeekMessagesAsync(maxMessages: 10);
        return messages.Value.Select(message => message.MessageText).ToList();
    }

    public Task<IReadOnlyList<string>> GetLogFilesAsync()
    {
        if (!UseAzure)
        {
            Directory.CreateDirectory(LocalDataPath);
            IReadOnlyList<string> logs = Directory.GetFiles(LocalDataPath, "*.log").Select(Path.GetFileName).OfType<string>().ToList();
            return Task.FromResult(logs);
        }

        return GetAzureLogFilesAsync();
    }

    public async Task AddCustomerAsync(CustomerProfile customer)
    {
        customer.PartitionKey = "CUSTOMER";
        customer.RowKey = string.IsNullOrWhiteSpace(customer.RowKey) ? Guid.NewGuid().ToString("N") : customer.RowKey;

        if (!UseAzure)
        {
            var customers = (await ReadLocalAsync<CustomerProfile>("customers.json")).ToList();
            customers.Add(customer);
            await WriteLocalAsync("customers.json", customers);
            return;
        }

        var table = new TableClient(_options.ConnectionString, _options.CustomerTable);
        await table.CreateIfNotExistsAsync();
        await table.UpsertEntityAsync(customer);
    }

    public async Task AddProductAsync(ProductItem product)
    {
        product.PartitionKey = "PRODUCT";
        product.RowKey = string.IsNullOrWhiteSpace(product.RowKey) ? Guid.NewGuid().ToString("N") : product.RowKey;

        if (!UseAzure)
        {
            var products = (await ReadLocalAsync<ProductItem>("products.json")).ToList();
            products.Add(product);
            await WriteLocalAsync("products.json", products);
            return;
        }

        var table = new TableClient(_options.ConnectionString, _options.ProductTable);
        await table.CreateIfNotExistsAsync();
        await table.UpsertEntityAsync(product);
    }

    public async Task<string> UploadImageAsync(IFormFile image)
    {
        if (image.Length == 0)
        {
            return string.Empty;
        }

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(image.FileName)}";

        if (!UseAzure)
        {
            Directory.CreateDirectory(LocalBlobPath);
            var filePath = Path.Combine(LocalBlobPath, fileName);
            await using var file = File.Create(filePath);
            await image.CopyToAsync(file);
            return $"/uploads/{fileName}";
        }

        var container = new BlobContainerClient(_options.ConnectionString, _options.BlobContainer);
        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient(fileName);
        await using var stream = image.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true);
        return blob.Uri.ToString();
    }

    public async Task QueueOrderAsync(OrderMessage order)
    {
        var payload = JsonSerializer.Serialize(new
        {
            order.OrderNumber,
            order.CustomerEmail,
            order.ProductName,
            order.Quantity,
            order.Status,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (!UseAzure)
        {
            var messages = (await ReadLocalAsync<string>("queue.json")).ToList();
            messages.Insert(0, payload);
            await WriteLocalAsync("queue.json", messages.Take(10).ToList());
            return;
        }

        var queue = new QueueClient(_options.ConnectionString, _options.QueueName);
        await queue.CreateIfNotExistsAsync();
        await queue.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)));
    }

    public async Task WriteLogAsync(string message)
    {
        var line = $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}";

        if (!UseAzure)
        {
            Directory.CreateDirectory(LocalDataPath);
            await File.AppendAllTextAsync(Path.Combine(LocalDataPath, "application.log"), line);
            return;
        }

        var share = new ShareClient(_options.ConnectionString, _options.FileShare);
        await share.CreateIfNotExistsAsync();
        var root = share.GetRootDirectoryClient();
        var file = root.GetFileClient("application.log");
        if (!await file.ExistsAsync())
        {
            await file.CreateAsync(0);
        }

        var existing = await file.GetPropertiesAsync();
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        await file.SetHttpHeadersAsync(existing.Value.ContentLength + bytes.Length, default, default, default, CancellationToken.None);
        await using var stream = new MemoryStream(bytes);
        await file.UploadRangeAsync(new Azure.HttpRange(existing.Value.ContentLength, bytes.Length), stream);
    }

    private async Task<string> SeedImageAsync(string fileName, string color, string label)
    {
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="640" height="640" viewBox="0 0 640 640">
              <rect width="640" height="640" rx="48" fill="#f3f6f8"/>
              <circle cx="320" cy="270" r="140" fill="{color}"/>
              <rect x="120" y="430" width="400" height="60" rx="30" fill="#18212f"/>
              <text x="320" y="468" text-anchor="middle" font-family="Arial, sans-serif" font-size="30" font-weight="700" fill="#ffffff">{label}</text>
            </svg>
            """;

        if (!UseAzure)
        {
            Directory.CreateDirectory(LocalBlobPath);
            var path = Path.Combine(LocalBlobPath, fileName);
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, svg);
            }
            return $"/uploads/{fileName}";
        }

        var container = new BlobContainerClient(_options.ConnectionString, _options.BlobContainer);
        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient(fileName);
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
        await blob.UploadAsync(stream, overwrite: true);
        return blob.Uri.ToString();
    }

    private async Task<string[]> SeedProductImagesAsync()
    {
        return
        [
            await SeedImageAsync("wireless-headphones.svg", "#1561a5", "Headphones"),
            await SeedImageAsync("cotton-hoodie.svg", "#7d5a50", "Hoodie"),
            await SeedImageAsync("desk-lamp.svg", "#d89b28", "Lamp"),
            await SeedImageAsync("running-shoes.svg", "#2f7d5c", "Shoes"),
            await SeedImageAsync("travel-mug.svg", "#6b7280", "Mug")
        ];
    }

    private async Task EnsureAzureAsync()
    {
        await new TableClient(_options.ConnectionString, _options.CustomerTable).CreateIfNotExistsAsync();
        await new TableClient(_options.ConnectionString, _options.ProductTable).CreateIfNotExistsAsync();
        await new BlobContainerClient(_options.ConnectionString, _options.BlobContainer).CreateIfNotExistsAsync();
        await new QueueClient(_options.ConnectionString, _options.QueueName).CreateIfNotExistsAsync();
        await new ShareClient(_options.ConnectionString, _options.FileShare).CreateIfNotExistsAsync();
    }

    private async Task<IReadOnlyList<string>> GetAzureLogFilesAsync()
    {
        var share = new ShareClient(_options.ConnectionString, _options.FileShare);
        var root = share.GetRootDirectoryClient();
        var files = new List<string>();
        await foreach (var item in root.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory)
            {
                files.Add(item.Name);
            }
        }
        return files;
    }

    private async Task<IReadOnlyList<T>> ReadLocalAsync<T>(string fileName)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(LocalDataPath);
            var path = Path.Combine(LocalDataPath, fileName);
            if (!File.Exists(path))
            {
                return [];
            }

            await using var file = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(file, _jsonOptions) ?? [];
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteLocalAsync<T>(string fileName, IReadOnlyList<T> values)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(LocalDataPath);
            var path = Path.Combine(LocalDataPath, fileName);
            await using var file = File.Create(path);
            await JsonSerializer.SerializeAsync(file, values, _jsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }
}
