# File Management System - Full-Stack Application

A production-like file management system built with Angular, ASP.NET Core, PostgreSQL, and Cloudinary.

## 🏗 Architecture

```
Frontend (Angular)
    ↓
API Gateway (ASP.NET Core Web API)
    ↓
Services Layer (Dapper + PostgreSQL Functions)
    ↓
PostgreSQL Database
    ↓
Cloudinary (File Storage)
```

## 🛠 Tech Stack

- **Frontend**: Angular (standalone components, SCSS)
- **Backend**: ASP.NET Core 8 Web API
- **Database**: PostgreSQL with custom functions
- **ORM**: Dapper (lightweight, raw SQL)
- **File Storage**: Cloudinary
- **API Pattern**: RESTful with consistent response format

## 📋 Prerequisites

- .NET 8 SDK
- Node.js 18+ and npm
- PostgreSQL 12+
- Cloudinary account (free tier available)
- Git

## 🚀 Quick Start

### 1. Database Setup

```bash
# Connect to PostgreSQL
psql -U postgres -h localhost

# Create database
CREATE DATABASE file_management;

# Connect to the new database
\c file_management

# Run schema scripts
\i Database/01_create_tables.sql
\i Database/02_create_functions.sql

# Verify tables
\dt  # List tables
\df  # List functions
```

### 2. Backend Setup

```bash
# Navigate to backend
cd Backend/FileManagement/FileManagement.Api

# Restore NuGet packages
dotnet restore

# Configure appsettings
# Edit appsettings.json with your:
# - PostgreSQL connection string
# - Cloudinary credentials

# Run migrations (if using Entity Framework - NOT USED HERE)
# All data operations go through PostgreSQL functions

# Start backend
dotnet run
# API runs on https://localhost:5001 and http://localhost:5000
```

### 3. Frontend Setup

```bash
# Navigate to frontend
cd Frontend/file-management-fe

# Install dependencies
npm install

# Configure API URL
# Update src/app/services/file.service.ts with backend API URL

# Start development server
ng serve
# Frontend runs on http://localhost:4200
```

## 📦 Project Structure

### Backend

```
FileManagement.Api/
├── Controllers/
│   └── FilesController.cs          # API endpoints
├── Services/
│   ├── FileService.cs               # Business logic
│   └── CloudinaryService.cs         # File storage integration
├── Data/
│   └── FileRepository.cs            # Database access via Dapper
├── Models/
│   └── FileDto.cs                  # DTOs and API models
├── Program.cs                       # DI configuration
└── appsettings.json                # Configuration
```

### Frontend

```
src/app/
├── components/
│   ├── file-upload/
│   │   ├── file-upload.component.ts
│   │   ├── file-upload.component.html
│   │   └── file-upload.component.scss
│   └── file-list/
│       ├── file-list.component.ts
│       ├── file-list.component.html
│       └── file-list.component.scss
├── services/
│   └── file.service.ts             # API integration
├── models/
│   └── file.model.ts               # TypeScript models
├── app.ts                          # Main component
└── app.html                        # Main template
```

### Database

```
Database/
├── 01_create_tables.sql            # Schema
└── 02_create_functions.sql         # PostgreSQL functions
```

## 🔌 API Endpoints

All responses follow this format:

```json
{
  "success": true,
  "data": { ... },
  "message": "Success message"
}
```

### Files

| Method | Endpoint                 | Description            |
| ------ | ------------------------ | ---------------------- |
| POST   | `/api/files/upload`      | Upload file            |
| GET    | `/api/files`             | List files (paginated) |
| GET    | `/api/files/{id}`        | Get file details       |
| PUT    | `/api/files/{id}/rename` | Rename file            |
| DELETE | `/api/files/{id}`        | Delete file            |
| GET    | `/api/files/search`      | Search files by name   |
| POST   | `/api/files/upload-url`  | Presigned PUT URL (S3) |
| POST   | `/api/files`             | Save metadata (S3 key) |

### Auth (JWT + Refresh)

> Most endpoints require `Authorization: Bearer <accessToken>`.

| Method | Endpoint             | Description |
| ------ | -------------------- | ----------- |
| POST   | `/api/auth/register` | Register    |
| POST   | `/api/auth/login`    | Login       |
| POST   | `/api/auth/refresh`  | Refresh JWT |

### Folders

| Method | Endpoint                    | Description   |
| ------ | --------------------------- | ------------- |
| GET    | `/api/folders?parentId=...` | List folders  |
| POST   | `/api/folders`              | Create folder |
| PUT    | `/api/folders/{id}/rename`  | Rename folder |
| DELETE | `/api/folders/{id}`         | Delete folder |

### Realtime (SSE)

| Method | Endpoint             | Description |
| ------ | -------------------- | ----------- |
| GET    | `/api/events/stream` | Live events |

### Query Parameters

**Pagination**:

- `pageNumber` (default: 1)
- `pageSize` (default: 20, max: 100)

**Filtering**:

- `folderId` (UUID, optional)

**Search**:

- `searchTerm` (string, case-insensitive)

### Examples

```bash
# Upload file
curl -X POST http://localhost:5000/api/files/upload \
  -F "file=@image.jpg" \
  -H "Content-Type: multipart/form-data"

# Get files (page 1, 20 items)
curl http://localhost:5000/api/files?pageNumber=1&pageSize=20

# Search files
curl "http://localhost:5000/api/files/search?searchTerm=invoice"

# Delete file
curl -X DELETE http://localhost:5000/api/files/{fileId}
```

## 🗄 Database Schema

### Tables

**files**

