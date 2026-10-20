namespace ShopifyIntegration.Models;

public enum ProductUpdateField
{
    Title,
    Vendor,
    Status
}

public record ProductUpdateRequest(string ProductId, ProductUpdateField Field, string Value);
