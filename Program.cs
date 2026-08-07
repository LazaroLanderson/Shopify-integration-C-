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

var csvReader = new CsvReader();
List<InventoryRecord> inventoryRecords;

try
{
    inventoryRecords = csvReader.Read(Path.Combine(AppContext.BaseDirectory, "inventory.csv"));
}
catch (InvalidOperationException exception)
{
    Console.WriteLine($"CSV error: {exception.Message}");
    return;
}

Console.WriteLine("Inventory records");
Console.WriteLine("-----------------");

foreach (var record in inventoryRecords)
{
    Console.WriteLine($"SKU: {record.Sku} | Quantity: {record.Quantity}");
}

Console.WriteLine();

var graphqlUrl = $"{storeUrl.TrimEnd('/')}/admin/api/2026-07/graphql.json";

using var httpClient = new HttpClient();
var shopifyService = new ShopifyService(httpClient, graphqlUrl, accessToken);
var products = await shopifyService.GetProducts();

Console.WriteLine("Shopify products");
Console.WriteLine("----------------");

if (products.Count == 0)
{
    Console.WriteLine("No products were found.");
}

foreach (var product in products)
{
    Console.WriteLine($"Title: {product.Title}");
    Console.WriteLine($"Id: {product.Id}");
    Console.WriteLine($"Vendor: {product.Vendor}");
    Console.WriteLine($"Type: {product.ProductType}");
    Console.WriteLine($"Status: {product.Status}");

    foreach (var variant in product.Variants.Nodes)
    {
        Console.WriteLine($"SKU: {variant.Sku}");
    }

    Console.WriteLine();
}

var productsBySku = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

foreach (var product in products)
{
    foreach (var variant in product.Variants.Nodes)
    {
        if (!string.IsNullOrWhiteSpace(variant.Sku))
        {
            productsBySku.TryAdd(variant.Sku, product);
        }
    }
}

var generatedAt = DateTimeOffset.UtcNow;
var reportRecords = new List<IntegrationReportRecord>();

foreach (var inventoryRecord in inventoryRecords)
{
    productsBySku.TryGetValue(inventoryRecord.Sku, out var product);

    reportRecords.Add(new IntegrationReportRecord
    {
        Sku = inventoryRecord.Sku,
        Exists = product is not null,
        Title = product?.Title,
        Vendor = product?.Vendor,
        Status = product?.Status,
        GeneratedAt = generatedAt
    });
}

var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "IntegrationReport.csv");
new ReportWriter().Write(reportPath, reportRecords);

var foundProducts = reportRecords.Count(record => record.Exists);

Console.WriteLine("Summary");
Console.WriteLine("-------");
Console.WriteLine($"Products in CSV: {inventoryRecords.Count}");
Console.WriteLine($"Found in Shopify: {foundProducts}");
Console.WriteLine($"Missing Products: {inventoryRecords.Count - foundProducts}");
Console.WriteLine($"Report created: {reportPath}");