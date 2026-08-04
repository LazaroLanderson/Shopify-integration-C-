namespace ShopifyIntegration.Queries;

public static class ShopifyQueries
{
    public const string GetShop = """
        {
          shop {
            name
            email
            myshopifyDomain
          }
        }
        """;
}
