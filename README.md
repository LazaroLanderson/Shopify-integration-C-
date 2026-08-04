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
