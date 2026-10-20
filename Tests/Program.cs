using System.Net;
using System.Text;
using System.Text.Json;
using ShopifyIntegration.Models;
using ShopifyIntegration.Services;

const string productId = "gid://shopify/Product/1";
var tests = new List<(string Name, Func<Task> Run)>();

Add("SKU lookup paginates and rejects near matches", async () =>
{
    using var handler = new StubHandler(
        Json(Page([Variant("ABC-1-extra", productId)], true, "page-2")),
        Json(Page([Variant("ABC-1", productId)])));
    using var client = new HttpClient(handler);
    var product = await Service(client).GetProductBySkuAsync(" ABC-1 ");
    Check(product?.Id == productId, "Expected the exact matching product.");
    Check(handler.Requests.Count == 2, "Expected two pages.");
    using var request = JsonDocument.Parse(handler.Requests[1]);
    Check(request.RootElement.GetProperty("variables").GetProperty("after").GetString() == "page-2", "Cursor not passed.");
});

Add("SKU lookup returns null when no exact match exists", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(Page([Variant("abc-1", productId)]))));
    Check(await Service(client).GetProductBySkuAsync("ABC-1") is null, "SKU matching must be ordinal and exact.");
});

Add("Duplicate SKU on different products is rejected across pages", async () =>
{
    using var client = new HttpClient(new StubHandler(
        Json(Page([Variant("ABC", productId)], true, "next")),
        Json(Page([Variant("ABC", "gid://shopify/Product/2")]))));
    await Throws<ShopifyApiException>(() => Service(client).GetProductBySkuAsync("ABC"), "multiple products");
});

Add("Duplicate SKU on the same product is allowed", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(Page([Variant("ABC", productId), Variant("ABC", productId)]))));
    Check((await Service(client).GetProductBySkuAsync("ABC"))?.Id == productId, "Expected the common parent.");
});

Add("Repeated pagination cursor fails instead of looping", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(Page([], true, "same")), Json(Page([], true, "same"))));
    await Throws<ShopifyApiException>(() => Service(client).GetProductBySkuAsync("ABC"), "cursor");
});

Add("SKU quotes and backslashes are escaped in search variables", async () =>
{
    using var handler = new StubHandler(Json(Page([])));
    using var client = new HttpClient(handler);
    await Service(client).GetProductBySkuAsync("A\"B\\C");
    using var request = JsonDocument.Parse(handler.Requests.Single());
    Check(request.RootElement.GetProperty("variables").GetProperty("search").GetString() == "sku:\"A\\\"B\\\\C\"", "Search escaping failed.");
    Check(!request.RootElement.GetProperty("query").GetString()!.Contains("A\"B"), "SKU interpolated into query.");
});

Add("Product retrieval uses the product ID", async () =>
{
    using var handler = new StubHandler(Json(new { data = new { product = Product(productId) } }));
    using var client = new HttpClient(handler);
    Check((await Service(client).GetProductByIdAsync(productId))?.Title == "Original", "Product was not read.");
    using var request = JsonDocument.Parse(handler.Requests.Single());
    Check(request.RootElement.GetProperty("variables").GetProperty("id").GetString() == productId, "Wrong product ID.");
});

Add("Missing product ID lookup returns null", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(new { data = new { product = (object?)null } })));
    Check(await Service(client).GetProductByIdAsync(productId) is null, "Expected null.");
});

foreach (var field in Enum.GetValues<ProductUpdateField>())
{
    Add($"{field} update sends only the selected field and ID", async () =>
    {
        using var handler = new StubHandler(Json(new { data = new { productUpdate = new { product = Product(productId), userErrors = Array.Empty<object>() } } }));
        using var client = new HttpClient(handler);
        var value = field == ProductUpdateField.Status ? " draft " : " New \"value\" ";
        await Service(client).UpdateProductAsync(new(productId, field, value));
        using var request = JsonDocument.Parse(handler.Requests.Single());
        var input = request.RootElement.GetProperty("variables").GetProperty("product");
        Check(input.EnumerateObject().Count() == 2, "Unexpected fields in update.");
        var fieldName = field.ToString().ToLowerInvariant();
        Check(input.GetProperty(fieldName).GetString() == (field == ProductUpdateField.Status ? "DRAFT" : "New \"value\""), "Value normalization failed.");
    });
}

Add("Invalid arguments are rejected before any HTTP request", async () =>
{
    using var handler = new StubHandler();
    using var client = new HttpClient(handler);
    var service = Service(client);
    await Throws<ArgumentException>(() => service.GetProductBySkuAsync(" "));
    await Throws<ArgumentException>(() => service.GetProductByIdAsync("gid://shopify/ProductVariant/1"));
    await Throws<ArgumentException>(() => service.UpdateProductAsync(new(productId, ProductUpdateField.Title, " ")));
    await Throws<ArgumentException>(() => service.UpdateProductAsync(new(productId, ProductUpdateField.Status, "INVALID")));
    await Throws<ArgumentOutOfRangeException>(() => service.UpdateProductAsync(new(productId, (ProductUpdateField)99, "value")));
    Check(handler.Requests.Count == 0, "Invalid input triggered HTTP.");
});

