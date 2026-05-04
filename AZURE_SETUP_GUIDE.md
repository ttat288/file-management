# Azure Blob Storage Implementation Guide

## Overview

This guide walks through the migration from Cloudinary to **Azure Blob Storage** for file management. This approach leverages the Azure ecosystem for cost-effective, fast file storage and serves files through Azure's CDN infrastructure.

## Architecture

```
Frontend (Angular)
       ↓
Backend API (.NET Core 8)
       ↓
Azure Blob Storage (File Storage)
       ↓
Azure Static Website / Azure App Service
```

## Prerequisites

- Azure Subscription (Free tier eligible)
- dotnet 8.0 SDK
- PostgreSQL database
- Azure Storage Account

## Step 1: Create Azure Storage Account

### Option A: Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new **Storage Account**
   - Resource Group: Create new or select existing
   - Storage account name: `filemanagementXXX` (must be globally unique)
   - Region: Choose closest to your users (e.g., Southeast Asia, US East, Europe West)
   - Performance: **Standard**
   - Redundancy: **LRS** (Local Redundant Storage - cheapest)
   - Review + Create

### Option B: Azure CLI

```bash
# Login to Azure
az login

# Create resource group
az group create --name file-management-rg --location southeastasia

# Create storage account
az storage account create \
  --name filemanagement${RANDOM} \
  --resource-group file-management-rg \
  --location southeastasia \
  --sku Standard_LRS \
  --kind StorageV2
```

## Step 2: Get Connection String

### From Azure Portal:

1. Go to Storage Account → Access Keys
2. Copy the **Connection string** (primary or secondary)
3. Format: `DefaultEndpointsProtocol=https;AccountName=XXX;AccountKey=XXX;EndpointSuffix=core.windows.net`

### From Azure CLI:

```bash
az storage account show-connection-string \
  --name filemanagementXXX \
  --resource-group file-management-rg \
  --query connectionString -o tsv
```

## Step 3: Database Migration

### Execute Migration Script

Run the migration SQL to update your PostgreSQL database:

```bash
# Using psql
psql -h localhost -U postgres -d file_management -f Database/03_migrate_to_azure_blob_storage.sql

# Or using your database client, execute the content of:
# Database/03_migrate_to_azure_blob_storage.sql
```

This script:

- ✅ Renames columns from `cloudinary_url`/`public_id` to `blob_url`/`blob_name`
- ✅ Updates all stored procedures
- ✅ Maintains data integrity
- ✅ Creates appropriate indexes

## Step 4: Configuration

### Update appsettings.json

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=your_account;AccountKey=your_key;EndpointSuffix=core.windows.net",
    "ContainerName": "file-management"
  }
}
```

### Update .env

```bash
AzureBlobStorage__ConnectionString=DefaultEndpointsProtocol=https;AccountName=your_account;AccountKey=your_key;EndpointSuffix=core.windows.net
AzureBlobStorage__ContainerName=file-management
```

### User Secrets (Development)

```bash
cd Backend/FileManagement/FileManagement.Api

# Store connection string securely
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "your_connection_string"
dotnet user-secrets set "AzureBlobStorage:ContainerName" "file-management"
```

## Step 5: Build and Deploy

### Local Development

1. **Restore NuGet packages:**

   ```bash
   cd Backend/FileManagement/FileManagement.Api
   dotnet restore
   ```

2. **Run migrations:**

   ```bash
   # Execute SQL migration in PostgreSQL
   psql -h localhost -U postgres -d file_management -f ../../../Database/03_migrate_to_azure_blob_storage.sql
   ```

3. **Run the API:**
   ```bash
   dotnet run
   ```

### Production Deployment

#### Option A: Azure App Service

```bash
# Create App Service Plan
az appservice plan create \
  --name file-management-plan \
  --resource-group file-management-rg \
  --sku B2

# Create Web App
az webapp create \
  --resource-group file-management-rg \
  --plan file-management-plan \
  --name file-management-api

# Configure app settings
az webapp config appsettings set \
  --resource-group file-management-rg \
  --name file-management-api \
  --settings \
    "AzureBlobStorage:ConnectionString=$CONNECTION_STRING" \
    "AzureBlobStorage:ContainerName=file-management"

