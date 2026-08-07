namespace ShopifyIntegration.Models;

public class IntegrationReportRecord
{
    public string Sku { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string? Title { get; set; }
    public string? Vendor { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}