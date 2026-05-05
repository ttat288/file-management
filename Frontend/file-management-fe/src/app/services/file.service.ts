import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpEvent,
  HttpEventType,
  HttpHeaders,
  HttpProgressEvent,
} from '@angular/common/http';
import {
  Observable,
  BehaviorSubject,
  Subject,
  throwError,
  switchMap,
  of,
  mergeMap,
  from,
} from 'rxjs';
import { catchError, map, tap, timeout } from 'rxjs/operators';
import { ApiResponse, FileItem, PagedResult, UploadProgress } from '../models/file.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class FileService {
  private apiUrl = `${environment.apiBaseUrl}/files`;
  private uploadProgressSubject = new BehaviorSubject<UploadProgress[]>([]);
  public uploadProgress$ = this.uploadProgressSubject.asObservable();
  private filesChangedSubject = new Subject<void>();
  public filesChanged$ = this.filesChangedSubject.asObservable();

  constructor(private http: HttpClient) {}

  /**
   * Upload a file to the server
   */
  uploadFile(file: File, folderId?: string): Observable<HttpEvent<ApiResponse<FileItem>>> {
    const formData = new FormData();
    formData.append('file', file);

    const uploadProgress: UploadProgress = {
      fileName: file.name,
      progress: 0,
      status: 'uploading',
    };
    this.updateUploadProgress(uploadProgress);

    let url = `${this.apiUrl}/upload`;
    if (folderId) {
      url += `?folderId=${folderId}`;
    }

    return this.http
      .post<ApiResponse<FileItem>>(url, formData, {
        reportProgress: true,
        observe: 'events',
      })
      .pipe(
        tap((event) => this.handleUploadProgress(event, file.name)),
        catchError((error) => {
          this.setUploadError(file.name, error);
          return throwError(() => error);
        }),
      );
  }

  /**
   * Batch upload multiple files sequentially
   */
  uploadFileDirectBatch(files: File[], folderId?: string | null): Observable<void> {
    return new Observable((subscriber) => {
      let index = 0;
      let failures = 0;

      const uploadNext = () => {
        if (index >= files.length) {
          if (failures > 0) {
            subscriber.error(new Error('One or more files failed to upload'));
          } else {
            subscriber.next();
            subscriber.complete();
          }
          return;
        }

        const file = files[index];
        index++;

        this.uploadFileDirect(file, folderId).subscribe({
          next: (event) => {
            if ((event as any).type === 4) {
              setTimeout(uploadNext, 100);
            }
          },
          error: () => {
            failures++;
            setTimeout(uploadNext, 100);
          },
        });
      };

      uploadNext();
    });
  }
  private getContentType(file: File): string {
    if (file.type && file.type !== 'application/octet-stream') {
      return file.type;
    }

    const ext = file.name.split('.').pop()?.toLowerCase();
    if (!ext) return 'application/octet-stream';

    const mimeTypes: { [key: string]: string } = {
      png: 'image/png',
      jpg: 'image/jpeg',
      jpeg: 'image/jpeg',
      gif: 'image/gif',
      webp: 'image/webp',
      svg: 'image/svg+xml',
      bmp: 'image/bmp',
      ico: 'image/x-icon',
      pdf: 'application/pdf',
      txt: 'text/plain',
      json: 'application/json',
      xml: 'application/xml',
      html: 'text/html',
      css: 'text/css',
      js: 'application/javascript',
      mp4: 'video/mp4',
      avi: 'video/x-msvideo',
      mp3: 'audio/mpeg',
      wav: 'audio/wav',
      zip: 'application/zip',
      rar: 'application/x-rar-compressed',
      doc: 'application/msword',
      docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      xls: 'application/vnd.ms-excel',
      xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    };

    return mimeTypes[ext] || 'application/octet-stream';
  }

  /**
   * Check if file name exists in folder (Google Drive style)
   */
  checkFileName(
    fileName: string,
    folderId?: string,
  ): Observable<ApiResponse<{ exists: boolean; suggestedName: string }>> {
    return this.http.post<ApiResponse<{ exists: boolean; suggestedName: string }>>(
      `${this.apiUrl}/check-filename`,
      {
        fileName,
        folderId,
      },
    );
  }

  /**
   * Create S3 presigned PUT URL
   */
  createUploadUrl(
    fileName: string,
    contentType: string,
    folderId?: string,
  ): Observable<ApiResponse<{ uploadUrl: string; key: string }>> {
    return this.http.post<ApiResponse<{ uploadUrl: string; key: string }>>(
      `${this.apiUrl}/upload-url`,
      {
        fileName,
        contentType,
        folderId,
      },
    );
  }

  /**
   * Finalize upload: persist metadata
   */
  createFileMetadata(payload: {
    name: string;
    key: string;
    size: number;
    contentType: string;
    folderId?: string | null;
  }): Observable<ApiResponse<FileItem>> {
    return this.http.post<ApiResponse<FileItem>>(`${this.apiUrl}`, payload);
  }

  /**
   * Presigned PUT upload (S3) + metadata create.
   * Uses Google Drive-style auto-rename if file name exists.
   */
  uploadFileDirect(
    file: File,
    folderId?: string | null,
  ): Observable<HttpEvent<ApiResponse<FileItem>>> {
    const uploadProgress: UploadProgress = {
      fileName: file.name,
      progress: 0,
      status: 'uploading',
    };
    this.updateUploadProgress(uploadProgress);

    const contentType = this.getContentType(file);

    return this.checkFileName(file.name, folderId || undefined).pipe(
      switchMap((checkRes) => {
        if (!checkRes.success) throw new Error(checkRes.message || 'Failed to check file name');

        const finalFileName = checkRes.data?.suggestedName || this.getFileBaseName(file.name);
        const extension = this.getFileExtension(file.name);
        const uploadFileName = extension ? `${finalFileName}.${extension}` : finalFileName;

        this.updateUploadProgress({
          fileName: finalFileName,
          progress: 0,
          status: 'uploading',
        });

        return this.createUploadUrl(uploadFileName, contentType, folderId || undefined).pipe(
          switchMap((res) => {
            if (!res.success || !res.data?.uploadUrl || !res.data?.key)
              throw new Error(res.message || 'Failed to create upload URL');

            const upload$ = new Observable<HttpEvent<any>>((subscriber) => {
              const xhr = new XMLHttpRequest();
              xhr.open('PUT', res.data.uploadUrl, true);
              xhr.setRequestHeader('Content-Type', contentType);

              xhr.upload.onprogress = (event) => {
                if (event.lengthComputable) {
                  subscriber.next({
                    type: HttpEventType.UploadProgress,
                    loaded: event.loaded,
                    total: event.total,
                  } as HttpProgressEvent);
                }
              };

              xhr.onload = () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                  subscriber.complete();
                } else {
                  subscriber.error(new Error('Upload to S3 failed'));
                }
              };

              xhr.onerror = () => subscriber.error(new Error('Upload to S3 failed'));
              xhr.send(file);

              return () => xhr.abort();
            });

            return upload$.pipe(
              switchMap(() =>
                this.createFileMetadata({
                  name: finalFileName,
                  key: res.data.key,
                  size: file.size,
                  contentType: contentType,
                  folderId: folderId || null,
                }).pipe(
                  map(
                    (finalRes) =>
                      ({
                        type: HttpEventType.Response,
                        body: finalRes,
                      }) as any,
                  ),
                ),
              ),
              tap(() => {
                this.updateUploadProgress({
                  fileName: finalFileName,
                  progress: 100,
                  status: 'success',
                });
                this.notifyFilesChanged();
              }),
            );
          }),
        );
      }),
      catchError((error) => {
        this.setUploadError(file.name, error);
        console.error('Upload failed:', error);
        return throwError(() => error);
      }),
    );
  }

  /**
   * Get paginated list of files
   */
  getFiles(
    folderId?: string,
    pageNumber: number = 1,
    pageSize: number = 20,
  ): Observable<ApiResponse<PagedResult<FileItem>>> {
    let url = `${this.apiUrl}?pageNumber=${pageNumber}&pageSize=${pageSize}`;
    if (folderId) {
      url += `&folderId=${folderId}`;
    }

    return this.http.get<ApiResponse<PagedResult<FileItem>>>(url).pipe(timeout(15000));
  }

  /**
   * Get a specific file by ID
   */
  getFile(fileId: string): Observable<ApiResponse<FileItem>> {
    return this.http.get<ApiResponse<FileItem>>(`${this.apiUrl}/${fileId}`);
  }

  /**
   * Get presigned URL for viewing/downloading a file
   */
  getFileUrl(
    fileId: string,
    expiresMinutes: number = 60,
    download: boolean = false,
  ): Observable<ApiResponse<string>> {
    const downloadQuery = download ? '&download=true' : '';
    return this.http
      .get<
        ApiResponse<string>
      >(`${this.apiUrl}/${fileId}/url?expires=${expiresMinutes}${downloadQuery}`)
      .pipe(timeout(15000));
  }

  /**
   * Search files by name
   */
  searchFiles(
    searchTerm: string,
    folderId?: string,
    pageNumber: number = 1,
    pageSize: number = 20,
  ): Observable<ApiResponse<PagedResult<FileItem>>> {
    let url = `${this.apiUrl}/search?searchTerm=${encodeURIComponent(
      searchTerm,
    )}&pageNumber=${pageNumber}&pageSize=${pageSize}`;
    if (folderId) {
      url += `&folderId=${folderId}`;
    }

    return this.http.get<ApiResponse<PagedResult<FileItem>>>(url).pipe(timeout(15000));
  }

  /**
   * Rename a file
   */
  renameFile(fileId: string, newName: string): Observable<ApiResponse<FileItem>> {
    return this.http.put<ApiResponse<FileItem>>(`${this.apiUrl}/${fileId}/rename`, { newName });
  }

  /**
   * Delete a file
   */
  deleteFile(fileId: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${fileId}`);
  }

  /**
   * Set API base URL (useful for environment configuration)
   */
  setApiUrl(url: string): void {
    this.apiUrl = url;
  }

  /**
   * Format file size for display
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  /**
   * Get file extension
   */
  getFileExtension(filename: string): string {
    const idx = filename.lastIndexOf('.');
    return idx > 0 ? filename.slice(idx + 1) : '';
  }

  /**
   * Get file base name without extension
   */
  getFileBaseName(filename: string): string {
    const idx = filename.lastIndexOf('.');
    return idx > 0 ? filename.slice(0, idx) : filename;
  }

  /**
   * Clear upload progress
   */
  clearUploadProgress(): void {
    this.uploadProgressSubject.next([]);
  }

  /**
   * Clear completed uploads (success/error)
   */
  clearCompletedUploads(): void {
    const current = this.uploadProgressSubject.value;
    const active = current.filter((p) => p.status === 'uploading');
    this.uploadProgressSubject.next(active);
  }

  notifyFilesChanged(): void {
    this.filesChangedSubject.next();
  }

  private handleUploadProgress(event: HttpEvent<any>, fileName: string): void {
    if (event.type === HttpEventType.UploadProgress) {
      const total = event.total ?? 0;
      const progress = total > 0 ? Math.round((event.loaded / total) * 100) : 0;
      this.updateUploadProgress({
        fileName,
        progress,
        status: 'uploading',
      });
    } else if (event.type === HttpEventType.Response) {
      this.updateUploadProgress({
        fileName,
        progress: 100,
        status: 'success',
      });
      this.notifyFilesChanged();
    }
  }

  private updateUploadProgress(progress: UploadProgress): void {
    const current = this.uploadProgressSubject.value;
    const existingIndex = current.findIndex((p) => p.fileName === progress.fileName);

    const next = [...current];
    if (existingIndex >= 0) {
      next[existingIndex] = { ...next[existingIndex], ...progress };
    } else {
      next.push(progress);
    }

    this.uploadProgressSubject.next(next);
  }

  private setUploadError(fileName: string, error: any): void {
    this.updateUploadProgress({
      fileName,
      progress: 0,
      status: 'error',
      error: error?.message || 'Upload failed',
    });
  }
}
