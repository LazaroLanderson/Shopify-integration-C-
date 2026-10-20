# Shopify Integration C#

## Run the project

Set your Shopify credentials once. They are saved outside the project and are not sent to Git.

```powershell
dotnet user-secrets set "Shopify:StoreUrl" "https://your-store.myshopify.com"
dotnet user-secrets set "Shopify:AccessToken" "your-access-token"
```

Then run the application:

```powershell
dotnet run
```

## Console menu

Choose `1` to update a product, `2` to generate the existing CSV and JSON inventory
reports, or `0` to exit. Invalid options are prompted again. End-of-input exits
cleanly.

To update a product:

1. Enter an existing variant SKU (case-sensitive).
2. Review the parent product's current details.
3. Choose title, vendor, or status. Only one field changes per operation.
4. Enter a nonempty title/vendor, or select a status from the numbered list.
5. Review the current and proposed values, then confirm with `y` or cancel with `n`.
6. The application sends the update and retrieves the product by ID to verify the
   saved field. It displays success only when the retrieved value matches.

Type `/cancel` at any update prompt to return to the menu. Field and status menus
also accept `0` to cancel. Unchanged values do not send an update. API errors return
to the menu; a failed verification is distinguished from a rejected update, since
the write may already have succeeded.

Report generation keeps the original file formats, SKU comparison, and output
filenames. It still queries the first 20 products; report pagination is outside
this feature's scope. Inventory configuration and CSV validation are checked when
reporting is selected, so a missing inventory file does not block product editing.

## Product API layer

The service now exposes these methods without console input or output:

- `GetProductBySkuAsync(sku)`: searches variants across all result pages and returns
  their parent product. SKU matching is exact and case-sensitive after trimming the
  supplied input. Returns `null` when no exact match exists; rejects a SKU shared by
  different products.
- `GetProductByIdAsync(productId)`: retrieves a product by its Shopify product GID;
  returns `null` if Shopify returns no product.
- `UpdateProductAsync(request)`: updates exactly one field (`Title`, `Vendor`, or
  `Status`) and returns the product from the mutation response. Blank values are
  rejected. Status accepts `ACTIVE`, `DRAFT`, `ARCHIVED`, or `UNLISTED` and is
  normalized to uppercase. Title and vendor values are trimmed.

Updates affect the parent product, including all its variants. The app needs
Shopify product read/write access for these operations.

Invalid arguments are rejected before sending requests. HTTP failures, GraphQL
errors, mutation validation errors, malformed responses, and transport failures
raise `ShopifyApiException`. Caller cancellation remains cancellation. The service
does not retry updates automatically: after a timeout or connection failure, the
write may already have reached Shopify.

The caller is responsible for confirmation and a separate retrieval by product ID
to verify the saved field. A mutation response alone is not that follow-up check.

API references: [variant search](https://shopify.dev/docs/api/admin-graphql/2026-07/queries/productVariants)
and [product update](https://shopify.dev/docs/api/admin-graphql/2026-07/mutations/productUpdate).

## Run the checks

```shell
dotnet run --project Tests/ShopifyIntegration.Checks.csproj
```

This dependency-free check runner uses simulated HTTP responses. It needs no store
credentials and does not contact Shopify. It checks pagination, SKU ambiguity,
request contents, validation, error handling, cancellation, and existing product
and order response handling. Console checks cover all three editable fields,
confirmation, verification mismatches/failures, menu recovery, end-of-input, and
CSV/JSON report generation in a temporary directory. It exits with a nonzero code
if a check fails. These simulated checks do not replace a live store test.
