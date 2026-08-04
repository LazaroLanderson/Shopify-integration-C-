namespace ShopifyIntegration.Models;

public class ShopifyResponse
{
    public ShopifyData? Data { get; set; }
}

public class ShopifyData
{
    public Shop? Shop { get; set; }
}

public class Shop
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? MyshopifyDomain { get; set; }
}
