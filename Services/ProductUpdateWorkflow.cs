using ShopifyIntegration.Models;

namespace ShopifyIntegration.Services;

public class ProductUpdateWorkflow(ShopifyService shopifyService, TextReader input, TextWriter output)
{
    public async Task RunAsync()
    {
        output.WriteLine("Update a product — type /cancel at any prompt to cancel.");
        var sku = ReadRequiredValue("Enter the SKU (case-sensitive):");
        if (sku is null) return;

        var product = await shopifyService.GetProductBySkuAsync(sku);
        if (product is null)
        {
            output.WriteLine($"Product not found for SKU: {sku}");
            return;
        }

        output.WriteLine($"Product: {product.Title}");
        output.WriteLine($"ID: {product.Id}");
        output.WriteLine($"Vendor: {product.Vendor}");
        output.WriteLine($"Status: {product.Status}");
        output.WriteLine("Changes apply to the entire product, including all its variants.");

        var field = ReadField();
        if (field is null) return;
        var value = field == ProductUpdateField.Status ? ReadStatus() : ReadRequiredValue($"Enter the new {field.Value.ToString().ToLowerInvariant()}:");
        if (value is null) return;

        var oldValue = GetValue(product, field.Value);
        if (string.Equals(oldValue, value, StringComparison.Ordinal))
        {
            output.WriteLine("The value is unchanged. No update was sent.");
            return;
        }

        output.WriteLine();
        output.WriteLine("Update preview");
        output.WriteLine($"Product: {product.Title} | SKU: {sku}");
        output.WriteLine($"Field: {field}");
        output.WriteLine($"Current value: {oldValue ?? "(empty)"}");
        output.WriteLine($"New value: {value}");
        if (!ConfirmUpdate()) return;

        var request = new ProductUpdateRequest(product.Id!, field.Value, value);
        await shopifyService.UpdateProductAsync(request);
        output.WriteLine("Shopify accepted the update. Verifying the saved value...");

        // A failed read after a successful write must not be reported as a failed write.
        Product? savedProduct;
        try
        {
            savedProduct = await shopifyService.GetProductByIdAsync(request.ProductId);
        }
        catch (ShopifyApiException exception)
        {
            output.WriteLine($"Update accepted, but verification could not be completed: {exception.Message}");
            output.WriteLine("Check the product in Shopify before retrying the update.");
            return;
        }

        if (savedProduct is null || savedProduct.Id != request.ProductId)
        {
            output.WriteLine("Update accepted, but the product could not be retrieved for verification.");
            output.WriteLine("Check the product in Shopify before retrying the update.");
            return;
        }

        var savedValue = GetValue(savedProduct, request.Field);
        if (!string.Equals(savedValue, request.Value, StringComparison.Ordinal))
        {
            output.WriteLine("Update accepted, but verification did not match the requested value.");
            output.WriteLine($"Expected: {request.Value}");
            output.WriteLine($"Retrieved: {savedValue ?? "(empty)"}");
            output.WriteLine("Check the product in Shopify before retrying the update.");
            return;
        }

        output.WriteLine("Update verified successfully.");
        output.WriteLine($"{request.Field}: {oldValue ?? "(empty)"} -> {savedValue}");
    }

    private string? ReadRequiredValue(string prompt)
    {
        while (true)
        {
            output.WriteLine(prompt);
            var value = input.ReadLine()?.Trim();
            if (IsCancelled(value)) return null;
            if (!string.IsNullOrWhiteSpace(value)) return value;
            output.WriteLine("The value cannot be empty. Try again or type /cancel.");
        }
    }

    private ProductUpdateField? ReadField()
    {
        while (true)
        {
            output.WriteLine("Choose a field: 1. Title | 2. Vendor | 3. Status | 0. Cancel");
            var value = input.ReadLine()?.Trim();
            if (IsCancelled(value, allowZero: true)) return null;
            switch (value)
            {
                case "1": return ProductUpdateField.Title;
                case "2": return ProductUpdateField.Vendor;
                case "3": return ProductUpdateField.Status;
                default: output.WriteLine("Invalid field. Choose 1, 2, 3, or 0."); break;
            }
        }
    }

    private string? ReadStatus()
    {
        while (true)
        {
            output.WriteLine("Choose a status: 1. ACTIVE | 2. DRAFT | 3. ARCHIVED | 4. UNLISTED | 0. Cancel");
            var value = input.ReadLine()?.Trim();
            if (IsCancelled(value, allowZero: true)) return null;
            switch (value)
            {
                case "1": return "ACTIVE";
                case "2": return "DRAFT";
                case "3": return "ARCHIVED";
                case "4": return "UNLISTED";
                default: output.WriteLine("Invalid status. Choose 1, 2, 3, 4, or 0."); break;
            }
        }
    }

    private bool ConfirmUpdate()
    {
        while (true)
        {
            output.WriteLine("Confirm update? [y/n]");
            var value = input.ReadLine()?.Trim();
            if (IsCancelled(value)) return false;
            if (string.Equals(value, "y", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "n", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Update cancelled. No update was sent.");
                return false;
            }
            output.WriteLine("Please enter y or n, or type /cancel.");
        }
    }

    private bool IsCancelled(string? value, bool allowZero = false)
    {
        if (value is not null && !string.Equals(value, "/cancel", StringComparison.OrdinalIgnoreCase) &&
            !(allowZero && value == "0")) return false;
        output.WriteLine("Update cancelled. No update was sent.");
        return true;
    }

    private static string? GetValue(Product product, ProductUpdateField field) => field switch
    {
        ProductUpdateField.Title => product.Title,
        ProductUpdateField.Vendor => product.Vendor,
        ProductUpdateField.Status => product.Status,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}
