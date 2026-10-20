using ShopifyIntegration.Models;

namespace ShopifyIntegration.Services;

public class InventoryReportWorkflow(ShopifyService shopifyService, string? inventoryFilePath,
    string outputDirectory, TextWriter output)
{
    public async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(inventoryFilePath))
        {
            output.WriteLine("Inventory file path is missing in appsettings.json.");
            return;
        }

        var csvReader = new CsvReader();
        List<InventoryRecord> inventoryRecords;

        try
        {
            inventoryRecords = csvReader.Read(inventoryFilePath);
        }
        catch (InvalidOperationException exception)
        {
            output.WriteLine($"CSV error: {exception.Message}");
            return;
        }

        output.WriteLine("Inventory records");
        output.WriteLine("-----------------");

        foreach (var record in inventoryRecords)
        {
            output.WriteLine($"SKU: {record.Sku} | Quantity: {record.Quantity}");
        }

        output.WriteLine();

        var products = await shopifyService.GetProducts();

        output.WriteLine("Shopify products");
        output.WriteLine("----------------");

        if (products.Count == 0)
        {
            output.WriteLine("No products were found.");
        }

        foreach (var product in products)
        {
            output.WriteLine($"Title: {product.Title}");
            output.WriteLine($"Id: {product.Id}");
            output.WriteLine($"Vendor: {product.Vendor}");
            output.WriteLine($"Type: {product.ProductType}");
            output.WriteLine($"Status: {product.Status}");

            foreach (var variant in product.Variants.Nodes)
            {
                output.WriteLine($"SKU: {variant.Sku}");
            }

            output.WriteLine();
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

        var reportWriter = new ReportWriter();
        var reportPath = Path.Combine(outputDirectory, "IntegrationReport.csv");
        var jsonReportPath = Path.Combine(outputDirectory, "IntegrationReport.json");
        reportWriter.Write(reportPath, reportRecords);
        reportWriter.WriteJson(jsonReportPath, reportRecords);

        var foundProducts = reportRecords.Count(record => record.Exists);

        output.WriteLine("Summary");
        output.WriteLine("-------");
        output.WriteLine($"Products in CSV: {inventoryRecords.Count}");
        output.WriteLine($"Found in Shopify: {foundProducts}");
        output.WriteLine($"Missing Products: {inventoryRecords.Count - foundProducts}");
        output.WriteLine($"CSV report created: {reportPath}");
        output.WriteLine($"JSON report created: {jsonReportPath}");
    }
}
