using Microsoft.Extensions.Configuration;
using ShopifyIntegration.Services;

// Lê as credenciais armazenadas fora do projeto.
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
var products = await shopifyService.GetProducts();

if (products.Count == 0)
{
    Console.WriteLine("No products were found.");
    return;
}

foreach (var product in products)
{
    Console.WriteLine($"Id: {product.Id}");
    Console.WriteLine($"Title: {product.Title}");
    Console.WriteLine($"Vendor: {product.Vendor}");
    Console.WriteLine($"Type: {product.ProductType}");
    Console.WriteLine($"Status: {product.Status}");
    Console.WriteLine();
}
