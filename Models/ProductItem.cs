using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class ProductItem : ITableEntity
{
    public string PartitionKey { get; set; } = "PRODUCT";
    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    [Required, Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, 999999)]
    public double Price { get; set; }

    [Range(0, 999999)]
    public int Stock { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
}
