using System.Net;
using System.Text.Json;
using ShopifyIntegration.Services;

internal static class WorkflowChecks
{
    private const string ProductId = "gid://shopify/Product/1";

    public static void Register(Action<string, Func<Task>> add)
    {
        foreach (var selection in new[] { ("1", "New title", "title"), ("2", "New vendor", "vendor"), ("3", "2", "status") })
        {
            add($"Console updates and verifies {selection.Item3} by product ID", async () =>
            {
                var value = selection.Item3 == "status" ? "DRAFT" : selection.Item2;
                var product = Product(selection.Item3, value);
                using var handler = new StubHandler(Lookup(), Updated(product), Json(new { data = new { product } }));
                using var client = new HttpClient(handler);
                using var output = new StringWriter();
                await Workflow(client, $"ABC\n{selection.Item1}\n{selection.Item2}\ny\n", output).RunAsync();
                Contains(output, "Update preview", "Current value:", $"New value: {value}", "Update verified successfully.");
                Check(handler.Requests.Count == 3, "Expected lookup, mutation, and verification requests.");
                using var verification = JsonDocument.Parse(handler.Requests[2]);
                Check(verification.RootElement.GetProperty("variables").GetProperty("id").GetString() == ProductId,
                    "Verification must use product ID.");
            });
        }

        foreach (var input in new[] { "/cancel\n", "", "ABC\n0\n", "ABC\n1\n/cancel\n", "ABC\n3\n0\n", "ABC\n1\nNew\nn\n", "ABC\n1\nNew\n/cancel\n", "ABC\n1\nNew\n" })
        {
            add($"Console cancellation sends no mutation ({input.Replace("\n", "/")})", async () =>
            {
                using var handler = new StubHandler(Lookup());
                using var client = new HttpClient(handler);
                using var output = new StringWriter();
                await Workflow(client, input, output).RunAsync();
                Contains(output, "Update cancelled. No update was sent.");
                Check(handler.Requests.Count <= 1, "Cancellation sent an update.");
            });
        }

        add("Console skips unchanged values", async () =>
        {
            using var handler = new StubHandler(Lookup());
            using var client = new HttpClient(handler);
            using var output = new StringWriter();
            await Workflow(client, "ABC\n1\nOriginal\n", output).RunAsync();
            Contains(output, "The value is unchanged. No update was sent.");
            Check(handler.Requests.Count == 1, "Unchanged value sent an update.");
        });

        add("Console retries blank input, invalid field, status, and confirmation", async () =>
        {
            var product = Product("status", "DRAFT");
            using var handler = new StubHandler(Lookup(), Updated(product), Json(new { data = new { product } }));
            using var client = new HttpClient(handler);
            using var output = new StringWriter();
            await Workflow(client, "\nABC\nbad\n3\nbad\n2\nmaybe\ny\n", output).RunAsync();
            Contains(output, "cannot be empty", "Invalid field", "Invalid status", "Please enter y or n", "Update verified successfully.");
            Check(handler.Requests.Count == 3, "Invalid input triggered extra HTTP requests.");
        });

        add("Console handles a missing SKU without an update", async () =>
        {
            using var handler = new StubHandler(Json(new { data = new { productVariants = new { nodes = Array.Empty<object>(), pageInfo = new { hasNextPage = false } } } }));
            using var client = new HttpClient(handler);
            using var output = new StringWriter();
            await Workflow(client, "MISSING\n", output).RunAsync();
            Contains(output, "Product not found");
            Check(handler.Requests.Count == 1, "Missing product triggered an update.");
        });

        foreach (var mode in new[] { "mismatch", "missing", "error" })
        {
            add($"Console distinguishes accepted update from verification {mode}", async () =>
            {
                var verification = mode switch
                {
                    "missing" => Json(new { data = new { product = (object?)null } }),
                    "error" => Json(new { errors = new[] { new { message = "Verification unavailable" } } }),
                    _ => Json(new { data = new { product = Product() } })
                };
                using var handler = new StubHandler(Lookup(), Updated(Product("title", "New")), verification);
                using var client = new HttpClient(handler);
                using var output = new StringWriter();
                await Workflow(client, "ABC\n1\nNew\ny\n", output).RunAsync();
                Contains(output, "Update accepted, but", "Check the product in Shopify before retrying");
                Check(!output.ToString().Contains("Update verified successfully"), "False verification success.");
                Check(handler.Requests.Count == 3, "Unexpected retry.");
            });
        }

        add("Menu recovers from API errors and accepts exit", async () =>
        {
            using var handler = new StubHandler(Json(new { errors = new[] { new { message = "Access denied" } } }));
            using var client = new HttpClient(handler);
            using var output = new StringWriter();
            using var input = new StringReader("bad\n1\nABC\n0\n");
            var service = Service(client);
            await new ConsoleMenu(new(service, input, output), new(service, null, Path.GetTempPath(), output), input, output).RunAsync();
            Contains(output, "Invalid option", "Shopify error:", "Goodbye.");
        });

        add("Menu reports mutation rejection without verification and stays usable", async () =>
        {
            using var handler = new StubHandler(Lookup(), Json(new { data = new { productUpdate = new
            {
                product = (object?)null, userErrors = new[] { new { field = new[] { "product", "title" }, message = "Rejected title" } }
            } } }));
            using var client = new HttpClient(handler);
            using var output = new StringWriter();
            using var input = new StringReader("1\nABC\n1\nNew\ny\n0\n");
            var service = Service(client);
            await new ConsoleMenu(new(service, input, output), new(service, null, Path.GetTempPath(), output), input, output).RunAsync();
            Contains(output, "Shopify rejected the update", "Rejected title", "Goodbye.");
            Check(handler.Requests.Count == 2, "Rejected mutation triggered verification.");
            Check(!output.ToString().Contains("Update verified successfully"), "False success.");
        });

        add("EOF exits the menu without API calls", async () =>
        {
            using var handler = new StubHandler();
            using var client = new HttpClient(handler);
            using var input = new StringReader("");
            using var output = new StringWriter();
            var service = Service(client);
            await new ConsoleMenu(new(service, input, output), new(service, null, Path.GetTempPath(), output), input, output).RunAsync();
            Contains(output, "Goodbye.");
            Check(handler.Requests.Count == 0, "EOF triggered HTTP.");
        });

        add("Report menu preserves CSV and JSON output and returns to the menu", async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), $"shopify-report-check-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var csv = Path.Combine(directory, "inventory.csv");
                await File.WriteAllTextAsync(csv, "SKU,Quantity\nABC,5\nMISSING,2\n");
                using var handler = new StubHandler(Json(new { data = new { products = new { nodes = new[] { new
                {
                    id = ProductId, title = "Original", vendor = "Vendor", status = "ACTIVE",
                    variants = new { nodes = new[] { new { sku = "ABC" } } }
                } } } } }));
                using var client = new HttpClient(handler);
                using var input = new StringReader("2\n0\n");
                using var output = new StringWriter();
                var service = Service(client);
                await new ConsoleMenu(new(service, input, output), new(service, csv, directory, output), input, output).RunAsync();
                Contains(output, "Found in Shopify: 1", "Missing Products: 1", "Goodbye.");
                Check(File.ReadAllText(Path.Combine(directory, "IntegrationReport.csv")).Contains("ABC,Yes,Original,Vendor,ACTIVE,"), "CSV report changed.");
                using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "IntegrationReport.json")));
                Check(json.RootElement.GetArrayLength() == 2 && json.RootElement[0].GetProperty("Exists").GetBoolean()
                    && !json.RootElement[1].GetProperty("Exists").GetBoolean(), "JSON report changed.");
            }
            finally { Directory.Delete(directory, recursive: true); }
        });

        add("Missing inventory configuration does not block product editing", async () =>
        {
            using var handler = new StubHandler(Lookup());
            using var client = new HttpClient(handler);
            using var input = new StringReader("2\n1\nABC\n0\n0\n");
            using var output = new StringWriter();
            var service = Service(client);
            await new ConsoleMenu(new(service, input, output), new(service, null, Path.GetTempPath(), output), input, output).RunAsync();
            Contains(output, "Inventory file path is missing", "Product: Original", "Goodbye.");
            Check(handler.Requests.Count == 1, "Product editing did not remain available.");
        });
    }

    private static ShopifyService Service(HttpClient client) => new(client, "https://example.myshopify.com/admin/api/2026-07/graphql.json", "test-token");
    private static ProductUpdateWorkflow Workflow(HttpClient client, string input, TextWriter output) => new(Service(client), new StringReader(input), output);
    private static object Product(string? field = null, string? value = null) => new
    {
        id = ProductId, title = field == "title" ? value : "Original",
        vendor = field == "vendor" ? value : "Vendor", status = field == "status" ? value : "ACTIVE"
    };
    private static Func<HttpRequestMessage, Task<HttpResponseMessage>> Lookup() => Json(new { data = new { productVariants = new
    {
        nodes = new[] { new { sku = "ABC", product = Product() } }, pageInfo = new { hasNextPage = false }
    } } });
    private static Func<HttpRequestMessage, Task<HttpResponseMessage>> Updated(object product) => Json(new { data = new { productUpdate = new { product, userErrors = Array.Empty<object>() } } });
    private static Func<HttpRequestMessage, Task<HttpResponseMessage>> Json(object body) => _ => Task.FromResult(
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) });
    private static void Contains(StringWriter output, params string[] expected)
    {
        foreach (var text in expected) Check(output.ToString().Contains(text), $"Console output did not contain: {text}");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
