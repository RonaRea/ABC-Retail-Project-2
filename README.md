# ABC Retail Azure Storage Project

ASP.NET Core MVC web app for the CLDV7112w Project 1 brief. The app demonstrates:

- Azure Tables: customer profiles and product records
- Azure Blob Storage: product image uploads
- Azure Queue Storage: order and inventory processing messages
- Azure Files: operational log file storage

## Run Locally

```bash
dotnet restore
dotnet build
dotnet run --urls http://localhost:5099
```

Open `http://localhost:5099`.

The default configuration uses local development storage files in `App_Data` and uploaded files in `wwwroot/uploads`, so the app can be tested before Azure credentials are added.

## Use Azure Storage

In `appsettings.json`, set:

```json
"AzureStorage": {
  "UseLocalStorage": false,
  "ConnectionString": "YOUR_STORAGE_ACCOUNT_CONNECTION_STRING"
}
```

The app will create/use these storage resources:

- `CustomerProfiles` table
- `Products` table
- `product-images` blob container
- `order-processing` queue
- `retail-logs` file share

For deployment, publish the app to Azure App Service and set the same `AzureStorage__UseLocalStorage=false` and `AzureStorage__ConnectionString=...` application settings in the Azure Portal.
