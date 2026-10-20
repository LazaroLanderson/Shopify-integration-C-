using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ShopifyIntegration.Models;
using ShopifyIntegration.Queries;

namespace ShopifyIntegration.Services;

public class ShopifyService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient httpClient;
    private readonly string graphqlUrl;
    private readonly string accessToken;

    public ShopifyService(HttpClient httpClient, string graphqlUrl, string accessToken)
    {
        this.httpClient = httpClient;
        this.graphqlUrl = graphqlUrl;
        this.accessToken = accessToken;
    }

    public async Task<string> SendGraphQlRequestAsync(string query, object? variables = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(query, variables, cancellationToken);
        return response.Body;
    }

    public async Task<List<Product>> GetProducts()
    {
        var response = await SendRequestAsync(ShopifyQueries.GetProducts());
        return (response.Data.Products?.Nodes ?? [])
            .OrderBy(product => product.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        sku = sku.Trim();
        // GraphQL variables escape JSON; the search expression needs its own escaping.
        var escapedSku = sku.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var search = $"sku:\"{escapedSku}\"";
        string? after = null;
        Product? match = null;
        var cursors = new HashSet<string>(StringComparer.Ordinal);

        do
        {
            var response = await SendRequestAsync(ShopifyQueries.GetProductBySku(),
                new { search, after }, cancellationToken);
            var variants = response.Data.ProductVariants
                ?? throw new ShopifyApiException("Shopify did not return the variant search result.");

            foreach (var variant in variants.Nodes)
            {
                // Search results can include near matches. Only an exact SKU is accepted.
                if (!string.Equals(variant.Sku, sku, StringComparison.Ordinal))
                {
                    continue;
                }

                var product = variant.Product;
                if (product is null || string.IsNullOrWhiteSpace(product.Id))
                {
                    throw new ShopifyApiException("Shopify returned a matching variant without a product ID.");
                }

                if (match is not null && match.Id != product.Id)
                {
                    throw new ShopifyApiException("This SKU belongs to multiple products. Use a unique SKU before updating.");
                }

                match = product;
            }

            var pageInfo = variants.PageInfo
                ?? throw new ShopifyApiException("Shopify did not return pagination information.");
            if (!pageInfo.HasNextPage)
            {
                return match;
            }

            after = pageInfo.EndCursor;
            if (string.IsNullOrWhiteSpace(after) || !cursors.Add(after))
            {
                throw new ShopifyApiException("Shopify returned an invalid pagination cursor.");
            }
        } while (true);
    }

    public async Task<Product?> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default)
    {
        ValidateProductId(productId);
        var response = await SendRequestAsync(ShopifyQueries.GetProductById(),
            new { id = productId }, cancellationToken);
        return response.Data.Product;
    }

    public async Task<Product> UpdateProductAsync(ProductUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProductId(request.ProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Value);
        var value = request.Value.Trim();
        var field = request.Field switch
        {
            ProductUpdateField.Title => "title",
            ProductUpdateField.Vendor => "vendor",
            ProductUpdateField.Status => "status",
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unsupported product field.")
        };

        if (request.Field == ProductUpdateField.Status)
        {
            value = value.ToUpperInvariant();
            if (value is not ("ACTIVE" or "DRAFT" or "ARCHIVED" or "UNLISTED"))
            {
                throw new ArgumentException("Status must be ACTIVE, DRAFT, ARCHIVED, or UNLISTED.", nameof(request));
            }
        }

        // Omit all other fields so one update cannot overwrite unrelated product data.
        var input = new Dictionary<string, string> { ["id"] = request.ProductId, [field] = value };
        var response = await SendRequestAsync(ShopifyQueries.UpdateProduct(),
            new { product = input }, cancellationToken);
        var payload = response.Data.ProductUpdate
            ?? throw new ShopifyApiException("Shopify did not return the product update result.");

        if (payload.UserErrors.Count > 0)
        {
            var errors = payload.UserErrors.Select(error =>
                $"{string.Join(".", error.Field ?? [])}: {error.Message}");
            throw new ShopifyApiException($"Shopify rejected the update: {string.Join("; ", errors)}");
        }

        if (payload.Product is null || payload.Product.Id != request.ProductId)
        {
            throw new ShopifyApiException("Shopify did not return the updated product. Verify its state before retrying.");
        }

        return payload.Product;
    }

    public Task<List<Order>> GetOrders() => GetOrdersFromQuery(ShopifyQueries.GetOrders());
    public Task<List<Order>> GetPaidOrders() => GetOrdersFromQuery(ShopifyQueries.GetPaidOrders());
    public Task<List<Order>> GetUnfulfilledOrders() => GetOrdersFromQuery(ShopifyQueries.GetUnfulfilledOrders());

    private async Task<List<Order>> GetOrdersFromQuery(string query)
    {
        var response = await SendRequestAsync(query);
        return response.Data.Orders?.Nodes ?? [];
    }

    private static void ValidateProductId(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        const string prefix = "gid://shopify/Product/";
        if (!productId.StartsWith(prefix, StringComparison.Ordinal) ||
            !ulong.TryParse(productId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
        {
            throw new ArgumentException("A valid Shopify product GID is required.", nameof(productId));
        }
    }

    private async Task<(string Body, ShopifyData Data)> SendRequestAsync(string query, object? variables = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, graphqlUrl);
        request.Headers.Add("X-Shopify-Access-Token", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Shopify authentication failed. Check the access token.",
                    HttpStatusCode.Forbidden => "Shopify denied access. Check the app's product permissions.",
                    HttpStatusCode.TooManyRequests => "Shopify's rate limit was reached. Try again later.",
                    _ => $"Shopify returned HTTP {(int)response.StatusCode}."
                };
                throw new ShopifyApiException($"{message} If this was an update, verify the product before retrying.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var envelope = JsonSerializer.Deserialize<ShopifyResponse>(body, JsonOptions)
                ?? throw new ShopifyApiException("Shopify returned an empty response.");
            if (envelope.Errors.Count > 0)
            {
                throw new ShopifyApiException($"Shopify GraphQL error: {string.Join("; ", envelope.Errors.Select(error => error.Message))}");
            }

            var data = envelope.Data
                ?? throw new ShopifyApiException("Shopify returned a response without data.");
            return (body, data);
        }
        catch (HttpRequestException exception)
        {
            throw new ShopifyApiException("Could not communicate with Shopify. If this was an update, verify the product before retrying.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopifyApiException("The Shopify request timed out. If this was an update, verify the product before retrying.", exception);
        }
        catch (JsonException exception)
        {
            throw new ShopifyApiException("Shopify returned an invalid JSON response. If this was an update, verify the product before retrying.", exception);
        }
    }
}
