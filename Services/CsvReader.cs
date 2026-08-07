using ShopifyIntegration.Models;

namespace ShopifyIntegration.Services;

public class CsvReader
{
    public List<InventoryRecord> Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("Inventory file was not found.");
        }

        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Inventory file is empty.");
        }

        var headers = lines[0].Split(',');

        if (headers.Length != 2 || headers[0].Trim() != "SKU" || headers[1].Trim() != "Quantity")
        {
            throw new InvalidOperationException("Inventory file must contain the SKU and Quantity columns.");
        }

        var records = new List<InventoryRecord>();

        for (var index = 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var columns = lines[index].Split(',');

            if (columns.Length != 2 ||
                string.IsNullOrWhiteSpace(columns[0]) ||
                !int.TryParse(columns[1], out var quantity) ||
                quantity < 0)
            {
                throw new InvalidOperationException($"Invalid inventory record on line {index + 1}.");
            }

            records.Add(new InventoryRecord
            {
                Sku = columns[0].Trim(),
                Quantity = quantity
            });
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Inventory file does not contain any records.");
        }

        return records;
    }
}