# Deploy from GitHub/local
az webapp deployment source config-zip \
  --resource-group file-management-rg \
  --name file-management-api \
  --src api.zip
```

#### Option B: Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY Backend/FileManagement/FileManagement.Api/bin/Release/net8.0/publish/ .
ENTRYPOINT ["dotnet", "FileManagement.Api.dll"]
```

## Cost Analysis

### Monthly Estimates (East US)

| Service              | Usage          | Cost              |
| -------------------- | -------------- | ----------------- |
| **Storage (LRS)**    | 100GB          | ~$2.30            |
| **Transactions**     | 10M operations | ~$0.50            |
| **Data retrieval**   | 50GB egress    | ~$0.00\*          |
| **App Service (B2)** | Month          | ~$45-65           |
| **Static Website**   | 100GB          | ~$1.95            |
| **Total**            |                | **~$50-70/month** |

\*Azure provides 200GB free egress per month

### Comparison with Cloudinary

- **Cloudinary**: $99-300+/month for similar storage
- **Azure**: $50-70/month (70% cheaper)
- **Trade-off**: More control, slightly more setup

## Features

### Automatic Container Creation

The service automatically creates the blob container if it doesn't exist with public access enabled.

### File Metadata

Each uploaded file stores:

- Original filename
- Upload date
- Content type
- File size
- Unique blob name (UUID)

### Cleanup

- When files are deleted from the database, they're also removed from blob storage
- Implements transaction-like behavior with rollback

### Scalability

- Upload limit: 500MB per file (configurable)
- Blob storage: Up to 5PB per account
- Automatic CDN support via Azure CDN or Azure Static Web Apps

## Production Checklist

- [ ] Create Azure Storage Account
- [ ] Configure connection string securely
- [ ] Run database migration script
- [ ] Test file upload/download locally
- [ ] Configure CORS in Azure Storage
- [ ] Set up Azure CDN (optional, for faster downloads)
- [ ] Deploy to Azure App Service
- [ ] Configure SSL/TLS certificate
- [ ] Setup monitoring and alerts
- [ ] Enable soft delete for accidental deletion recovery
- [ ] Configure backup/lifecycle policies
- [ ] Monitor costs in Azure Cost Management

## CORS Configuration (If Needed)

```bash
# Enable CORS for your frontend domain
az storage cors add \
  --services b \
  --methods GET POST DELETE \
  --origins "https://yourdomain.com" \
  --allowed-headers "*" \
  --exposed-headers "*" \
  --max-age 3600 \
  --account-name filemanagementXXX
```

## Troubleshooting

### Issue: "ConnectionString not configured"

**Solution:** Ensure `AzureBlobStorage:ConnectionString` is set in appsettings or environment variables

### Issue: "Container not found"

**Solution:** The service auto-creates the container. Check Azure Portal → Storage Account → Containers

### Issue: "File size exceeds 500MB"

**Solution:** Azure Blob Storage supports up to 4.75TB. Adjust max size in `AzureBlobStorageService.cs` if needed

### Issue: "CORS error when downloading from frontend"

**Solution:** Configure CORS in Azure Storage or use Azure CDN

### Issue: "Connection timeout"

**Solution:** Check network connectivity and firewall rules for your Azure Storage Account

## References

- [Azure Blob Storage Documentation](https://docs.microsoft.com/azure/storage/blobs/)
- [Azure Storage Client Library for .NET](https://docs.microsoft.com/dotnet/api/azure.storage.blobs)
- [Pricing - Azure Blob Storage](https://azure.microsoft.com/pricing/details/storage/blobs/)
- [Azure App Service Pricing](https://azure.microsoft.com/pricing/details/app-service/)

## Next Steps

1. **Enable Static Website Hosting:**

   ```bash
   az storage blob service-properties update \
     --account-name filemanagementXXX \
     --static-website \
     --index-document index.html \
     --404-document index.html
   ```

2. **Configure Azure CDN for faster downloads**

3. **Setup Application Insights for monitoring**

4. **Enable backup and disaster recovery**

5. **Configure lifecycle policies to archive old files to cool storage**

---

**Migration Completed!** Your backend now uses Azure Blob Storage instead of Cloudinary. The entire Azure ecosystem is at your fingertips for scaling and optimization.
