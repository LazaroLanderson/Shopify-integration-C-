using Microsoft.Extensions.Configuration;
using ShopifyIntegration.Models;
using ShopifyIntegration.Services;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>()
    .Build();

var storeUrl = configuration["Shopify:StoreUrl"];
var accessToken = configuration["Shopify:AccessToken"];

if (string.IsNullOrWhiteSpace(storeUrl) || string.IsNullOrWhiteSpace(accessToken))
{
    Console.WriteLine("Please set Shopify credentials with dotnet user-secrets.");
    return;
}

if (!storeUrl.StartsWith("http://") && !storeUrl.StartsWith("https://"))
{
    storeUrl = $"https://{storeUrl}";
}

if (!Uri.TryCreate(storeUrl, UriKind.Absolute, out _))
{
    Console.WriteLine("StoreUrl must be a valid URL.");
    return;
}

var graphqlUrl = $"{storeUrl.TrimEnd('/')}/admin/api/2026-07/graphql.json";

using var httpClient = new HttpClient();
var shopifyService = new ShopifyService(httpClient, graphqlUrl, accessToken);
var orders = await shopifyService.GetOrders();
var paidOrders = await shopifyService.GetPaidOrders();
var unfulfilledOrders = await shopifyService.GetUnfulfilledOrders();

Console.WriteLine($"Total orders: {orders.Count}");
Console.WriteLine($"Paid orders: {paidOrders.Count}");
Console.WriteLine($"Unfulfilled orders: {unfulfilledOrders.Count}");
Console.WriteLine();

if (orders.Count == 0)
{
    Console.WriteLine("No orders were found.");
    return;
}

DisplayOrders("Paid orders", paidOrders);
DisplayOrders("Unfulfilled orders", unfulfilledOrders);

static void DisplayOrders(string title, List<Order> orders)
{
    Console.WriteLine(title);
    Console.WriteLine(new string('-', title.Length));

    if (orders.Count == 0)
    {
        Console.WriteLine("No orders found.");
        Console.WriteLine();
        return;
    }

    foreach (var order in orders)
    {
        var customerName = $"{order.Customer?.FirstName} {order.Customer?.LastName}".Trim();
        var total = order.TotalPriceSet?.ShopMoney;

        Console.WriteLine($"Order: {order.Name}");
        Console.WriteLine($"Id: {order.Id}");
        Console.WriteLine($"Customer: {customerName}");
        Console.WriteLine($"Email: {order.Customer?.Email}");
        Console.WriteLine($"Created at: {order.CreatedAt:dd/MM/yyyy HH:mm}");
        Console.WriteLine($"Financial status: {order.DisplayFinancialStatus}");
        Console.WriteLine($"Fulfillment status: {order.DisplayFulfillmentStatus}");
        Console.WriteLine($"Total: {total?.Amount} {total?.CurrencyCode}");
        Console.WriteLine();
    }
}