Add("Mutation userErrors include the field and message", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(new { data = new { productUpdate = new
    {
        product = (object?)null,
        userErrors = new[] { new { field = new[] { "product", "title" }, message = "Title is invalid" } }
    } } })));
    await Throws<ShopifyApiException>(() => Service(client).UpdateProductAsync(new(productId, ProductUpdateField.Title, "New")), "product.title: Title is invalid");
});

Add("GraphQL errors reject partial data", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(new
    {
        data = new { product = Product(productId) }, errors = new[] { new { message = "Access denied" } }
    })));
    await Throws<ShopifyApiException>(() => Service(client).GetProductByIdAsync(productId), "Access denied");
});

foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
{
    Add($"HTTP {(int)status} is reported as an API failure", async () =>
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status))));
        await Throws<ShopifyApiException>(() => Service(client).GetProductByIdAsync(productId));
    });
}

Add("Malformed JSON is reported as an API failure", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json") })));
    await Throws<ShopifyApiException>(() => Service(client).GetProductByIdAsync(productId), "invalid JSON");
});

Add("Missing data and missing update payload are rejected", async () =>
{
    using var client = new HttpClient(new StubHandler(Json(new { }), Json(new { data = new { } })));
    var service = Service(client);
    await Throws<ShopifyApiException>(() => service.GetProductByIdAsync(productId), "without data");
    await Throws<ShopifyApiException>(() => service.UpdateProductAsync(new(productId, ProductUpdateField.Title, "New")), "update result");
});

Add("Transport failure is reported without automatic retry", async () =>
{
    using var handler = new StubHandler(_ => throw new HttpRequestException("network failed"));
    using var client = new HttpClient(handler);
    await Throws<ShopifyApiException>(() => Service(client).GetProductByIdAsync(productId), "communicate");
    Check(handler.Requests.Count == 1, "Unexpected automatic retry.");
});

Add("Timeout is reported as an API failure", async () =>
{
    using var client = new HttpClient(new StubHandler(_ => throw new TaskCanceledException("timeout")));
    await Throws<ShopifyApiException>(() => Service(client).GetProductByIdAsync(productId), "timed out");
});

Add("Caller cancellation remains cancellation", async () =>
{
    using var cancellation = new CancellationTokenSource();
    using var client = new HttpClient(new StubHandler(_ =>
    {
        cancellation.Cancel();
        throw new OperationCanceledException(cancellation.Token);
    }));
    await Throws<OperationCanceledException>(() => Service(client).GetProductByIdAsync(productId, cancellation.Token));
});

Add("Existing product and order methods still deserialize responses", async () =>
{
    using var client = new HttpClient(new StubHandler(
        Json(new { data = new { products = new { nodes = new[] { new { title = "Z" }, new { title = "A" } } } } }),
        Json(new { data = new { orders = new { nodes = new[] { new { name = "Order 1" } } } } })));
    var service = Service(client);
    Check((await service.GetProducts()).Select(product => product.Title).SequenceEqual(new[] { "A", "Z" }), "Product sort changed.");
    Check((await service.GetOrders()).Single().Name == "Order 1", "Order response changed.");
});

WorkflowChecks.Register(Add);

var failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failures++; Console.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} checks passed.");
return failures == 0 ? 0 : 1;

void Add(string name, Func<Task> run) => tests.Add((name, run));
static ShopifyService Service(HttpClient client) => new(client, "https://example.myshopify.com/admin/api/2026-07/graphql.json", "test-token");
static object Product(string id) => new { id, title = "Original", vendor = "Vendor", status = "ACTIVE" };
static object Variant(string sku, string id) => new { sku, product = Product(id) };
static object Page(object[] nodes, bool hasNextPage = false, string? endCursor = null) =>
    new { data = new { productVariants = new { nodes, pageInfo = new { hasNextPage, endCursor } } } };
static Func<HttpRequestMessage, Task<HttpResponseMessage>> Json(object body) => _ => Task.FromResult(
    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task Throws<T>(Func<Task> action, string? message = null) where T : Exception
{
    try { await action(); }
    catch (T exception)
    {
        Check(message is null || exception.Message.Contains(message, StringComparison.OrdinalIgnoreCase), $"Unexpected error: {exception.Message}");
        return;
    }
    throw new Exception($"Expected {typeof(T).Name}.");
}

sealed class StubHandler(params Func<HttpRequestMessage, Task<HttpResponseMessage>>[] responses) : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses = new(responses);
    public List<string> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (request.Headers.GetValues("X-Shopify-Access-Token").Single() != "test-token")
            throw new Exception("Missing authentication header.");
        if (responses.Count == 0) throw new Exception("Unexpected HTTP request.");
        return await responses.Dequeue()(request);
    }
}
