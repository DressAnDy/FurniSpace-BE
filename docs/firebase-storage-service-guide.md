# FurniSpace Firebase Storage Service Guide

This guide explains how FurniSpace stores uploaded project files in Firebase Storage and links them to business records in PostgreSQL.

## 1. Scope

Current implementation supports:

- `POST /projects/{projectId}/files`
- `multipart/form-data` uploads
- Firebase Storage binary upload
- `files` metadata persistence
- `file_links` persistence with `reference_type = PROJECT`
- Project participant access checks

The project does not use CQRS. The request flow follows the current backend pattern:

```text
API Controller
  -> Application service
  -> Infrastructure repository + storage provider
  -> PostgreSQL + Firebase Storage
  -> ServiceResult<T>
```

## 2. Environment Variables

Required:

```env
FIREBASE_STORAGE_BUCKET=your-project.appspot.com
```

Credential options:

```env
FIREBASE_CREDENTIALS_PATH=/app/secrets/firebase_key.json
```

or:

```env
GOOGLE_APPLICATION_CREDENTIALS=/app/secrets/firebase_key.json
```

or service account fields directly in `.env`:

```env
FIREBASE_TYPE=service_account
FIREBASE_PROJECT_ID=your-project-id
FIREBASE_PRIVATE_KEY_ID=...
FIREBASE_PRIVATE_KEY="-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n"
FIREBASE_CLIENT_EMAIL=firebase-adminsdk-xxx@your-project-id.iam.gserviceaccount.com
FIREBASE_CLIENT_ID=...
FIREBASE_AUTH_URI=https://accounts.google.com/o/oauth2/auth
FIREBASE_TOKEN_URI=https://oauth2.googleapis.com/token
FIREBASE_AUTH_PROVIDER_X509_CERT_URL=https://www.googleapis.com/oauth2/v1/certs
FIREBASE_CLIENT_X509_CERT_URL=...
```

If no credentials path is configured, `Google.Cloud.Storage.V1` uses Application Default Credentials.

Optional:

```env
FirebaseStorage__ProjectFilesPrefix=projects
FileUpload__MaxFileSizeBytes=52428800
```

## 3. NuGet Package

Infrastructure uses:

```xml
<PackageReference Include="Google.Cloud.Storage.V1" Version="4.13.0" />
```

## 4. Upload Endpoint

```http
POST /projects/{projectId}/files
Content-Type: multipart/form-data
Authorization: Bearer <access-token>
```

Form fields:

```text
file: binary
fileType: SPACE_IMAGE | FLOOR_PLAN | REFERENCE_IMAGE | CAD_FILE | PDF_DRAWING | MODEL_3D | OTHER | ...
visibility: CUSTOMER_VISIBLE | STAFF_ONLY | PRIVATE
note: optional text
```

If `visibility` is omitted:

- Customer uploads default to `CUSTOMER_VISIBLE`.
- Staff/Admin uploads default to `STAFF_ONLY`.

## 5. Access Rules

- `ADMIN` can upload to any project.
- `CUSTOMER` can upload only to projects where `projects.customer_id` is their account id.
- `SALES` can upload only to projects where `projects.assigned_sales_id` is their account id.
- `DESIGNER` can upload only to projects where `projects.assigned_designer_id` is their account id.
- `uploaded_by` is always taken from JWT `ClaimTypes.NameIdentifier`.

## 6. Accepted File Categories

The upload validator supports common FurniSpace assets:

- Images: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`, `.svg`
- Video: `.mp4`, `.mov`, `.webm`
- 3D/model files: `.glb`, `.gltf`, `.obj`, `.fbx`, `.stl`, `.usdz`
- Drawings/CAD/BIM: `.dwg`, `.dxf`, `.ifc`, `.skp`
- Documents: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.csv`
- Archives: `.zip`, `.rar`, `.7z`

Allowed MIME types are configured through `FileUpload:AllowedMimeTypes`. Some CAD/3D tools upload with `application/octet-stream`, so that MIME type is allowed and extension validation remains required.

## 7. Storage Object Naming

Uploaded files are stored under:

```text
{ProjectFilesPrefix}/{projectId}/{fileId}{extension}
```

Example:

```text
projects/2e7b9c5d-1b32-43ef-96ce-9d2abf2ec4da/4a4f8c9e3f5f4f39a9d9e19db63f1a76.glb
```

Firebase download URLs are generated with a Firebase Storage download token in object metadata.

## 8. Database Persistence

Current schema stores file metadata in `files`:

```text
file_id
file_name
file_url
mime_type
file_size_bytes
uploaded_by
created_at
updated_at
```

And links project files through `file_links`:

```text
file_link_id
file_id
reference_type = PROJECT
reference_id = projectId
file_type
visibility
description
created_at
```

The current DB does not have separate columns for `original_file_name`, `storage_path`, or file status. The API response returns:

- `originalFileName` from the multipart file name
- `fileName` from the generated storage object file name
- `storagePath` from the Firebase object name
- `publicUrl` from `files.file_url`

Add DB columns later if those values must be persisted independently.

## 9. Implementation Files

- `src/FurniSpace.API/Controllers/ProjectFilesController.cs`
- `src/FurniSpace.Application/Interfaces/ProjectFiles/IProjectFileService.cs`
- `src/FurniSpace.Application/Services/ProjectFiles/ProjectFileService.cs`
- `src/FurniSpace.Application/DTOs/ProjectFiles/*`
- `src/FurniSpace.Infrastructure/Interfaces/IFileStorageService.cs`
- `src/FurniSpace.Infrastructure/Storage/FirebaseStorageService.cs`
- `src/FurniSpace.Infrastructure/Repositories/IRepository/IProjectFileRepository.cs`
- `src/FurniSpace.Infrastructure/Repositories/Repository/ProjectFileRepository.cs`

## 10. Operational Notes

- Do not commit Firebase service account JSON files.
- Keep Firebase credentials outside the repo and mount them through Docker or deployment secrets.
- Restrict Firebase service account permissions to the target storage bucket where possible.
- Uploading a project file does not create a notification by default.
