namespace ShopifyIntegration.Queries;

public static class ShopifyQueries
{
    public static string GetShopInformation() => """
        {
          shop {
            name
            email
            myshopifyDomain
          }
        }
        """;

    public static string GetProducts() => """
        query GetProducts {
          products(first: 20) {
            nodes {
              id
              title
              vendor
              productType
              status
            }
          }
        }
        """;
}
