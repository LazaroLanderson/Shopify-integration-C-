namespace ShopifyIntegration.Models;

public class ShopifyResponse
{
    public ShopifyData? Data { get; set; }
}

public class ShopifyData
{
    public Shop? Shop { get; set; }
    public ProductsResult? Products { get; set; }
    public OrdersResult? Orders { get; set; }
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

public class OrdersResult
{
    public List<Order> Nodes { get; set; } = [];
}

public class Order
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DisplayFinancialStatus { get; set; }
    public string? DisplayFulfillmentStatus { get; set; }
    public Customer? Customer { get; set; }
    public MoneyBag? TotalPriceSet { get; set; }
}

public class Customer
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class MoneyBag
{
    public Money? ShopMoney { get; set; }
}

public class Money
{
    public string? Amount { get; set; }
    public string? CurrencyCode { get; set; }
}