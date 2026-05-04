# AWS S3 Setup Guide - Quick Start

## Overview

Migrated from **Azure Blob Storage** to **AWS S3** for file storage. AWS S3 provides:

- ✅ Cost-effective storage ($0.023/GB)
- ✅ Global availability & CDN integration (CloudFront)
- ✅ 99.999999999% durability (11 9s)
- ✅ Simple API & AWS ecosystem integration

---

## Prerequisites

1. **AWS Account** - [Create free account](https://aws.amazon.com/free)
2. **AWS CLI** - [Install AWS CLI v2](https://aws.amazon.com/cli/)
3. **.NET 8.0 SDK**
4. **PostgreSQL Database**

---

## Step 1: Create AWS S3 Bucket

### Option A: AWS Console (Easy)

1. Go to [AWS S3 Console](https://s3.console.aws.amazon.com)
2. Click **"Create bucket"**
3. **Bucket name:** `file-management-{yourname}` (globally unique)
4. **Region:** Choose closest to your users
5. **Block Public Access:** Uncheck all (to serve files publicly)
6. Click **"Create bucket"**

### Required (Browser Uploads): Configure CORS
If you use the frontend's **presigned PUT** flow (`PUT` directly from the browser to S3), your bucket must allow CORS for:
- `PUT` (upload)
- `GET`/`HEAD` (preview/download)

Otherwise the browser blocks the request and Angular typically shows:
`Http failure response for <s3-presigned-url>: 0 Unknown Error`

**AWS Console**
1. S3 → your bucket → **Permissions**
2. Scroll to **Cross-origin resource sharing (CORS)**
3. Paste a CORS configuration like:

```json
[
  {
    "AllowedOrigins": ["http://localhost:4200", "https://YOUR_FRONTEND_DOMAIN"],
    "AllowedMethods": ["GET", "HEAD", "PUT", "DELETE"],
    "AllowedHeaders": ["*"],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3000
  }
]
```

Notes:
- Use your real FE domain(s); you can list multiple origins.
- `AllowedHeaders: ["*"]` is the simplest for presigned PUT with `Content-Type`.
- If uploads still fail with `400 Bad Request`, it’s usually **not CORS** (CORS failures typically show as `0 Unknown Error`). Most common causes:
  - `AWSS3__Region` does not match the bucket’s actual region (signature/redirect mismatch → S3 can return 400 XML).
  - Presigned URL was created for `PUT` but the client is doing `GET`/`HEAD` (method mismatch).
- If your frontend sends `x-amz-content-sha256` (some SigV4 flows require it), keep it allowed by CORS (with `AllowedHeaders: ["*"]` you're covered).

### Option B: AWS CLI (Fast)

```bash
# Set AWS credentials
aws configure

# Create bucket
aws s3 mb s3://file-management-yourname --region us-east-1

# Enable public access (optional)
aws s3api put-bucket-acl --bucket file-management-yourname --acl public-read

# Enable versioning (optional - for recovery)
aws s3api put-bucket-versioning \
  --bucket file-management-yourname \
  --versioning-configuration Status=Enabled
```

---

## Step 2: Create IAM User for API Access

### Option A: AWS Console

1. Go to [IAM Users](https://console.aws.amazon.com/iam/home#/users)
2. Click **"Create user"**
   - **User name:** `file-management-api`
   - Check **"Access key - Programmatic access"**
3. Click **"Next: Permissions"**
4. Click **"Attach existing policies"**
   - Search: `AmazonS3FullAccess`
   - Check: **AmazonS3FullAccess** (or create custom policy)
5. Click **"Next: Tags"** → **"Create user"**
6. **Save your credentials:**
   - Access Key ID
   - Secret Access Key

### Option B: AWS CLI

```bash
# Create user
aws iam create-user --user-name file-management-api

# Create access key
aws iam create-access-key --user-name file-management-api

# Attach S3 policy
aws iam attach-user-policy \
  --user-name file-management-api \
  --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess

# Get credentials
aws iam list-access-keys --user-name file-management-api
```

---

## Step 3: Configure Backend

### Local Development

```bash
cd Backend/FileManagement/FileManagement.Api

# Set environment variables (option 1)
$env:AWSS3__AccessKeyId = "your_access_key"
$env:AWSS3__SecretAccessKey = "your_secret_key"
$env:AWSS3__Region = "us-east-1"
$env:AWSS3__BucketName = "file-management-yourname"
```

Or use **user-secrets** (recommended):

```bash
dotnet user-secrets init

dotnet user-secrets set "AWSS3:AccessKeyId" "YOUR_ACCESS_KEY"
dotnet user-secrets set "AWSS3:SecretAccessKey" "YOUR_SECRET_KEY"
dotnet user-secrets set "AWSS3:Region" "us-east-1"
dotnet user-secrets set "AWSS3:BucketName" "file-management-yourname"
```

---

## Step 4: Build & Test

```bash
# Restore & Build
cd Backend/FileManagement/FileManagement.Api
dotnet restore
dotnet build

# Run API
dotnet run

# Test at: https://localhost:5001/swagger
```

### Test Upload in Swagger

1. Open Swagger UI
2. POST /api/files/upload
3. Select a file
4. Click "Execute"
5. Check S3 Console → Your bucket → Verify file appeared ✅

---

## Environment Variables Reference

### Development (.env or user-secrets)

```bash
# AWS S3
AWSS3__AccessKeyId=your_access_key_id
AWSS3__SecretAccessKey=your_secret_access_key
AWSS3__Region=us-east-1
AWSS3__BucketName=file-management-yourname

# Database
ConnectionStrings__PostgreSQL=Host=localhost;Port=5432;Database=file_management;Username=postgres;Password=password
```

### Production (Environment Variables in App Service/EC2)

Same as above, set in your deployment platform.

---

## API Endpoints (No Changes)

```
POST   /api/files/upload          → Upload file to S3
GET    /api/files                 → List files from DB
GET    /api/files/{id}            → Get file details
PUT    /api/files/{id}/rename     → Rename file in DB
DELETE /api/files/{id}            → Delete from S3 & DB
```

---

## Cost Breakdown

### Typical Monthly Usage (100GB)

| Service             | Usage | Cost       |
| ------------------- | ----- | ---------- |
| S3 Storage          | 100GB | $2.30      |
| Data Transfer (Out) | 10GB  | $0.90      |
| API Requests        | 100K  | $0.40      |
| **Total**           |       | **~$3.60** |

**Comparison:**

- Cloudinary: $99-300+/month
- Azure: $50-70/month
- AWS S3: **$3-10/month** ✅ (cheapest!)

---

## Common Issues & Fixes

### ❌ "Access Denied" Error

```
Solution: Check IAM user permissions
- Go to IAM → Users → file-management-api
- Ensure "AmazonS3FullAccess" policy is attached
- Verify credentials in .env are correct
```

### ❌ "NoSuchBucket" Error

```
Solution: Bucket doesn't exist or wrong region
- Verify bucket name in console
- Ensure bucket is in same region as config
- Check bucket doesn't have blocked public access (if serving publicly)
```

### ❌ "InvalidArgument" Error for File Upload

```
Solution: File size or metadata issue
- Check file size < 500MB
- Verify file not corrupted
- Check bucket doesn't have upload restrictions
```

### ❌ "Expired Token" or "Invalid Signature"

```
Solution: Credentials issue
- Verify credentials haven't changed
- Check clock sync on server
- Regenerate access keys if unsure
```

### âŒ Browser PUT presigned URL returns 400 (often no readable response body)
Common causes:
- Backend is generating **SigV2** presigned URLs (query contains `AWSAccessKeyId=...&Signature=...&Expires=...`). Many buckets/regions reject SigV2.
  - Fix: force **SigV4** in backend S3 client config.
- `AWSS3__Region` does not match the bucket's actual region (redirect/signature mismatch).

Security note:
- Never paste/share presigned URLs publicly. If you exposed an AWS Access Key, rotate/revoke it immediately.

---

## Production Deployment

### AWS EC2

```bash
# On EC2 instance, set environment variables in /etc/environment
AWSS3__AccessKeyId=your_key
AWSS3__SecretAccessKey=your_secret
AWSS3__Region=us-east-1
AWSS3__BucketName=file-management
ConnectionStrings__PostgreSQL=...
```

### AWS App Runner / Fargate

Set environment variables in deployment configuration:

```yaml
Environment:
  - AWSS3__AccessKeyId: your_key
  - AWSS3__SecretAccessKey: your_secret
  - AWSS3__Region: us-east-1
  - AWSS3__BucketName: file-management
  - ConnectionStrings__PostgreSQL: your_db_connection
```

---

## AWS S3 Benefits Over Azure/Cloudinary

| Feature            | Cloudinary | Azure Blob | AWS S3                  |
| ------------------ | ---------- | ---------- | ----------------------- |
| Cost/month         | $99-300+   | $50-70     | **$3-10**               |
| Storage/GB         | Limited    | $0.023     | **$0.023**              |
| CDN                | Built-in   | Optional   | CloudFront (+$0.085/GB) |
| Public Serving     | ✅         | ✅         | ✅                      |
| Pre-signed URLs    | ✅         | ✅         | ✅                      |
| Versioning         | ✅         | ✅         | ✅                      |
| Lifecycle Policies | ✅         | ✅         | ✅                      |

---

## Enable CloudFront CDN (Optional)

For faster global downloads:

```bash
# Create CloudFront distribution
aws cloudfront create-distribution \
  --origin-domain-name file-management-yourname.s3.us-east-1.amazonaws.com \
  --default-root-object index.html
```

Or use AWS Console:

1. CloudFront → Create distribution
2. Origin: Your S3 bucket
3. Viewer protocol: Redirect HTTP to HTTPS
4. Create

---

## Next Steps

1. ✅ Create S3 bucket
2. ✅ Create IAM user & get credentials
3. ✅ Configure backend
4. ✅ Build & test locally
5. ✅ Deploy to production
6. 📊 Monitor usage in AWS Console
7. 💰 Enjoy 95% cost savings! 🎉

---

## References

- [AWS S3 Documentation](https://docs.aws.amazon.com/s3/)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
- [AWS S3 Pricing](https://aws.amazon.com/s3/pricing/)
- [IAM Best Practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)

---

**Status:** ✅ Ready to Deploy  
**Confidence:** 5/5 ⭐⭐⭐⭐⭐
