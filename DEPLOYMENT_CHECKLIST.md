# Azure Blob Storage Deployment Checklist

Complete this checklist to successfully deploy the Azure Blob Storage backend.

## Phase 1: Azure Setup

- [ ] Create Azure Storage Account
  - [ ] Login to [Azure Portal](https://portal.azure.com)
  - [ ] Create new Storage Account
  - [ ] Set Region (choose closest to users)
  - [ ] Set Redundancy to LRS (cheapest)
  - [ ] Note the storage account name

- [ ] Get Connection String
  - [ ] Go to Storage Account → Access Keys
  - [ ] Copy Connection string (primary)
  - [ ] Format: `DefaultEndpointsProtocol=https;AccountName=XXX;AccountKey=XXX;EndpointSuffix=core.windows.net`

- [ ] Create Blob Container (optional - auto-created on first use)
  - [ ] Storage Account → Containers → Create
  - [ ] Container name: `file-management`
  - [ ] Public access level: Blob

## Phase 2: Code Verification

- [ ] ✅ New Azure service created: `Services/AzureBlobStorageService.cs`
- [ ] ✅ FileService updated to use Azure service
- [ ] ✅ FileRepository updated with new schema
- [ ] ✅ Program.cs configured for Azure
- [ ] ✅ appsettings.json template updated
- [ ] ✅ .csproj has Azure.Storage.Blobs NuGet package
- [ ] ✅ Database functions updated (02_create_functions.sql)
- [ ] ✅ Database schema updated (01_create_tables.sql)

## Phase 3: Database Migration

### Development Environment

- [ ] Backup PostgreSQL database

  ```bash
  pg_dump -h localhost -U postgres file_management > backup_$(date +%Y%m%d).sql
  ```

- [ ] Execute migration script

  ```bash
  # Option 1: Using psql
  psql -h localhost -U postgres -d file_management -f Database/03_migrate_to_azure_blob_storage.sql

  # Option 2: Using pgAdmin
  # Copy-paste content of 03_migrate_to_azure_blob_storage.sql
  ```

- [ ] Verify migration

  ```sql
  -- Check new columns
  SELECT column_name FROM information_schema.columns
  WHERE table_name='files';

  -- Should show: blob_url, blob_name (not cloudinary_url, public_id)
  ```

### Production Environment (if applicable)

- [ ] Backup production database
- [ ] Schedule maintenance window
- [ ] Execute migration script
- [ ] Run verification queries

## Phase 4: Local Configuration

### Option A: appsettings.json (Simple)

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "YOUR_CONNECTION_STRING_HERE",
    "ContainerName": "file-management"
  }
}
```

### Option B: Environment Variables (.env)

```bash
AzureBlobStorage__ConnectionString=YOUR_CONNECTION_STRING_HERE
AzureBlobStorage__ContainerName=file-management
```

### Option C: User Secrets (Recommended for Development)

```bash
cd Backend/FileManagement/FileManagement.Api

dotnet user-secrets init  # First time only

dotnet user-secrets set "AzureBlobStorage:ConnectionString" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "AzureBlobStorage:ContainerName" "file-management"

# Verify
dotnet user-secrets list
```

## Phase 5: Local Testing

- [ ] Restore NuGet packages

  ```bash
  cd Backend/FileManagement/FileManagement.Api
  dotnet restore
  ```

- [ ] Build project

  ```bash
  dotnet build
  ```

- [ ] Run API locally

  ```bash
  dotnet run
  ```

- [ ] Test endpoints using Swagger UI at `https://localhost:5001/swagger`
  - [ ] POST /api/files/upload (test with a small file)
  - [ ] GET /api/files (verify file is listed)
  - [ ] GET /api/files/{id} (check file details)
  - [ ] PUT /api/files/{id}/rename (rename test file)
  - [ ] DELETE /api/files/{id} (delete test file)

- [ ] Verify in Azure Portal
  - [ ] Storage Account → Containers → file-management
  - [ ] Should see uploaded blob file(s)

## Phase 6: Production Deployment

### Option A: Azure App Service (Recommended)

- [ ] Create App Service Plan

  ```bash
  az appservice plan create \
    --name file-management-plan \
    --resource-group YOUR_RESOURCE_GROUP \
    --sku B2
  ```

- [ ] Create Web App

  ```bash
  az webapp create \
    --resource-group YOUR_RESOURCE_GROUP \
    --plan file-management-plan \
    --name file-management-api-prod
  ```

- [ ] Configure App Settings

  ```bash
  az webapp config appsettings set \
    --resource-group YOUR_RESOURCE_GROUP \
    --name file-management-api-prod \
    --settings \
      "AzureBlobStorage:ConnectionString=$AZURE_CONNECTION_STRING" \
      "AzureBlobStorage:ContainerName=file-management" \
      "ConnectionStrings:PostgreSQL=$DB_CONNECTION_STRING"
  ```

- [ ] Deploy API

  ```bash
  # Publish build
  dotnet publish -c Release -o ./publish

  # Create zip for deployment
  cd publish && zip -r ../deploy.zip * && cd ..

  # Deploy to Azure
  az webapp deployment source config-zip \
    --resource-group YOUR_RESOURCE_GROUP \
    --name file-management-api-prod \
    --src deploy.zip
  ```

