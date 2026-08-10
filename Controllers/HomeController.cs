using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AbcRetail.Models;
using AbcRetail.Services;

namespace AbcRetail.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRetailStorageService _storage;

    public HomeController(ILogger<HomeController> logger, IRetailStorageService storage)
    {
        _logger = logger;
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        await _storage.SeedAsync();
        return View(await BuildDashboardAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomer(CustomerProfile customer)
    {
        if (ModelState.IsValid)
        {
            await _storage.AddCustomerAsync(customer);
            await _storage.WriteLogAsync($"Customer profile saved for {customer.Email}.");
            TempData["Status"] = "Customer profile saved to Azure Tables.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(ProductItem product, IFormFile? image)
    {
        if (image is { Length: > 0 })
        {
            product.ImageUrl = await _storage.UploadImageAsync(image);
        }

        if (ModelState.IsValid)
        {
            await _storage.AddProductAsync(product);
            await _storage.WriteLogAsync($"Product record saved for {product.Name}.");
            TempData["Status"] = "Product saved to Azure Tables and image stored in Blob Storage.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile image)
    {
        if (image.Length > 0)
        {
            await _storage.UploadImageAsync(image);
            await _storage.WriteLogAsync($"Image uploaded: {image.FileName}.");
            TempData["Status"] = "Image uploaded to Blob Storage.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QueueOrder(OrderMessage order)
    {
        if (ModelState.IsValid)
        {
            await _storage.QueueOrderAsync(order);
            await _storage.WriteLogAsync($"Queued {order.Status} message for {order.OrderNumber}.");
            TempData["Status"] = "Order/inventory event added to Azure Queue Storage.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WriteLog(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _storage.WriteLogAsync(message);
            TempData["Status"] = "Operational log written to Azure Files.";
        }

        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<DashboardViewModel> BuildDashboardAsync()
    {
        return new DashboardViewModel
        {
            Customers = await _storage.GetCustomersAsync(),
            Products = await _storage.GetProductsAsync(),
            BlobImages = await _storage.GetBlobImagesAsync(),
            QueueMessages = await _storage.GetQueueMessagesAsync(),
            LogFiles = await _storage.GetLogFilesAsync(),
            StorageMode = _storage.Mode
        };
    }
}
