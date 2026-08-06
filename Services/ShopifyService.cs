using System.Text;
using System.Text.Json;
using ShopifyIntegration.Models;
using ShopifyIntegration.Queries;

namespace ShopifyIntegration.Services;

public class ShopifyService
{
    private readonly HttpClient httpClient;
    private readonly string graphqlUrl;

    public ShopifyService(HttpClient httpClient, string graphqlUrl, string accessToken)
    {
        this.httpClient = httpClient;
        this.graphqlUrl = graphqlUrl;
        this.httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
    }

    public async Task<string> SendGraphQlRequestAsync(string query)
    {
        var requestBody = JsonSerializer.Serialize(new { query });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(graphqlUrl, content);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<Product>> GetProducts()
    {
        var response = await SendGraphQlRequestAsync(ShopifyQueries.GetProducts());
        var shopifyResponse = JsonSerializer.Deserialize<ShopifyResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return (shopifyResponse?.Data?.Products?.Nodes ?? [])
            .OrderBy(product => product.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<Order>> GetOrders(string? searchFilter = null)
    {
        var response = await SendGraphQlRequestAsync(ShopifyQueries.GetOrders(searchFilter));
        var shopifyResponse = JsonSerializer.Deserialize<ShopifyResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return shopifyResponse?.Data?.Orders?.Nodes ?? [];
    }
}
