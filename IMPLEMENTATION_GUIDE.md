# Implementation Guide - Full-Stack File Management System

## Overview

This document provides a complete implementation guide for the File Management System project. Follow along with the folder structure and verify all components are in place.

## ✅ Implementation Checklist

### Database Layer

- [x] PostgreSQL schema created (`Database/01_create_tables.sql`)
  - [x] `files` table with UUID PK, size, content_type, cloudinary_url, public_id
  - [x] `folders` table with hierarchical structure (parent_id nullable)
  - [x] Indexes on commonly queried columns
  - [x] UUID extension enabled

- [x] PostgreSQL functions created (`Database/02_create_functions.sql`)
  - [x] `fn_file_create()` - Insert file metadata
  - [x] `fn_file_get_list()` - Paginated retrieval
  - [x] `fn_file_get_by_id()` - Single file lookup
  - [x] `fn_file_rename()` - Update file name
  - [x] `fn_file_delete()` - Delete with public_id return
  - [x] `fn_file_search()` - Case-insensitive search
  - [x] Folder functions for hierarchical support
  - [x] Proper error handling in functions

### Backend - ASP.NET Core

**Project Structure:**

```
FileManagement.Api/
├── Controllers/
│   └── FilesController.cs ✓
├── Services/
│   ├── FileService.cs ✓ (business logic)
│   └── CloudinaryService.cs ✓ (file storage)
├── Data/
│   ├── FileRepository.cs ✓ (Dapper + PostgreSQL)
│   └── FolderRepository.cs ✓ (hierarchical support)
├── Models/
│   └── FileDto.cs ✓ (DTOs and API models)
├── Program.cs ✓ (DI + configuration)
├── appsettings.json ✓ (with placeholders)
└── FileManagement.Api.csproj ✓ (Dapper, Npgsql, CloudinaryDotNet)
```

**Packages Added:**

- Dapper 2.1.15
- Npgsql 8.0.1
- CloudinaryDotNet 1.26.0
- Swashbuckle.AspNetCore 6.6.2

**API Endpoints Implemented:**

- [x] POST `/api/files/upload` - Upload with progress
- [x] GET `/api/files` - Paginated list with folder filter
- [x] GET `/api/files/{id}` - Single file retrieval
- [x] PUT `/api/files/{id}/rename` - Rename endpoint
- [x] DELETE `/api/files/{id}` - Delete with Cloudinary cleanup
- [x] GET `/api/files/search` - Search by name

**Features:**

- [x] Consistent API response format: `{ success, data, message }`
- [x] Pagination: pageNumber, pageSize (max 100)
- [x] Validation: file size (10GB), name length (255)
- [x] CORS enabled for frontend
- [x] Logging configured
- [x] Swagger documentation

### Frontend - Angular

**Project Structure:**

```
src/app/
├── components/
│   ├── file-upload/ ✓
│   │   ├── file-upload.component.ts
│   │   ├── file-upload.component.html
│   │   └── file-upload.component.scss
│   └── file-list/ ✓
│       ├── file-list.component.ts
│       ├── file-list.component.html
│       └── file-list.component.scss
├── services/
│   └── file.service.ts ✓ (API + RxJS patterns)
├── models/
│   └── file.model.ts ✓ (interfaces)
├── app.ts ✓ (standalone main component)
└── app.html ✓ (layout template)
```

**Angular Features:**

- [x] Standalone components (no modules)
- [x] TypeScript strict mode enabled
- [x] RxJS with proper unsubscribe patterns
- [x] Async pipe where applicable
- [x] FormModule for two-way binding
- [x] HttpClientModule configured
- [x] SCSS styling with responsive design

**Components:**

1. **FileUploadComponent** ✓
   - Drag & drop file upload
   - Progress bar with percentage
   - File size validation (10GB)
   - Error/success messages
   - Cancel functionality

2. **FileListComponent** ✓
   - Paginated table display
   - File search with debouncing
   - Rename functionality
   - Delete with confirmation
   - Download (open in new tab)
   - File icons based on type
   - Responsive layout

3. **FileService** ✓
   - All CRUD operations
   - Upload progress tracking
   - Error handling
   - File size formatting
   - Search with pagination

### Configuration Files

- [x] `.env.example` - Backend (PostgreSQL, Cloudinary)
- [x] `.env.example` - Frontend (API URL)
- [x] `appsettings.json` - With demo values
- [x] `README.md` - Comprehensive documentation

## 🔧 Installation Steps

### 1. Database Setup

```bash
# Create database
createdb -U postgres file_management

# Apply schema
psql -U postgres -d file_management -f Database/01_create_tables.sql
psql -U postgres -d file_management -f Database/02_create_functions.sql

# Verify
psql -U postgres -d file_management -c "\dt"
psql -U postgres -d file_management -c "\df"
```

### 2. Cloudinary Setup

1. Sign up: https://cloudinary.com
2. Get credentials from dashboard
3. Add to `appsettings.json`:
   ```json
   {
     "Cloudinary": {
       "CloudName": "your-cloud",
       "ApiKey": "your-key",
       "ApiSecret": "your-secret"
     }
   }
   ```

### 3. Backend Installation

