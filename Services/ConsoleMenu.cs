namespace ShopifyIntegration.Services;

public class ConsoleMenu(ProductUpdateWorkflow updateWorkflow, InventoryReportWorkflow reportWorkflow,
    TextReader input, TextWriter output)
{
    public async Task RunAsync()
    {
        while (true)
        {
            output.WriteLine();
            output.WriteLine("Shopify Integration");
            output.WriteLine("1. Update a product");
            output.WriteLine("2. Generate CSV and JSON inventory reports");
            output.WriteLine("0. Exit");
            output.WriteLine("Choose an option:");
            var choice = input.ReadLine()?.Trim();
            if (choice is null or "0")
            {
                output.WriteLine("Goodbye.");
                return;
            }

            try
            {
                switch (choice)
                {
                    case "1":
                        await updateWorkflow.RunAsync();
                        break;
                    case "2":
                        await reportWorkflow.RunAsync();
                        break;
                    default:
                        output.WriteLine("Invalid option. Choose 1, 2, or 0.");
                        break;
                }
            }
            catch (ShopifyApiException exception)
            {
                output.WriteLine($"Shopify error: {exception.Message}");
            }
            catch (IOException)
            {
                output.WriteLine("The report file could not be read or written. Check its path and try again.");
            }
            catch (UnauthorizedAccessException)
            {
                output.WriteLine("Access to the report file was denied. Check file permissions.");
            }
        }
    }
}