### Option B: Docker Deployment

- [ ] Build Docker image

  ```bash
  docker build -t file-management-api:latest .
  ```

- [ ] Push to Container Registry

  ```bash
  docker push your-registry/file-management-api:latest
  ```

- [ ] Deploy to Azure Container Instances or App Service

## Phase 7: Post-Deployment Verification

- [ ] Test API endpoints
  - [ ] Health check endpoint
  - [ ] Upload test file
  - [ ] List files
  - [ ] Download file
  - [ ] Delete file

- [ ] Check logs

  ```bash
  az webapp log tail --resource-group YOUR_RESOURCE_GROUP --name file-management-api-prod
  ```

- [ ] Monitor in Azure Portal
  - [ ] App Service → Monitoring → Application insights
  - [ ] Storage Account → Monitoring
  - [ ] Check error rates

- [ ] Verify blob storage usage
  - [ ] Storage Account → Overview → Used capacity
  - [ ] Should increase with uploads

## Phase 8: Security Hardening (Production)

- [ ] Configure firewall for Storage Account
  - [ ] Allow only App Service access
  - [ ] Block public access if not needed

- [ ] Enable CORS (if frontend on different domain)

  ```bash
  az storage cors add \
    --services b \
    --methods GET POST DELETE \
    --origins "https://yourdomain.com" \
    --allowed-headers "*" \
    --exposed-headers "*" \
    --max-age 3600 \
    --account-name YOUR_STORAGE_ACCOUNT
  ```

- [ ] Use Managed Identity
  - [ ] Remove connection string
  - [ ] Use system-assigned managed identity

- [ ] Enable encryption
  - [ ] Storage Account → Encryption → Enable (usually default)

- [ ] Setup Key Vault for secrets
  - [ ] Store connection string in Key Vault
  - [ ] Reference from App Service

## Phase 9: Monitoring & Maintenance

- [ ] Setup alerts for:
  - [ ] High error rates
  - [ ] Storage saturation
  - [ ] Bandwidth overages

- [ ] Configure lifecycle policies
  - [ ] Move old files to cool storage after 30 days
  - [ ] Archive after 90 days

- [ ] Enable soft delete
  - [ ] Allow recovery of accidentally deleted blobs
  - [ ] Configure retention period (7-365 days)

- [ ] Setup logging
  - [ ] Enable diagnostic logging for storage
  - [ ] Send to Log Analytics workspace

- [ ] Regular backups
  - [ ] Enable versioning on blobs
  - [ ] Test restore procedures

## Troubleshooting

### Issue: "Invalid connection string"

**Solution:** Verify copied connection string, ensure proper format

### Issue: "Container not found"

**Solution:** Service auto-creates container. Check if permissions allow container creation

### Issue: "File upload fails silently"

**Solution:** Check App Service logs → Check Application Insights

### Issue: "CORS errors on frontend"

**Solution:** Run cors add command, verify domain in allowed origins

### Issue: "500 errors after deployment"

**Solution:** Check app settings are correctly set, check PostgreSQL connection

### Issue: "Can't see uploaded files in portal"

**Solution:** Refresh portal, ensure you're in correct container, check access keys

## Rollback Plan

If issues arise:

1. **Database Rollback:**

   ```bash
   # Restore from backup
   psql -h localhost -U postgres -d file_management < backup_YYYYMMDD.sql
   ```

2. **Code Rollback:**

   ```bash
   git checkout HEAD~1
   # Redeploy previous version
   ```

3. **Storage Rollback:**
   ```bash
   # Delete blobs (if needed)
   az storage blob delete-batch \
     --source file-management \
     --account-name YOUR_STORAGE_ACCOUNT
   ```

## Final Checklist

- [ ] ✅ Azure Storage Account created
- [ ] ✅ Connection string obtained
- [ ] ✅ Database migrated
- [ ] ✅ Configuration updated
- [ ] ✅ Local testing passed
- [ ] ✅ Production deployment complete
- [ ] ✅ Monitoring configured
- [ ] ✅ Documentation updated
- [ ] ✅ Team trained on new system

---

## Quick Start Command (Copy-Paste for Experienced Users)

```bash
# 1. Backup database
pg_dump -h localhost -U postgres file_management > backup_$(date +%Y%m%d).sql

# 2. Migrate database
psql -h localhost -U postgres -d file_management < Database/03_migrate_to_azure_blob_storage.sql

# 3. Setup user secrets
cd Backend/FileManagement/FileManagement.Api
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "YOUR_CONNECTION_STRING"

# 4. Test locally
dotnet run

# 5. Deploy
dotnet publish -c Release
```

---

**Support Resources:**

- AZURE_SETUP_GUIDE.md - Detailed setup instructions
- MIGRATION_SUMMARY.md - What changed and why
- [Azure Documentation](https://docs.microsoft.com/azure/)
- [Azure Support](https://azure.microsoft.com/support/)

**Estimated Time:**

- Azure Setup: 10-15 minutes
- Database Migration: 5-10 minutes
- Local Testing: 15-20 minutes
- Production Deployment: 30-45 minutes
- **Total: ~2-3 hours**
