namespace ShopifyIntegration.Models;

public class ShopifyResponse
{
    public ShopifyData? Data { get; set; }
}

public class ShopifyData
{
    public Shop? Shop { get; set; }
    public ProductsResult? Products { get; set; }
}

public class Shop
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? MyshopifyDomain { get; set; }
}

public class ProductsResult
{
    public List<Product> Nodes { get; set; } = [];
}

public class Product
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Vendor { get; set; }
    public string? ProductType { get; set; }
    public string? Status { get; set; }
}
