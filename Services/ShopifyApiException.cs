namespace ShopifyIntegration.Services;

public class ShopifyApiException : Exception
{
    public ShopifyApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
