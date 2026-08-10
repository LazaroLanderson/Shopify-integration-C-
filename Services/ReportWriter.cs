using System.Text.Json;
using ShopifyIntegration.Models;

namespace ShopifyIntegration.Services;

public class ReportWriter
{
    public void Write(string filePath, List<IntegrationReportRecord> records)
    {
        var lines = new List<string> { "SKU,Exists,Title,Vendor,Status,GeneratedAt" };

        foreach (var record in records)
        {
            var exists = record.Exists ? "Yes" : "No";
            lines.Add($"{Escape(record.Sku)},{exists},{Escape(record.Title)},{Escape(record.Vendor)},{Escape(record.Status)},{record.GeneratedAt:O}");
        }

        File.WriteAllLines(filePath, lines);
    }

    public void WriteJson(string filePath, List<IntegrationReportRecord> records)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(filePath, JsonSerializer.Serialize(records, options));
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;

        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}