using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ShopifyIntegration.Models;
using ShopifyIntegration.Queries;
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
var response = await shopifyService.SendGraphQlRequestAsync(ShopifyQueries.GetShop);

var shopifyResponse = JsonSerializer.Deserialize<ShopifyResponse>(
    response,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

var shop = shopifyResponse?.Data?.Shop;

if (shop is null)
{
    Console.WriteLine("Could not read shop data.");
    return;
}

Console.WriteLine($"Shop Name: {shop.Name}");
Console.WriteLine($"Shop Email: {shop.Email}");
Console.WriteLine($"Shop Domain: {shop.MyshopifyDomain}");