```bash
cd Backend/FileManagement/FileManagement.Api

# Restore packages
dotnet restore

# Update appsettings.json
# - PostgreSQL connection string
# - Cloudinary credentials

# Run
dotnet run

# Swagger docs at: https://localhost:5001/swagger
```

### 4. Frontend Installation

```bash
cd Frontend/file-management-fe

# Install dependencies
npm install

# Update API URL in file.service.ts if needed
# Update FileService constructor

# Run
ng serve

# Open: http://localhost:4200
```

## 🧪 Testing Checklist

### Backend API Testing

```bash
# List files
curl http://localhost:5000/api/files

# Search
curl "http://localhost:5000/api/files/search?searchTerm=test"

# Upload (requires file)
curl -X POST http://localhost:5000/api/files/upload \
  -F "file=@test.txt"
```

### Frontend Testing

- [x] File upload with drag & drop
- [x] Progress bar display
- [x] File listing with pagination
- [x] Search functionality
- [x] Rename file
- [x] Delete file
- [x] Responsive design (mobile/tablet/desktop)

## 📊 Database Schema Reference

### Files Table

```sql
CREATE TABLE files (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    size BIGINT NOT NULL,
    content_type VARCHAR(100),
    cloudinary_url TEXT,
    public_id VARCHAR(255) UNIQUE,
    folder_id UUID REFERENCES folders(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Folders Table

```sql
CREATE TABLE folders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    parent_id UUID REFERENCES folders(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## 🔐 Security Checklist

- [x] Input validation (file size, name length)
- [x] MIME type filtering
- [x] SQL injection prevention (Dapper + parameterized queries)
- [x] XSS prevention (Angular sanitization)
- [x] CORS configured
- [x] HTTPS enforced in production
- [x] Sensitive data NOT in version control
- [ ] ~TODO: Add authentication/authorization~
- [ ] ~TODO: Add rate limiting~
- [ ] ~TODO: Add file encryption~

## 📈 Performance Optimizations

- [x] Database indexes on frequently queried columns
- [x] Pagination to prevent memory issues
- [x] Connection pooling with Dapper
- [x] RxJS unsubscribe on component destroy
- [x] Change detection strategy optimization opportunity
- [x] Lazy loading ready (Angular routing not added)

## 🚀 Deployment Considerations

### Backend Deployment

```bash
# Production build
dotnet publish -c Release

# Environment variables
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://api.example.com
ConnectionStrings__PostgreSQL=prod_connection_string
Cloudinary__CloudName=prod_cloud_name
```

### Frontend Deployment

```bash
# Production build
ng build --configuration production

# Environment configuration
# src/environments/environment.prod.ts with production API URL
```

### Docker (Optional)

```dockerfile
# Backend Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish .
ENTRYPOINT ["dotnet", "FileManagement.Api.dll"]

# Frontend Dockerfile
FROM node:18 AS build
WORKDIR /app
COPY . .
RUN npm install && ng build --configuration production

FROM nginx:alpine
COPY --from=build /app/dist/file-management-fe /usr/share/nginx/html
```

## 🐛 Troubleshooting

### Common Issues

1. **PostgreSQL connection fails**
   - Verify PostgreSQL is running
   - Check connection string format
   - Verify database exists

2. **Cloudinary upload fails**
   - Verify API credentials
   - Check account storage quota
   - Test with small file first

3. **CORS errors in browser**
   - Ensure backend CORS policy allows frontend origin
   - Check backend is running on correct port

4. **Angular HttpClient errors**
   - Verify API URL is correct in file.service.ts
   - Check backend is returning proper response format

## 📚 File Organization

```
Project/
├── Backend/
│   └── FileManagement/
│       ├── FileManagement.sln
│       ├── FileManagement.Api/
│       │   ├── Controllers/ ✓
│       │   ├── Services/ ✓
│       │   ├── Data/ ✓
│       │   ├── Models/ ✓
│       │   ├── Program.cs ✓
│       │   └── appsettings.json ✓
│       ├── .env.example ✓
│       └── FileManagement.Core/ (optional)
├── Frontend/
│   └── file-management-fe/
│       ├── src/app/
│       │   ├── components/ ✓
│       │   ├── services/ ✓
│       │   ├── models/ ✓
│       │   └── app.ts ✓
│       └── .env.example ✓
├── Database/
│   ├── 01_create_tables.sql ✓
│   └── 02_create_functions.sql ✓
└── README.md ✓
```

## ✨ Features Summary

### Completed ✓

- Full CRUD operations for files
- Hierarchical folder structure (schema ready)
- Pagination with configurable page size
- Search functionality with case-insensitive matching
- File upload with progress tracking
- Drag & drop upload
- Real-time file size formatting
- Comprehensive error handling
- Responsive UI design
- API documentation with Swagger
- PostgreSQL functions for all data access
- Cloudinary integration for file storage
- Clean architecture separation

### Ready for Enhancement

- User authentication (add JWT)
- Authorization (permissions, sharing)
- File versioning
- Advanced search filters
- Batch operations
- WebSocket real-time updates
- File compression
- Virus scanning
- Full-text search with PostgreSQL
- Activity logging
- S3/Azure Blob alternative storage

## 📞 Support

Refer to:

- `README.md` - General documentation
- Component files - Code comments and JSDoc
- Database scripts - SQL comments
