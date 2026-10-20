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
              variants(first: 100) {
                nodes {
                  sku
                }
              }
            }
          }
        }
        """;

    public static string GetProductBySku() => """
        query GetProductBySku($search: String!, $after: String) {
          productVariants(first: 100, query: $search, after: $after) {
            nodes {
              sku
              product {
                id
                title
                vendor
                productType
                status
              }
            }
            pageInfo {
              hasNextPage
              endCursor
            }
          }
        }
        """;

    public static string GetProductById() => """
        query GetProductById($id: ID!) {
          product(id: $id) {
            id
            title
            vendor
            productType
            status
          }
        }
        """;

    public static string UpdateProduct() => """
        mutation UpdateProduct($product: ProductUpdateInput!) {
          productUpdate(product: $product) {
            product {
              id
              title
              vendor
              productType
              status
            }
            userErrors {
              field
              message
            }
          }
        }
        """;

    public static string GetOrders() => BuildOrdersQuery();
    public static string GetPaidOrders() => BuildOrdersQuery("financial_status:paid");
    public static string GetUnfulfilledOrders() => BuildOrdersQuery("fulfillment_status:unfulfilled");

    private static string BuildOrdersQuery(string? searchFilter = null)
    {
        var filter = string.IsNullOrWhiteSpace(searchFilter)
            ? string.Empty
            : $", query: \"{searchFilter.Replace("\"", "\\\"")}\"";

        return $$"""
            query GetOrders {
              orders(first: 20{{filter}}) {
                nodes {
                  id
                  name
                  createdAt
                  displayFinancialStatus
                  displayFulfillmentStatus
                  customer {
                    firstName
                    lastName
                    email
                  }
                  totalPriceSet {
                    shopMoney {
                      amount
                      currencyCode
                    }
                  }
                }
              }
            }
            """;
    }
}
