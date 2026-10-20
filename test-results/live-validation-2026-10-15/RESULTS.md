# Live validation results

Executed against a Shopify partner development store through the console workflows with scripted input. Credentials and store product identifiers are intentionally excluded.

| Test | Result |
|---|---|
| Immediate cancellation | PASS |
| Nonexistent SKU | PASS |
| Cancellation at confirmation | PASS |
| Confirmed title update and independent verification | PASS |
| Original title restored and independently verified | PASS |
| CSV and JSON report generation with a found and missing SKU | PASS |

Exactly two product update mutations were sent: one temporary title change and its restoration. The restored product's title, vendor, and status were confirmed with a separate product-ID query.

The detailed local console transcripts and generated report files contain live development-store catalog data; they are not included in the repository commit.
