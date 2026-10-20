using Microsoft.Extensions.Configuration;
using ShopifyIntegration.Services;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>()
    .Build();

var logger = new ExecutionLogger(Path.Combine(Directory.GetCurrentDirectory(), "logs", "execution.log"));
logger.Log("Application started.");

var storeUrl = configuration["Shopify:StoreUrl"];
var accessToken = configuration["Shopify:AccessToken"];
var inventoryFilePath = configuration["Inventory:FilePath"];

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

if (!string.IsNullOrWhiteSpace(inventoryFilePath) && !Path.IsPathRooted(inventoryFilePath))
{
    inventoryFilePath = Path.Combine(AppContext.BaseDirectory, inventoryFilePath);
}

var graphqlUrl = $"{storeUrl.TrimEnd('/')}/admin/api/2026-07/graphql.json";
using var httpClient = new HttpClient();
var shopifyService = new ShopifyService(httpClient, graphqlUrl, accessToken);
var updateWorkflow = new ProductUpdateWorkflow(shopifyService, Console.In, Console.Out);
var reportWorkflow = new InventoryReportWorkflow(shopifyService, inventoryFilePath,
    Directory.GetCurrentDirectory(), Console.Out);
var menu = new ConsoleMenu(updateWorkflow, reportWorkflow, Console.In, Console.Out);
await menu.RunAsync();
