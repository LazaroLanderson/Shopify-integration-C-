using System.Text;
using System.Text.Json;

namespace ShopifyIntegration.Services;

public class ShopifyService
{
    private readonly HttpClient httpClient;
    private readonly string graphqlUrl;

    public ShopifyService(HttpClient httpClient, string graphqlUrl, string accessToken)
    {
        this.httpClient = httpClient;
        this.graphqlUrl = graphqlUrl;

        // Adiciona o token necessário para acessar a Shopify.
        this.httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
    }

    public async Task<string> SendGraphQlRequestAsync(string query)
    {
        var requestBody = JsonSerializer.Serialize(new { query });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(graphqlUrl, content);

        return await response.Content.ReadAsStringAsync();
    }
}