- `id` (UUID, PK)
- `name` (VARCHAR 255)
- `size` (BIGINT, bytes)
- `content_type` (VARCHAR 100)
- `cloudinary_url` (TEXT)
- `public_id` (VARCHAR 255, unique)
- `folder_id` (UUID, FK, nullable)
- `created_at` (TIMESTAMP)

**folders**

- `id` (UUID, PK)
- `name` (VARCHAR 255)
- `parent_id` (UUID, FK, nullable)
- `created_at` (TIMESTAMP)

### PostgreSQL Functions

All data access happens through functions:

- `fn_file_create()` - Create file with Cloudinary metadata
- `fn_file_get_list()` - Paginated file list with folder filtering
- `fn_file_get_by_id()` - Get single file
- `fn_file_rename()` - Rename file
- `fn_file_delete()` - Delete file, returns public_id for Cloudinary cleanup
- `fn_file_search()` - Search with pagination
- `fn_folder_create()` - Create folder
- `fn_folder_get_list()` - List folders with parent filter
- `fn_folder_delete()` - Delete folder (cascades to files)

## ☁️ Cloudinary Integration

### Features

- Direct upload from backend to Cloudinary
- Support for files up to 10GB (chunked upload)
- Automatic cleanup on delete
- Secure URL storage in database
- MIME type validation

### Configuration

1. Get free Cloudinary account: https://cloudinary.com/users/register/free
2. Copy credentials:
   - Cloud Name
   - API Key
   - API Secret
3. Add to `appsettings.json`:

```json
{
  "Cloudinary": {
    "CloudName": "your_cloud_name",
    "ApiKey": "your_api_key",
    "ApiSecret": "your_api_secret"
  }
}
```

## 🛡️ Validation & Error Handling

### Backend Validation

- File size limit: 10GB
- Allowed MIME types: images, PDF, text, video
- File name length: max 255 characters
- Pagination size: max 100 items per page

### Frontend Validation

- Same file size limits
- Real-time upload progress
- Comprehensive error messages
- Network error handling

### Error Response

```json
{
  "success": false,
  "data": null,
  "message": "File size exceeds 10GB limit"
}
```

## 🔄 Data Flow

### Upload Flow

1. Frontend: User selects file
2. Frontend: Validate file size/type
3. Backend: Receive file + metadata
4. Backend: Upload to Cloudinary
5. Backend: Save metadata to PostgreSQL via `fn_file_create()`
6. Backend: Return file info to frontend
7. Frontend: Display in list

### Delete Flow

1. Frontend: User deletes file
2. Backend: Call `fn_file_delete()`
3. Backend: Get public_id from database
4. Backend: Delete from Cloudinary
5. Backend: Return success
6. Frontend: Refresh list

## 🧪 Testing API

### Using Swagger

```
https://localhost:5001/swagger
```

### Using HTTP Client (VS Code)

Create `.http` files in root directory:

```http
### Upload File
POST http://localhost:5000/api/files/upload
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary

------WebKitFormBoundary
Content-Disposition: form-data; name="file"; filename="test.txt"
Content-Type: text/plain

<@ test.txt
------WebKitFormBoundary--

### Get Files
GET http://localhost:5000/api/files?pageNumber=1&pageSize=10
```

## 📝 Configuration Files

### Backend: appsettings.json

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=file_management;Username=postgres;Password=..."
  },
  "Cloudinary": {
    "CloudName": "...",
    "ApiKey": "...",
    "ApiSecret": "..."
  }
}
```

### Frontend: environment.ts

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
};
```

## 🚨 Security Considerations

- Validate all file uploads
- Use Cloudinary for secure file storage (not in database)
- Implement authentication (not included in base version)
- Use HTTPS in production
- Sanitize file names
- Rate limit API endpoints
- CORS configured for development

## 📈 Performance

- Paginated file listing (prevents memory issues)
- Efficient database queries via PostgreSQL functions
- Connection pooling with Dapper
- Indexed fields: folder_id, created_at, public_id
- RxJS unsubscribe on component destroy

## 🔧 Development Commands

### Backend

```bash
# Build
dotnet build

# Run
dotnet run

# Tests (add xUnit project)
dotnet test

# Watch mode
dotnet watch run
```

### Frontend

```bash
# Build
ng build

# Development server
ng serve

# Production build
ng build --configuration production

# Tests
ng test

# Lint
ng lint
```

## 📚 Documentation

- [Angular Docs](https://angular.dev)
- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core)
- [PostgreSQL Docs](https://www.postgresql.org/docs)
- [Cloudinary API](https://cloudinary.com/documentation/cloudinary_api)
- [Dapper Documentation](https://github.com/DapperLib/Dapper)

## 🐛 Troubleshooting

### PostgreSQL Connection Issues

```bash
# Check if PostgreSQL is running
psql -U postgres -h localhost -c "SELECT 1"

# Verify database exists
psql -U postgres -h localhost -c "\l"
```

### Cloudinary Upload Fails

- Verify API credentials
- Check account storage limit
- Test with small file first

### CORS Issues

- Ensure backend CORS policy allows frontend origin
- Check browser console for specific error

### Port Already in Use

```bash
# Backend port 5000/5001
netstat -ano | findstr :5000

# Frontend port 4200
netstat -ano | findstr :4200
```

## 📄 License

MIT

## 💡 Future Enhancements

- User authentication & authorization
- File sharing links
- Virus scanning integration
- Advanced search (date, size filters)
- File versioning
- Bulk operations
- WebSocket for real-time updates
- S3/Azure Blob Storage support
- File compression
- OCR for documents
