# File Management System

This repository contains a full-stack file management application. The project combines an Angular frontend, an ASP.NET Core backend, PostgreSQL database scripts, and AWS S3 for file storage.

## Architecture

The application is implemented as a three-tier solution:

- Angular frontend: single-page application
- ASP.NET Core backend: REST API and business logic
- PostgreSQL: relational metadata storage
- AWS S3: object storage for uploaded files

## Technology stack

- Frontend: Angular
- Backend: ASP.NET Core 8
- Database: PostgreSQL
- Data access: Dapper
- File storage: AWS S3
- Authentication: JWT with refresh tokens

## Prerequisites

- .NET 8 SDK
- Node.js 18 or later
- PostgreSQL 12 or later
- AWS credentials with access to an S3 bucket
- Git

## Setup

### Database

1. Open `Backend/FileManagement/FileManagement.Api/appsettings.json` or environment configuration.
2. Configure the PostgreSQL connection string under `ConnectionStrings:PostgreSQL`.
3. Run the database scripts in order:
   - `Database/01_create_tables.sql`
   - `Database/02_create_functions.sql`

### Backend

1. Change directory to `Backend/FileManagement/FileManagement.Api`.
2. Restore dependencies:
   ```bash
   dotnet restore
   ```

````
3. Configure AWS S3 credentials and bucket settings in `appsettings.json` or via user secrets:
   - `AWSS3:AccessKeyId`
   - `AWSS3:SecretAccessKey`
   - `AWSS3:Region`
   - `AWSS3:BucketName`
   - optional: `AWSS3:UsePublicReadAcl`
4. Start the backend service:
   ```bash
dotnet run
````

The backend listens on local ASP.NET Core ports, typically `https://localhost:5001` and `http://localhost:5000`.

### Frontend

1. Change directory to `Frontend/file-management-fe`.
2. Install dependencies:
   ```bash
   npm install
   ```

````
3. Start the development server:
   ```bash
ng serve
````

4. Open the application in a browser at `http://localhost:4200`.

If necessary, configure the backend base URL in the Angular environment settings.

## Project structure

- `Backend/FileManagement/FileManagement.Api`: API implementation, controllers, and dependency injection
- `Backend/FileManagement/FileManagement.Core`: business logic and service interfaces
- `Backend/FileManagement/FileManagement.Data`: repository and storage implementations including AWS S3 service
- `Database`: SQL definitions and function scripts
- `Frontend/file-management-fe`: Angular client application

## API endpoints

### Authentication

- `POST /api/auth/register` — register a new user
- `POST /api/auth/login` — authenticate and obtain access/refresh tokens
- `POST /api/auth/refresh` — rotate and renew JWT tokens

### Files

- `POST /api/files/upload` — upload a file via multipart form data
- `POST /api/files/upload-url` — request a presigned S3 PUT URL
- `POST /api/files` — save uploaded file metadata after direct S3 upload
- `GET /api/files` — list files with optional pagination and folder filter
- `GET /api/files/{id}` — retrieve file metadata
- `GET /api/files/{id}/url` — obtain a presigned download/view URL
- `PUT /api/files/{id}/rename` — rename a file
- `DELETE /api/files/{id}` — delete a file
- `GET /api/files/search` — search files by name

### Folders

- `GET /api/folders` — list folders
- `POST /api/folders` — create a folder
- `PUT /api/folders/{id}/rename` — rename a folder
- `DELETE /api/folders/{id}` — delete a folder

### Realtime

- `GET /api/events/stream` — subscribe to server-sent events

## Configuration notes

The current implementation uses AWS S3 as the file storage provider. The backend enforces AWS Signature Version 4 for presigned URLs and organizes object keys by user and folder.

Requests requiring authentication expect the `Authorization: Bearer <accessToken>` header.

## Database scripts

- `Database/01_create_tables.sql`
- `Database/02_create_functions.sql`

Other SQL files in the repository are present for migration history. The current deployment flow uses the scripts above.

## Important detail

This repository is configured for AWS S3 storage. References to other object storage providers are legacy and not part of the current deployment flow.

- `fn_folder_create()` - Create folder
- `fn_folder_get_list()` - List folders with parent filter
- `fn_folder_delete()` - Delete folder (cascades to files)

## Cloudinary Integration

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

## Validation & Error Handling

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

## Data Flow

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

## Testing API

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

## Configuration Files

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

## Security Considerations

- Validate all file uploads
- Use Cloudinary for secure file storage (not in database)
- Implement authentication (not included in base version)
- Use HTTPS in production
- Sanitize file names
- Rate limit API endpoints
- CORS configured for development

## Performance

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

## Documentation

- [Angular Docs](https://angular.dev)
- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core)
- [PostgreSQL Docs](https://www.postgresql.org/docs)
- [Cloudinary API](https://cloudinary.com/documentation/cloudinary_api)
- [Dapper Documentation](https://github.com/DapperLib/Dapper)

## Troubleshooting

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

## Future Enhancements

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
